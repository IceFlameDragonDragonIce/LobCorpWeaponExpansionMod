using System;
using HarmonyLib;
using UnityEngine;

namespace LobCorpWeaponExpansion
{
    /// <summary>
    /// 赤瞳&忏悔的冲刺攻击双击补丁。
    /// 原版执行 idx=0 后，同一帧追加 idx=1。
    /// 不修改普通攻击连段。
    /// </summary>
    [HarmonyPatch(
        typeof(WeaponControllerSimple),
        nameof(WeaponControllerSimple.CreateDashAttackSwing),
        new[] { typeof(int) }
    )]
    internal static class RedEyeConfessionTwinDashDoubleHitPatch
    {
        private const int GreatswordLayer = 2;

        [HarmonyPostfix]
        private static void Postfix(
            WeaponControllerSimple __instance,
            int idx)
        {
            // 只在第一击后追加第二击。
            // idx=1 再次进入时退出，避免递归。
            if (__instance == null || idx != 0)
            {
                return;
            }

            WeaponSimple weapon = __instance.currentWeapon;

            if (!RedEyeConfessionCombatPatchHelper.IsTargetWeapon(
                    weapon))
            {
                return;
            }

            Animator animator = __instance.animator;

            if (animator == null ||
                animator.layerCount <= GreatswordLayer)
            {
                return;
            }

            // 只处理当前使用 Crossbow_DashAttack 的双锤冲刺。
            AnimatorClipInfo[] clips =
                animator.GetCurrentAnimatorClipInfo(
                    GreatswordLayer);

            bool isOriginalDash = false;

            foreach (AnimatorClipInfo info in clips)
            {
                if (info.clip != null &&
                    string.Equals(
                        info.clip.name,
                        "Crossbow_DashAttack",
                        StringComparison.Ordinal))
                {
                    isOriginalDash = true;
                    break;
                }
            }

            if (!isOriginalDash)
            {
                return;
            }

            try
            {
                // 沿用游戏原有流程执行第二次冲刺攻击。
                __instance.CreateDashAttackSwing(1);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展][双锤冲刺] " +
                    "第二次冲刺攻击执行失败：" +
                    exception);
            }
        }
    }
}