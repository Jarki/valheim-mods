using System;
using System.Collections.Generic;
using UnityEngine;

namespace PistolMod
{
    // An upper-body animation layer on the actual character skeleton. Locomotion remains
    // vanilla; the arms use weapon-specific two-bone IK, including the reload hand path.
    [DefaultExecutionOrder(10000)]
    public sealed class FirearmPose : MonoBehaviour
    {
        private Player player;
        private PistolController controller;
        private Transform head, rightUpper, rightLower, rightHand, leftUpper, leftLower, leftHand;
        private Transform rightMount, leftMount;
        private Transform[] bones;
        private Quaternion[] previous;
        private bool applied;
        private float nextRigSearch;

        private void Awake()
        {
            player = GetComponent<Player>(); controller = GetComponent<PistolController>();
        }

        private void Update() => Restore();
        private void OnDisable() => Restore();

        private void Restore()
        {
            if (!applied) return;
            for (int i = 0; i < bones.Length; i++) if (bones[i]) bones[i].localRotation = previous[i];
            applied = false;
        }

        private bool FindRig()
        {
            if (bones != null && previous != null && rightUpper && rightLower && rightHand
                && leftUpper && leftLower && leftHand && head) return true;
            if (Time.time < nextRigSearch) return false;
            nextRigSearch = Time.time + 2;
            var animator = GetComponentInChildren<Animator>();
            if (!animator) return false;
            head = Bone(animator, HumanBodyBones.Head, "Head");
            rightUpper = Bone(animator, HumanBodyBones.RightUpperArm, "RightArm");
            rightLower = Bone(animator, HumanBodyBones.RightLowerArm, "RightForeArm");
            rightHand = Bone(animator, HumanBodyBones.RightHand, "RightHand");
            leftUpper = Bone(animator, HumanBodyBones.LeftUpperArm, "LeftArm");
            leftLower = Bone(animator, HumanBodyBones.LeftLowerArm, "LeftForeArm");
            leftHand = Bone(animator, HumanBodyBones.LeftHand, "LeftHand");
            if (!head || !rightUpper || !rightLower || !rightHand || !leftUpper || !leftLower || !leftHand) return false;
            rightMount = rightHand.Find("RightHand_Attach");
            leftMount = leftHand.Find("LeftHand_Attach");
            var posed = new List<Transform> { rightUpper, rightLower, rightHand, leftUpper, leftLower, leftHand };
            foreach (var hand in new[] { rightHand, leftHand })
                foreach (var bone in hand.GetComponentsInChildren<Transform>())
                    if (bone != hand && bone.name.Contains("Hand")
                        && (bone.name.EndsWith("1") || bone.name.EndsWith("2") || bone.name.EndsWith("3"))) posed.Add(bone);
            bones = posed.ToArray();
            previous = new Quaternion[bones.Length];
            return true;
        }

        private static Transform Bone(Animator animator, HumanBodyBones human, string name)
        {
            if (animator.isHuman) return animator.GetBoneTransform(human);
            foreach (var bone in animator.GetComponentsInChildren<Transform>(true))
                if (bone.name.Equals(name, StringComparison.OrdinalIgnoreCase)
                    || bone.name.EndsWith(":" + name, StringComparison.OrdinalIgnoreCase)) return bone;
            return null;
        }

        private void LateUpdate()
        {
            var spec = controller.EquippedSpec();
            if (spec == null || !controller.CanPose || !FindRig()) return;
            var visual = controller.FindVisual();
            if (!visual) return;
            visual.Spec = spec;
            for (int i = 0; i < bones.Length; i++) previous[i] = bones[i].localRotation;
            applied = true;

            Vector3 forward = player.GetLookDir().normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < .01f) right = transform.right;
            Vector3 up = Vector3.Cross(forward, right).normalized;
            float progress = visual.ReloadProgress;
            float reload = Mathf.Sin(progress * Mathf.PI);
            float reach = spec == WeaponSpec.Pistol ? .48f : spec.Explosive ? .18f : .30f;
            var rightTarget = head.position + forward * reach + right * .16f - up * (.25f + .12f * reload)
                - forward * (.035f * visual.Recoil);
            var rotation = Quaternion.LookRotation(forward, up) * Quaternion.Euler(15 * reload, -12 * reload, -20 * reload);
            Vector3 support = spec == WeaponSpec.Pistol ? new Vector3(-.035f, -.015f, .015f)
                : spec.Explosive ? new Vector3(0, .01f, -.21f) : new Vector3(0, .045f, .23f);
            Vector3 magazine = spec.Explosive ? new Vector3(-.12f, .005f, .43f)
                : new Vector3(-.02f, spec == WeaponSpec.Pistol ? -.14f : -.21f, spec == WeaponSpec.Pistol ? -.02f : .14f);
            float handTravel = Mathf.SmoothStep(0, 1, Mathf.Clamp01(reload * 1.7f));
            var leftTarget = rightTarget + rotation * Vector3.Lerp(support, magazine, handTravel);
            var rightRotation = rotation * (rightMount ? Quaternion.Inverse(rightMount.localRotation) : Quaternion.Euler(0, 0, -90));
            var leftRotation = rotation * (leftMount ? Quaternion.Inverse(leftMount.localRotation) : Quaternion.Euler(0, 0, 90));
            var rightOffset = rightMount ? rightRotation * Vector3.Scale(rightMount.localPosition, rightHand.lossyScale) : Vector3.zero;
            var leftOffset = leftMount ? leftRotation * Vector3.Scale(leftMount.localPosition, leftHand.lossyScale) : Vector3.zero;
            Solve(rightUpper, rightLower, rightHand, rightTarget - rightOffset, right * .8f - up);
            Solve(leftUpper, leftLower, leftHand, leftTarget - leftOffset, -right * .8f - up);
            // Wrist orientations are independent of the two-bone solver. The weapon mount
            // is aligned explicitly after the wrists, so aim stays stable during locomotion.
            rightHand.rotation = rightRotation;
            leftHand.rotation = leftRotation;
            for (int i = 6; i < bones.Length; i++)
            {
                bool thumb = bones[i].name.Contains("Thumb");
                bool trigger = bones[i].name.Contains("RightHandIndex");
                float curl = thumb ? 18 : trigger ? 30 + 12 * visual.Recoil : 55;
                if (bones[i].name.Contains("LeftHand")) curl *= 1 - .35f * handTravel;
                bones[i].localRotation = previous[i] * Quaternion.Euler(curl, 0, 0);
            }
            visual.transform.parent.rotation = rotation;
        }

        private static void Solve(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 pole)
        {
            var origin = upper.position;
            float a = Vector3.Distance(origin, lower.position), b = Vector3.Distance(lower.position, hand.position);
            if (a < .001f || b < .001f) return;
            Vector3 delta = target - origin;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f);
            Vector3 direction = delta.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(pole, direction).normalized;
            if (bend.sqrMagnitude < .01f) bend = Vector3.ProjectOnPlane(Vector3.forward, direction).normalized;
            float along = (a * a + distance * distance - b * b) / (2 * distance);
            Vector3 elbow = origin + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            upper.rotation = Quaternion.FromToRotation(lower.position - origin, elbow - origin) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position,
                origin + direction * distance - lower.position) * lower.rotation;
        }
    }
}
