using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Fitness.InGameTests
{
    public sealed partial class Host
    {
        // Disposable scene direction only. No patches to terrain, AI, damage, running or resource calculations.
        sealed class BiomeShot
        {
            internal readonly Heightmap.Biome Biome;
            internal readonly string Slug;
            internal readonly float Run;
            internal readonly string[] Gear, Food;
            internal readonly string Wildlife;
            internal BiomeShot(Heightmap.Biome biome, string slug, float run, string[] gear, string[] food, string wildlife)
            { Biome = biome; Slug = slug; Run = run; Gear = gear; Food = food; Wildlife = wildlife; }
        }

        static readonly BiomeShot[] ShowcaseScenes =
        {
            new BiomeShot(Heightmap.Biome.Meadows, "meadows", 5,
                new[] { "ArmorLeatherChest", "ArmorLeatherLegs", "HelmetLeather", "CapeDeerHide", "AxeFlint", "ShieldWood" },
                new[] { "CookedDeerMeat", "Honey", "Mushroom" }, "Deer"),
            new BiomeShot(Heightmap.Biome.BlackForest, "blackforest", 15,
                new[] { "ArmorBronzeChest", "ArmorBronzeLegs", "HelmetBronze", "CapeTrollHide", "AxeBronze", "ShieldBronzeBuckler" },
                new[] { "DeerStew", "CarrotSoup", "QueensJam" }, "Greydwarf"),
            new BiomeShot(Heightmap.Biome.Swamp, "swamp", 30,
                new[] { "ArmorIronChest", "ArmorIronLegs", "HelmetIron", "CapeTrollHide", "MaceIron", "ShieldBanded" },
                new[] { "Sausages", "TurnipStew", "CarrotSoup" }, "Draugr"),
            new BiomeShot(Heightmap.Biome.Mountain, "mountain", 40,
                new[] { "ArmorWolfChest", "ArmorWolfLegs", "HelmetDrake", "CapeWolf", "SwordSilver", "ShieldSilver" },
                new[] { "WolfMeatSkewer", "OnionSoup", "WolfJerky" }, "Wolf"),
            new BiomeShot(Heightmap.Biome.Plains, "plains", 50,
                new[] { "ArmorPaddedCuirass", "ArmorPaddedGreaves", "HelmetPadded", "CapeLox", "SwordBlackmetal", "ShieldBlackmetal" },
                new[] { "LoxPie", "Bread", "BloodPudding" }, "Lox"),
            new BiomeShot(Heightmap.Biome.Mistlands, "mistlands", 60,
                new[] { "ArmorCarapaceChest", "ArmorCarapaceLegs", "HelmetCarapace", "CapeFeather", "Demister", "SwordMistwalker", "ShieldCarapace" },
                new[] { "MisthareSupreme", "FishAndBread", "Salad" }, "Hare"),
            new BiomeShot(Heightmap.Biome.AshLands, "ashlands", 70,
                new[] { "ArmorFlametalChest", "ArmorFlametalLegs", "HelmetFlametal", "CapeAsksvin", "SwordNiedhogg", "ShieldFlametal" },
                new[] { "MashedMeat", "PiquantPie", "ScorchingMedley" }, "DvergerAshlands"),
            // These are exact l-1.0.21 installed manifest names, not imagined future equipment.
            new BiomeShot(Heightmap.Biome.DeepNorth, "deepnorth", 80,
                new[] { "ArmorDeepNorthHeavyChest", "ArmorDeepNorthHeavylegs", "HelmetDNHeavy", "CapeDeepNorth", "SwordNiedhogg", "ShieldFlametal" },
                new[] { "MooseKebab", "OatmealLingonberryJam", "SealSoup" }, "Deer_White"),
            new BiomeShot(Heightmap.Biome.Ocean, "ocean", 45,
                new[] { "ArmorWolfChest", "ArmorWolfLegs", "HelmetDrake", "CapeWolf", "SwordSilver", "ShieldSilver" },
                new[] { "WolfMeatSkewer", "OnionSoup", "WolfJerky" }, "Seagal")
        };

        readonly List<GameObject> showcaseObjects = new List<GameObject>();
        readonly List<GameObject> showcaseAnimals = new List<GameObject>();
        readonly List<Vector3> showcaseCandidates = new List<Vector3>();
        readonly List<ItemDrop.ItemData> showcaseGear = new List<ItemDrop.ItemData>();
        Vector3 showcaseOrigin, showcaseTreadmillAt, showcaseForward, showcaseRunCamera, showcaseTrainingCamera;
        GameObject showcaseBoat;
        int ShowcaseSceneryMask => LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");

        IEnumerator BiomeShowcase()
        {
            Stage("showcase-gap");
            Check(player && ZNet.IsSinglePlayer && player == Player.m_localPlayer && !player.IsDead(), "showcase owns a live disposable local player");
            Check(player.GetFoods().Count == 0 && fixedSkill < 0 && player.m_staminaRegen > 0 && !(bool)Get(player, "m_godMode"),
                "showcase starts before boosted arena resources, with fresh food and ordinary regeneration");
            string filter = Environment.GetEnvironmentVariable("FITNESS_SHOWCASE_BIOME");
            var selected = new HashSet<Heightmap.Biome>();
            if (string.IsNullOrEmpty(filter))
            {
                foreach (var scene in ShowcaseScenes) selected.Add(scene.Biome);
            }
            else
            {
                foreach (string name in filter.Split(','))
                {
                    bool known = Enum.TryParse(name, true, out Heightmap.Biome requested);
                    bool supported = false;
                    foreach (var scene in ShowcaseScenes) supported |= scene.Biome == requested;
                    Check(known && supported, "requested showcase biome is an exact native enum name: " + name);
                    selected.Add(requested);
                }
            }
            foreach (Heightmap.Biome biome in Enum.GetValues(typeof(Heightmap.Biome)))
            {
                int bits = (int)biome;
                if (bits == 0 || (bits & (bits - 1)) != 0) continue;
                bool covered = false;
                foreach (var scene in ShowcaseScenes) covered |= scene.Biome == biome;
                Check(covered, "showcase includes native biome " + biome);
            }
            var controller = player.GetComponent<PlayerController>();
            var rig = GameCamera.instance;
            Check(controller && rig && Hud.instance && MessageHud.instance, "native control, camera and HUD ready");
            bool controllerEnabled = controller.enabled, cameraEnabled = rig.enabled;
            bool oldDebugTime = EnvMan.instance.m_debugTimeOfDay;
            float oldTime = EnvMan.instance.m_debugTime;
            string oldEnvironment = EnvMan.instance.m_debugEnv;
            Vector3 cameraPosition = rig.transform.position;
            Quaternion cameraRotation = rig.transform.rotation;
            Vector3 returnPosition = player.transform.position;
            Quaternion returnRotation = player.transform.rotation;
            float baseHealth = player.m_baseHP, baseStamina = player.m_baseStamina, regen = player.m_staminaRegen;
            var pristine = new ZPackage(); player.Save(pristine);
            byte[] cleanPlayer = pristine.GetArray();
            drive = false; sprint = false; fixedSkill = -1f;
            controller.enabled = false;
            // A fixed daytime is disclosed; weather stays the native current biome's weather selection.
            EnvMan.instance.m_debugTimeOfDay = true; EnvMan.instance.m_debugTime = .42f;
            EnvMan.instance.m_debugEnv = "";
            Log("SHOWCASE_STAGING disposable native Save/Load resets between scenes; native inventory grants, EquipItem and ConsumeItem; Run set once per scene then free to progress. " +
                "Native biome terrain/vegetation/weather, fixed daytime .42 and composed camera; no terraforming, land platforms, bases, god/infinite resources, forced resource refill, frozen AI or accelerated config. " +
                "Each treadmill is directly instantiated as a disclosed scene prop, not claimed as another Hammer placement test. Filter=" + (filter ?? "ALL"));
            try
            {
                foreach (var scene in ShowcaseScenes)
                {
                    if (!selected.Contains(scene.Biome)) continue;
                    Stage("showcase-gap");
                    ShowcaseCleanup();
                    rig.enabled = true;
                    if (Vector3.Distance(player.transform.position, returnPosition) > 10f)
                    {
                        yield return Wait(() => player.TeleportTo(returnPosition + Vector3.up * .2f, returnRotation, true),
                            "native return to safe preparation area", 20f);
                        yield return Wait(() => !player.IsTeleporting() && ZNetScene.instance.IsAreaReady(returnPosition) &&
                            ZoneSystem.instance.IsZoneLoaded(returnPosition), "native preparation area ready", 300f);
                    }
                    ShowcaseReset(cleanPlayer);
                    yield return ShowcaseLoadout(scene);
                    yield return Wait(() =>
                    {
                        ShowcaseRestedStamina();
                        return player.GetHealth() >= player.GetMaxHealth() * .85f;
                    }, "native food healing before biome travel", 240f);
                    float sceneStarted = Time.realtimeSinceStartup;
                    yield return ShowcaseFindCandidates(scene);
                    yield return ShowcaseTravelAndCompose(scene);
                    MessageHud.instance.ClearUnlockQueue();
                    yield return Wait(NativeMessagesSettled, "showcase grant, biome and food messages settle: " + scene.Biome, 90f);
                    yield return ShowcaseDefensiveRoll();
                    yield return Wait(ShowcaseRestedStamina,
                        "ordinary native stamina regeneration before outdoor run: " + scene.Biome, 75f);
                    yield return ShowcaseWildlife(scene);
                    ShowcaseObserve(scene, "ready", baseHealth, baseStamina, regen);
                    Log("SHOWCASE_READY biome=" + scene.Biome + " loadSeconds=" + F(Time.realtimeSinceStartup - sceneStarted) +
                        " origin=" + showcaseOrigin + " environment=" + EnvMan.instance.GetCurrentEnvironment().m_name + " resolution=" + Screen.width + "x" + Screen.height);
                    rig.enabled = false;
                    yield return ShowcaseRunShot(scene);
                    Stage("showcase-gap");
                    Controls(Vector3.zero);
                    foreach (var animal in showcaseAnimals) ShowcaseDestroy(animal);
                    showcaseAnimals.Clear();
                    yield return ShowcaseDefensiveRoll();
                    yield return Wait(ShowcaseRestedStamina,
                        "ordinary native regeneration between run and treadmill: " + scene.Biome, 75f);
                    yield return ShowcasePlaceTreadmill(scene);
                    yield return ShowcaseWildlife(scene);
                    yield return ShowcaseTrainingShot(scene);
                    Stage("showcase-gap");
                    ShowcaseObserve(scene, "finished", baseHealth, baseStamina, regen);
                    Log("SHOWCASE_SCENE_COMPLETE biome=" + scene.Biome + " elapsed=" + F(Time.realtimeSinceStartup - sceneStarted));
                }
                Stage("showcase-gap");
                rig.enabled = true;
                yield return Wait(() => player.TeleportTo(returnPosition + Vector3.up * .2f, returnRotation, true),
                    "native showcase return teleport accepted", 20f);
                yield return Wait(() => !player.IsTeleporting() && ZNetScene.instance.IsAreaReady(returnPosition) &&
                    ZoneSystem.instance.IsZoneLoaded(returnPosition), "native showcase restores starting area before relinquishing fixture", 300f);
                Check(nativeErrors == 0, "no native error/exception/assert logs during biome showcase");
                Stage("showcase-finished");
            }
            finally
            {
                Controls(Vector3.zero);
                if (player.IsAttached() && treadmill) ((Interactable)treadmill).Interact(player, false, false);
                ShowcaseCleanup();
                if (!player.IsDead()) ShowcaseReset(cleanPlayer);
                rig.transform.SetPositionAndRotation(cameraPosition, cameraRotation); rig.enabled = cameraEnabled;
                controller.enabled = controllerEnabled;
                EnvMan.instance.m_debugTimeOfDay = oldDebugTime; EnvMan.instance.m_debugTime = oldTime;
                EnvMan.instance.m_debugEnv = oldEnvironment;
                EnvMan.instance.ForceInstantEnvironmentSwitch();
            }
        }

        static string F(float number) => number.ToString("F3", CultureInfo.InvariantCulture);

        void ShowcaseReset(byte[] cleanPlayer)
        {
            Controls(Vector3.zero);
            Check(!player.IsAttached() && !player.IsDead(), "scene transition is detached and alive before native reset");
            player.GetSEMan().RemoveAllStatusEffects(true);
            // AttachStart natively sheathes hand items. Restore those references before UnequipAllItems;
            // that native method otherwise deliberately leaves the back slots alone.
            Call(player, "ShowHandItems", false, false);
            player.UnequipAllItems();
            player.ClearFood();
            // Native Load expects the constructor's empty station dictionary. Its production postfix resets strain state.
            ((IDictionary)Get(player, "m_knownStations")).Clear();
            player.Load(new ZPackage(cleanPlayer));
            showcaseGear.Clear();
            Check(player.GetFoods().Count == 0 && player.GetInventory().NrOfItems() == 0 && Level == 0,
                "native pristine player Load removes previous scene food, gear and strain");
            Check(PersistedLoad() < .002f, "native pristine player Load restores zero partial strain");
            Log("SHOWCASE_RESET native Player.Load restored pristine character resources/skills/food/inventory/strain between scenes only; no resource setters or repeated refills during shots");
        }

        IEnumerator ShowcaseLoadout(BiomeShot scene)
        {
            SetSkill(scene.Run);
            foreach (string name in scene.Gear)
            {
                var item = Add(name, 1);
                Check(player.EquipItem(item, false) && item.m_equipped, "native showcase equipped " + scene.Biome + " " + name);
                showcaseGear.Add(item);
                Log("SHOWCASE_GEAR biome=" + scene.Biome + " prefab=" + name + " quality=" + item.m_quality + " equipped=" + item.m_equipped);
            }
            foreach (string name in scene.Food)
            {
                var food = Add(name, 1);
                int before = InventoryCount(name);
                Check(food.m_shared.m_food > 0 && food.m_shared.m_foodStamina > 0 && player.CanEat(food, false) &&
                    player.ConsumeItem(player.GetInventory(), food) && InventoryCount(name) == before - 1,
                    "native showcase consumed " + scene.Biome + " " + name);
                Log("SHOWCASE_FOOD biome=" + scene.Biome + " prefab=" + name + " health=" + F(food.m_shared.m_food) +
                    " stamina=" + F(food.m_shared.m_foodStamina) + " regen=" + F(food.m_shared.m_foodRegen) + " duration=" + F(food.m_shared.m_foodBurnTime));
            }
            if (scene.Biome == Heightmap.Biome.Swamp || scene.Biome == Heightmap.Biome.Mountain || scene.Biome == Heightmap.Biome.DeepNorth)
            {
                string mead = scene.Biome == Heightmap.Biome.Swamp ? "MeadPoisonResist" : "MeadFrostResist";
                var item = Add(mead, 1);
                Check(player.ConsumeItem(player.GetInventory(), item) && InventoryCount(mead) == 0, "native protective mead consumed " + mead);
                Log("SHOWCASE_MEAD biome=" + scene.Biome + " prefab=" + mead + " nativeEffect=" + item.m_shared.m_consumeStatusEffect.name);
            }
            if (scene.Biome == Heightmap.Biome.DeepNorth)
                Log("SHOWCASE_DEEPNORTH native l-1.0.21 ArmorDeepNorthHeavyChest/ArmorDeepNorthHeavylegs/HelmetDNHeavy/CapeDeepNorth and MooseKebab/OatmealLingonberryJam/SealSoup; actual installed content, with prior-tier sword/shield travel equipment");
            yield return null;
            Check(player.GetFoods().Count == 3 && !player.IsEncumbered() && player.GetBodyArmor() > 0, "native showcase three foods, armor and ordinary carried weight");
            MessageHud.instance.ClearUnlockQueue();
        }

        bool ShowcaseBiome(Vector3 point, Heightmap.Biome biome)
        {
            return WorldGenerator.instance.GetBiome(point) == biome && WorldGenerator.instance.GetBiomeSector(point).BiomeType.Biome == biome;
        }

        IEnumerator ShowcaseFindCandidates(BiomeShot scene)
        {
            showcaseCandidates.Clear();
            var scores = new List<float>();
            int examined = 0;
            float began = Time.realtimeSinceStartup, water = ZoneSystem.instance.m_waterLevel;
            // Search the whole native map, including polar biomes. Only the subject's own footprint must be dry;
            // asking for a large dry Swamp perimeter incorrectly rejects real swamp terrain.
            for (int radius = 240; radius <= 10000; radius += 140)
            {
                int count = Mathf.CeilToInt(2 * Mathf.PI * radius / 140f);
                for (int step = 0; step < count; ++step)
                {
                    if (++examined % 32 == 0)
                    {
                        if (Time.realtimeSinceStartup - began > 120f) throw new TimeoutException("native biome search " + scene.Biome);
                        yield return null;
                    }
                    float angle = step * Mathf.PI * 2f / count;
                    Vector3 p = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                    if (!ShowcaseBiome(p, scene.Biome)) continue;
                    p.y = WorldGenerator.instance.GetHeight(p);
                    if (scene.Biome == Heightmap.Biome.Ocean)
                    {
                        if (p.y > water - 8 || radius > 6000) continue;
                    }
                    else
                    {
                        if (p.y < water + .18f || (scene.Biome == Heightmap.Biome.Mountain && p.y < 100)) continue;
                        if (scene.Biome == Heightmap.Biome.AshLands && ZoneSystem.IsLavaPreHeightmap(p)) continue;
                    }
                    float min = p.y, max = p.y;
                    bool interior = true;
                    for (int k = 0; k < 8; ++k)
                    {
                        Vector3 q = p + Quaternion.Euler(0, k * 45, 0) * Vector3.forward * 5f;
                        if (!ShowcaseBiome(q, scene.Biome)) { interior = false; break; }
                        float h = WorldGenerator.instance.GetHeight(q); min = Mathf.Min(min, h); max = Mathf.Max(max, h);
                    }
                    if (!interior || (scene.Biome != Heightmap.Biome.Ocean && max - min > 4)) continue;
                    float score = -(max - min) - radius * .00003f;
                    bool near = false;
                    foreach (Vector3 candidate in showcaseCandidates) near |= Vector3.Distance(candidate, p) < 100;
                    if (near) continue;
                    int insert = 0;
                    while (insert < scores.Count && scores[insert] >= score) ++insert;
                    if (insert >= 8) continue;
                    showcaseCandidates.Insert(insert, p); scores.Insert(insert, score);
                    if (scores.Count > 8) { scores.RemoveAt(8); showcaseCandidates.RemoveAt(8); }
                }
                if (showcaseCandidates.Count == 8 && radius > (scene.Biome == Heightmap.Biome.Meadows ? 1200 : scene.Biome == Heightmap.Biome.Ocean ? 2400 : 9500)) break;
            }
            Check(showcaseCandidates.Count > 0, "native terrain candidate exists for " + scene.Biome + "; no substitute biome/weather fallback");
            Log("SHOWCASE_SEARCH biome=" + scene.Biome + " examined=" + examined + " candidates=" + showcaseCandidates.Count + " seconds=" + F(Time.realtimeSinceStartup - began));
        }

        IEnumerator ShowcaseTravelAndCompose(BiomeShot scene)
        {
            bool composed = false;
            foreach (Vector3 candidate in showcaseCandidates)
            {
                Controls(Vector3.zero); GameCamera.instance.enabled = true;
                if (showcaseBoat) { ShowcaseDestroy(showcaseBoat); showcaseBoat = null; }
                Vector3 destination = candidate;
                destination.y = scene.Biome == Heightmap.Biome.Ocean ? ZoneSystem.instance.m_waterLevel + 4f : candidate.y + .6f;
                float began = Time.realtimeSinceStartup;
                yield return Wait(() => player.TeleportTo(destination, Quaternion.identity, true), "native showcase teleport accepted " + scene.Biome, 20f);
                yield return Wait(() => ZNetScene.instance.IsAreaReady(destination) && ZoneSystem.instance.IsZoneLoaded(destination) &&
                    Heightmap.FindHeightmap(destination), "native showcase terrain and scene streaming " + scene.Biome, 300f);
                if (scene.Biome == Heightmap.Biome.Ocean)
                {
                    showcaseBoat = Instantiate(Prefab("VikingShip"), new Vector3(destination.x, ZoneSystem.instance.m_waterLevel + .25f, destination.z), Quaternion.identity);
                    showcaseObjects.Add(showcaseBoat);
                    Check(showcaseBoat.GetComponent<Ship>() && showcaseBoat.GetComponent<Rigidbody>(), "native Ocean longship has live buoyancy and real deck colliders");
                    Log("SHOWCASE_OCEAN staged native VikingShip (single small boat, not terrain/platform/base); native buoyancy stays live. Treadmill parented to its deck is a capture fixture, not a claim vanilla Hammer allows ship building.");
                }
                yield return Wait(() => !player.IsTeleporting(), "native showcase teleport completes " + scene.Biome, 300f);
                yield return Wait(() => player.GetCurrentBiome() == scene.Biome && EnvMan.instance.GetCurrentEnvironment() != null,
                    "player actual biome and native environment ready " + scene.Biome, 30f);
                Check(ShowcaseBiome(player.transform.position, scene.Biome), "actual native generator and sector agree after teleport " + scene.Biome);
                composed = scene.Biome == Heightmap.Biome.Ocean ? ShowcaseComposeBoat() : ShowcaseComposeLand(scene, candidate);
                Log("SHOWCASE_STREAM biome=" + scene.Biome + " candidate=" + candidate + " seconds=" + F(Time.realtimeSinceStartup - began) + " composed=" + composed);
                if (composed) break;
            }
            Check(composed, "unobstructed natural terrain/deck run and treadmill camera found for " + scene.Biome);
            ShowcaseMove(showcaseBoat ? showcaseBoat.transform.TransformPoint(showcaseOrigin) : showcaseOrigin);
            yield return Wait(() => player.IsOnGround() && !player.IsSwimming() && !player.IsTeleporting(), "native showcase dry footing " + scene.Biome, 15f);
            // Settle native clutter/environment only after moving into the chosen safe clearing.
            yield return new WaitForSecondsRealtime(10f);
        }

        bool ShowcaseGround(Vector3 at, Heightmap.Biome biome, out Vector3 ground)
        {
            ground = at;
            if (!ShowcaseBiome(at, biome) || !Heightmap.GetHeight(at, out float height)) return false;
            var heightmap = Heightmap.FindHeightmap(at);
            if (!heightmap || heightmap.GetBiome(at) != biome) return false;
            if (height < ZoneSystem.instance.m_waterLevel + .08f) return false;
            ground.y = height + .08f;
            if (biome == Heightmap.Biome.AshLands && ZoneSystem.instance.IsLava(ground)) return false;
            return !Physics.CheckCapsule(ground + Vector3.up * .6f, ground + Vector3.up * 1.65f, .45f,
                LayerMask.GetMask("Default", "static_solid", "Default_small", "piece"), QueryTriggerInteraction.Ignore);
        }

        bool ShowcaseSightline(Vector3 camera, Vector3 subject)
        {
            if (Heightmap.GetHeight(camera, out float ground) && camera.y < ground + .5f) return false;
            if (Physics.CheckSphere(camera, .25f, ShowcaseSceneryMask, QueryTriggerInteraction.Ignore)) return false;
            for (int sample = 0; sample < 3; ++sample)
                if (Physics.Linecast(camera, subject + Vector3.up * (sample == 0 ? .55f : sample == 1 ? 1.1f : 1.85f),
                    ShowcaseSceneryMask, QueryTriggerInteraction.Ignore)) return false;
            return true;
        }

        bool ShowcaseComposeLand(BiomeShot scene, Vector3 around)
        {
            var path = new Vector3[16];
            for (int radius = 0; radius <= 42; radius += 6)
                for (int heading = 0; heading < (radius == 0 ? 1 : 16); ++heading)
                {
                    Vector3 offset = Quaternion.Euler(0, heading * 22.5f, 0) * Vector3.forward * radius;
                    if (!ShowcaseGround(around + offset, scene.Biome, out Vector3 origin)) continue;
                    bool threatened = false;
                    foreach (var character in Character.GetAllCharacters())
                        if (character != player && !character.IsDead() &&
                            (character.transform.position - origin).sqrMagnitude < 60f * 60f &&
                            BaseAI.IsEnemy(player, character)) { threatened = true; break; }
                    if (threatened) continue;
                    bool open = true;
                    for (int k = 0; k < path.Length; ++k)
                    {
                        Vector3 p = origin + Quaternion.Euler(0, k * 360f / path.Length, 0) * Vector3.forward * 3.4f;
                        if (!ShowcaseGround(p, scene.Biome, out path[k]) || Mathf.Abs(path[k].y - origin.y) > 1.4f) { open = false; break; }
                    }
                    if (!open) continue;
                    // The treadmill's full footprint, not merely its center, must fit dry native ground.
                    for (int k = 0; k < 8; ++k)
                    {
                        Vector3 p = origin + Quaternion.Euler(0, k * 45, 0) * Vector3.forward * 1.6f;
                        if (!ShowcaseGround(p, scene.Biome, out Vector3 g) || Mathf.Abs(g.y - origin.y) > .35f) { open = false; break; }
                    }
                    if (!open) continue;
                    for (int cameraHeading = 0; cameraHeading < 24; ++cameraHeading)
                    {
                        Quaternion yaw = Quaternion.Euler(0, cameraHeading * 15f, 0);
                        Vector3 runCamera = origin + yaw * new Vector3(-7, 4.8f, -10);
                        Vector3 trainingCamera = origin + yaw * new Vector3(-4.5f, 3, -5.5f);
                        if (!ShowcaseSightline(trainingCamera, origin) || !ShowcaseSightline(runCamera, origin)) continue;
                        foreach (Vector3 p in path) if (!ShowcaseSightline(runCamera, p)) { open = false; break; }
                        if (!open) { open = true; continue; }
                        showcaseOrigin = showcaseTreadmillAt = origin; showcaseForward = yaw * Vector3.forward;
                        showcaseRunCamera = runCamera; showcaseTrainingCamera = trainingCamera;
                        Log("SHOWCASE_COMPOSITION nativeGround=" + origin + " runCamera=" + runCamera + " treadmillCamera=" + trainingCamera + " no scenery deleted");
                        return true;
                    }
                }
            return false;
        }

        bool ShowcaseDeck(Vector3 local, out Vector3 world)
        {
            world = showcaseBoat.transform.TransformPoint(local);
            var hits = Physics.RaycastAll(world + Vector3.up * 6f, Vector3.down, 15f, ShowcaseSceneryMask, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            bool found = false;
            foreach (var hit in hits)
                if (hit.collider.transform.IsChildOf(showcaseBoat.transform) && hit.normal.y > .85f && hit.distance < best)
                { best = hit.distance; world = hit.point + Vector3.up * .08f; found = true; }
            if (!found) return false;
            return !Physics.CheckCapsule(world + Vector3.up * .6f, world + Vector3.up * 1.65f, .38f,
                ShowcaseSceneryMask, QueryTriggerInteraction.Ignore);
        }

        bool ShowcaseComposeBoat()
        {
            // Find a real clear side aisle, away from the longship mast, then an aft/bow treadmill footprint.
            foreach (float x in new[] { -1.15f, 1.15f, -.8f, .8f })
                for (float z = -5; z <= 2; z += .5f)
                {
                    if (!ShowcaseDeck(new Vector3(x, 0, z), out Vector3 origin)) continue;
                    bool clear = true;
                    for (int k = 0; k <= 12; ++k)
                        if (!ShowcaseDeck(new Vector3(x, 0, z + k * .25f), out Vector3 p) || Mathf.Abs(p.y - origin.y) > .5f) { clear = false; break; }
                    if (!clear) continue;
                    Vector3 runway = showcaseBoat.transform.InverseTransformPoint(origin);
                    for (float treadmillZ = -5.5f; treadmillZ <= 5.5f; treadmillZ += .5f)
                    {
                        if (!ShowcaseDeck(new Vector3(0, 0, treadmillZ), out Vector3 deck)) continue;
                        bool footprint = true;
                        foreach (float dx in new[] { -1.05f, 1.05f })
                            foreach (float dz in new[] { -1.25f, 1.25f })
                                if (!ShowcaseDeck(new Vector3(dx, 0, treadmillZ + dz), out Vector3 edge) || Mathf.Abs(edge.y - deck.y) > .3f)
                                    footprint = false;
                        if (!footprint) continue;
                        Vector3 runCamera = origin + showcaseBoat.transform.rotation * new Vector3(-8, 5, -9);
                        Vector3 trainingCamera = deck + showcaseBoat.transform.rotation * new Vector3(-5, 3.5f, -5);
                        if (!ShowcaseSightline(runCamera, origin) || !ShowcaseSightline(trainingCamera, deck)) continue;
                        showcaseOrigin = runway;
                        showcaseTreadmillAt = showcaseBoat.transform.InverseTransformPoint(deck);
                        showcaseForward = Vector3.forward;
                        showcaseRunCamera = new Vector3(-8, 5, -9);
                        showcaseTrainingCamera = new Vector3(-5, 3.5f, -5);
                        Log("SHOWCASE_OCEAN_COMPOSITION realDeckRunway=" + runway + " realDeckTreadmill=" + showcaseTreadmillAt + " entire treadmill footprint supported by boat");
                        return true;
                    }
                }
            return false;
        }

        void ShowcaseMove(Vector3 point)
        {
            var body = player.GetComponent<Rigidbody>();
            body.position = point + Vector3.up * .12f; body.linearVelocity = Vector3.zero;
            player.transform.position = body.position;
            Set(player, "m_maxAirAltitude", body.position.y);
            Physics.SyncTransforms();
            Log("SHOWCASE_POSITION between-shot fixture relocation=" + body.position);
        }

        IEnumerator ShowcaseDefensiveRoll()
        {
            if (showcaseBoat) yield break;
            Vector3 direction = showcaseOrigin - player.transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude < .25f) direction = showcaseForward;
            Call(player, "Dodge", direction.normalized);
            yield return Wait(() => player.InDodge(), "native defensive roll starts before filming", 3f);
            yield return Wait(() => !player.InDodge(), "native defensive roll finishes before filming", 3f);
            Log("SHOWCASE_PREPARATION native dodge removes attached ticks; normal stamina recovers afterward, no invulnerability during takes.");
        }

        bool ShowcaseRestedStamina()
        {
            if (player.IsDead() || player.GetFoods().Count != 3)
                throw new InvalidOperationException("Showcase rest lost its live, fed character; no respawn or resource-refill substitute is allowed.");
            return player.GetStamina() >= player.GetMaxStamina() - .5f;
        }

        IEnumerator ShowcaseWildlife(BiomeShot scene)
        {
            foreach (var previousAnimal in showcaseAnimals) ShowcaseDestroy(previousAnimal);
            showcaseAnimals.Clear();
            Vector3 origin = showcaseBoat ? showcaseBoat.transform.TransformPoint(showcaseOrigin) : showcaseOrigin;
            if (scene.Biome == Heightmap.Biome.Ocean)
            {
                var bird = Instantiate(Prefab(scene.Wildlife), origin + Vector3.right * 9 + Vector3.up * 3, Quaternion.identity);
                showcaseObjects.Add(bird); showcaseAnimals.Add(bird);
                Check(bird.GetComponent<RandomFlyingBird>() && bird.GetComponent<RandomFlyingBird>().enabled, "native Ocean seabird has live flight behavior");
            }
            else
            {
                bool hostile = scene.Biome == Heightmap.Biome.BlackForest || scene.Biome == Heightmap.Biome.Swamp || scene.Biome == Heightmap.Biome.Mountain;
                int wanted = scene.Biome == Heightmap.Biome.Meadows ? 2 : 1;
                for (int animal = 0; animal < wanted; ++animal)
                {
                    bool found = false;
                    for (int k = 0; k < 24; ++k)
                    {
                        // Keep live hostile AI beyond ordinary awareness while preparing a non-combat training shot.
                        float distance = hostile ? 80 : 12 + animal * 4;
                        Vector3 at = origin + Quaternion.Euler(0, k * 15 + animal * 75, 0) * showcaseForward * distance;
                        if (!ShowcaseGround(at, scene.Biome, out Vector3 ground)) continue;
                        var obj = Instantiate(Prefab(scene.Wildlife), ground + Vector3.up * .1f, Quaternion.identity);
                        showcaseObjects.Add(obj); showcaseAnimals.Add(obj);
                        var character = obj.GetComponent<Character>(); var ai = obj.GetComponent<BaseAI>();
                        Check(character && ai && ai.enabled, "native staged " + scene.Wildlife + " has live AI in " + scene.Biome);
                        Log("SHOWCASE_WILDLIFE stagedPrefab=" + scene.Wildlife + " nativeBiome=" + scene.Biome + " position=" + ground +
                            " liveAI=" + ai.enabled + " hostile=" + BaseAI.IsEnemy(player, character) + "; natural spawns remain untouched");
                        found = true; break;
                    }
                    Check(found, "plausible native nearby wildlife footing found " + scene.Biome + " " + scene.Wildlife);
                }
            }
            yield return new WaitForSecondsRealtime(1f);
        }

        void ShowcaseCamera(bool training)
        {
            Vector3 localTarget = training ? showcaseTreadmillAt : showcaseOrigin;
            Vector3 target = showcaseBoat ? showcaseBoat.transform.TransformPoint(localTarget) : localTarget;
            Vector3 camera = training ? showcaseTrainingCamera : showcaseRunCamera;
            if (showcaseBoat) camera = target + showcaseBoat.transform.rotation * camera;
            GameCamera.instance.transform.SetPositionAndRotation(camera, Quaternion.LookRotation(target + Vector3.up * 1.1f - camera));
        }

        IEnumerator ShowcaseRunShot(BiomeShot scene)
        {
            Controls(Vector3.zero);
            Vector3 start = showcaseBoat ? showcaseBoat.transform.TransformPoint(showcaseOrigin) : showcaseOrigin + showcaseForward * 3.4f;
            ShowcaseMove(start);
            yield return Wait(() => player.IsOnGround() && !player.IsSwimming(), "outdoor sprint starts on native footing " + scene.Biome, 10f);
            ShowcaseCamera(false);
            float staminaBefore = player.GetStamina(), skillBefore = Skill().m_level, accumulatorBefore = Skill().m_accumulator;
            float began = Time.realtimeSinceStartup, captureStarted = -1, distance = 0, runningSeconds = 0, nextLog = 0;
            Vector3 previous = player.transform.position;
            bool capturing = false, captured = false;
            int boatEnd = 1;
            while (Time.realtimeSinceStartup - began < 10.5f)
            {
                float elapsed = Time.realtimeSinceStartup - began;
                Vector3 target;
                if (showcaseBoat)
                {
                    Vector3 local = showcaseBoat.transform.InverseTransformPoint(player.transform.position);
                    Vector3 end = showcaseOrigin + Vector3.forward * (boatEnd == 1 ? 2.8f : 0);
                    if (Vector2.Distance(new Vector2(local.x, local.z), new Vector2(end.x, end.z)) < .65f) boatEnd = 1 - boatEnd;
                    target = showcaseBoat.transform.TransformPoint(showcaseOrigin + Vector3.forward * (boatEnd == 1 ? 2.8f : 0));
                }
                else
                {
                    Vector3 radial = player.transform.position - showcaseOrigin; radial.y = 0;
                    if (radial.sqrMagnitude < .5f) radial = showcaseForward;
                    Vector3 tangent = Vector3.Cross(Vector3.up, radial.normalized);
                    target = showcaseOrigin + (radial.normalized + tangent * .7f).normalized * 3.4f;
                }
                Vector3 direction = target - player.transform.position; direction.y = 0;
                player.SetLookDir(direction.normalized); Controls(Vector3.forward, true);
                ShowcaseCamera(false);
                if (!capturing && elapsed >= 1f && player.IsRunning())
                { Stage("showcase-" + scene.Slug + "-run"); capturing = true; captureStarted = Time.realtimeSinceStartup; }
                distance += Vector3.Distance(previous, player.transform.position); previous = player.transform.position;
                if (player.IsRunning()) runningSeconds += Time.unscaledDeltaTime;
                CheckShowcaseShot(scene, false);
                if (elapsed >= nextLog) { ShowcaseProgress(scene, "run", elapsed); nextLog = elapsed + 1; }
                if (!captured && elapsed >= 5)
                { captured = true; yield return Capture("showcase-" + scene.Slug + "-run", 0); }
                yield return null;
            }
            Controls(Vector3.zero);
            Check(capturing && Time.realtimeSinceStartup - captureStarted >= 8 && runningSeconds >= 8 && distance > 10 && player.GetStamina() < staminaBefore - 5,
                "native outdoor sprint visibly runs, travels and consumes ordinary stamina for " + scene.Biome);
            Check(Skill().m_level > skillBefore || Skill().m_accumulator > accumulatorBefore, "outdoor run genuinely gains native unfrozen XP " + scene.Biome);
            Log("SHOWCASE_RUN biome=" + scene.Biome + " runningSeconds=" + F(runningSeconds) + " distance=" + F(distance) +
                " staminaBefore=" + F(staminaBefore) + " staminaAfter=" + F(player.GetStamina()));
        }

        IEnumerator ShowcasePlaceTreadmill(BiomeShot scene)
        {
            Vector3 at = showcaseBoat ? showcaseBoat.transform.TransformPoint(showcaseTreadmillAt) : showcaseTreadmillAt;
            if (showcaseBoat)
            {
                Check(ShowcaseDeck(showcaseTreadmillAt, out at), "Ocean treadmill stands on real native boat deck");
            }
            else
                Check(ShowcaseGround(at, scene.Biome, out at), "treadmill remains on unmodified native biome terrain");
            var obj = Instantiate(Prefab("FitnessTreadmill"), at, Quaternion.LookRotation(showcaseBoat ? showcaseBoat.transform.forward : showcaseForward));
            showcaseObjects.Add(obj);
            if (showcaseBoat) obj.transform.SetParent(showcaseBoat.transform, true);
            treadmill = (Behaviour)obj.GetComponent(treadmillType);
            yield return Wait(() => treadmill && treadmill.isActiveAndEnabled && (bool)Get(treadmill, "initialized"), "showcase treadmill native network initialization", 10f);
            var anchor = (Transform)Get(treadmill, "attachPoint");
            Vector3 interact = default;
            bool approachFound = false;
            foreach (var offset in new[] { -obj.transform.right * 1.8f, obj.transform.right * 1.8f,
                -obj.transform.forward * 2f, obj.transform.forward * 2f })
            {
                Vector3 candidate = anchor.position + offset;
                bool clear = showcaseBoat
                    ? ShowcaseDeck(showcaseBoat.transform.InverseTransformPoint(candidate), out interact)
                    : ShowcaseGround(candidate, scene.Biome, out interact);
                if (!clear || Vector3.Distance(interact, anchor.position) >= 2.9f) continue;
                approachFound = true;
                break;
            }
            Check(approachFound, "dry native interaction approach outside the treadmill colliders");
            ShowcaseMove(interact);
            Controls(Vector3.zero);
            yield return Wait(() => player.IsOnGround() && !player.IsSwimming() && player.CanMove() && !player.InAttack() && !player.InDodge(),
                "native showcase interaction footing ready", 10f);
            ShowcaseCamera(true);
            Check(((Interactable)treadmill).Interact(player, false, false), "native showcase treadmill interaction accepted " + scene.Biome);
            yield return Wait(() => player.IsAttached() && player.GetAttachPoint() == anchor && player.IsRunning(), "real showcase treadmill training begins " + scene.Biome, 10f);
            yield return new WaitForSecondsRealtime(.5f);
        }

        IEnumerator ShowcaseTrainingShot(BiomeShot scene)
        {
            float staminaBefore = player.GetStamina(), skillBefore = Skill().m_level, accumulatorBefore = Skill().m_accumulator;
            float began = Time.realtimeSinceStartup, nextLog = 0;
            bool captured = false;
            Stage("showcase-" + scene.Slug + "-treadmill");
            while (Time.realtimeSinceStartup - began < 9f)
            {
                float elapsed = Time.realtimeSinceStartup - began;
                ShowcaseCamera(true);
                CheckShowcaseShot(scene, true);
                if (elapsed >= nextLog) { ShowcaseProgress(scene, "treadmill", elapsed); nextLog = elapsed + 1; }
                if (!captured && elapsed >= 4)
                { captured = true; yield return Capture("showcase-" + scene.Slug + "-treadmill", 0); }
                yield return null;
            }
            Check(player.GetStamina() < staminaBefore - 5 && (Skill().m_level > skillBefore || Skill().m_accumulator > accumulatorBefore),
                "real treadmill native stamina drain and free XP progression " + scene.Biome);
            Stage("showcase-gap");
            yield return Stop("showcase " + scene.Biome + " ordinary use dismount");
        }

        void CheckShowcaseShot(BiomeShot scene, bool training)
        {
            var heightmap = Heightmap.FindHeightmap(player.transform.position);
            if (!heightmap || heightmap.GetBiome(player.transform.position) != scene.Biome)
                throw new InvalidOperationException("Showcase loaded heightmap biome differs from requested native biome: " + scene.Biome);
            if (player.IsDead() || player.IsSwimming() || player.IsTeleporting() || player.GetCurrentBiome() != scene.Biome ||
                !ShowcaseBiome(player.transform.position, scene.Biome) || fixedSkill >= 0 || (bool)Get(player, "m_godMode"))
                throw new InvalidOperationException("Showcase native scene/resource/biome invariant lost: " + scene.Biome);
            if (training && (!player.IsAttached() || !player.IsRunning() || (bool)Get(player, "m_run")))
                throw new InvalidOperationException("Showcase treadmill must really run attached without Run input: " + scene.Biome);
        }

        void ShowcaseProgress(BiomeShot scene, string shot, float elapsed)
        {
            int live = 0;
            foreach (var obj in showcaseAnimals)
                if (obj && (!obj.GetComponent<BaseAI>() || obj.GetComponent<BaseAI>().enabled)) ++live;
            Log("SHOWCASE_PROGRESS biome=" + scene.Biome + " shot=" + shot + " elapsed=" + F(elapsed) + " actualBiome=" + player.GetCurrentBiome() +
                " Run=" + F(Skill().m_level) + " accumulator=" + F(Skill().m_accumulator) + " health=" + F(player.GetHealth()) + "/" + F(player.GetMaxHealth()) +
                " stamina=" + F(player.GetStamina()) + "/" + F(player.GetMaxStamina()) + " strain=" + Level + " liveStagedWildlife=" + live + " runInput=" + Get(player, "m_run"));
        }

        void ShowcaseObserve(BiomeShot scene, string phase, float baseHealth, float baseStamina, float regen)
        {
            Check(player.m_baseHP == baseHealth && player.m_baseStamina == baseStamina && player.m_staminaRegen == regen && regen > 0 &&
                !(bool)Get(player, "m_godMode") && !player.IsDead() && !player.IsEncumbered(), "showcase ordinary native base resources unchanged " + phase + " " + scene.Biome);
            Check(player.GetFoods().Count == 3, "three actual consumed native foods persist " + phase + " " + scene.Biome);
            foreach (var item in showcaseGear)
            {
                bool sheathed = ReferenceEquals(Get(player, "m_hiddenRightItem"), item) || ReferenceEquals(Get(player, "m_hiddenLeftItem"), item);
                Check(item.m_equipped || sheathed, "native gear remains equipped or natively sheathed " + item.m_dropPrefab.name + " " + phase);
                Log("SHOWCASE_GEAR_STATE biome=" + scene.Biome + " phase=" + phase + " prefab=" + item.m_dropPrefab.name +
                    " equipped=" + item.m_equipped + " nativeBackSlot=" + sheathed);
            }
            foreach (var food in player.GetFoods())
                Log("SHOWCASE_ACTIVE_FOOD biome=" + scene.Biome + " phase=" + phase + " prefab=" + food.m_name + " time=" + F(food.m_time) + " health=" + F(food.m_health) + " stamina=" + F(food.m_stamina));
            ShowcaseProgress(scene, phase, 0);
        }

        void ShowcaseDestroy(GameObject obj)
        {
            if (!obj) return;
            var view = obj.GetComponent<ZNetView>();
            if (view && view.IsValid()) ZNetScene.instance.Destroy(obj); else Destroy(obj);
        }

        void ShowcaseCleanup()
        {
            // Only props/animals created by this routine. Native terrain, vegetation and natural spawns are never removed.
            for (int i = showcaseObjects.Count - 1; i >= 0; --i) ShowcaseDestroy(showcaseObjects[i]);
            showcaseObjects.Clear(); showcaseAnimals.Clear(); showcaseBoat = null; treadmill = null;
        }
    }
}
