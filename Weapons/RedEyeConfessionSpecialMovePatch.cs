using HarmonyLib;

namespace LobCorpWeaponExpansion
{
    internal static class RedEyeConfessionSpecialMove
    {
        internal static bool IsTargetWeapon(WeaponSimple weapon)
        {
            return weapon != null
                && RedEyeConfessionWeapon.RegisteredWeaponId >= 0
                && weapon.entityId ==
                    RedEyeConfessionWeapon.RegisteredWeaponId;
        }
    }

    /// <summary>
    /// 基础版赤瞳&忏悔禁用特殊攻击。
    /// 只拦截格挡状态下的攻击输入，
    /// 不影响普通攻击和冲刺攻击。
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
                !RedEyeConfessionSpecialMove.IsTargetWeapon(
                    __instance))
            {
                return true;
            }

            WeaponControllerSimple owner =
                __instance.Networkowner;

            if (owner == null || owner.animator == null)
            {
                return true;
            }

            // 原版在格挡状态下处理特殊攻击输入。
            // 基础版直接拦截该输入。
            if (owner.animator.GetBool(
                    AnimHashContainer.Instance.GuardHash))
            {
                return false;
            }

            // 非格挡状态不受此补丁影响。
            return true;
        }
    }
}