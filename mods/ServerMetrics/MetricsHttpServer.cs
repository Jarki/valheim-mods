using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ValheimServerMetrics
{
    internal sealed class MetricsHttpServer : IDisposable
    {
        private const int WorkerCount = 2;
        private const int WorkerJoinMilliseconds = 2000;

        private readonly object lifecycleSync = new object();
        private readonly MetricsState state;
        private HttpListener listener;
        private readonly string prefix;
        private readonly IPAddress allowedScraper;
        private readonly Thread[] workers = new Thread[WorkerCount];
        private bool started;
        private bool disposed;
        private int bindFailures;
        private double nextBindAttempt = double.NegativeInfinity;

        internal MetricsHttpServer(MetricsState state, string bindAddress, int port, string allowedScraperAddress = null)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port));
            if (string.IsNullOrWhiteSpace(bindAddress) || !IPAddress.TryParse(bindAddress, out IPAddress address))
                throw new ArgumentException("The bind address must be an IP literal.", nameof(bindAddress));
            if (IsForbiddenAddress(address))
                throw new ArgumentException("Wildcard, unspecified, and multicast addresses are not allowed.", nameof(bindAddress));

            address = NormalizeAddress(address);
            if (string.IsNullOrWhiteSpace(allowedScraperAddress))
            {
                if (!IPAddress.IsLoopback(address))
                    throw new ArgumentException("Non-loopback binding requires an explicit allowed scraper IP.", nameof(allowedScraperAddress));
            }
            else
            {
                if (!IPAddress.TryParse(allowedScraperAddress, out IPAddress scraper) || IsForbiddenAddress(scraper))
                    throw new ArgumentException("The allowed scraper must be a specific unicast IP literal.", nameof(allowedScraperAddress));
                allowedScraper = NormalizeAddress(scraper);
            }

            string host = address.AddressFamily == AddressFamily.InterNetworkV6
                ? "[" + address + "]"
                : address.ToString();
            prefix = "http://" + host + ":" + port + "/";
        }

        internal void Start()
        {
            lock (lifecycleSync)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(MetricsHttpServer));
                if (started)
                    throw new InvalidOperationException("The metrics HTTP server has already been started.");

                listener = new HttpListener();
                listener.Prefixes.Add(prefix);
                try
                {
                    listener.Start();
                    started = true;
                    for (int index = 0; index < workers.Length; index++)
                    {
                        var worker = new Thread(WorkerLoop)
                        {
                            IsBackground = true,
                            Name = "ServerMetrics HTTP " + (index + 1)
                        };
                        workers[index] = worker;
                        worker.Start();
                    }
                }
                catch
                {
                    started = false;
                    listener.Close();
                    for (int index = 0; index < workers.Length; index++)
                        JoinWorker(workers[index]);
                    listener = null;
                    throw;
                }
            }
        }

        // Called on the game thread with a monotonic clock. Only listener startup
        // failures are retried; invalid configuration and observation failures are fatal.
        internal bool StartWhenDue(double now, out Exception failure, out double retryDelaySeconds)
        {
            failure = null;
            retryDelaySeconds = 0;
            lock (lifecycleSync)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(MetricsHttpServer));
                if (started || now < nextBindAttempt)
                    return false;
                try
                {
                    Start();
                    bindFailures = 0;
                    return true;
                }
                catch (Exception error) when (error is SocketException || error is HttpListenerException)
                {
                    if (bindFailures < 3) bindFailures++;
                    retryDelaySeconds = bindFailures == 1 ? 60 : bindFailures == 2 ? 300 : 1800;
                    nextBindAttempt = now + retryDelaySeconds;
                    failure = error;
                    return false;
                }
            }
        }

        public void Dispose()
        {
            lock (lifecycleSync)
            {
                if (disposed)
                    return;
                disposed = true;
                started = false;
                listener?.Close();
            }

            for (int index = 0; index < workers.Length; index++)
                JoinWorker(workers[index]);
        }

        private void WorkerLoop()
        {
            while (true)
            {
                HttpListenerContext context;
                try
                {
                    context = listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (InvalidOperationException)
                {
                    return;
                }
                catch
                {
                    return;
                }

                HandleRequest(context);
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            HttpListenerResponse response = context.Response;
            try
            {
                response.KeepAlive = false;
                IPAddress remote = context.Request.RemoteEndPoint?.Address;
                if (remote == null || !IsAllowedScraper(NormalizeAddress(remote)))
                {
                    response.StatusCode = (int)HttpStatusCode.Forbidden;
                    response.ContentLength64 = 0;
                    return;
                }
                string path = context.Request.Url == null ? string.Empty : context.Request.Url.AbsolutePath;
                if (!string.Equals(path, "/metrics", StringComparison.Ordinal) &&
                    !string.Equals(path, "/metrics/", StringComparison.Ordinal))
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.ContentLength64 = 0;
                    return;
                }

                string method = context.Request.HttpMethod;
                bool isHead = string.Equals(method, "HEAD", StringComparison.Ordinal);
                if (!isHead && !string.Equals(method, "GET", StringComparison.Ordinal))
                {
                    response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    response.Headers[HttpResponseHeader.Allow] = "GET, HEAD";
                    response.ContentLength64 = 0;
                    return;
                }

                byte[] snapshot = state.GetSnapshot();
                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
                response.ContentLength64 = snapshot.Length;
                if (!isHead)
                    response.OutputStream.Write(snapshot, 0, snapshot.Length);
            }
            catch (HttpListenerException)
            {
                SafeAbort(response);
                return;
            }
            catch (ObjectDisposedException)
            {
                SafeAbort(response);
                return;
            }
            catch (IOException)
            {
                SafeAbort(response);
                return;
            }
            catch
            {
                SafeAbort(response);
                return;
            }
            finally
            {
                try
                {
                    response.Close();
                }
                catch
                {
                    SafeAbort(response);
                }
            }
        }

        private bool IsAllowedScraper(IPAddress remote)
            => allowedScraper == null ? IPAddress.IsLoopback(remote) : allowedScraper.Equals(remote);

        private static IPAddress NormalizeAddress(IPAddress address)
            => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

        private static bool IsForbiddenAddress(IPAddress address)
        {
            if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
                return true;
            if (address.IsIPv4MappedToIPv6)
                return IsForbiddenAddress(address.MapToIPv4());
            if (address.AddressFamily == AddressFamily.InterNetworkV6)
                return address.IsIPv6Multicast;
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                byte[] bytes = address.GetAddressBytes();
                return bytes[0] >= 224 && bytes[0] <= 239;
            }
            return true;
        }

        private static void SafeAbort(HttpListenerResponse response)
        {
            try
            {
                response.Abort();
            }
            catch
            {
                // The peer or listener may already have closed the response.
            }
        }

        private static void JoinWorker(Thread worker)
        {
            if (worker != null && worker != Thread.CurrentThread)
                worker.Join(WorkerJoinMilliseconds);
        }
    }
}
