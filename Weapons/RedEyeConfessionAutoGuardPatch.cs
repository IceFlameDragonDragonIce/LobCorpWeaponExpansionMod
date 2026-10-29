using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace LobCorpWeaponExpansion
{
    /// <summary>
    /// 赤瞳＆忏悔的自动格挡管理器。
    /// 使用游戏原版 UnitAvatar 格挡系统，不自行拦截伤害。
    /// </summary>
    internal static class RedEyeConfessionAutoGuard
    {
        // 与原版 StartGuard 使用相同的角度。
        private const float GuardAngle = 190f;

        // 通过反射读取 UnitAvatar 的真实格挡状态。
        private static readonly FieldInfo GuardEnabledField =
            AccessTools.Field(
                typeof(UnitAvatar),
                "isGuardEnabled");

        // 字段不存在时，尝试读取网络属性。
        private static readonly PropertyInfo NetworkGuardEnabledProperty =
            AccessTools.Property(
                typeof(UnitAvatar),
                "NetworkisGuardEnabled");

        private static bool missingGuardStateWarningLogged;

        private sealed class GuardSession
        {
            public UnitAvatar Avatar;
            public bool EnabledByThisPatch;
        }

        private static readonly ConditionalWeakTable<
            WeaponSimple_SwordAndShield,
            GuardSession> Sessions =
                new ConditionalWeakTable<
                    WeaponSimple_SwordAndShield,
                    GuardSession>();

        /// <summary>
        /// 普通攻击开始时尝试开启自动格挡。
        /// </summary>
        internal static void Begin(
            WeaponSimple_SwordAndShield weapon)
        {
            try
            {
                if (weapon == null
                    || !weapon.isServer
                    || !RedEyeConfessionWeapon.IsManagedWeaponId(
                        weapon.entityId))
                {
                    return;
                }

                WeaponControllerSimple owner =
                    weapon.Networkowner;

                if (owner == null || owner.unitAvatar == null)
                {
                    return;
                }

                UnitAvatar avatar = owner.unitAvatar;

                GuardSession session = null;

                bool hasSession = Sessions.TryGetValue(
                    weapon,
                    out session);

                // 武器更换持有者时，不能把旧角色的格挡状态留住。
                if (hasSession
                    && session.EnabledByThisPatch
                    && session.Avatar != null
                    && session.Avatar != avatar)
                {
                    ReleaseSessionGuard(session);

                    Sessions.Remove(weapon);
                    session = null;
                    hasSession = false;
                }

                if (!TryGetGuardEnabled(
                        avatar,
                        out bool guardAlreadyEnabled))
                {
                    return;
                }

                // 由本补丁开启且仍在生效的格挡，无须重复开启。
                if (hasSession
                    && session != null
                    && session.EnabledByThisPatch
                    && session.Avatar == avatar
                    && guardAlreadyEnabled)
                {
                    return;
                }

                // 如果格挡原本就开启，尊重原版或其他模组的状态。
                if (guardAlreadyEnabled)
                {
                    return;
                }

                if (avatar.IsDead)
                {
                    return;
                }

                // 沿用原有 MP 检查。
                if (avatar.GetCustomStatUnsafe("INFINITYMP") <= 0
                    && avatar.MP <= 0)
                {
                    return;
                }

                // 开启普通格挡，不主动开启完美格挡。
                avatar.ActivateGuard(
                    GuardAngle,
                    enablePerfectGuard: false,
                    latency: 0f,
                    ignoreExternalPower: false);

                if (!TryGetGuardEnabled(
                        avatar,
                        out bool guardEnabledAfterActivation))
                {
                    return;
                }

                // 只有确认真实格挡已开启，才记录所有权。
                if (!guardEnabledAfterActivation)
                {
                    return;
                }

                if (session == null)
                {
                    session = new GuardSession();
                    Sessions.Add(weapon, session);
                }

                session.Avatar = avatar;
                session.EnabledByThisPatch = true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展] 开启赤瞳＆忏悔自动格挡失败："
                    + exception);
            }
        }

        /// <summary>
        /// 普通攻击结束时，只关闭由本补丁开启的格挡。
        /// </summary>
        internal static void End(
            WeaponSimple_SwordAndShield weapon)
        {
            if (weapon == null || !weapon.isServer)
            {
                return;
            }

            if (!Sessions.TryGetValue(
                    weapon,
                    out GuardSession session))
            {
                return;
            }

            // 先删除会话，避免清理过程重复调用。
            Sessions.Remove(weapon);

            try
            {
                ReleaseSessionGuard(session);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展] 关闭赤瞳＆忏悔自动格挡失败："
                    + exception);
            }
        }

        private static void ReleaseSessionGuard(
            GuardSession session)
        {
            if (session == null
                || !session.EnabledByThisPatch
                || session.Avatar == null)
            {
                return;
            }

            UnitAvatar avatar = session.Avatar;

            session.EnabledByThisPatch = false;
            session.Avatar = null;

            // 如果格挡已因破防或其他原版逻辑关闭，不重复处理。
            if (TryGetGuardEnabled(avatar, out bool guardEnabled)
                && guardEnabled)
            {
                avatar.InactivateGuard();
            }
        }

        private static bool TryGetGuardEnabled(
            UnitAvatar avatar,
            out bool enabled)
        {
            enabled = false;

            try
            {
                if (GuardEnabledField != null)
                {
                    object value =
                        GuardEnabledField.GetValue(avatar);

                    if (value is bool fieldValue)
                    {
                        enabled = fieldValue;
                        return true;
                    }
                }

                if (NetworkGuardEnabledProperty != null
                    && NetworkGuardEnabledProperty.CanRead)
                {
                    object value =
                        NetworkGuardEnabledProperty.GetValue(
                            avatar,
                            null);

                    if (value is bool propertyValue)
                    {
                        enabled = propertyValue;
                        return true;
                    }
                }
            }
            catch (Exception exception)
            {
                if (!missingGuardStateWarningLogged)
                {
                    missingGuardStateWarningLogged = true;

                    Debug.LogWarning(
                        "[脑叶武器扩展] 无法读取角色真实格挡状态，"
                        + "本次不会修改格挡。详情："
                        + exception);
                }

                return false;
            }

            if (!missingGuardStateWarningLogged)
            {
                missingGuardStateWarningLogged = true;

                Debug.LogWarning(
                    "[脑叶武器扩展] 找不到 UnitAvatar 的格挡状态字段或属性，"
                    + "自动格挡已安全跳过。请检查当前游戏版本的反编译结果。");
            }

            return false;
        }
    }

    /// <summary>
    /// 普通攻击开始后启用自动格挡。
    /// </summary>
    [HarmonyPatch(
        typeof(WeaponSimple_SwordAndShield),
        nameof(WeaponSimple_SwordAndShield.StartBasicAttackAnimation)
    )]
    internal static class RedEyeConfessionAutoGuardStartPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            WeaponSimple_SwordAndShield __instance)
        {
            RedEyeConfessionAutoGuard.Begin(__instance);
        }
    }

    /// <summary>
    /// 普通攻击结束后清理自动格挡。
    /// </summary>
    [HarmonyPatch(
        typeof(WeaponSimple_SwordAndShield),
        nameof(WeaponSimple_SwordAndShield.EndAttackAnimation)
    )]
    internal static class RedEyeConfessionAutoGuardEndPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            WeaponSimple_SwordAndShield __instance)
        {
            RedEyeConfessionAutoGuard.End(__instance);
        }
    }
}