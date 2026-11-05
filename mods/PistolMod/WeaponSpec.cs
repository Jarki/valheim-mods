namespace PistolMod
{
    internal sealed class WeaponSpec
    {
        internal readonly string Prefab, Label, Ammo, AmmoLabel, Asset;
        internal readonly int Capacity;
        internal readonly float Interval, Reload, Damage, Speed, Weight;
        internal readonly bool Automatic, Explosive;
        internal string ShotSound => Explosive ? "rpg_launch" : Automatic ? "ak_shot" : "pistol_shot";
        internal string ReloadSound => this == Pistol ? "pistol_reload" : "ak_reload";

        private WeaponSpec(string prefab, string label, string ammo, string ammoLabel, string asset,
            int capacity, float interval, float reload, float damage, float speed, float weight,
            bool automatic = false, bool explosive = false)
        {
            Prefab = prefab; Label = label; Ammo = ammo; AmmoLabel = ammoLabel; Asset = asset;
            Capacity = capacity; Interval = interval; Reload = reload; Damage = damage;
            Speed = speed; Weight = weight; Automatic = automatic; Explosive = explosive;
        }

        internal static readonly WeaponSpec Pistol = new WeaponSpec("Pistol9mm", "9mm pistol",
            "Ammo9mm", "9mm cartridges", "pistol", 17, .16f, 1.8f, 35f, 180f, 1f);
        internal static readonly WeaponSpec Rifle = new WeaponSpec("RifleAKM", "AKM rifle",
            "Ammo762", "7.62mm cartridges", "rifle", 30, .1f, 2.5f, 42f, 240f, 3.5f, true);
        internal static readonly WeaponSpec Launcher = new WeaponSpec("LauncherRPG", "RPG launcher",
            "AmmoRocket", "RPG rockets", "launcher", 1, .8f, 3f, 160f, 45f, 7f, false, true);
        internal static readonly WeaponSpec[] All = { Pistol, Rifle, Launcher };

        internal static WeaponSpec Find(string prefab)
        {
            foreach (var spec in All) if (spec.Prefab == prefab) return spec;
            return null;
        }
    }
}
