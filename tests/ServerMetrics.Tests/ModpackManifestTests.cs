using System.Security.Cryptography;
using System.Text;

namespace ValheimServerMetrics
{
    internal static class ModpackManifestTests
    {
        internal static async Task Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "server-metrics-modpack-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string manifestPath = Path.Combine(root, "manifest.json");
                string manifest = "{\"schema_version\":1,\"profile_name\":\"Northern Hearth\",\"community\":\"valheim\",\"packages\":[{\"namespace\":\"local\",\"name\":\"ServerMetrics\",\"version\":\"1.2.0\",\"source\":\"local\",\"client\":false,\"server\":true}]}";
                byte[] raw = Encoding.UTF8.GetBytes(manifest);
                File.WriteAllBytes(manifestPath, raw);

                ModpackManifestResult missing = ModpackManifestReader.Read(manifestPath);
                Equal(null, missing.ProfileCode, "A missing profile sidecar must suppress only the code.");
                Equal(1, missing.Packages.Length, "Valid packages must survive a missing sidecar.");

                File.WriteAllText(Path.Combine(root, "profile.json"), "not json");
                Equal(null, ModpackManifestReader.Read(manifestPath).ProfileCode, "A corrupt profile sidecar must suppress only the code.");

                string code = Guid.NewGuid().ToString();
                File.WriteAllText(Path.Combine(root, "profile.json"), Profile(code, new string('0', 64)));
                Equal(null, ModpackManifestReader.Read(manifestPath).ProfileCode, "A stale profile sidecar must suppress only the code.");

                File.WriteAllText(Path.Combine(root, "profile.json"), Profile(code, Hash(raw)).Replace(new string('0', 64), "bad", StringComparison.Ordinal));
                Equal(null, ModpackManifestReader.Read(manifestPath).ProfileCode, "An invalid archive hash must suppress the code.");
                File.WriteAllText(Path.Combine(root, "profile.json"), Profile(code, Hash(raw)));
                Equal(code, ModpackManifestReader.Read(manifestPath).ProfileCode, "A matching raw-byte hash and UUID must publish the code.");

                var state = new MetricsState("test", () => 0, () => 0);
                var warnings = new List<string>();
                using (var monitor = new ModpackManifestMonitor(state, manifestPath, warnings.Add))
                {
                    string current = await WaitForSnapshot(state, text => text.Contains("profile_code=\"" + code + "\"", StringComparison.Ordinal));
                    Contains(current, "valheim_modpack_package_info{namespace=\"local\",name=\"ServerMetrics\",version=\"1.2.0\",source=\"local\",client=\"false\",server=\"true\"} 1");
                    File.WriteAllText(manifestPath, "{}");
                    monitor.RequestRefresh();
                    string unavailable = await WaitForSnapshot(state, text => text.Contains("valheim_modpack_manifest_success 0", StringComparison.Ordinal));
                    False(unavailable.Contains("valheim_modpack_package_info", StringComparison.Ordinal));
                    False(unavailable.Contains("valheim_modpack_info", StringComparison.Ordinal));

                    File.WriteAllBytes(manifestPath, raw);
                    File.WriteAllText(Path.Combine(root, "profile.json"), Profile("not-a-uuid", Hash(raw)));
                    monitor.RequestRefresh();
                    string replaced = await WaitForSnapshot(state, text => text.Contains("valheim_modpack_manifest_success 1", StringComparison.Ordinal));
                    Contains(replaced, "valheim_modpack_package_info");
                    False(replaced.Contains("valheim_modpack_info", StringComparison.Ordinal));
                }

                File.WriteAllText(manifestPath, manifest.Replace("ServerMetrics", "bad-name"));
                Throws<InvalidDataException>(() => ModpackManifestReader.Read(manifestPath), "Unsafe package identities must be rejected.");
                File.WriteAllText(manifestPath, manifest.Replace("1.2.0", "1.2"));
                Throws<InvalidDataException>(() => ModpackManifestReader.Read(manifestPath), "Non-strict versions must be rejected.");
                File.WriteAllText(manifestPath, manifest.Replace("\"local\",\"client\"", "\"unknown\",\"client\""));
                Throws<InvalidDataException>(() => ModpackManifestReader.Read(manifestPath), "Unknown package sources must be rejected.");
                File.WriteAllText(manifestPath, manifest.Replace("1.2.0", "01.2.0"));
                Throws<InvalidDataException>(() => ModpackManifestReader.Read(manifestPath), "Version segments with leading zeroes must be rejected.");
                File.WriteAllText(manifestPath, manifest.Replace("\"packages\":[{", "\"packages\":[],\"unused\":[{"));
                Throws<InvalidDataException>(() => ModpackManifestReader.Read(manifestPath), "An empty package inventory must be rejected.");
                File.WriteAllText(manifestPath, manifest.Replace("\"client\":false", "\"client\":true"));
                Throws<InvalidDataException>(() => ModpackManifestReader.Read(manifestPath), "Local client packages must be rejected.");
                File.WriteAllBytes(manifestPath, raw);
                string linkedManifest = Path.Combine(root, "linked-manifest.json");
                File.CreateSymbolicLink(linkedManifest, manifestPath);
                Throws<IOException>(() => ModpackManifestReader.Read(linkedManifest), "Manifest symbolic links must be rejected.");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static string Profile(string code, string hash) =>
            "{\"code\":\"" + code + "\",\"archive_sha256\":\"" + new string('0', 64) + "\",\"manifest_sha256\":\"" + hash + "\"}";

        private static string Hash(byte[] bytes)
        {
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
        }

        private static async Task<string> WaitForSnapshot(MetricsState state, Func<string, bool> predicate)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                string snapshot = Encoding.UTF8.GetString(state.GetSnapshot());
                if (predicate(snapshot)) return snapshot;
                await Task.Delay(25);
            }
            throw new InvalidOperationException("Timed out waiting for the modpack monitor.");
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException(message);
        }

        private static void Contains(string text, string expected)
        {
            if (!text.Contains(expected, StringComparison.Ordinal)) throw new InvalidOperationException("Missing: " + expected);
        }

        private static void False(bool value)
        {
            if (value) throw new InvalidOperationException("Expected false.");
        }

        private static void Throws<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException(message);
        }
    }
}
