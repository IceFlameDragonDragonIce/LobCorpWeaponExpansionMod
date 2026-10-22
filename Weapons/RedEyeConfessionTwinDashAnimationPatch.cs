using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace LobCorpWeaponExpansion
{
    [HarmonyPatch]
    internal static class RedEyeConfessionTwinDashAnimationPatch
    {
        private const string OriginalClipName =
            "GreatSword_New_DashAttack";

        private const string TwinClipName =
            "Staff_DashAttack";

        private sealed class OverrideContext
        {
            public RuntimeAnimatorController Original;
            public AnimatorOverrideController Override;
        }

        private static readonly ConditionalWeakTable<
            WeaponControllerSimple,
            OverrideContext> Contexts =
                new ConditionalWeakTable<
                    WeaponControllerSimple,
                    OverrideContext>();

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(WeaponControllerSimple),
                "SetAnimationTrigger",
                new[] { typeof(int) });
        }

        [HarmonyPrefix]
        private static void Prefix(
            WeaponControllerSimple __instance,
            int __0)
        {
            if (__instance == null ||
                __instance.animator == null ||
                __instance.animator.runtimeAnimatorController == null)
            {
                return;
            }

            if (__0 != AnimHashContainer.Instance.DashAttackHash)
                return;

            if (!RedEyeConfessionCombatPatchHelper.IsTargetWeapon(
                    __instance.currentWeapon))
            {
                return;
            }

            try
            {
                InstallOverride(__instance);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展][双锤冲刺动画] 安装失败：" +
                    exception);
            }
        }

        private static void InstallOverride(
            WeaponControllerSimple controller)
        {
            Animator animator = controller.animator;

            OverrideContext context = Contexts.GetValue(
                controller,
                _ => new OverrideContext());

            RuntimeAnimatorController current =
                animator.runtimeAnimatorController;

            if (context.Override != null)
            {
                if (ReferenceEquals(current, context.Override))
                    return;

                if (ReferenceEquals(current, context.Original))
                {
                    animator.runtimeAnimatorController =
                        context.Override;

                    Debug.Log(
                        "[脑叶武器扩展][双锤冲刺动画] " +
                        "已重新启用双锤斩击覆盖。");
                    return;
                }

                DestroyOverride(context);
            }

            AnimationClip twinClip =
                FindClip(current, TwinClipName);

            if (twinClip == null)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][双锤冲刺动画] 找不到目标片段：" +
                    TwinClipName);
                return;
            }

            if (!HasRequiredEvents(twinClip))
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][双锤冲刺动画] " +
                    "目标片段缺少必要事件，保留原版动画。");
                return;
            }

            AnimatorOverrideController replacement =
                new AnimatorOverrideController(current);

            List<KeyValuePair<AnimationClip, AnimationClip>> overrides =
                new List<KeyValuePair<AnimationClip, AnimationClip>>();

            replacement.GetOverrides(overrides);

            bool foundOriginal = false;

            for (int i = 0; i < overrides.Count; i++)
            {
                AnimationClip key = overrides[i].Key;

                if (key == null ||
                    !string.Equals(
                        key.name,
                        OriginalClipName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                overrides[i] =
                    new KeyValuePair<AnimationClip, AnimationClip>(
                        key,
                        twinClip);

                foundOriginal = true;
                break;
            }

            if (!foundOriginal)
            {
                UnityEngine.Object.Destroy(replacement);

                Debug.LogWarning(
                    "[脑叶武器扩展][双锤冲刺动画] " +
                    "当前控制器的覆盖列表中找不到 " +
                    OriginalClipName);
                return;
            }

            replacement.ApplyOverrides(overrides);

            context.Original = current;
            context.Override = replacement;

            animator.runtimeAnimatorController = replacement;

            Debug.Log(
                "[脑叶武器扩展][双锤冲刺动画] 已将 " +
                OriginalClipName + " 覆盖为 " + TwinClipName);
        }

        private static AnimationClip FindClip(
            RuntimeAnimatorController controller,
            string clipName)
        {
            if (controller != null)
            {
                foreach (AnimationClip clip in controller.animationClips)
                {
                    if (clip != null &&
                        string.Equals(
                            clip.name,
                            clipName,
                            StringComparison.Ordinal))
                    {
                        return clip;
                    }
                }
            }

            foreach (AnimationClip clip in
                     Resources.FindObjectsOfTypeAll<AnimationClip>())
            {
                if (clip != null &&
                    string.Equals(
                        clip.name,
                        clipName,
                        StringComparison.Ordinal))
                {
                    return clip;
                }
            }

            return null;
        }

        private static bool HasRequiredEvents(AnimationClip clip)
        {
            bool hasBegin = false;
            bool hasFirstHit = false;
            bool hasEnd = false;

            foreach (AnimationEvent evt in clip.events)
            {
                if (evt == null)
                    continue;

                if (evt.functionName == "BeginDashAttackAnimation")
                    hasBegin = true;

                if (evt.functionName == "CreateDashAttackProjectile" &&
                    evt.intParameter == 0)
                {
                    hasFirstHit = true;
                }

                if (evt.functionName == "EndAttackAnimation")
                    hasEnd = true;
            }

            return hasBegin && hasFirstHit && hasEnd;
        }

        private static void RestoreOverride(
            WeaponControllerSimple controller)
        {
            if (controller == null ||
                !Contexts.TryGetValue(controller, out OverrideContext context))
            {
                return;
            }

            if (context.Override == null)
                return;

            Animator animator = controller.animator;

            if (animator != null &&
                ReferenceEquals(
                    animator.runtimeAnimatorController,
                    context.Override))
            {
                animator.runtimeAnimatorController =
                    context.Original;
            }

            DestroyOverride(context);
        }

        private static void DestroyOverride(OverrideContext context)
        {
            if (context.Override != null)
            {
                UnityEngine.Object.Destroy(context.Override);
            }

            context.Original = null;
            context.Override = null;
        }

        [HarmonyPatch(
            typeof(WeaponControllerSimple),
            "EndAttckAnimation")]
        private static class RestoreOnEndPatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                WeaponControllerSimple __instance)
            {
                RestoreOverride(__instance);
            }
        }

        [HarmonyPatch(
            typeof(WeaponControllerSimple),
            nameof(WeaponControllerSimple.CancelAction))]
        private static class RestoreOnCancelPatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                WeaponControllerSimple __instance)
            {
                RestoreOverride(__instance);
            }
        }
    }
    [HarmonyPatch(
    typeof(WeaponControllerSimple),
    nameof(WeaponControllerSimple.BeginDashAttackAnimation),
    new[] { typeof(int) })]
    internal static class RedEyeConfessionDashRuntimeClipDiagnosticPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            WeaponControllerSimple __instance,
            int fireId)
        {
            if (__instance == null ||
                !RedEyeConfessionCombatPatchHelper.IsTargetWeapon(
                    __instance.currentWeapon))
            {
                return;
            }

            Animator animator = __instance.animator;

            if (animator == null)
            {
                return;
            }

            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                AnimatorClipInfo[] clips =
                    animator.GetCurrentAnimatorClipInfo(layer);

                foreach (AnimatorClipInfo info in clips)
                {
                    if (info.clip == null)
                    {
                        continue;
                    }

                    Debug.Log(
                        "[脑叶武器扩展][冲刺实际片段] " +
                        "fireId=" + fireId +
                        ", Layer=" + layer +
                        ", StateHash=" +
                        animator.GetCurrentAnimatorStateInfo(layer)
                            .shortNameHash +
                        ", Clip=" + info.clip.name +
                        ", Weight=" + info.weight);
                }
            }
        }
    }
}