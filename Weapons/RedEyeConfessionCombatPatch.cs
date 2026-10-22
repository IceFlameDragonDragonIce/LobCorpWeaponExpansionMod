using System;
using HarmonyLib;
using UnityEngine;

namespace LobCorpWeaponExpansion
{
    internal static class RedEyeConfessionCombatPatchHelper
    {
        // 两道斩击的角度偏移，可在实际测试后调整。
        internal const float DualSwingAngle = 18f;

        internal static bool IsTargetWeapon(WeaponSimple weapon)
        {
            return weapon != null
                && RedEyeConfessionWeapon.RegisteredWeaponId >= 0
                && weapon.entityId ==
                    RedEyeConfessionWeapon.RegisteredWeaponId;
        }
    }

    /// <summary>
    /// 第三段普通攻击改为两次独立的攻击判定。
    /// 注意：这不会改变角色本身的动画片段。
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
                return true;

            WeaponSimple_SwordAndShield weapon =
                __instance.currentWeapon as WeaponSimple_SwordAndShield;

            // 双锤动画已经使用 0～5 的事件索引。
            // 原版 finalComboIdx 通常为 2，但双锤第二段的第一个事件也是 idx=2。
            // 不能再把这个事件误判成最后一段并额外生成两次攻击。
            if (!RedEyeConfessionCombatPatchHelper.IsTargetWeapon(weapon) ||
                weapon.animatorLayer == 2)
            {
                return true;
            }

            // 只接管最后一段，其余攻击继续走原版流程。
            if (idx != weapon.finalComboIdx)
                return true;

            NewWeaponFireData fxData = weapon.GetBasicAttack(idx, "");

            if (fxData == null)
                return true;

            try
            {
                float angle =
                    RedEyeConfessionCombatPatchHelper.DualSwingAngle;

                // 客户端生成两道有角度差的斩击特效。
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

                // 阻止原版再创建第三次、普通的单次攻击。
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展] 第三段双重攻击失败："
                    + exception);

                // 已开始执行自定义流程时，不再回退执行原版攻击，
                // 避免异常发生在第一次命中后又追加普通攻击。
                return false;
            }
        }
    }

    /// <summary>
    /// 赤瞳＆忏悔的普通攻击动画速度额外提高 15%。
    /// 不修改角色的基础攻击速度属性。
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
            if (__instance == null || __instance.animator == null)
                return;

            if (!RedEyeConfessionCombatPatchHelper.IsTargetWeapon(
                    __instance.currentWeapon))
                return;

            // 不把施法动画也当作武器普通攻击加速。
            if (__instance.IsCastAnimationRunning)
                return;

            try
            {
                int hash = AnimHashContainer.Instance.AttachSpeedHash;

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