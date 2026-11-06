using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Rotheart.InGameTests
{
    // This assembly belongs only in the explicitly enabled disposable lab profile.
    [BepInPlugin("local.rotheart.ingame-tests", "Rotheart isolated in-game checks", "1.0.0")]
    [BepInDependency("jarki.rotheart")]
    [DefaultExecutionOrder(20000)]
    public sealed class Host : BaseUnityPlugin
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        static Host active;
        string dir, profileName, worldName;
        Player player;
        ItemDrop.ItemData charm;
        int passes, nativeErrors;
        readonly List<Character> fixtures = new List<Character>();
        readonly List<float> pulseTimes = new List<float>();
        readonly List<float> pulseDamage = new List<float>();
        Character near, far, tame, friendly;
        Player other;
        float activationTime;
        Harmony observer;

        static FieldInfo Member(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, Flags);
                if (field != null) return field;
            }
            throw new MissingFieldException(obj.GetType().FullName, name);
        }
        static object Get(object obj, string name) => Member(obj, name).GetValue(obj);
        static void Set(object obj, string name, object value) => Member(obj, name).SetValue(obj, value);
        void Log(string text) { File.AppendAllText(Path.Combine(dir, "runtime.log"), text + "\n"); Logger.LogInfo(text); }
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + label);
            ++passes; Log("PASS: " + label);
        }
        void Start()
        {
            if (Environment.GetEnvironmentVariable("ROTHEART_RUN") != "disposable-local-only") { enabled = false; return; }
            dir = Environment.GetEnvironmentVariable("ROTHEART_EVIDENCE");
            string saves = Environment.GetEnvironmentVariable("ROTHEART_SAVES");
            string xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            string run = Environment.GetEnvironmentVariable("ROTHEART_RUN_ID");
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(saves) || string.IsNullOrEmpty(xdg) || string.IsNullOrEmpty(run) ||
                !Path.GetFullPath(dir).Contains("/.dev/rotheart-lab/") || !Path.GetFullPath(saves).Contains("/.dev/rotheart-lab/") ||
                !Path.GetFullPath(xdg).Contains("/.dev/rotheart-lab/") ||
                Array.IndexOf(Environment.GetCommandLineArgs(), saves) < 0)
            { Logger.LogError("ROTHEART harness refuses non-isolated paths or missing -savedir argument"); enabled = false; return; }
            Directory.CreateDirectory(dir);
            profileName = "rotheart" + run;
            worldName = "Rotheart" + run;
            Application.runInBackground = true;
            active = this;
            Application.logMessageReceived += ObserveLog;
            observer = new Harmony("local.rotheart.ingame-tests.observer");
            observer.Patch(typeof(Character).GetMethod("Damage", new[] { typeof(HitData) }), prefix: new HarmonyMethod(typeof(Host), nameof(ObserveDamage)));
            StartCoroutine(Execute(Run()));
        }
        void ObserveLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) ++nativeErrors;
        }
        // Observation only: never changes the hit, target, clock, or production status effect.
        static void ObserveDamage(Character __instance, HitData hit)
        {
            if (active == null || __instance != active.near || hit.GetAttacker() != active.player) return;
            active.pulseTimes.Add(Time.time - active.activationTime);
            active.pulseDamage.Add(hit.m_damage.m_blunt);
            active.Log("PULSE time=" + (Time.time - active.activationTime).ToString("F3") + " blunt=" + hit.m_damage.m_blunt);
        }
        IEnumerator Execute(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            bool failed = false;
            while (stack.Count != 0)
            {
                var top = stack.Peek(); bool more = false;
                try { more = top.MoveNext(); }
                catch (Exception error) { Log("FAIL: " + error); failed = true; }
                if (failed) break;
                if (!more) { stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) stack.Push(nested); else yield return top.Current;
            }
            observer.UnpatchSelf(); active = null;
            Application.logMessageReceived -= ObserveLog;
            string result = (failed ? "FAIL" : "PASS") + " " + passes;
            File.WriteAllText(Path.Combine(dir, "complete.txt"), result + "\n");
            Log("ROTHEART_RUNTIME_COMPLETE " + result);
            yield return new WaitForSecondsRealtime(2f);
            Application.Quit();
        }
        IEnumerator Wait(Func<bool> ready, string label, float timeout = 45f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!ready())
            {
                if (CinematicsManager.s_instance && CinematicsManager.IsStartedPlaying()) CinematicsManager.Stop();
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException(label);
                yield return null;
            }
        }
        IEnumerator Capture(string name, float hold = 2f)
        {
            yield return new WaitForSecondsRealtime(hold);
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(dir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return Wait(() => File.Exists(path) && new FileInfo(path).Length > 100, "PNG written: " + name, 15f);
            Log("SCREENSHOT " + name + ".png");
        }
        IEnumerator FramedCapture(string name, Vector3 cameraPosition, Vector3 target, float hold, float fov = 65f)
        {
            // Frame the real scene, not a rendered stand-in. Gameplay/SEMan continues untouched.
            var rig = GameCamera.instance;
            bool wasEnabled = rig.enabled;
            var camera = rig.GetComponent<Camera>();
            float previousFov = camera.fieldOfView;
            camera.fieldOfView = fov;
            Vector3 position = rig.transform.position;
            Quaternion rotation = rig.transform.rotation;
            rig.enabled = false;
            rig.transform.SetPositionAndRotation(cameraPosition, Quaternion.LookRotation(target - cameraPosition));
            yield return Capture(name, hold);
            rig.transform.SetPositionAndRotation(position, rotation);
            camera.fieldOfView = previousFov;
            rig.enabled = wasEnabled;
        }
        GameObject Prefab(string name)
        {
            var prefab = ZNetScene.instance.GetPrefab(name);
            if (!prefab) throw new InvalidOperationException("Missing fixture prefab " + name);
            return prefab;
        }
        ItemDrop.ItemData Add(string name, int amount)
        {
            var prefab = Prefab(name); var item = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
            item.m_dropPrefab = prefab; item.m_stack = amount;
            Check(player.GetInventory().AddItem(item), "fixture inventory add " + name);
            return item;
        }
        ItemDrop.ItemData Find(string name)
        {
            foreach (var item in player.GetInventory().GetAllItems()) if (item.m_dropPrefab && item.m_dropPrefab.name == name) return item;
            return null;
        }
        int Count(string name)
        {
            int count = 0;
            foreach (var item in player.GetInventory().GetAllItems()) if (item.m_dropPrefab && item.m_dropPrefab.name == name) count += item.m_stack;
            return count;
        }
        StatusEffect Aura => player.GetSEMan().GetStatusEffect("RotheartAura".GetStableHashCode());
        StatusEffect Resistance => player.GetSEMan().GetStatusEffect("RotheartResistance".GetStableHashCode());
        void ResetAdrenaline()
        {
            player.AddAdrenaline(-player.GetAdrenaline());
            // Fixture removes native gain/degen variability, not threshold/activation behavior.
            player.m_adrenalineGainMultiplier = AnimationCurve.Linear(0f, 1f, 1f, 1f);
            player.m_adrenalineDegen = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        }
        void Activate()
        {
            ResetAdrenaline(); pulseTimes.Clear(); pulseDamage.Clear(); activationTime = Time.time;
            player.AddAdrenaline(10f);
            Check(Aura && Mathf.Abs(player.GetAdrenaline()) < .01f, "native full adrenaline activates aura and consumes meter");
        }
        Character Creature(string prefabName, Vector3 offset)
        {
            var character = Instantiate(Prefab(prefabName), player.transform.position + offset, Quaternion.identity).GetComponent<Character>();
            if (!character) throw new InvalidOperationException("Not a creature: " + prefabName);
            var ai = character.GetComponent<BaseAI>(); if (ai) ai.enabled = false;
            character.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
            fixtures.Add(character); return character;
        }
        GameObject Piece(string name, Vector3 position, Quaternion rotation)
        {
            var go = Instantiate(Prefab(name), position, rotation);
            var wear = go.GetComponent<WearNTear>(); if (wear) wear.enabled = false;
            return go;
        }
        IEnumerator Run()
        {
            yield return Wait(() => FejdStartup.instance && FejdStartup.instance.m_mainMenu.activeInHierarchy, "startup main menu", 180f);
            var menu = FejdStartup.instance;
            var profile = new PlayerProfile(profileName, FileHelpers.FileSource.Local);
            profile.SetName("Rotheart Lab"); profile.m_firstSpawn = false;
            Check(profile.Save(), "new disposable local character saved");
            Set(menu, "m_profiles", new List<PlayerProfile> { profile }); Set(menu, "m_profileIndex", 0);
            Game.SetProfile(profileName, FileHelpers.FileSource.Local);
            var world = new World(worldName, "rotheart") { m_fileSource = FileHelpers.FileSource.Local, m_needsDB = false };
            world.SaveWorldFWLData(DateTime.Now); Set(menu, "m_world", world);
            menu.m_openServerToggle.SetIsOnWithoutNotify(false); menu.m_publicServerToggle.SetIsOnWithoutNotify(false);
            menu.m_crossplayServerToggle.SetIsOnWithoutNotify(false); menu.m_serverPassword.text = "";
            menu.OnWorldStart();
            yield return Wait(() => !FejdStartup.instance && Game.instance && !Game.instance.WaitingForRespawn() && Player.m_localPlayer &&
                !Player.m_localPlayer.IsTeleporting() && !Player.m_localPlayer.InCutscene() && !Player.m_localPlayer.InIntro(), "disposable world spawn", 300f);
            Check(ZNet.IsSinglePlayer && ZNet.instance.GetWorld().m_name == worldName && ZNet.instance.GetWorld().m_fileSource == FileHelpers.FileSource.Local,
                "single-player local disposable world guard");
            player = Player.m_localPlayer; player.SetGodMode(false); player.GetInventory().RemoveAll();
            if (TextViewer.instance) TextViewer.instance.Hide();
            yield return new WaitForSecondsRealtime(5f);
            // Remove random attackers from measurement without altering test targets or production logic.
            foreach (var character in new List<Character>(Character.GetAllCharacters()))
                if (character != player && character.GetComponent<BaseAI>()) character.GetComponent<BaseAI>().enabled = false;
            var prefab = Prefab("Rotheart");
            Recipe recipe = null;
            foreach (var candidate in ObjectDB.instance.m_recipes)
                if (candidate.m_item && candidate.m_item.gameObject.name == "Rotheart") { Check(recipe == null, "no duplicate Rotheart recipe"); recipe = candidate; }
            Check(recipe && recipe.m_enabled && recipe.m_amount == 1 && recipe.m_craftingStation && recipe.m_craftingStation.gameObject.name == "forge" && recipe.m_minStationLevel == 1,
                "enabled recipe yields one at forge tier one");
            var expected = new Dictionary<string, int> { { "Ooze", 10 }, { "Guck", 5 }, { "Entrails", 5 }, { "Iron", 5 } };
            Check(recipe.m_resources.Length == expected.Count, "exact recipe ingredient count");
            foreach (var requirement in recipe.m_resources)
            {
                Check(expected.TryGetValue(requirement.m_resItem.gameObject.name, out var count) && requirement.GetAmount(1) == count, "recipe quantity " + requirement.m_resItem.name);
                Add(requirement.m_resItem.gameObject.name, count);
            }
            // A real tier-one forge, with a physical shelter. Support decay is disabled only on fixture building pieces.
            EnvMan.instance.m_debugTimeOfDay = true;
            EnvMan.instance.m_debugTime = .5f;
            Vector3 forgeAt = player.transform.position + new Vector3(0f, 0f, 1.2f);
            var forge = Piece("forge", forgeAt, Quaternion.identity).GetComponent<CraftingStation>();
            for (int x = -2; x <= 2; ++x)
                for (int z = -2; z <= 2; ++z) Piece("wood_roof", forgeAt + new Vector3(x * 2f, 3f, z * 2f), Quaternion.identity);
            for (int side = 0; side < 4; ++side)
                for (int across = -2; across <= 2; ++across)
                    for (int level = 0; level < 2; ++level)
                    {
                        var rotation = Quaternion.Euler(0f, side * 90f, 0f);
                        Piece("woodwall", forgeAt + rotation * new Vector3(across * 2f, level * 2f, 4f), rotation);
                    }
            if (forge.m_craftRequireFire)
                Piece("fire_pit", forgeAt + new Vector3(1f, 0f, 1f), Quaternion.identity);
            yield return new WaitForSeconds(2f);
            Cover.GetCoverForPoint(forge.m_roofCheckPoint.position, out var cover, out var roof);
            Log("FORGE cover=" + cover + " roof=" + roof + " requiresFire=" + forge.m_craftRequireFire
                + " hasFire=" + Get(forge, "m_haveFire") + " level=" + forge.GetLevel() + " freeCraft=" + player.NoCostCheat());
            yield return Capture("00-forge-fixture", .5f);
            Check(!player.NoCostCheat() && forge.GetLevel() == 1 && forge.CheckUsable(player, false), "real usable tier-one forge, no free-craft cheat");
            Check(forge.InUseDistance(player), "fixture wearer stands inside native forge interaction range");
            forge.Interact(player, false, false);
            yield return Wait(() => InventoryGui.IsVisible() && player.GetCurrentCraftingStation() == forge, "forge opens native crafting GUI");
            var gui = InventoryGui.instance;
            int index = -1;
            yield return Wait(() =>
            {
                var recipes = (IList)Get(gui, "m_availableRecipes");
                for (int i = 0; i < recipes.Count; ++i)
                    if ((Recipe)recipes[i].GetType().GetProperty("Recipe").GetValue(recipes[i]) == recipe) { index = i; return true; }
                return false;
            }, "Rotheart discovered in real recipe list");
            typeof(InventoryGui).GetMethod("SetRecipe", Flags).Invoke(gui, new object[] { index, true });
            yield return Wait(() => gui.m_craftButton.interactable, "native craft button enabled");
            yield return Capture("01-forge-recipe");
            gui.m_craftButton.onClick.Invoke();
            yield return Wait(() => Find("Rotheart") != null, "native crafting timer produces Rotheart");
            charm = Find("Rotheart");
            Check(charm.m_quality == 1 && charm.m_crafterID == player.GetPlayerID(), "real craft produces quality-one player-authored item");
            foreach (var ingredient in expected) Check(Count(ingredient.Key) == 0, "native crafting consumed " + ingredient.Key);
            gui.Hide(); player.SetCraftingStation(null);
            // Move the measured combat scene clear of the forge while preserving natural terrain height.
            var combatAt = player.transform.position + new Vector3(14f, 0f, 0f);
            combatAt.y = ZoneSystem.instance.GetGroundHeight(combatAt) + .05f;
            player.transform.position = combatAt;
            player.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
            yield return new WaitForSeconds(1f);
            player.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
            float normalMax = player.GetMaxAdrenaline();
            Check(player.EquipItem(charm, false), "native EquipItem accepts crafted trinket");
            yield return Wait(() => Resistance, "passive equip status");
            Check(player.GetDamageModifiers().m_poison == HitData.DamageModifier.Resistant, "equipped poison resistance is native 50 percent Resistant");
            var mead = Prefab("MeadPoisonResist").GetComponent<ItemDrop>().m_itemData.m_shared.m_consumeStatusEffect;
            player.GetSEMan().AddStatusEffect(mead);
            Check(player.GetDamageModifiers().m_poison == HitData.DamageModifier.VeryResistant, "stronger native poison mead is not weakened by Rotheart");
            player.GetSEMan().RemoveStatusEffect(mead.NameHash());
            Check(player.GetDamageModifiers().m_poison == HitData.DamageModifier.Resistant, "Rotheart resistance remains after mead ends");
            Check(Mathf.Abs(player.GetMaxAdrenaline() - 10f) < .01f, "equipped native activation threshold ten");
            Check(!Aura, "equipping alone does not activate aura");
            gui.Show(null);
            yield return new WaitForSecondsRealtime(1f);
            var elements = (List<InventoryElement>)Get(gui.m_playerGrid, "m_elements");
            InventoryElement icon = null;
            foreach (var element in elements) if (element.Position.x == charm.m_gridPos.x && element.Position.y == charm.m_gridPos.y) icon = element;
            Check(icon, "crafted icon exists in actual inventory grid");
            var corners = new Vector3[4]; icon.GetElementRectTransform().GetWorldCorners(corners);
            var point = RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) * .5f);
            File.WriteAllText(Path.Combine(dir, "input.json"), "{\"kind\":\"hover\",\"x\":" + (int)point.x + ",\"y\":" + (Screen.height - (int)point.y) + "}");
            yield return Wait(() => File.Exists(Path.Combine(dir, "input.done")), "launcher hovers actual inventory icon");
            yield return Capture("02-inventory-icon-tooltip", 3f);
            gui.Hide();
            yield return Wait(() => Utils.FindChild(player.transform, "Rotheart_Visual"), "equipped charm attached to native skeleton");
            var worn = Utils.FindChild(player.transform, "Rotheart_Visual");
            yield return FramedCapture("02b-equipped-charm", worn.position + player.transform.forward * 2.5f + Vector3.up * .2f,
                worn.position, 2f, 25f);
            profile = Game.instance.GetPlayerProfile(); profile.SavePlayerData(player);
            Check(profile.Save(), "actual character saved with custom item");
            var loaded = new PlayerProfile(profileName, FileHelpers.FileSource.Local);
            Check(loaded.Load(), "actual local character file reloaded");
            // Native Load expects a fresh Player; loading over the live instance duplicates known-station dictionaries.
            var restoredObject = Instantiate(Prefab("Player"), player.transform.position + Vector3.up * 50f, Quaternion.identity);
            var restoredPlayer = restoredObject.GetComponent<Player>();
            loaded.LoadPlayerData(restoredPlayer);
            ItemDrop.ItemData restoredCharm = null;
            foreach (var item in restoredPlayer.GetInventory().GetAllItems())
                if (item.m_dropPrefab && item.m_dropPrefab.name == "Rotheart") restoredCharm = item;
            Check(restoredCharm != null && restoredCharm.m_quality == 1 && restoredCharm.m_crafterID == player.GetPlayerID(),
                "custom prefab quality crafter survive disk save/load into fresh Player");
            ZNetScene.instance.Destroy(restoredObject);
            near = Creature("Greydwarf", new Vector3(3f, 0f, 0f));
            far = Creature("Greydwarf", new Vector3(6.5f, 0f, 0f));
            var draugr = Creature("Draugr", new Vector3(2.5f, 0f, 2.5f));
            var blob = Creature("Blob", new Vector3(-2.5f, 0f, -2.5f));
            tame = Creature("Boar", new Vector3(-3f, 0f, 0f)); tame.SetTamed(true);
            friendly = Creature("Deer", new Vector3(0f, 0f, 3f)); friendly.m_faction = player.m_faction;
            other = (Player)Creature("Player", new Vector3(0f, 0f, -3f)); other.SetPlayerID(918273645L, "Disposable observer");
            var structure = Piece("woodwall", player.transform.position + new Vector3(-2f, 0f, 2f), Quaternion.identity).GetComponent<WearNTear>();
            other.SetPVP(true); player.SetPVP(true);
            var controller = other.GetComponent<PlayerController>(); if (controller) controller.enabled = false;
            yield return new WaitForSeconds(1f);
            foreach (var character in fixtures) { character.SetMaxHealth(200f); character.SetHealth(200f); }
            player.SetHealth(25f);
            Check(tame.IsTamed() && BaseAI.IsEnemy(player, near) && !BaseAI.IsEnemy(player, friendly), "real hostile tamed and friendly fixtures classified");
            Check(Vector3.Distance(near.transform.position, player.transform.position) < 6f && Vector3.Distance(far.transform.position, player.transform.position) > 6f,
                "targets physically inside and outside six metres");
            ResetAdrenaline(); player.AddAdrenaline(9f);
            yield return new WaitForSeconds(2.2f);
            Check(!Aura && Mathf.Abs(player.GetHealth() - 25f) < .01f && Mathf.Abs(near.GetHealth() - 200f) < .01f, "below-ten meter does not activate or damage");
            float otherHealth = other.GetHealth();
            float structureHealth = structure.GetHealthPercentage();
            float expectedHealthLoss = 60f * Game.instance.GetDifficultyDamageScaleEnemy(near.transform.position) * Game.m_playerDamageRate;
            Activate();
            yield return FramedCapture("03-aura-combat", player.transform.position + new Vector3(5f, 3f, -7f),
                player.transform.position + new Vector3(1f, 1f, 0f), 3f);
            yield return Wait(() => !Aura, "ten-second aura expires", 15f);
            yield return new WaitForSeconds(.2f);
            Check(pulseTimes.Count == 5, "five native pulses during ten-second effect");
            for (int i = 0; i < pulseTimes.Count; ++i)
            {
                Check(Mathf.Abs(pulseTimes[i] - (i + 1) * 2f) < .25f, "native pulse timing " + (i + 1));
                Check(Mathf.Abs(pulseDamage[i] - 12f) < .01f, "twelve BLUNT hit payload " + (i + 1));
            }
            Check(Mathf.Abs(player.GetHealth() - 20f) < .01f, "five pulses cost exactly five wearer health");
            Check(Mathf.Abs(near.GetHealth() - (200f - expectedHealthLoss)) < .1f, "five blunt hits apply actual native difficulty-scaled health damage: " + expectedHealthLoss);
            Check(200f - draugr.GetHealth() > 5f && 200f - blob.GetHealth() > 5f,
                "swamp draugr and poison-immune blob lose more health than wearer");
            Check(Mathf.Abs(far.GetHealth() - 200f) < .01f, "outside six metres takes no damage");
            Check(Mathf.Abs(tame.GetHealth() - 200f) < .01f, "tamed creature takes no damage");
            Check(Mathf.Abs(friendly.GetHealth() - 200f) < .01f, "same-faction creature takes no damage");
            Check(Mathf.Abs(other.GetHealth() - otherHealth) < .01f, "other actual Player excluded even with PvP enabled");
            Check(structure && Mathf.Abs(structure.GetHealthPercentage() - structureHealth) < .001f, "nearby physical structure excluded from aura damage");
            player.SetHealth(1.5f); Activate();
            yield return new WaitForSeconds(4.3f);
            Check(!player.IsDead() && Mathf.Abs(player.GetHealth() - 1f) < .01f, "fractional last health cost clamps to nonlethal floor one");
            player.UnequipItem(charm, false);
            yield return Wait(() => !Aura && !Resistance, "unequip removes active and passive effects");
            Check(Mathf.Abs(player.GetMaxAdrenaline() - normalMax) < .01f && player.GetDamageModifiers().m_poison != HitData.DamageModifier.Resistant,
                "unequip restores native meter maximum and poison modifier");
            float afterUnequip = near.GetHealth();
            yield return new WaitForSeconds(2.3f);
            Check(Mathf.Abs(near.GetHealth() - afterUnequip) < .01f && Mathf.Abs(player.GetHealth() - 1f) < .01f, "unequipped aura stops hostile damage and self cost");
            player.SetHealth(25f); player.EquipItem(charm, false);
            yield return Wait(() => Resistance, "reequip for refresh test");
            Activate(); yield return new WaitForSeconds(2.3f);
            Check(pulseTimes.Count == 1, "first activation has one pulse before refresh");
            Activate(); yield return new WaitForSeconds(1.8f);
            Check(pulseTimes.Count == 0, "native refresh resets pulse timer without duplicate early pulse");
            yield return new WaitForSeconds(.5f);
            Check(pulseTimes.Count == 1, "refreshed aura resumes one pulse at two seconds");
            player.UnequipItem(charm, false);
            var drop = Instantiate(prefab, player.transform.position + player.transform.forward * 1.5f + Vector3.up * .5f, Quaternion.identity);
            var body = drop.GetComponent<Rigidbody>(); if (body) body.constraints = RigidbodyConstraints.FreezeAll;
            drop.GetComponent<ItemDrop>().m_autoPickup = false;
            foreach (var renderer in drop.GetComponentsInChildren<MeshRenderer>())
            {
                var material = renderer.sharedMaterial;
                Log("MATERIAL " + material.name + " shader=" + material.shader.name + " color=" + material.color);
            }
            yield return FramedCapture("04-visible-charm-drop", drop.transform.position + new Vector3(.8f, .45f, -1.1f),
                drop.transform.position + Vector3.up * .05f, 4f, 25f);
            player.EquipItem(charm, false); yield return Wait(() => Resistance, "reequip for death test");
            Activate(); yield return new WaitForSeconds(2.2f);
            float beforeDeath = near.GetHealth();
            var lethal = new HitData(); lethal.m_damage.m_damage = 10000f; lethal.m_point = player.GetCenterPoint();
            player.Damage(lethal);
            yield return Wait(() => player.IsDead(), "real lethal hit causes native death", 10f);
            yield return new WaitForSeconds(2.5f);
            Check(!Aura && Mathf.Abs(near.GetHealth() - beforeDeath) < .01f, "native death removes aura and stops hostile pulses");
            yield return Capture("05-death-stops-aura", .5f);
            Check(nativeErrors == 0, "no Unity errors or exceptions during the native scenario");
        }
    }
}
