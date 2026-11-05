using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PistolMod
{
    public sealed class RocketBlast : MonoBehaviour
    {
        private bool detonated;
        private static readonly Func<Projectile, IDestructible, bool> ValidTarget =
            AccessTools.MethodDelegate<Func<Projectile, IDestructible, bool>>(AccessTools.Method(typeof(Projectile), "IsValidTarget"));

        private sealed class Target
        {
            internal GameObject Object;
            internal IDestructible Destructible;
            internal Collider Collider;
            internal Vector3 Point;
            internal float Distance;
            internal bool Allowed;
        }

        [HarmonyPatch(typeof(Projectile), "DoAOE")]
        private static class Splash
        {
            private static bool Prefix(Projectile __instance, Vector3 hitPoint, ref bool hitCharacter, ref bool didDamage,
                Character ___m_owner, HitData ___m_originalHitData, ZNetView ___m_nview)
            {
                var blast = __instance.GetComponent<RocketBlast>();
                if (!blast) return true;
                // Only the projectile's authoritative shooter evaluates ward access. The
                // game's CheckAccess uses Player.m_localPlayer, not a passed-in identity.
                if (blast.detonated || !___m_nview || !___m_nview.IsValid() || !___m_nview.IsOwner()
                    || !___m_owner || ___m_owner != Player.m_localPlayer || ___m_originalHitData == null) return false;
                blast.detonated = true;
                blast.Detonate(__instance, hitPoint, ___m_owner, ___m_originalHitData, ref hitCharacter, ref didDamage);
                return false;
            }
        }

        private void Detonate(Projectile projectile, Vector3 center, Character owner, HitData original,
            ref bool hitCharacter, ref bool didDamage)
        {
            int mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid",
                "terrain", "character", "character_net", "character_ghost", "character_noenv", "hitbox", "vehicle");
            var targets = new Dictionary<GameObject, Target>();
            foreach (var collider in Physics.OverlapSphere(center, BlastRules.Radius, mask, QueryTriggerInteraction.Ignore))
            {
                var obj = Projectile.FindHitObject(collider);
                if (!obj) continue;
                var destructible = obj.GetComponent<IDestructible>();
                if (destructible == null) continue;
                Vector3 point = collider.ClosestPointOnBounds(center);
                float distance = Vector3.Distance(center, point);
                if (targets.TryGetValue(obj, out var old) && old.Distance <= distance) continue;
                targets[obj] = new Target { Object = obj, Destructible = destructible,
                    Collider = collider, Point = point, Distance = distance };
            }
            // Snapshot occlusion and permissions BEFORE damaging anything. Destroying the
            // outer wall must not expose a second wall to this same explosion. Likewise,
            // destroying an accessible ward must not change permissions halfway through.
            Vector3 rayOrigin = center - projectile.GetVelocity().normalized * .08f;
            foreach (var target in targets.Values)
            {
                if (BlastRules.Falloff(target.Distance) <= 0 || !ValidTarget(projectile, target.Destructible)) continue;
                if (target.Destructible is WearNTear || target.Object.GetComponent<Piece>())
                {
                    if (!PrivateArea.CheckAccess(target.Object.transform.position, 0, false, true)
                        || !PrivateArea.CheckAccess(target.Point, 0, false, true)) continue;
                }
                Vector3 delta = target.Point - rayOrigin;
                bool blocked = false;
                foreach (var obstacle in Physics.RaycastAll(rayOrigin, delta.normalized, delta.magnitude, mask, QueryTriggerInteraction.Ignore))
                {
                    var obj = Projectile.FindHitObject(obstacle.collider);
                    // Characters don't shield structures; solid world geometry does.
                    if (obj == target.Object || (obj && obj.GetComponent<Character>())) continue;
                    if (obstacle.distance < delta.magnitude - .03f) { blocked = true; break; }
                }
                target.Allowed = !blocked;
            }
            foreach (var target in targets.Values)
            {
                if (!target.Allowed || !target.Object || !target.Collider) continue;
                float scale = BlastRules.Falloff(target.Distance);
                var hit = original.Clone();
                hit.m_damage.Modify(scale);
                hit.m_pushForce *= scale;
                hit.m_point = target.Point; hit.m_dir = (target.Point - center).normalized;
                hit.m_hitCollider = target.Collider; hit.m_ranged = true;
                hit.m_toolTier = 2; // Stone and ordinary building pieces, not boss-gated minerals.
                hit.m_radius = BlastRules.Radius;
                hit.SetAttacker(owner);
                // Native Character.Damage/RPC_Damage still enforces PVP, resistances,
                // dodge invincibility and ownership. Never set ignorePVP.
                target.Destructible.Damage(hit);
                didDamage = true;
                if (target.Destructible is Character) hitCharacter = true;
            }
        }
    }

    public sealed class RocketExplosionAudio : MonoBehaviour
    {
        private void Start() => PistolAssets.Play("explosion", transform.position, .9f, 100f);
    }
}
