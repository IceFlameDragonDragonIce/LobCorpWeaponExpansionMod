using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LobCorpWeaponExpansion
{
    internal static class RedEyeConfessionSpecialMove
    {
        private const string AnimationName =
            "GreatSword_New_SweepDouble";

        private const int AnimationLayer = 2;

        private static readonly FieldInfo IsSweepUsingField =
            AccessTools.Field(
                typeof(WeaponSimple_SwordAndShield),
                "isSweepUsing"
            );

        internal static bool IsTargetWeapon(WeaponSimple weapon)
        {
            return weapon != null
                && RedEyeConfessionWeapon.RegisteredWeaponId >= 0
                && weapon.entityId ==
                    RedEyeConfessionWeapon.RegisteredWeaponId;
        }

        internal static void TryStart(
            WeaponSimple_SwordAndShield weapon)
        {
            try
            {
                if (weapon == null
                    || !weapon.isServer
                    || !IsTargetWeapon(weapon))
                {
                    return;
                }

                WeaponControllerSimple owner = weapon.Networkowner;

                if (owner == null || owner.animator == null)
                    return;

                // 原版没有成功启动横扫时，不接管动画。
                if (IsSweepUsingField == null
                    || !(IsSweepUsingField.GetValue(weapon) is bool)
                    || !(bool)IsSweepUsingField.GetValue(weapon))
                {
                    return;
                }

                Animator animator = owner.animator;

                int stateHash = Animator.StringToHash(AnimationName);

                if (!animator.HasState(AnimationLayer, stateHash))
                {
                    Debug.LogWarning(
                        "[脑叶武器扩展][重击] 找不到候选动画状态：" +
                        AnimationName
                    );
                    return;
                }

                // 动画会调用 CreateSpecialAttackProjectile(2)，
                // 所以必须确认第 2 号特殊攻击数据存在。
                NewWeaponFireData data = weapon.GetSpecialAttack(2);

                if (data == null)
                {
                    Debug.LogWarning(
                        "[脑叶武器扩展][重击] 特殊攻击索引 2 没有数据，" +
                        "保留原版横扫。"
                    );
                    return;
                }

                Debug.Log(
                    "[脑叶武器扩展][重击] 播放候选动画：" +
                    AnimationName +
                    "，攻击数据=" + data.name +
                    "，倍率=" + data.damageMultiplier
                );

                // 清除原版横扫触发器，防止它覆盖直接播放的动画。
                owner.ResetAnimationTrigger(
                    AnimHashContainer.Instance.SweepHash
                );

                // 临时启用动画第 2 层。
                owner.SetWeaponAnimationLayer(AnimationLayer);

                // 直接播放已确认存在的 Animator 状态。
                animator.Play(
                    AnimationName,
                    AnimationLayer,
                    0f
                );
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展][重击] 启动候选动画失败：" +
                    exception
                );
            }
        }

        internal static void RestoreLayer(
            WeaponSimple_SwordAndShield weapon)
        {
            if (weapon == null || !IsTargetWeapon(weapon))
                return;

            WeaponControllerSimple owner = weapon.Networkowner;

            if (owner == null)
                return;

            // 恢复该武器原本使用的动画层。
            owner.SetWeaponAnimationLayer(weapon.animatorLayer);
        }
    }
    /// <summary>
    /// 基础版赤瞳＆忏悔禁用特殊攻击。
    /// 只拦截格挡状态下的攻击输入，不影响普通攻击和冲刺攻击。
    /// </summary>
    [HarmonyPatch(
    typeof(WeaponSimple_SwordAndShield),
    nameof(WeaponSimple_SwordAndShield.AttackButtonDown)
)]
    internal static class RedEyeConfessionSpecialMoveStartPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(
            WeaponSimple_SwordAndShield __instance)
        {
            if (__instance == null ||
                !RedEyeConfessionSpecialMove.IsTargetWeapon(__instance))
            {
                return true;
            }

            WeaponControllerSimple owner = __instance.Networkowner;

            if (owner == null || owner.animator == null)
            {
                return true;
            }

            // 原版在格挡状态下处理特殊攻击（Sweep）。
            // 基础版直接拦截这次输入。
            if (owner.animator.GetBool(
                    AnimHashContainer.Instance.GuardHash))
            {
                Debug.Log(
                    "[脑叶武器扩展][特殊攻击] " +
                    "基础版已禁用特殊攻击输入。");

                return false;
            }

            // 非格挡状态：保留原版输入处理。
            // 普通攻击和冲刺攻击继续正常执行。
            return true;
        }
    }

    /// <summary>
    /// 客户端收到该动画的同步消息前，先启用动画第 2 层。
    /// </summary>
    [HarmonyPatch(
        typeof(WeaponControllerSimple),
        "UserCode_RpcPlayAnimation__String"
    )]
    internal static class RedEyeConfessionSpecialMoveClientPlayPatch
    {
        [HarmonyPrefix]
        private static void Prefix(
            WeaponControllerSimple __instance,
            string clipName)
        {
            if (__instance == null
                || !string.Equals(
                    clipName,
                    "GreatSword_New_SweepDouble",
                    StringComparison.Ordinal))
            {
                return;
            }

            WeaponSimple_SwordAndShield weapon =
                __instance.currentWeapon as WeaponSimple_SwordAndShield;

            if (!RedEyeConfessionSpecialMove.IsTargetWeapon(weapon))
                return;

            __instance.SetWeaponAnimationLayer(2);
        }
    }

    /// <summary>
    /// 服务器端攻击动画结束时恢复原动画层。
    /// </summary>
    [HarmonyPatch(
        typeof(WeaponSimple_SwordAndShield),
        nameof(WeaponSimple_SwordAndShield.EndAttackAnimation)
    )]
    internal static class RedEyeConfessionSpecialMoveEndPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            WeaponSimple_SwordAndShield __instance)
        {
            if (__instance != null && __instance.isServer)
            {
                RedEyeConfessionSpecialMove.RestoreLayer(__instance);
            }
        }
    }

    /// <summary>
    /// 客户端收到攻击结束 RPC 后，恢复原动画层。
    /// </summary>
    [HarmonyPatch(
        typeof(WeaponControllerSimple),
        "UserCode_RpcEndAttackAnimation"
    )]
    internal static class RedEyeConfessionSpecialMoveClientEndPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            WeaponControllerSimple __instance)
        {
            if (__instance == null)
                return;

            WeaponSimple_SwordAndShield weapon =
                __instance.currentWeapon as WeaponSimple_SwordAndShield;

            if (RedEyeConfessionSpecialMove.IsTargetWeapon(weapon))
            {
                RedEyeConfessionSpecialMove.RestoreLayer(weapon);
            }
        }
    }

    /// <summary>
    /// 攻击被取消时，也恢复原动画层。
    /// </summary>
    [HarmonyPatch(
        typeof(WeaponControllerSimple),
        nameof(WeaponControllerSimple.CancelAction)
    )]
    internal static class RedEyeConfessionSpecialMoveCancelPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            WeaponControllerSimple __instance)
        {
            if (__instance == null)
                return;

            WeaponSimple_SwordAndShield weapon =
                __instance.currentWeapon as WeaponSimple_SwordAndShield;

            if (RedEyeConfessionSpecialMove.IsTargetWeapon(weapon))
            {
                RedEyeConfessionSpecialMove.RestoreLayer(weapon);
            }
        }
    }
    /// <summary>
    /// 诊断赤瞳＆忏悔的冲刺动画及其动画事件。
    /// 只输出信息，不修改攻击逻辑。
    /// </summary>
    [HarmonyPatch]
    internal static class RedEyeConfessionDashAnimationDiagnosticPatch
    {
        private static bool animationInfoDumped;

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(WeaponControllerSimple),
                "SetAnimationTrigger",
                new Type[] { typeof(int) }
            );
        }

        [HarmonyPostfix]
        private static void Postfix(
            WeaponControllerSimple __instance,
            int __0)
        {
            if (__instance == null ||
                !RedEyeConfessionCombatPatchHelper.IsTargetWeapon(
                    __instance.currentWeapon))
            {
                return;
            }

            if (__0 != AnimHashContainer.Instance.DashAttackHash)
                return;

            if (animationInfoDumped)
                return;

            Animator animator = __instance.animator;

            if (animator == null ||
                animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][冲刺诊断] 找不到 Animator 或动画控制器。");
                return;
            }

            animationInfoDumped = true;

            RuntimeAnimatorController controller =
                animator.runtimeAnimatorController;

            Debug.Log(
                "[脑叶武器扩展][冲刺诊断] Controller=" +
                controller.name
            );

            bool found = false;

            foreach (AnimationClip clip in controller.animationClips)
            {
                if (clip == null ||
                    clip.name.IndexOf(
                        "Dash",
                        StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                found = true;

                Debug.Log(
                    "[脑叶武器扩展][冲刺动画] Clip=" +
                    clip.name +
                    ", Length=" + clip.length
                );

                foreach (AnimationEvent evt in clip.events)
                {
                    Debug.Log(
                        "[脑叶武器扩展][冲刺事件] " +
                        $"Clip={clip.name}, " +
                        $"Time={evt.time:F3}, " +
                        $"Function={evt.functionName}, " +
                        $"Int={evt.intParameter}, " +
                        $"Float={evt.floatParameter}, " +
                        $"String={evt.stringParameter}"
                    );
                }
            }

            if (!found)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][冲刺诊断] " +
                    "没有找到名称包含 Dash 的动画片段。"
                );
            }
        }
    }
}