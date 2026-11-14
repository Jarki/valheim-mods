using System;
using System.Collections;
using System.Globalization;
using UnityEngine;

namespace Fitness.InGameTests
{
    public sealed partial class Host
    {
        double Distance()
        {
            return (double)Call(fitness.GetType().Assembly.GetType("Fitness.RunDistance", true), "Metres", player);
        }

        IEnumerator CheckDistance()
        {
            Stage("distance-server-world-counter");
            SetSkill(0f); FillStamina();
            double before = Distance();
            yield return Begin();
            float start = Time.time;
            yield return new WaitForSeconds(2f);
            double delta = Distance() - before;
            float elapsed = Time.time - start;
            Check(delta > 5d && Math.Abs(delta - player.m_runSpeed * elapsed) < 1d,
                "Run0 treadmill metres follow native running speed times simulation seconds, not stationary displacement");
            yield return Stop("distance-persistence-stop");
            double stopped = Distance();
            yield return new WaitForSeconds(.3f);
            Check(Distance() == stopped, "dismounted player adds no treadmill distance");
            string key = "jarki.fitness.treadmillMetres." + ZNet.instance.GetWorldUID().ToString("x16", CultureInfo.InvariantCulture);
            var package = new ZPackage(); player.Save(package);
            Check(Math.Abs(double.Parse(player.m_customData[key], CultureInfo.InvariantCulture) - stopped) < .0001d,
                "native character Save persists exact accumulated metres under this world's unique ID");
            // Native Load expects the constructor's empty station dictionary.
            ((IDictionary)Get(player, "m_knownStations")).Clear();
            player.Load(new ZPackage(package.GetArray()));
            foreach (var item in player.GetInventory().GetAllItems())
                if (item.m_dropPrefab && item.m_dropPrefab.name == "Hammer") { hammer = item; break; }
            Check(Distance() == stopped, "native character Load restores saved world total without rounding loss");
            yield return Begin();
            player.UseStamina(player.GetStamina() + 1f);
            yield return Wait(() => (bool)Get(treadmill, "recovering"), "distance fixture reaches recovery walking");
            double walking = Distance();
            yield return new WaitForSeconds(.5f);
            Check(Distance() == walking, "recovery walking contributes zero running kilometres");
            FillStamina();
            yield return Wait(() => player.IsRunning(), "distance fixture automatically resumes running");
            yield return new WaitForSeconds(.5f);
            Check(Distance() > walking + 1d, "automatic running resume continues the same cumulative counter");
            yield return Stop("distance-recovery-stop");
            yield return null;
            var label = treadmill.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            Check(label, "physical treadmill screen has a rendered readout");
            string[] lines = label.text.Split('\n');
            Check(lines.Length == 2 && lines[0].EndsWith(" km") && lines[1].StartsWith("~") && lines[1].EndsWith(" kcal"),
                "screen contains only kilometres and approximate calories, with no player or world caption");
            double displayedKm = double.Parse(lines[0].Substring(0, lines[0].Length - 3), CultureInfo.InvariantCulture);
            double displayedCalories = double.Parse(lines[1].Substring(1, lines[1].Length - 6), CultureInfo.InvariantCulture);
            Check(Math.Abs(displayedKm - Distance() / 1000d) < .001d,
                "screen kilometres reflect this character's persisted treadmill distance");
            Check(Math.Abs(displayedCalories - Distance() * .07d) <= .6d,
                "approximate calories follow 70 kcal per kilometre, rounded to whole kcal");
            var rig = GameCamera.instance;
            bool enabledBefore = rig.enabled;
            Vector3 pos = rig.transform.position; Quaternion rot = rig.transform.rotation;
            rig.enabled = false;
            Vector3 target = treadmill.transform.TransformPoint(new Vector3(.7f, 1.87f, .88f));
            Vector3 eye = treadmill.transform.TransformPoint(new Vector3(-1.2f, 2.7f, -1.4f));
            rig.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
            yield return Capture("distance-native-screen");
            rig.transform.SetPositionAndRotation(pos, rot); rig.enabled = enabledBefore;
            Log("DISTANCE metres=" + Distance().ToString("R", CultureInfo.InvariantCulture) + " key=" + key + " screen=" + label.text.Replace('\n', '|'));
        }
    }
}
