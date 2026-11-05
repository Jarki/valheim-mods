using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

// Separate opt-in client test plugin; never included in PistolMod.dll or the launcher.
[BepInPlugin("local.firearms.tests", "Firearm local tests", "1.0.0")]
[BepInDependency("local.pistol9mm")]
[DefaultExecutionOrder(20000)]
public sealed partial class FirearmSmoke : BaseUnityPlugin
{
    private string directory;
    private bool running;
    private bool poseOnly;
    private int passed, failed;
    private readonly List<GameObject> spawned = new List<GameObject>();
    private const string Rounds = "PistolMod.Rounds";
    private bool inspectCamera;
    private Vector3? impactCamera;
    private float readyAt;
    private static FirearmSmoke instance;
    private Harmony cameraPatch;

    private void ApplyInspectionCamera()
    {
        if (impactCamera.HasValue && Camera.main)
        {
            var origin = impactCamera.Value;
            Camera.main.transform.position = origin + new Vector3(6, 3, -1);
            Camera.main.transform.LookAt(origin + new Vector3(0, 1, 8));
            return;
        }
        if (!inspectCamera || !Player.m_localPlayer || !Camera.main) return;
        var body = Player.m_localPlayer.transform;
        var camera = Camera.main.transform;
        camera.position = body.position + body.right * 2 - body.forward * .6f + Vector3.up * 1.55f;
        camera.LookAt(body.position + Vector3.up * 1.35f + body.forward * .25f);
        // Native first-person hiding was calculated before this inspection camera override.
        AccessTools.Method(typeof(Character), "SetVisible").Invoke(Player.m_localPlayer, new object[] { true });
        foreach (var lod in Player.m_localPlayer.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);
    }

    private void Awake()
    {
        instance = this;
        directory = Environment.GetEnvironmentVariable("FIREARM_TEST_DIR");
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            cameraPatch = new Harmony("local.firearms.tests.camera");
            cameraPatch.PatchAll(typeof(FirearmSmoke).Assembly);
        }
    }

    private void OnDestroy()
    {
        cameraPatch?.UnpatchSelf();
        if (instance == this) instance = null;
    }

    [HarmonyPatch(typeof(GameCamera), "LateUpdate")]
    private static class InspectionCameraPatch
    {
        private static void Postfix() { if (instance) instance.ApplyInspectionCamera(); }
    }

    private void Update()
    {
        if (running || string.IsNullOrEmpty(directory)) return;
        poseOnly = File.Exists(Path.Combine(directory, "pose.request"));
        string request = Path.Combine(directory, poseOnly ? "pose.request" : "run.request");
        if (!File.Exists(request)) return;
        if (!Player.m_localPlayer || !ZNet.instance || Player.m_localPlayer.InCutscene()
            || Player.m_localPlayer.IsTeleporting() || !Player.m_localPlayer.IsOnGround()) return;
        var controller = Player.m_localPlayer.GetComponent("PistolController");
        if (!controller || !Call(controller, "CanUse")) { readyAt = 0; return; }
        if (readyAt == 0) readyAt = Time.time + 2;
        if (Time.time < readyAt) return;
        File.Delete(request);
        if (!ZNet.instance.IsServer() || ZNet.IsOpenServer() || ZNet.instance.GetPeerConnections() != 0
            || !ZNet.instance.GetWorldName().StartsWith("FirearmLab", StringComparison.OrdinalIgnoreCase))
        {
            Log("REFUSED: requires an unshared local FirearmLab* disposable world.");
            return;
        }
        running = true;
        StartCoroutine(Guard(Run()));
    }

    private IEnumerator Guard(IEnumerator scenario)
    {
        while (true)
        {
            object next;
            try
            {
                if (!scenario.MoveNext()) break;
                next = scenario.Current;
            }
            catch (Exception e) { failed++; Log("ERROR " + e); break; }
            yield return next;
        }
        foreach (var obj in spawned) if (obj) ZNetScene.instance.Destroy(obj);
        inspectCamera = false;
        impactCamera = null;
        if (Player.m_localPlayer)
            foreach (var lod in Player.m_localPlayer.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(-1);
        if (InventoryGui.instance) InventoryGui.instance.Hide();
        if (Player.m_localPlayer) Player.m_localPlayer.SetCraftingStation(null);
        spawned.Clear();
        Log($"RESULT {passed} passed, {failed} failed");
        File.WriteAllText(Path.Combine(directory, "complete.txt"), $"{passed} passed, {failed} failed");
        running = false;
    }

    private IEnumerator Run()
    {
        passed = failed = 0;
        var player = Player.m_localPlayer;
        var inventory = player.GetInventory();
        var controller = player.GetComponent("PistolController");
        Check(controller, "controller attached");
        foreach (var entry in new[] { ("Pistol9mm", "Ammo9mm", 17, 1.8f), ("RifleAKM", "Ammo762", 30, 2.5f), ("LauncherRPG", "AmmoRocket", 1, 3f) })
        {
            var prefab = PrefabManager.Instance.GetPrefab(entry.Item1);
            Check(prefab, entry.Item1 + " registered");
            var weapon = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
            weapon.m_dropPrefab = prefab;
            Check(inventory.AddItem(weapon), entry.Item1 + " added to inventory");
            inventory.AddItem(PrefabManager.Instance.GetPrefab(entry.Item2), entry.Item3 + 5);
            Check(player.EquipItem(weapon, false), entry.Item1 + " equips");
            yield return new WaitForSeconds(.6f);
            float readyDeadline = Time.time + 8;
            while (!Call(controller, "CanUse") && Time.time < readyDeadline) yield return null;
            Check(Call(controller, "CanUse"), entry.Item1 + " ready after equip");
            AccessTools.Field(typeof(Player), "m_lookPitch").SetValue(player, 0f);
            player.SetLookDir(player.transform.forward);
            AccessTools.Method(typeof(Player), "UpdateEyeRotation").Invoke(player, null);
            inspectCamera = true;
            yield return new WaitForSeconds(1);
            var visual = (Component)AccessTools.Method(controller.GetType(), "FindVisual").Invoke(controller, null);
            Check(visual, entry.Item1 + " equipped visual exists");
            var pose = player.GetComponent("FirearmPose");
            Check(pose && (bool)AccessTools.Method(pose.GetType(), "FindRig").Invoke(pose, null), entry.Item1 + " character rig resolves");
            if (visual)
            {
                var renderers = visual.GetComponentsInChildren<MeshRenderer>();
                bool supported = renderers.Length > 0;
                foreach (var renderer in renderers) supported &= renderer.sharedMaterial && renderer.sharedMaterial.shader.isSupported;
                Check(supported, entry.Item1 + " meshes have supported materials");
                Log(entry.Item1 + " material: " + renderers[0].sharedMaterial.shader.name);
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, entry.Item1 + "-hold.png"));
            yield return new WaitForSeconds(1);
            Check(!Call(controller, "Fire"), entry.Item1 + " empty fire rejected");
            Check(Call(controller, "BeginReload"), entry.Item1 + " reload starts");
            bool reloadPlaying = false;
            if (visual)
                foreach (var audio in visual.GetComponents<AudioSource>())
                    reloadPlaying |= audio.isPlaying && audio.clip && audio.clip.samples > 0;
            Check(reloadPlaying, entry.Item1 + " recorded reload audio plays");
            yield return new WaitForSeconds(entry.Item4 * .5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, entry.Item1 + "-reload.png"));
            yield return new WaitForSeconds(entry.Item4 * .5f + .2f);
            Check(Loaded(weapon) == entry.Item3, entry.Item1 + " magazine filled");
            float durability = weapon.m_durability;
            // Aim upward for trigger tests to avoid accidental scene damage.
            AimUp(player);
            Controls(player, true, true);
            yield return new WaitForSeconds(.2f);
            Check(Loaded(weapon) == entry.Item3 - 1, entry.Item1 + " attack consumes exactly one round");
            Check(weapon.m_durability < durability, entry.Item1 + " durability decreases");
            AimUp(player);
            Controls(player, false, true);
            yield return new WaitForSeconds(.2f);
            Check(Loaded(weapon) == Math.Max(0, entry.Item3 - (entry.Item1 == "RifleAKM" ? 2 : 1)), entry.Item1 + " held-trigger behavior");
            if (entry.Item1 == "RifleAKM")
            {
                int startRounds = Loaded(weapon);
                float until = Time.time + .7f;
                while (Time.time < until)
                {
                    Controls(player, false, true);
                    yield return null;
                }
                int shots = startRounds - Loaded(weapon);
                Check(shots >= 2 && shots <= 8, "RifleAKM sustained held fire respects cadence (shots=" + shots + ")");
            }
            inspectCamera = false;
            foreach (var lod in player.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(-1);
            var package = new ZPackage();
            inventory.Save(package);
            var copy = new Inventory("roundtrip", null, 8, 4);
            copy.Load(new ZPackage(package.GetArray()));
            var restored = copy.GetAllItems().Find(item => item.m_dropPrefab && item.m_dropPrefab.name == entry.Item1);
            Check(restored != null && Loaded(restored) == Loaded(weapon), entry.Item1 + " inventory Save/Load");
            int before = Loaded(weapon);
            Check(Call(controller, "BeginReload"), entry.Item1 + " partial reload starts");
            yield return new WaitForSeconds(.2f);
            player.UnequipItem(weapon, false);
            yield return new WaitForSeconds(entry.Item4 + .2f);
            Check(Loaded(weapon) == before, entry.Item1 + " interrupted reload preserves magazine");
            inventory.RemoveItem(weapon);
        }

        if (poseOnly) yield break;

        // Each target is a real networked building piece with real collision and Damage RPCs.
        Vector3 origin = player.transform.position + Vector3.up * 8 + Vector3.right * 15;
        var wall = Spawn("woodwall", origin, Quaternion.identity);
        var wear = wall.GetComponent<WearNTear>();
        wear.m_noSupportWear = false;
        yield return new WaitForFixedUpdate();
        float initial = wear.GetHealthPercentage();
        Detonate(player, origin + Vector3.forward * .4f);
        yield return new WaitForSeconds(.2f);
        Check(!wall || wear.GetHealthPercentage() < initial, "RPG damages unprotected structure");
        if (wall) ZNetScene.instance.Destroy(wall);

        wall = Spawn("woodwall", origin, Quaternion.identity);
        wear = wall.GetComponent<WearNTear>(); wear.m_noSupportWear = false;
        var ward = Spawn("guard_stone", origin + Vector3.right * 3, Quaternion.identity);
        ward.GetComponent<WearNTear>().m_noSupportWear = false;
        // Different creator, no permission for local shooter.
        ward.GetComponent<Piece>().SetCreator(player.GetPlayerID() + 123456, default);
        Check(ward.GetComponent<Piece>().GetCreator() == player.GetPlayerID() + 123456, "test ward has foreign creator");
        AccessTools.Method(typeof(PrivateArea), "SetEnabled").Invoke(ward.GetComponent<PrivateArea>(), new object[] { true });
        yield return new WaitForFixedUpdate();
        Check(!PrivateArea.CheckAccess(origin, 0, false, true), "test ward denies shooter");
        initial = wear.GetHealthPercentage();
        Detonate(player, origin + Vector3.forward * .4f);
        yield return new WaitForSeconds(.2f);
        Check(wall && Mathf.Approximately(initial, wear.GetHealthPercentage()), "RPG preserves denied ward structure");
        Check(ward, "RPG preserves denied ward itself");
        AccessTools.Method(typeof(PrivateArea), "AddPermitted").Invoke(ward.GetComponent<PrivateArea>(), new object[] { player.GetPlayerID(), "FirearmLab" });
        Check(PrivateArea.CheckAccess(origin, 0, false, true), "test ward permits shooter");
        Detonate(player, origin + Vector3.forward * .4f);
        yield return new WaitForSeconds(.2f);
        Check(!wall || wear.GetHealthPercentage() < initial, "RPG damages permitted ward structure");
        if (wall) ZNetScene.instance.Destroy(wall);
        if (ward) ZNetScene.instance.Destroy(ward);

        var near = SpawnWall(origin + Vector3.forward * 1.2f);
        var far = SpawnWall(origin - Vector3.forward * 4);
        var outside = SpawnWall(origin + Vector3.right * 8);
        yield return new WaitForFixedUpdate();
        Detonate(player, origin);
        yield return new WaitForSeconds(.2f);
        Check(near && far && near.GetComponent<WearNTear>().GetHealthPercentage() < far.GetComponent<WearNTear>().GetHealthPercentage(),
            "RPG damage falls off with distance");
        Check(outside && Mathf.Approximately(outside.GetComponent<WearNTear>().GetHealthPercentage(), 1), "outside blast radius untouched");
        if (near) ZNetScene.instance.Destroy(near);
        if (far) ZNetScene.instance.Destroy(far);

        var shield = SpawnWall(origin + Vector3.forward * 1.2f);
        var behind = SpawnWall(origin + Vector3.forward * 3);
        yield return new WaitForFixedUpdate();
        Detonate(player, origin);
        yield return new WaitForSeconds(.2f);
        Check(behind && Mathf.Approximately(behind.GetComponent<WearNTear>().GetHealthPercentage(), 1), "solid wall blocks blast damage");
        foreach (var obj in spawned) if (obj) ZNetScene.instance.Destroy(obj);
        spawned.Clear();
        var integration = Integration(player, origin);
        while (integration.MoveNext()) yield return integration.Current;
    }

    private GameObject Spawn(string prefab, Vector3 position, Quaternion rotation)
    {
        var template = ZNetScene.instance.GetPrefab(prefab) ?? PrefabManager.Instance.GetPrefab(prefab);
        if (!template) throw new InvalidOperationException("Test prefab unavailable: " + prefab);
        var obj = Instantiate(template, position, rotation);
        spawned.Add(obj);
        return obj;
    }

    private GameObject SpawnWall(Vector3 position)
    {
        var obj = Spawn("woodwall", position, Quaternion.identity);
        obj.GetComponent<WearNTear>().m_noSupportWear = false;
        return obj;
    }

    private void Detonate(Player player, Vector3 point)
    {
        var obj = Spawn("ProjectileRPG", point - Vector3.forward, Quaternion.identity);
        var projectile = obj.GetComponent<Projectile>();
        projectile.enabled = false;
        var hit = new HitData { m_damage = new HitData.DamageTypes { m_blunt = 160, m_fire = 40 }, m_pushForce = 80 };
        hit.SetAttacker(player);
        projectile.Setup(player, Vector3.forward * 45, 100, hit, null, null);
        AccessTools.Method(typeof(Projectile), "DoAOE").Invoke(projectile, new object[] { point, false, false });
        ZNetScene.instance.Destroy(obj);
    }

    private static int Loaded(ItemDrop.ItemData item) => item.m_customData.TryGetValue(Rounds, out var value) ? int.Parse(value) : 0;
    private void AimUp(Player player)
    {
        AccessTools.Field(typeof(Player), "m_lookPitch").SetValue(player, -89f);
        AccessTools.Method(typeof(Player), "UpdateEyeRotation").Invoke(player, null);
        Check(Vector3.Dot(player.GetAimDir(player.GetEyePoint()), Vector3.up) > .99f, "trigger-test aim is upward");
    }
    private static bool Call(Component component, string method) => (bool)AccessTools.Method(component.GetType(), method).Invoke(component, null);

    private static void Controls(Player player, bool pressed, bool held)
    {
        var method = AccessTools.Method(typeof(Player), "SetControls");
        var parameters = method.GetParameters();
        var args = new object[parameters.Length];
        for (int i = 0; i < args.Length; i++)
        {
            Type type = parameters[i].ParameterType;
            args[i] = type.IsValueType ? Activator.CreateInstance(type) : null;
            if (parameters[i].Name == "attack") args[i] = pressed;
            if (parameters[i].Name == "attackHold") args[i] = held;
        }
        method.Invoke(player, args);
    }

    private void Check(bool condition, string message)
    {
        if (condition) passed++; else failed++;
        Log((condition ? "PASS " : "FAIL ") + message);
    }
    private void Log(string message)
    {
        Logger.LogInfo(message);
        File.AppendAllText(Path.Combine(directory, "results.log"), message + Environment.NewLine);
    }
}
