using System;
using System.Reflection;
using HarmonyLib;

namespace LobCorpWeaponExpansion
{
    internal static class RedEyeConfessionTwinComboHelper
    {
        private static readonly FieldInfo CurrentWeaponSwingField =
            AccessTools.Field(
                typeof(WeaponControllerSimple),
                "currentWeaponSwing");

        internal static bool IsTarget(WeaponSimple weapon)
        {
            return RedEyeConfessionWeapon.IsManagedWeapon(weapon);
        }

        internal static int GetCurrentSwing(
            WeaponControllerSimple owner)
        {
            if (owner == null
                || CurrentWeaponSwingField == null)
            {
                return -1;
            }

            object value =
                CurrentWeaponSwingField.GetValue(owner);

            return value == null
                ? -1
                : Convert.ToInt32(value);
        }
    }

    /// <summary>
    /// 初始化双锤动画层，并将普通攻击数据扩展为六个事件索引。
    /// </summary>
    [HarmonyPatch]
    internal static class RedEyeConfessionTwinInitializePatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(WeaponSimple),
                "Initialize");
        }

        [HarmonyPrefix]
        private static void Prefix(WeaponSimple __instance)
        {
            if (!RedEyeConfessionTwinComboHelper.IsTarget(__instance))
            {
                return;
            }

            // 双锤攻击动画使用第 2 层。
            __instance.animatorLayer = 2;

            // 使用双锤专用动画招式组。
            __instance.attackMoveSet = 0;

            NewWeaponFireData[] original =
                __instance.basicComboAttacks;

            // 原版三段攻击扩展为六个动画事件索引：
            // 0、1 对应第一段；
            // 2、3 对应第二段；
            // 4、5 对应第三段。
            if (original != null
                && original.Length >= 3
                && original.Length < 6)
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
        }
    }

    /// <summary>
    /// 第一段只执行索引 0；
    /// 第二段只执行索引 2；
    /// 第三段执行索引 4 和 5。
    /// </summary>
    [HarmonyPatch]
    internal static class RedEyeConfessionTwinAttackEventPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(WeaponSimple),
                "CreateBasicAttackProjectile");
        }

        [HarmonyPrefix]
        private static bool Prefix(
            WeaponSimple __instance,
            object[] __args)
        {
            // 仅处理赤瞳＆忏悔的基础版与强化版。
            if (!RedEyeConfessionTwinComboHelper.IsTarget(__instance))
            {
                return true;
            }

            if (__args == null
                || __args.Length == 0
                || !(__args[0] is int index))
            {
                return true;
            }

            WeaponControllerSimple owner =
                __instance.Networkowner;

            int swing =
                RedEyeConfessionTwinComboHelper.GetCurrentSwing(
                    owner);

            // 第一段：保留 0，抑制 1。
            // 第二段：保留 2，抑制 3。
            // 第三段：保留 4、5。
            bool skip =
                (swing == 0 && index == 1)
                || (swing == 1 && index == 3);

            return !skip;
        }
    }
}