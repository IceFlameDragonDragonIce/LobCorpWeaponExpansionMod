using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LobCorpWeaponExpansion.Weapons
{
    internal static class RedEyeConfessionComboRuntimeDiagnostic
    {
        private static FieldInfo FindField(System.Type type, string name)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);

                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static bool IsTarget(WeaponControllerSimple controller)
        {
            FieldInfo field = FindField(
                typeof(WeaponControllerSimple),
                "currentWeapon");

            if (field == null)
            {
                return false;
            }

            WeaponSimple weapon =
                field.GetValue(controller) as WeaponSimple;

            return weapon != null
                && weapon.entityId ==
                    RedEyeConfessionWeapon.RegisteredWeaponId;
        }

        private static void DumpCurrentClips(
            WeaponControllerSimple controller,
            string eventName)
        {
            FieldInfo field = FindField(
                typeof(WeaponControllerSimple),
                "animator");

            Animator animator = field?.GetValue(controller) as Animator;

            if (animator == null)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][连击诊断] 找不到 Animator 字段或实例。");
                return;
            }

            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                AnimatorStateInfo state =
                    animator.GetCurrentAnimatorStateInfo(layer);

                AnimatorClipInfo[] clips =
                    animator.GetCurrentAnimatorClipInfo(layer);

                List<string> clipNames = new List<string>();

                foreach (AnimatorClipInfo info in clips)
                {
                    if (info.clip != null)
                    {
                        clipNames.Add(
                            $"{info.clip.name}@{info.weight:F2}");
                    }
                }

                string names = clipNames.Count == 0
                    ? "(无当前 Clip)"
                    : string.Join(", ", clipNames);

                Debug.Log(
                    $"[脑叶武器扩展][连击诊断] {eventName}" +
                    $" | Layer={layer}:{animator.GetLayerName(layer)}" +
                    $" | StateHash={state.shortNameHash}" +
                    $" | Time={state.normalizedTime:F2}" +
                    $" | Clips={names}");
            }
        }

        [HarmonyPatch(
            typeof(WeaponControllerSimple),
            nameof(WeaponControllerSimple.BeginAttackAnimation),
            new[] { typeof(int) })]
        private static class BeginAttackAnimationPatch
        {
            private static void Prefix(
                WeaponControllerSimple __instance,
                int fireId)
            {
                if (!__instance.isServer || !IsTarget(__instance))
                {
                    return;
                }

                DumpCurrentClips(
                    __instance,
                    $"BeginAttackAnimation fireId={fireId}");
            }
        }

        [HarmonyPatch(
            typeof(WeaponControllerSimple),
            nameof(WeaponControllerSimple.CreateBasicAttackSwing),
            new[] { typeof(int) })]
        private static class CreateBasicAttackSwingPatch
        {
            private static void Prefix(
                WeaponControllerSimple __instance,
                int idx)
            {
                if (!__instance.isServer || !IsTarget(__instance))
                {
                    return;
                }

                DumpCurrentClips(
                    __instance,
                    $"CreateBasicAttackSwing idx={idx}");
            }
        }
    }
}