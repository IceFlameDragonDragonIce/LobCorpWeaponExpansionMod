using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace LobCorpWeaponExpansion
{
    /// <summary>
    /// 赤瞳＆忏悔的自动格挡管理器。
    ///
    /// 只处理 WeaponId = 14000 的剑盾武器实例。
    /// 使用游戏原版 UnitAvatar 格挡系统，不自行拦截伤害。
    /// </summary>
    internal static class RedEyeConfessionAutoGuard
    {
        // 与原版 StartGuard 使用相同的角度。
        private const float GuardAngle = 190f;

        // 通过反射读取 UnitAvatar 的真实格挡状态，
        // 不要求 isGuardEnabled 字段必须为 public。
        private static readonly FieldInfo GuardEnabledField =
            AccessTools.Field(typeof(UnitAvatar), "isGuardEnabled");

        // 作为字段不存在时的备用读取方式。
        private static readonly PropertyInfo NetworkGuardEnabledProperty =
            AccessTools.Property(typeof(UnitAvatar), "NetworkisGuardEnabled");

        private static bool missingGuardStateWarningLogged;

        private sealed class GuardSession
        {
            public UnitAvatar Avatar;
            public bool EnabledByThisPatch;
        }

        // 弱引用表避免武器销毁后，管理器长期持有武器实例。
        private static readonly ConditionalWeakTable<
            WeaponSimple_SwordAndShield,
            GuardSession
        > Sessions =
            new ConditionalWeakTable<
                WeaponSimple_SwordAndShield,
                GuardSession
            >();

        /// <summary>
        /// 普通攻击开始时尝试开启自动格挡。
        /// </summary>
        public static void Begin(WeaponSimple_SwordAndShield weapon)
        {
            int registeredWeaponId =
                RedEyeConfessionWeapon.RegisteredWeaponId;
            try
            {
                if (weapon == null
                    || !weapon.isServer
                    || registeredWeaponId < 0
                    || weapon.entityId != registeredWeaponId)
                {
                    return;
                }

                WeaponControllerSimple owner = weapon.Networkowner;

                if (owner == null || owner.unitAvatar == null)
                {
                    return;
                }

                UnitAvatar avatar = owner.unitAvatar;

                GuardSession session = null;

                bool hasSession = Sessions.TryGetValue(
                    weapon,
                    out session
                );

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

                if (!TryGetGuardEnabled(avatar, out bool guardAlreadyEnabled))
                {
                    return;
                }

                // 已经由本补丁开启的格挡仍然生效时，无须重复开启。
                if (hasSession
                    && session != null
                    && session.EnabledByThisPatch
                    && session.Avatar == avatar
                    && guardAlreadyEnabled)
                {
                    return;
                }

                // 如果格挡原本就开启，尊重其他系统或玩家的格挡状态，
                // 不将它登记成由本补丁开启的格挡。
                if (guardAlreadyEnabled)
                {
                    return;
                }

                // 避免在死亡或没有 MP 时调用 ActivateGuard。
                // 原版格挡系统仍负责判断格挡是否可用。
                if (avatar.IsDead)
                {
                    return;
                }

                if (avatar.GetCustomStatUnsafe("INFINITYMP") <= 0
                    && avatar.MP <= 0)
                {
                    return;
                }

                // 启用正常格挡，不由这次调用开启完美格挡。
                // 格挡 MP 消耗、角度与破防仍沿用游戏自身逻辑。
                avatar.ActivateGuard(
                    GuardAngle,
                    enablePerfectGuard: false,
                    latency: 0f,
                    ignoreExternalPower: false
                );

                if (!TryGetGuardEnabled(avatar, out bool guardEnabledAfterActivation))
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
                    + exception
                );
            }
        }

        /// <summary>
        /// 普通攻击结束时，只关闭由本补丁开启的格挡。
        /// </summary>
        public static void End(WeaponSimple_SwordAndShield weapon)
        {
            if (weapon == null || !weapon.isServer)
            {
                return;
            }

            if (!Sessions.TryGetValue(weapon, out GuardSession session))
            {
                return;
            }

            // 先删除会话，避免清理过程出现重复调用。
            Sessions.Remove(weapon);

            try
            {
                ReleaseSessionGuard(session);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展] 关闭赤瞳＆忏悔自动格挡失败："
                    + exception
                );
            }
        }

        private static void ReleaseSessionGuard(GuardSession session)
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
                    object value = GuardEnabledField.GetValue(avatar);

                    if (value is bool)
                    {
                        enabled = (bool)value;
                        return true;
                    }
                }

                if (NetworkGuardEnabledProperty != null
                    && NetworkGuardEnabledProperty.CanRead)
                {
                    object value =
                        NetworkGuardEnabledProperty.GetValue(avatar, null);

                    if (value is bool)
                    {
                        enabled = (bool)value;
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
                        + exception
                    );
                }

                return false;
            }

            if (!missingGuardStateWarningLogged)
            {
                missingGuardStateWarningLogged = true;

                Debug.LogWarning(
                    "[脑叶武器扩展] 找不到 UnitAvatar 的格挡状态字段或属性，"
                    + "自动格挡已安全跳过。请检查当前游戏版本的反编译结果。"
                );
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
        private static void Postfix(WeaponSimple_SwordAndShield __instance)
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
        private static void Postfix(WeaponSimple_SwordAndShield __instance)
        {
            RedEyeConfessionAutoGuard.End(__instance);
        }
    }
}