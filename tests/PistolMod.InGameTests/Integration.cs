using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

public sealed partial class FirearmSmoke
{
    private IEnumerator Integration(Player player, Vector3 origin)
    {
        // Exercise native swept collision and the real RPG impact callback, not DoAOE directly.
        foreach (string projectileName in new[] { "Projectile9mm", "ProjectileRPG" })
        {
            var front = SpawnWall(origin + Vector3.forward * 8);
            var rear = SpawnWall(origin + Vector3.forward * 11);
            impactCamera = origin;
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(1);
            bool rocket = projectileName == "ProjectileRPG";
            var obj = Spawn(projectileName, origin + Vector3.up, Quaternion.identity);
            var hit = new HitData { m_damage = rocket
                ? new HitData.DamageTypes { m_blunt = 160, m_fire = 40 }
                : new HitData.DamageTypes { m_pierce = 35 }, m_toolTier = 2 };
            hit.SetAttacker(player);
            obj.GetComponent<Projectile>().Setup(player, Vector3.forward * (rocket ? 45 : 180), 40, hit, null, null);
            yield return new WaitForSeconds(.3f);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, projectileName + "-impact.png"));
            yield return new WaitForSeconds(1.7f);
            Check(!front || front.GetComponent<WearNTear>().GetHealthPercentage() < 1, projectileName + " travels and damages first wall");
            Check(rear && Mathf.Approximately(rear.GetComponent<WearNTear>().GetHealthPercentage(), 1), projectileName + " wall shields rear target");
            foreach (var piece in new[] { front, rear, obj }) if (piece) ZNetScene.instance.Destroy(piece);
            impactCamera = null;
        }

        // Real forge, real extensions, normal recipe discovery/resources and the craft button handler.
        // Only this disposable fixture's roof/fire checks are waived to avoid constructing a hut.
        var forgeObject = Spawn("forge", player.transform.position + player.transform.right * 1.4f, Quaternion.identity);
        forgeObject.GetComponent<WearNTear>().m_noSupportWear = false;
        var forge = forgeObject.GetComponent<CraftingStation>();
        forge.m_craftRequireRoof = false;
        forge.m_craftRequireFire = false;
        // A fixture on the start altar must not shove the player away from interaction range.
        foreach (var collider in forgeObject.GetComponentsInChildren<Collider>()) collider.enabled = false;
        forge.transform.position = player.transform.position + Vector3.down * (forge.m_useDistance * .25f);
        int extensions = 0;
        foreach (var template in ZNetScene.instance.m_prefabs)
        {
            var extension = template.GetComponent<StationExtension>();
            if (!extension || !extension.m_craftingStation || extension.m_craftingStation.m_name != forge.m_name) continue;
            var piece = Spawn(template.name, forge.transform.position + Vector3.forward * (1 + extensions), Quaternion.identity);
            if (piece.GetComponent<WearNTear>()) piece.GetComponent<WearNTear>().m_noSupportWear = false;
            foreach (var collider in piece.GetComponentsInChildren<Collider>()) collider.enabled = false;
            if (++extensions == 2) break;
        }
        yield return new WaitForSeconds(.3f);
        Check(forge.GetLevel() >= 3, "fixture has level-three forge");
        var inventory = player.GetInventory();
        foreach (string name in new[] { "Pistol9mm", "Ammo9mm", "RifleAKM", "Ammo762", "LauncherRPG", "AmmoRocket" })
        {
            Recipe recipe = ObjectDB.instance.GetRecipe(PrefabManager.Instance.GetPrefab(name).GetComponent<ItemDrop>().m_itemData);
            Check(recipe && recipe.m_enabled, name + " enabled crafting recipe");
            if (!recipe) continue;
            var before = new Dictionary<string, int>();
            foreach (var resource in recipe.m_resources)
            {
                int remaining = resource.GetAmount(1);
                while (remaining > 0)
                {
                    int stack = Math.Min(remaining, resource.m_resItem.m_itemData.m_shared.m_maxStackSize);
                    Check(inventory.AddItem(resource.m_resItem.gameObject, stack), name + " fixture grants " + resource.m_resItem.name);
                    remaining -= stack;
                }
                string resourceName = resource.m_resItem.m_itemData.m_shared.m_name;
                before[resourceName] = inventory.CountItems(resourceName);
            }
            yield return new WaitForSeconds(.2f);
            Log("forge distance=" + Vector3.Distance(forge.transform.position, player.transform.position) + " useDistance=" + forge.m_useDistance);
            Check(forge.InUseDistance(player), "fixture is within forge use distance");
            if (!forge.InUseDistance(player)) throw new InvalidOperationException("Forge fixture outside native use distance; see measured values");
            forge.Interact(player, false, false);
            yield return new WaitForSeconds(.3f);
            var gui = InventoryGui.instance;
            Check(player.GetCurrentCraftingStation() == forge && InventoryGui.IsVisible(), "forge interaction opens crafting UI");
            gui.OnTabCraftPressed();
            var recipes = (IList)AccessTools.Field(typeof(InventoryGui), "m_availableRecipes").GetValue(gui);
            int index = -1;
            for (int i = 0; i < recipes.Count; i++)
                if ((Recipe)AccessTools.Property(recipes[i].GetType(), "Recipe").GetValue(recipes[i]) == recipe) { index = i; break; }
            Check(index >= 0, name + " appears in forge crafting UI");
            if (index < 0) { gui.Hide(); continue; }
            AccessTools.Method(typeof(InventoryGui), "SetRecipe").Invoke(gui, new object[] { index, false });
            yield return new WaitForSeconds(.2f);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, name + "-craft.png"));
            string itemName = recipe.m_item.m_itemData.m_shared.m_name;
            int initial = inventory.CountItems(itemName);
            AccessTools.Method(typeof(InventoryGui), "OnCraftPressed").Invoke(gui, null);
            yield return new WaitForSeconds(3);
            Check(inventory.CountItems(itemName) == initial + recipe.m_amount, name + " crafted by normal UI handler");
            foreach (var resource in recipe.m_resources)
            {
                string resourceName = resource.m_resItem.m_itemData.m_shared.m_name;
                Check(inventory.CountItems(resourceName) == before[resourceName] - resource.GetAmount(1), name + " consumes " + resource.m_resItem.name);
            }
            gui.Hide();
        }
        player.SetCraftingStation(null);
    }
}
