using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace LobCorpWeaponExpansion
{
    internal static class RedEyeConfessionTier2System
    {
        internal const int MaxEnergy = 100;
        internal const int SkullcrushBonusDamagePercent = 100;

        private const int EnergyPerHit = 10;
        private const int EnergyPerGuard = 20;

        private const float ShieldPerGuard = 8f;
        private const float ShieldPerPerfectGuard = 16f;
        private const float MaxShield = 60f;

        private const string ShieldKey =
            "LobCorpWeaponExpansion_RedEyeConfession_Tier2";

        private sealed class ChargeState
        {
            public int Energy;
        }

        private sealed class ControllerBinding
        {
            public UnitAvatar Avatar;

            public Action<UnitAvatar, DamageInstance> AttackHandler;
            public Action<DamageInstance, bool> GuardHandler;
        }

        private static readonly ConditionalWeakTable<
            UnitAvatar,
            ChargeState> Charges =
                new ConditionalWeakTable<
                    UnitAvatar,
                    ChargeState>();

        private static readonly ConditionalWeakTable<
            WeaponControllerSimple,
            ControllerBinding> Bindings =
                new ConditionalWeakTable<
                    WeaponControllerSimple,
                    ControllerBinding>();

        internal static int GetEnergy(UnitAvatar avatar)
        {
            if (avatar == null
                || !Charges.TryGetValue(
                    avatar,
                    out ChargeState state))
            {
                return 0;
            }

            return state.Energy;
        }

        private static void AddEnergy(
            UnitAvatar avatar,
            int amount)
        {
            if (!NetworkServer.active
                || avatar == null
                || amount <= 0)
            {
                return;
            }

            ChargeState state =
                Charges.GetValue(
                    avatar,
                    _ => new ChargeState());

            int oldEnergy = state.Energy;

            state.Energy = Mathf.Min(
                MaxEnergy,
                state.Energy + amount);

            // 只在跨越 20 点能量区间时输出一次诊断。
            if (oldEnergy / 20 != state.Energy / 20)
            {
                Debug.Log(
                    "[脑叶武器扩展][能量] 当前能量="
                    + state.Energy
                    + "/"
                    + MaxEnergy);
            }
        }

        internal static bool TryConsumeEnergy(
            UnitAvatar avatar,
            int amount)
        {
            if (!NetworkServer.active
                || avatar == null
                || amount <= 0
                || !Charges.TryGetValue(
                    avatar,
                    out ChargeState state)
                || state.Energy < amount)
            {
                return false;
            }

            state.Energy -= amount;

            Debug.Log(
                "[脑叶武器扩展][能量] 消耗 "
                + amount
                + "，剩余 "
                + state.Energy
                + "/"
                + MaxEnergy);

            return true;
        }

        private static void HandleSuccessfulHit(
            UnitAvatar avatar,
            UnitAvatar victim,
            DamageInstance damage)
        {
            if (avatar == null
                || victim == null
                || damage == null)
            {
                return;
            }

            AddEnergy(avatar, EnergyPerHit);
        }

        private static void HandleSuccessfulGuard(
            UnitAvatar avatar,
            DamageInstance damage,
            bool perfectGuard)
        {
            if (!NetworkServer.active
                || avatar == null
                || damage == null)
            {
                return;
            }

            // 成功格挡：获得能量。
            AddEnergy(avatar, EnergyPerGuard);

            // 根据是否为完美格挡确定本次护盾增量。
            float shieldGain = perfectGuard
                ? ShieldPerPerfectGuard
                : ShieldPerGuard;

            float currentShield =
                avatar.GetShield(ShieldKey);

            // 计算真正可以增加的护盾，避免超过上限。
            float actualGain = Mathf.Min(
                shieldGain,
                Mathf.Max(0f, MaxShield - currentShield));

            if (actualGain > 0f)
            {
                avatar.AddShield(actualGain, ShieldKey);
            }
        }

        private static void Detach(
            ControllerBinding binding)
        {
            if (binding == null)
            {
                return;
            }

            UnitAvatar avatar = binding.Avatar;

            if (avatar != null)
            {
                if (binding.AttackHandler != null)
                {
                    avatar.OnAttackUnit -= binding.AttackHandler;
                }

                if (binding.GuardHandler != null)
                {
                    avatar.OnGuardSucceeded -= binding.GuardHandler;
                }

                // 离开强化版时清除本系统提供的护盾和能量。
                if (NetworkServer.active)
                {
                    avatar.RemoveShield(ShieldKey);
                }

                Charges.Remove(avatar);
            }

            binding.Avatar = null;
            binding.AttackHandler = null;
            binding.GuardHandler = null;
        }

        internal static void UpdateController(
            WeaponControllerSimple controller)
        {
            if (!NetworkServer.active
                || controller == null
                || !controller.isServer)
            {
                return;
            }

            ControllerBinding binding =
                Bindings.GetValue(
                    controller,
                    _ => new ControllerBinding());

            UnitAvatar avatar = controller.unitAvatar;
            WeaponSimple weapon = controller.currentWeapon;

            bool isTier2Equipped =
                avatar != null
                && weapon != null
                && RedEyeConfessionWeapon.IsTier2WeaponId(
                    weapon.entityId);

            // 更换武器或角色时解除旧事件订阅。
            if (binding.Avatar != null
                && (!isTier2Equipped || binding.Avatar != avatar))
            {
                Detach(binding);
            }

            // 没有装备强化版，或者已经绑定当前角色。
            if (!isTier2Equipped
                || binding.Avatar == avatar)
            {
                return;
            }

            binding.Avatar = avatar;

            binding.AttackHandler =
                (victim, damage) =>
                    HandleSuccessfulHit(
                        avatar,
                        victim,
                        damage);

            binding.GuardHandler =
                (damage, perfectGuard) =>
                    HandleSuccessfulGuard(
                        avatar,
                        damage,
                        perfectGuard);

            avatar.OnAttackUnit += binding.AttackHandler;
            avatar.OnGuardSucceeded += binding.GuardHandler;

            Debug.Log(
                "[脑叶武器扩展][第二阶段] 已启用能量与护盾系统。");
        }
    }

    /// <summary>
    /// 在服务器端更新强化版的事件订阅。
    /// </summary>
    [HarmonyPatch]
    internal static class RedEyeConfessionTier2ChargeHookPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(WeaponControllerSimple),
                "Update");
        }

        [HarmonyPostfix]
        private static void Postfix(
            WeaponControllerSimple __instance)
        {
            RedEyeConfessionTier2System.UpdateController(__instance);
        }
    }

    /// <summary>
    /// 仅拦截强化版的特殊攻击。
    /// 基础版继续由 RedEyeConfessionSpecialMovePatch 禁用特殊攻击。
    /// </summary>
    [HarmonyPatch]
    internal static class RedEyeConfessionTier2SpecialAttackPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(WeaponSimple),
                "CreateSpecialAttackProjectile",
                new Type[]
                {
                    typeof(int),
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(List<CombatBehaviour>),
                    typeof(float),
                    typeof(int),
                    typeof(int),
                    typeof(int)
                });
        }

        [HarmonyPrefix]
        private static bool Prefix(
            WeaponSimple __instance,
            ref int bonusDamagePercent)
        {
            // 只处理强化版。
            if (__instance == null
                || !RedEyeConfessionWeapon.IsTier2WeaponId(
                    __instance.entityId))
            {
                return true;
            }

            // 能量消耗与伤害只由服务器决定。
            if (!NetworkServer.active)
            {
                return true;
            }

            WeaponControllerSimple owner =
                __instance.Networkowner;

            UnitAvatar avatar =
                owner == null
                    ? null
                    : owner.unitAvatar;

            // 碎颅必须在格挡状态下发动。
            // 条件不满足时既不扣能量，也不生成特殊攻击。
            if (owner == null
                || avatar == null
                || owner.animator == null
                || AnimHashContainer.Instance == null
                || !owner.animator.GetBool(
                    AnimHashContainer.Instance.GuardHash))
            {
                Debug.Log(
                    "[脑叶武器扩展][碎颅] 未处于格挡状态，"
                    + "取消本次特殊攻击。");

                return false;
            }

            // 满能量时才能发动碎颅。
            if (!RedEyeConfessionTier2System.TryConsumeEnergy(
                    avatar,
                    RedEyeConfessionTier2System.MaxEnergy))
            {
                Debug.Log(
                    "[脑叶武器扩展][碎颅] 能量不足，"
                    + "取消本次特殊攻击。");

                return false;
            }

            // 消耗 100 点能量，特殊攻击伤害加成 +100%。
            bonusDamagePercent +=
                RedEyeConfessionTier2System
                    .SkullcrushBonusDamagePercent;

            Debug.Log(
                "[脑叶武器扩展][碎颅] 能量已消耗，"
                + "特殊攻击伤害加成 +100%。");

            return true;
        }
    }
}