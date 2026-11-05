using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace ValheimServerMetrics
{
    internal readonly struct ModpackPackageEntry
    {
        internal ModpackPackageEntry(string packageNamespace, string name, string version, string source, bool client, bool server)
        {
            Namespace = packageNamespace;
            Name = name;
            Version = version;
            Source = source;
            Client = client;
            Server = server;
        }

        internal string Namespace { get; }
        internal string Name { get; }
        internal string Version { get; }
        internal string Source { get; }
        internal bool Client { get; }
        internal bool Server { get; }
    }

    internal sealed class ModpackManifestResult
    {
        internal ModpackManifestResult(string profileName, string profileCode, ModpackPackageEntry[] packages)
        {
            ProfileName = profileName;
            ProfileCode = profileCode;
            Packages = packages;
        }

        internal string ProfileName { get; }
        internal string ProfileCode { get; }
        internal ModpackPackageEntry[] Packages { get; }
    }

    internal static class ModpackManifestReader
    {
        private const int MaximumManifestBytes = 256 * 1024;
        private const int MaximumProfileBytes = 4 * 1024;
        private const int MaximumPackages = 128;

        internal static ModpackManifestResult Read(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
                throw new ArgumentException("A manifest path is required.", nameof(manifestPath));

            byte[] raw = ReadBounded(manifestPath, MaximumManifestBytes);
            ManifestDto manifest = Deserialize<ManifestDto>(raw);
            if (manifest == null || manifest.SchemaVersion != 1 || !string.Equals(manifest.Community, "valheim", StringComparison.Ordinal) ||
                string.IsNullOrEmpty(manifest.ProfileName) || manifest.ProfileName.Length > 256 || manifest.Packages == null ||
                manifest.Packages.Length == 0 || manifest.Packages.Length > MaximumPackages)
                throw new InvalidDataException("The modpack manifest schema is invalid.");

            var packages = new ModpackPackageEntry[manifest.Packages.Length];
            var identities = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < manifest.Packages.Length; index++)
            {
                PackageDto package = manifest.Packages[index];
                if (package != null && string.Equals(package.Source, "local", StringComparison.Ordinal) && package.Client)
                    throw new InvalidDataException("Local packages cannot be client packages.");
                if (package == null || !IsIdentity(package.Namespace) || !IsIdentity(package.Name) || !IsVersion(package.Version) ||
                    (!string.Equals(package.Source, "thunderstore", StringComparison.Ordinal) && !string.Equals(package.Source, "local", StringComparison.Ordinal)) ||
                    (!package.Client && !package.Server))
                    throw new InvalidDataException("The modpack manifest contains an invalid package.");
                if (!identities.Add(package.Namespace + "\0" + package.Name))
                    throw new InvalidDataException("The modpack manifest contains duplicate packages.");
                packages[index] = new ModpackPackageEntry(package.Namespace, package.Name, package.Version, package.Source, package.Client, package.Server);
            }

            string code = ReadCurrentProfileCode(manifestPath, raw);
            return new ModpackManifestResult(manifest.ProfileName, code, packages);
        }

        private static string ReadCurrentProfileCode(string manifestPath, byte[] rawManifest)
        {
            try
            {
                string directory = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
                byte[] rawProfile = ReadBounded(Path.Combine(directory, "profile.json"), MaximumProfileBytes);
                ProfileDto profile = Deserialize<ProfileDto>(rawProfile);
                Guid ignored;
                if (profile == null || !Guid.TryParse(profile.Code, out ignored) || !IsSha256(profile.ArchiveSha256) || !IsSha256(profile.ManifestSha256))
                    return null;
                using (SHA256 sha = SHA256.Create())
                {
                    string actual = ToHex(sha.ComputeHash(rawManifest));
                    return string.Equals(actual, profile.ManifestSha256, StringComparison.OrdinalIgnoreCase) ? profile.Code : null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static byte[] ReadBounded(string path, int maximumBytes)
        {
            EnsureNoReparsePoints(path);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (stream.Length > maximumBytes)
                    throw new InvalidDataException("The file exceeds its size limit.");
                var bytes = new byte[(int)stream.Length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int count = stream.Read(bytes, offset, bytes.Length - offset);
                    if (count == 0)
                        throw new EndOfStreamException();
                    offset += count;
                }
                if (stream.ReadByte() != -1)
                    throw new InvalidDataException("The file exceeds its size limit.");
                return bytes;
            }
        }

        private static T Deserialize<T>(byte[] bytes) where T : class
        {
            try
            {
                using (var stream = new MemoryStream(bytes, false))
                    return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("The JSON file is invalid.", exception);
            }
        }

        private static bool IsIdentity(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 256)
                return false;
            foreach (char character in value)
                if (!(character >= 'a' && character <= 'z') && !(character >= 'A' && character <= 'Z') &&
                    !(character >= '0' && character <= '9') && character != '_')
                    return false;
            return true;
        }

        private static bool IsVersion(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 256)
                return false;
            string[] parts = value.Split('.');
            if (parts.Length != 3)
                return false;
            foreach (string part in parts)
            {
                if (part.Length == 0 || (part.Length > 1 && part[0] == '0'))
                    return false;
                foreach (char character in part)
                    if (character < '0' || character > '9')
                        return false;
            }
            return true;
        }

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64)
                return false;
            foreach (char character in value)
                if (!(character >= '0' && character <= '9') && !(character >= 'a' && character <= 'f') && !(character >= 'A' && character <= 'F'))
                    return false;
            return true;
        }

        private static void EnsureNoReparsePoints(string path)
        {
            string current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Modpack metadata paths cannot contain symbolic links or reparse points.");
                string parent = Path.GetDirectoryName(current);
                if (string.Equals(parent, current, StringComparison.Ordinal))
                    break;
                current = parent;
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var text = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
                text.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return text.ToString();
        }

        [DataContract]
        private sealed class ManifestDto
        {
            [DataMember(Name = "schema_version", IsRequired = true)] internal int SchemaVersion;
            [DataMember(Name = "profile_name", IsRequired = true)] internal string ProfileName;
            [DataMember(Name = "community", IsRequired = true)] internal string Community;
            [DataMember(Name = "packages", IsRequired = true)] internal PackageDto[] Packages;
        }

        [DataContract]
        private sealed class PackageDto
        {
            [DataMember(Name = "namespace", IsRequired = true)] internal string Namespace;
            [DataMember(Name = "name", IsRequired = true)] internal string Name;
            [DataMember(Name = "version", IsRequired = true)] internal string Version;
            [DataMember(Name = "source", IsRequired = true)] internal string Source;
            [DataMember(Name = "client", IsRequired = true)] internal bool Client;
            [DataMember(Name = "server", IsRequired = true)] internal bool Server;
        }

        [DataContract]
        private sealed class ProfileDto
        {
            [DataMember(Name = "code", IsRequired = true)] internal string Code;
            [DataMember(Name = "archive_sha256", IsRequired = true)] internal string ArchiveSha256;
            [DataMember(Name = "manifest_sha256", IsRequired = true)] internal string ManifestSha256;
        }
    }

    internal sealed class ModpackManifestMonitor : IDisposable
    {
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);
        private readonly MetricsState state;
        private readonly Action<string> logWarning;
        private readonly string path;
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly Thread worker;
        private readonly object sync = new object();
        private bool stopping;
        private bool errorLogged;

        internal ModpackManifestMonitor(MetricsState state, string path, Action<string> logWarning)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.path = string.IsNullOrWhiteSpace(path) ? throw new ArgumentException("A manifest path is required.", nameof(path)) : path;
            this.logWarning = logWarning ?? throw new ArgumentNullException(nameof(logWarning));
            worker = new Thread(WorkerLoop) { IsBackground = true, Name = "Valheim modpack manifest" };
            worker.Start();
        }

        internal void RequestRefresh()
        {
            lock (sync)
                if (!stopping) wake.Set();
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (stopping) return;
                stopping = true;
                wake.Set();
            }
            if (Thread.CurrentThread != worker) worker.Join(ShutdownTimeout);
        }

        private void WorkerLoop()
        {
            try
            {
                while (true)
                {
                    lock (sync) if (stopping) return;
                    try
                    {
                        ModpackManifestResult result = ModpackManifestReader.Read(path);
                        lock (sync)
                        {
                            if (stopping) return;
                            state.SetModpackManifest(result.ProfileName, result.ProfileCode, result.Packages);
                            errorLogged = false;
                        }
                    }
                    catch (Exception exception) { PublishUnavailable(exception); }
                    wake.WaitOne(RefreshInterval);
                }
            }
            catch (Exception exception) { PublishUnavailable(exception); }
            finally
            {
                lock (sync) stopping = true;
                wake.Dispose();
            }
        }

        private void PublishUnavailable(Exception exception)
        {
            lock (sync)
            {
                if (stopping) return;
                try { state.MarkModpackManifestUnavailable(); }
                catch (Exception stateException) { exception = exception ?? stateException; }
                if (!errorLogged)
                {
                    errorLogged = true;
                    try { logWarning("Unable to read the modpack manifest: " + exception.Message); }
                    catch (Exception) { }
                }
            }
        }
    }
}
