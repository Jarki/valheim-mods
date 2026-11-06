using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rotheart
{
    internal static class RotheartAssets
    {
        private sealed class MeshPart
        {
            internal int Material;
            internal Mesh Mesh;
        }

        private static MeshPart[] meshes;
        private static Material[] materials;
        private static Material particleMaterial;
        private static Sprite icon;

        private static Stream Resource(string extension)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream("Rotheart.Assets.Models.rotheart." + extension)
                ?? throw new InvalidDataException("Missing embedded Rotheart " + extension + " asset");
        }

        // Metres, +Y up, +Z front. Pivot is the heart centre, not the hanging cord.
        // Caller owns the drop/equipment attachment transform and its placement.
        internal static GameObject Create(Transform parent, Material fallback)
        {
            EnsureMaterials(fallback);
            var root = new GameObject("Rotheart_Visual");
            root.transform.SetParent(parent, false);
            foreach (var part in LoadMeshes())
            {
                var geometry = new GameObject("Heart_" + part.Material);
                geometry.transform.SetParent(root.transform, false);
                geometry.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
                geometry.AddComponent<MeshRenderer>().sharedMaterial = materials[part.Material];
            }
            return root;
        }

        internal static Sprite Icon()
        {
            if (icon) return icon;
            using (var stream = Resource("png"))
            using (var bytes = new MemoryStream())
            {
                stream.CopyTo(bytes);
                var texture = new Texture2D(2, 2)
                {
                    name = "Rotheart inventory icon",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                if (!ImageConversion.LoadImage(texture, bytes.ToArray(), true))
                    throw new InvalidDataException("Invalid Rotheart inventory icon");
                icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
                icon.name = "Rotheart";
                return icon;
            }
        }

        private static MeshPart[] LoadMeshes()
        {
            if (meshes != null) return meshes;
            using (var reader = new BinaryReader(Resource("mesh")))
            {
                if (new string(reader.ReadChars(4)) != "FHM1")
                    throw new InvalidDataException("Unknown Rotheart mesh format");
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
                    var mesh = new Mesh
                    {
                        name = "Rotheart/" + group + "/" + material,
                        indexFormat = count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16,
                        vertices = vertices,
                        normals = normals,
                        triangles = triangles
                    };
                    mesh.RecalculateBounds();
                    mesh.UploadMeshData(true);
                    parts[p] = new MeshPart { Material = material, Mesh = mesh };
                }
                meshes = parts;
                return meshes;
            }
        }

        private static void EnsureMaterials(Material fallback)
        {
            if (materials != null && materials[0]) return;
            var shader = Shader.Find("Standard") ?? (fallback ? fallback.shader : null);
            if (!shader) throw new InvalidOperationException("No Rotheart mesh shader available");
            // FHM1 material-index contract: keep identical to Tools/build_models.py PALETTE.
            // 0 iron, 1 rust, 2 bone, 3 cord, 4 ooze, 5 pale ooze, 6 dark ooze, 7 worn iron.
            var colors = new[]
            {
                new Color(.095f, .115f, .105f), new Color(.29f, .115f, .045f),
                new Color(.69f, .65f, .43f), new Color(.19f, .145f, .075f),
                new Color(.38f, .59f, .19f), new Color(.69f, .86f, .39f),
                new Color(.16f, .27f, .075f), new Color(.25f, .28f, .22f)
            };
            var metallic = new[] { .72f, .22f, 0, 0, 0, 0, 0, .65f };
            var roughness = new[] { .70f, .92f, .86f, 1, .36f, .30f, .58f, .65f };
            var emission = new[] { 0, 0, 0, 0, .32f, .65f, .12f, 0 };
            materials = new Material[colors.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                var material = new Material(shader) { name = "Rotheart finish " + i, color = colors[i] };
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic[i]);
                if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 1 - roughness[i]);
                if (emission[i] > 0 && material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", colors[i] * emission[i]);
                }
                materials[i] = material;
            }
        }

        // Returned inactive to avoid playing/network spawning during registration.
        // Parent it under a registered network-effect prefab, then set activeSelf=true.
        // That parent's ZNetView is required by StatusEffect.RemoveStartEffects.
        // Looping visuals have no timer/damage behaviour; the status effect owns lifetime.
        internal static GameObject CreateAuraPrefab()
        {
            EnsureParticleMaterial();
            var root = new GameObject("vfx_RotheartAura");
            root.SetActive(false);
            CreateParticles(root.transform, "GroundMotes", false);
            CreateParticles(root.transform, "OozeWisps", true);
            return root;
        }

        private static void CreateParticles(Transform parent, string name, bool wisps)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = new Vector3(0, -.8f, 0);
            var particles = child.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 3;
            main.startLifetime = new ParticleSystem.MinMaxCurve(wisps ? 1.8f : 1.1f, wisps ? 2.8f : 2.0f);
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(wisps ? .19f : .035f, wisps ? .34f : .065f);
            main.startColor = wisps ? new Color(.47f, .68f, .23f, .12f) : new Color(.70f, .86f, .39f, .44f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.maxParticles = wisps ? 18 : 32;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;
            var emission = particles.emission;
            emission.rateOverTime = wisps ? 5 : 12;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 6;
            shape.radiusThickness = 1;
            shape.rotation = new Vector3(90, 0, 0);
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(.08f, wisps ? .18f : .28f);
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .2f), new GradientAlphaKey(.6f, .65f), new GradientAlphaKey(0, 1) });
            color.color = fade;
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .55f, 1, wisps ? 1.6f : .8f));
            var renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.maxParticleSize = .08f;
        }

        private static void EnsureParticleMaterial()
        {
            if (particleMaterial) return;
            var shader = Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                ?? Shader.Find("Sprites/Default");
            if (!shader) throw new InvalidOperationException("No Rotheart particle shader available");
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Rotheart soft mote",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + .5f) * 2 / size - 1, dy = (y + .5f) * 2 / size - 1;
                float alpha = Mathf.Clamp01(1 - dx * dx - dy * dy);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * alpha * alpha));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            particleMaterial = new Material(shader)
            {
                name = "Rotheart translucent motes",
                mainTexture = texture,
                renderQueue = (int)RenderQueue.Transparent
            };
            if (particleMaterial.HasProperty("_Color")) particleMaterial.SetColor("_Color", Color.white);
            if (particleMaterial.HasProperty("_TintColor")) particleMaterial.SetColor("_TintColor", new Color(.5f, .5f, .5f, .5f));
            if (particleMaterial.HasProperty("_Mode")) particleMaterial.SetFloat("_Mode", 2);
            if (particleMaterial.HasProperty("_SrcBlend")) particleMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (particleMaterial.HasProperty("_DstBlend")) particleMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (particleMaterial.HasProperty("_ZWrite")) particleMaterial.SetInt("_ZWrite", 0);
            particleMaterial.EnableKeyword("_ALPHABLEND_ON");
            particleMaterial.DisableKeyword("_ALPHATEST_ON");
            particleMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
    }
}
