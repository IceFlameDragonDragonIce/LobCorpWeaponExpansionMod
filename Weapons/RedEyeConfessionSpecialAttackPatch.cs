using System;
using HarmonyLib;
using UnityEngine;

namespace LobCorpWeaponExpansion
{
    /// <summary>
    /// 查找赤瞳＆忏悔使用的动画资源，
    /// 为特殊重击选择正确的动画提供依据。
    /// </summary>
    [HarmonyPatch(
        typeof(WeaponControllerSimple),
        nameof(WeaponControllerSimple.CreateSpecialAttackSwing)
    )]
    internal static class RedEyeConfessionSpecialAttackDiagnosticPatch
    {
        private static bool animationInfoDumped;

        [HarmonyPrefix]
        private static void Prefix(
            WeaponControllerSimple __instance,
            int idx)
        {
            if (__instance == null)
                return;

            WeaponSimple_SwordAndShield weapon =
                __instance.currentWeapon as WeaponSimple_SwordAndShield;

            if (!RedEyeConfessionCombatPatchHelper.IsTargetWeapon(weapon))
                return;

            try
            {
                NewWeaponFireData data = weapon.GetSpecialAttack(idx);

                Debug.Log(
                    "[脑叶武器扩展][重击] " +
                    $"WeaponId={weapon.entityId}, " +
                    $"Index={idx}, " +
                    $"Data={(data != null ? data.name : "<null>")}, " +
                    $"Damage={(data != null ? data.damageMultiplier : 0f)}"
                );

                if (animationInfoDumped)
                    return;

                Animator animator = __instance.animator;

                if (animator == null)
                {
                    Debug.LogWarning(
                        "[脑叶武器扩展][动画] Animator 为 null。");
                    return;
                }

                animationInfoDumped = true;

                RuntimeAnimatorController controller =
                    animator.runtimeAnimatorController;

                Debug.Log(
                    "[脑叶武器扩展][动画] Controller=" +
                    (controller != null
                        ? controller.name
                        : "<null>")
                );

                if (controller != null)
                {
                    string[] candidateNames =
                    {
                        "SwordAndShield_Sweep0",
                        "SwordAndShield_Strike",
                        "GreatSword_Twin_Attack1",
                        "GreatSword_Twin_Attack2",
                        "GreatSword_Twin_Attack3",
                        "GreatSword_New_Sweep",
                        "GreatSword_New_SweepDouble",
                        "GreatSword_New_SweepDouble2"
                    };

                    foreach (AnimationClip clip in controller.animationClips)
                    {
                        if (clip == null)
                            continue;

                        if (Array.IndexOf(candidateNames, clip.name) < 0)
                            continue;

                        Debug.Log(
                            "[脑叶武器扩展][动画事件] Clip=" +
                            clip.name +
                            ", Length=" + clip.length
                        );

                        foreach (AnimationEvent evt in clip.events)
                        {
                            Debug.Log(
                                "[脑叶武器扩展][动画事件] " +
                                $"Clip={clip.name}, " +
                                $"Time={evt.time:F3}, " +
                                $"Function={evt.functionName}, " +
                                $"Int={evt.intParameter}, " +
                                $"Float={evt.floatParameter}, " +
                                $"String={evt.stringParameter}"
                            );
                        }

                        int stateHash = Animator.StringToHash(clip.name);

                        for (int layer = 0;
                             layer < __instance.animator.layerCount;
                             layer++)
                        {
                            Debug.Log(
                                "[脑叶武器扩展][动画状态] " +
                                $"Clip={clip.name}, " +
                                $"Layer={layer}, " +
                                $"HasState={__instance.animator.HasState(layer, stateHash)}"
                            );
                        }
                    }
                }

                foreach (AnimatorControllerParameter parameter
                         in animator.parameters)
                {
                    Debug.Log(
                        "[脑叶武器扩展][动画] Parameter=" +
                        parameter.name +
                        ", Type=" + parameter.type
                    );
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展][动画] 诊断失败：" +
                    exception
                );
            }
        }
    }
}