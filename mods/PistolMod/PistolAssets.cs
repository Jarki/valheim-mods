using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PistolMod
{
    // Original Blender meshes and licensed recordings are embedded: a deployment only needs the DLL.
    internal static class PistolAssets
    {
        private sealed class MeshPart
        {
            internal string Group;
            internal int Material;
            internal Mesh Mesh;
        }

        private static readonly Dictionary<string, MeshPart[]> meshes = new Dictionary<string, MeshPart[]>();
        private static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private static Material[] materials;
        private static AudioSource[] pool;
        private static int nextSource;

        internal static Stream Resource(string path)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream("PistolMod.Assets." + path)
                ?? throw new InvalidDataException("Missing embedded firearm asset: " + path);
        }

        internal static GameObject Create(Transform parent, string asset, Material fallback, WeaponSpec spec = null)
        {
            EnsureMaterials(fallback);
            var root = new GameObject("Firearm_Visual");
            root.transform.SetParent(parent, false);
            foreach (var part in LoadMeshes(asset))
            {
                var group = root.transform.Find(part.Group);
                if (!group)
                {
                    group = new GameObject(part.Group).transform;
                    group.SetParent(root.transform, false);
                }
                var geometry = new GameObject("Mesh");
                geometry.transform.SetParent(group, false);
                geometry.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
                geometry.AddComponent<MeshRenderer>().sharedMaterial = materials[part.Material];
            }
            if (spec != null)
            {
                var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flash.name = "MuzzleFlash";
                UnityEngine.Object.DestroyImmediate(flash.GetComponent<Collider>());
                flash.transform.SetParent(root.transform, false);
                flash.transform.localPosition = spec == WeaponSpec.Pistol ? new Vector3(0, .084f, .17f)
                    : spec == WeaponSpec.Rifle ? new Vector3(0, .084f, .58f) : new Vector3(0, .155f, .82f);
                flash.transform.localScale = spec.Explosive ? new Vector3(.12f, .12f, .24f) : new Vector3(.025f, .025f, .07f);
                flash.GetComponent<Renderer>().sharedMaterial = materials[7];
                flash.SetActive(false);
                root.AddComponent<FirearmVisual>().Spec = spec;
            }
            return root;
        }

        private static MeshPart[] LoadMeshes(string asset)
        {
            if (meshes.TryGetValue(asset, out var cached)) return cached;
            using (var reader = new BinaryReader(Resource("Models." + asset + ".mesh")))
            {
                if (new string(reader.ReadChars(4)) != "FHM1") throw new InvalidDataException("Unknown firearm mesh format");
                var parts = new MeshPart[reader.ReadInt32()];
                for (int p = 0; p < parts.Length; p++)
                {
                    var group = new string(reader.ReadChars(reader.ReadInt32()));
                    int material = reader.ReadInt32(), count = reader.ReadInt32();
                    var vertices = new Vector3[count];
                    var normals = new Vector3[count];
                    for (int i = 0; i < count; i++)
                    {
                        vertices[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        normals[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    }
                    var triangles = new int[reader.ReadInt32()];
                    for (int i = 0; i < triangles.Length; i++) triangles[i] = reader.ReadInt32();
                    var mesh = new Mesh { name = asset + "/" + group,
                        indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, vertices = vertices, normals = normals, triangles = triangles };
                    mesh.RecalculateBounds();
                    mesh.UploadMeshData(true);
                    parts[p] = new MeshPart { Group = group, Material = material, Mesh = mesh };
                }
                meshes.Add(asset, parts);
                return parts;
            }
        }

        internal static Sprite Icon(string asset)
        {
            if (icons.TryGetValue(asset, out var icon)) return icon;
            using (var stream = Resource("Models." + asset + ".png"))
            using (var bytes = new MemoryStream())
            {
                stream.CopyTo(bytes);
                var texture = new Texture2D(2, 2) { name = asset + " icon", wrapMode = TextureWrapMode.Clamp };
                if (!ImageConversion.LoadImage(texture, bytes.ToArray())) throw new InvalidDataException("Invalid icon " + asset);
                icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
                icons.Add(asset, icon);
                return icon;
            }
        }

        internal static AudioClip Clip(string name)
        {
            if (clips.TryGetValue(name, out var clip) && clip) return clip;
            using (var reader = new BinaryReader(Resource("Audio." + name + ".wav")))
            {
                if (new string(reader.ReadChars(4)) != "RIFF") throw new InvalidDataException("Expected RIFF WAV");
                reader.ReadInt32();
                if (new string(reader.ReadChars(4)) != "WAVE") throw new InvalidDataException("Expected WAVE");
                int channels = 0, rate = 0, bits = 0, format = 0;
                byte[] data = null;
                while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                {
                    var chunk = new string(reader.ReadChars(4));
                    int length = reader.ReadInt32();
                    long end = reader.BaseStream.Position + length;
                    if (length < 0 || end > reader.BaseStream.Length) throw new InvalidDataException("Invalid WAV chunk");
                    if (chunk == "fmt ")
                    {
                        format = reader.ReadInt16(); channels = reader.ReadInt16(); rate = reader.ReadInt32();
                        reader.ReadInt32(); reader.ReadInt16(); bits = reader.ReadInt16();
                    }
                    else if (chunk == "data") data = reader.ReadBytes(length);
                    reader.BaseStream.Position = end + (length & 1);
                }
                if (format != 1 || bits != 16 || channels < 1 || rate < 8000 || data == null)
                    throw new InvalidDataException("Firearm audio must be PCM16 WAV");
                var samples = new float[data.Length / 2];
                for (int i = 0; i < samples.Length; i++) samples[i] = (short)(data[i * 2] | data[i * 2 + 1] << 8) / 32768f;
                clip = AudioClip.Create(name, samples.Length / channels, channels, rate, false);
                clip.SetData(samples, 0);
                clips[name] = clip;
                return clip;
            }
        }

        internal static void Play(string clip, Vector3 position, float volume = .8f, float distance = 65f)
        {
            if (pool == null || !pool[0])
            {
                pool = new AudioSource[24];
                var root = new GameObject("FirearmAudio");
                for (int i = 0; i < pool.Length; i++)
                {
                    var emitter = new GameObject("Emitter");
                    emitter.transform.SetParent(root.transform, false);
                    pool[i] = emitter.AddComponent<AudioSource>();
                    ConfigureAudio(pool[i]);
                }
            }
            var source = pool[nextSource++ % pool.Length];
            source.transform.position = position;
            source.clip = Clip(clip); source.volume = volume; source.maxDistance = distance;
            source.Play();
        }

        internal static void ConfigureAudio(AudioSource source)
        {
            source.playOnAwake = false; source.spatialBlend = 1f;
            if (AudioMan.instance) source.outputAudioMixerGroup = AudioMan.instance.m_ambientMixer;
            source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = 2f;
            source.maxDistance = 14f; source.dopplerLevel = 0;
        }

        private static void EnsureMaterials(Material fallback)
        {
            if (materials != null && materials[0]) return;
            Shader shader = Shader.Find("Standard") ?? (fallback ? fallback.shader : null);
            if (!shader) throw new InvalidOperationException("No firearm shader available");
            var colors = new[] { new Color(.035f,.041f,.048f), new Color(.13f,.15f,.17f), new Color(.008f,.012f,.016f),
                new Color(.30f,.105f,.035f), new Color(.24f,.29f,.12f), new Color(.62f,.40f,.12f), new Color(.7f,.73f,.65f), new Color(1,.5f,.05f) };
            materials = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                var mat = new Material(shader) { name = "Firearm finish " + i, color = colors[i] };
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", i == 1 || i == 5 || i == 6 ? .65f : .05f);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", i == 1 || i == 5 || i == 6 ? .68f : .35f);
                materials[i] = mat;
            }
            materials[7].EnableKeyword("_EMISSION");
            materials[7].SetColor("_EmissionColor", new Color(4, 1.2f, .1f));
        }
    }

    public sealed class FirearmVisual : MonoBehaviour
    {
        internal WeaponSpec Spec;
        private Transform slide, magazine, flash;
        private AudioSource reloadAudio;
        private float shotAt = -100f, reloadAt = -100f;
        internal float Recoil => Mathf.Max(0, 1 - (Time.time - shotAt) / .13f);
        internal float ReloadProgress => Spec == null ? 1f : Mathf.Clamp01((Time.time - reloadAt) / Spec.Reload);

        private void Awake()
        {
            slide = transform.Find("Slide"); magazine = transform.Find("Magazine"); flash = transform.Find("MuzzleFlash");
            reloadAudio = gameObject.AddComponent<AudioSource>();
            PistolAssets.ConfigureAudio(reloadAudio);
        }

        internal void Shot()
        {
            shotAt = Time.time;
            PistolAssets.Play(Spec.ShotSound, transform.position);
        }

        internal void Reload()
        {
            reloadAt = Time.time;
            reloadAudio.clip = PistolAssets.Clip(Spec.ReloadSound);
            reloadAudio.pitch = reloadAudio.clip.length / Spec.Reload;
            reloadAudio.volume = .65f;
            reloadAudio.Play();
        }

        internal void CancelReload()
        {
            reloadAt = -100f;
            reloadAudio.Stop();
            if (magazine) { magazine.localPosition = Vector3.zero; magazine.localRotation = Quaternion.identity; }
        }

        private void LateUpdate()
        {
            if (Spec == null) return;
            float recoil = Recoil;
            transform.localRotation = Quaternion.Euler(-recoil * (Spec.Explosive ? 9 : 5), 0, 0);
            if (slide) slide.localPosition = Vector3.back * (.026f * Mathf.Sin(recoil * Mathf.PI));
            if (flash) flash.gameObject.SetActive(Time.time - shotAt < .045f);
            float progress = ReloadProgress;
            if (magazine)
            {
                float drop = progress < .3f ? progress / .3f : progress < .65f ? 1 : (1 - progress) / .35f;
                magazine.localPosition = Spec.Explosive ? new Vector3(-.12f * drop, -.15f * drop, -.06f * drop)
                    : Vector3.down * ((Spec == WeaponSpec.Pistol ? .13f : .23f) * drop);
                magazine.localRotation = Spec == WeaponSpec.Rifle ? Quaternion.Euler(18 * drop, 0, 0) : Quaternion.identity;
            }
        }
    }
}
