using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LobCorpWeaponExpansion
{
    internal static class RedEyeConfessionTwinComboHelper
    {
        private static readonly FieldInfo CurrentWeaponSwingField =
            AccessTools.Field(
                typeof(WeaponControllerSimple),
                "currentWeaponSwing"
            );

        internal static bool IsTarget(WeaponSimple weapon)
        {
            return weapon != null
                && RedEyeConfessionWeapon.RegisteredWeaponId >= 0
                && weapon.entityId ==
                    RedEyeConfessionWeapon.RegisteredWeaponId;
        }

        internal static int GetCurrentSwing(
            WeaponControllerSimple owner)
        {
            if (owner == null || CurrentWeaponSwingField == null)
                return -1;

            object value = CurrentWeaponSwingField.GetValue(owner);

            return value == null ? -1 : Convert.ToInt32(value);
        }
    }

    /// <summary>
    /// 在武器初始化时切换到双武器动画层，
    /// 并扩展普通攻击数据以对应 Twin 动画的六个事件索引。
    /// </summary>
    [HarmonyPatch]
    internal static class RedEyeConfessionTwinInitializePatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(WeaponSimple),
                "Initialize"
            );
        }

        [HarmonyPrefix]
        private static void Prefix(WeaponSimple __instance)
        {
            if (!RedEyeConfessionTwinComboHelper.IsTarget(__instance))
                return;

            // Twin 攻击动画存在于第 2 层。
            __instance.animatorLayer = 2;
            // 测试双锤专用的动画招式组
            __instance.attackMoveSet = 0;

            NewWeaponFireData[] original =
                __instance.basicComboAttacks;

            // 只扩展一次，避免反复初始化时错误地重排数据。
            if (original != null &&
                original.Length >= 3 &&
                original.Length < 6)
            {
                NewWeaponFireData[] comboData =
                    new NewWeaponFireData[6];

                comboData[0] = original[0];
                comboData[1] = original[0];

                comboData[2] = original[1];
                comboData[3] = original[1];

                comboData[4] = original[2];
                comboData[5] = original[2];

                __instance.basicComboAttacks = comboData;
            }

            Debug.Log(
                $"[脑叶武器扩展][双锤] " +
                $"AnimatorLayer={__instance.animatorLayer}, " +
                $"AttackMoveSet={__instance.attackMoveSet}, " +
                $"BasicCount={__instance.basicComboAttacks?.Length ?? 0}"
                );
        }
    }

    /// <summary>
    /// Twin Attack1：仅保留索引 0 的攻击事件。
    /// Twin Attack2：仅保留索引 2 的攻击事件。
    /// Twin Attack3：保留索引 4、5 两次同时发生的攻击事件。
    /// </summary>
    [HarmonyPatch]
    internal static class RedEyeConfessionTwinAttackEventPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(WeaponSimple),
                "CreateBasicAttackProjectile"
            );
        }

        [HarmonyPrefix]
        private static bool Prefix(
            WeaponSimple __instance,
            object[] __args)
        {
            if (!RedEyeConfessionTwinComboHelper.IsTarget(__instance))
                return true;

            if (__args == null ||
                __args.Length == 0 ||
                !(__args[0] is int index))
            {
                return true;
            }

            WeaponControllerSimple owner =
                __instance.Networkowner;

            int swing =
                RedEyeConfessionTwinComboHelper.GetCurrentSwing(owner);

            // 第一段：0 保留，1 跳过。
            // 第二段：2 保留，3 跳过。
            // 第三段：4、5 均保留。
            bool skip =
                (swing == 0 && index == 1) ||
                (swing == 1 && index == 3);

            if (skip)
            {
                Debug.Log(
                    "[脑叶武器扩展][双锤] " +
                    $"已抑制额外攻击事件：Swing={swing}, Index={index}"
                );

                return false;
            }

            return true;
        }
    }
}