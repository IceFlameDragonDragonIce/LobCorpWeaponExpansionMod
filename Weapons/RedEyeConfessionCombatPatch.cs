using System;
using HarmonyLib;
using UnityEngine;

namespace LobCorpWeaponExpansion
{
    internal static class RedEyeConfessionCombatPatchHelper
    {
        // 两道斩击的角度偏移。
        internal const float DualSwingAngle = 18f;

        // 同时识别基础版和强化版。
        internal static bool IsTargetWeapon(WeaponSimple weapon)
        {
            return RedEyeConfessionWeapon.IsManagedWeapon(weapon);
        }
    }

    /// <summary>
    /// 旧版最后一段双重攻击补丁。
    /// 双锤版本使用 0～5 的动画事件索引，
    /// 第三段的两次攻击由 TwinCombo 和 TwinStagger 负责。
    /// 因此不能再次接管双锤的最后一段。
    /// </summary>
    [HarmonyPatch(
        typeof(WeaponControllerSimple),
        nameof(WeaponControllerSimple.CreateBasicAttackSwing)
    )]
    internal static class RedEyeConfessionDualFinalHitPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(
            WeaponControllerSimple __instance,
            int idx)
        {
            if (__instance == null)
            {
                return true;
            }

            WeaponSimple currentWeapon = __instance.currentWeapon;

            // 不是赤瞳＆忏悔的武器，完全交给原版。
            if (!RedEyeConfessionCombatPatchHelper.IsTargetWeapon(
                    currentWeapon))
            {
                return true;
            }

            // 双锤使用 0～5 的动画事件索引。
            // 不能在这里再次复制第三段攻击。
            if (currentWeapon.animatorLayer == 2)
            {
                return true;
            }

            WeaponSimple_SwordAndShield weapon =
                currentWeapon as WeaponSimple_SwordAndShield;

            if (weapon == null)
            {
                return true;
            }

            // 只接管最后一段，其余攻击继续走原版流程。
            if (idx != weapon.finalComboIdx)
            {
                return true;
            }

            NewWeaponFireData fxData =
                weapon.GetBasicAttack(idx, "");

            if (fxData == null)
            {
                return true;
            }

            try
            {
                float angle =
                    RedEyeConfessionCombatPatchHelper.DualSwingAngle;

                // 两道斩击特效。
                weapon.CreateSwingFx(
                    fxData,
                    true,
                    new Vector3(0f, 0f, -angle));

                weapon.CreateSwingFx(
                    fxData,
                    true,
                    new Vector3(0f, 0f, angle));

                if (__instance.isServer)
                {
                    string parameter =
                        weapon.GetBasicAttackParameter(idx);

                    Vector3 originalDirection =
                        __instance.attackDirection;

                    Vector3 directionA =
                        Quaternion.Euler(0f, 0f, -angle)
                        * originalDirection;

                    Vector3 directionB =
                        Quaternion.Euler(0f, 0f, angle)
                        * originalDirection;

                    // 第一道攻击判定。
                    weapon.CreateBasicAttackProjectile(
                        idx,
                        directionA,
                        weapon.GetBasicAttackSharedTargetList(idx),
                        1f,
                        __instance.mpConsumedAttackTriggerFromAnimationValue,
                        parameter);

                    // 第二道独立攻击判定。
                    weapon.CreateBasicAttackProjectile(
                        idx,
                        directionB,
                        weapon.GetBasicAttackSharedTargetList(idx),
                        1f,
                        __instance.mpConsumedAttackTriggerFromAnimationValue,
                        parameter);

                    // 仍然只通知一次：这是同一段连击动作。
                    __instance.OnBaisAttackSwing?.Invoke(idx);
                }

                // 阻止原版再创建一次普通攻击。
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展] 最后一段双重攻击失败："
                    + exception);

                return false;
            }
        }
    }

    /// <summary>
    /// 赤瞳＆忏悔的普通攻击动画速度额外提高 15%。
    /// </summary>
    [HarmonyPatch(
        typeof(WeaponControllerSimple),
        "UpdateAnimatorAttackSpeed"
    )]
    internal static class RedEyeConfessionAttackSpeedPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            WeaponControllerSimple __instance)
        {
            if (__instance == null
                || __instance.animator == null)
            {
                return;
            }

            if (!RedEyeConfessionCombatPatchHelper.IsTargetWeapon(
                    __instance.currentWeapon))
            {
                return;
            }

            // 不把施法动画当作普通攻击加速。
            if (__instance.IsCastAnimationRunning)
            {
                return;
            }

            try
            {
                int hash =
                    AnimHashContainer.Instance.AttachSpeedHash;

                float currentSpeed =
                    __instance.animator.GetFloat(hash);

                __instance.animator.SetFloat(
                    hash,
                    currentSpeed * 1.15f);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展] 普通攻击速度补丁失败："
                    + exception);
            }
        }
    }
}