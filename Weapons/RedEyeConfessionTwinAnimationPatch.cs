using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace LobCorpWeaponExpansion.Weapons
{
    internal static class RedEyeConfessionTwinAnimationPatch
    {
        private const int GreatswordLayer = 2;

        private sealed class RedirectContext
        {
            public int RedirectedFireId = int.MinValue;
        }

        private static readonly ConditionalWeakTable<
            WeaponControllerSimple,
            RedirectContext> Contexts = new ConditionalWeakTable<
                WeaponControllerSimple,
                RedirectContext>();

        private static FieldInfo FindField(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);

                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static WeaponSimple GetCurrentWeapon(
            WeaponControllerSimple controller)
        {
            FieldInfo field = FindField(
                typeof(WeaponControllerSimple),
                "currentWeapon");

            return field?.GetValue(controller) as WeaponSimple;
        }

        private static Animator GetAnimator(
            WeaponControllerSimple controller)
        {
            FieldInfo field = FindField(
                typeof(WeaponControllerSimple),
                "animator");

            return field?.GetValue(controller) as Animator;
        }

        private static bool IsTarget(
            WeaponControllerSimple controller)
        {
            WeaponSimple weapon = GetCurrentWeapon(controller);

            return weapon != null &&
                   weapon.entityId ==
                       RedEyeConfessionWeapon.RegisteredWeaponId;
        }

        private static string GetTwinClipName(int fireId)
        {
            switch (fireId)
            {
                case 0:
                    return "GreatSword_Twin_Attack1";

                case 1:
                    return "GreatSword_Twin_Attack2";

                case 2:
                    return "GreatSword_Twin_Attack3";

                default:
                    return null;
            }
        }

        private static bool IsTwinClipPlaying(Animator animator)
        {
            AnimatorClipInfo[] clips =
                animator.GetCurrentAnimatorClipInfo(GreatswordLayer);

            foreach (AnimatorClipInfo info in clips)
            {
                if (info.clip != null &&
                    info.clip.name.StartsWith(
                        "GreatSword_Twin_Attack",
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
        
        private static string FindStatePath(
            Animator animator,
            string clipName)
        {
            string fullPath =
                animator.GetLayerName(GreatswordLayer) + "." + clipName;

            if (animator.HasState(
                    GreatswordLayer,
                    Animator.StringToHash(fullPath)))
            {
                return fullPath;
            }

            if (animator.HasState(
                    GreatswordLayer,
                    Animator.StringToHash(clipName)))
            {
                return clipName;
            }

            return null;
        }
        [HarmonyPatch(
            typeof(WeaponControllerSimple),
            nameof(WeaponControllerSimple.BeginAttackAnimation),
            new[] { typeof(int) })]
        private static class BeginAttackAnimationPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(
                WeaponControllerSimple __instance,
                int fireId)
            {
                if (fireId < 0 ||
                    fireId > 2 ||
                    !IsTarget(__instance))
                {
                    return true;
                }

                Animator animator = GetAnimator(__instance);

                if (animator == null ||
                    animator.layerCount <= GreatswordLayer)
                {
                    Debug.LogError(
                        "[脑叶武器扩展][双锤动作] 找不到巨剑动画层。");

                    return true;
                }

                RedirectContext context =
                    Contexts.GetValue(
                        __instance,
                        _ => new RedirectContext());

                // 双锤动画自身也有 BeginAttackAnimation 事件。
                // 如果它是刚刚切换动画产生的同一段事件，就避免重复初始化。
                if (IsTwinClipPlaying(animator))
                {
                    if (context.RedirectedFireId == fireId)
                    {
                        context.RedirectedFireId = int.MinValue;

                        Debug.Log(
                            $"[脑叶武器扩展][双锤动作] " +
                            $"已抑制重复初始化 fireId={fireId}");

                        return false;
                    }

                    return true;
                }

                // 只替换普通巨剑攻击，不干扰其他类型的动作。
                AnimatorClipInfo[] currentClips =
                    animator.GetCurrentAnimatorClipInfo(
                        GreatswordLayer);

                bool isNormalGreatswordAttack = false;

                foreach (AnimatorClipInfo info in currentClips)
                {
                    if (info.clip == null)
                    {
                        continue;
                    }

                    string clipName = info.clip.name;

                    if (clipName.StartsWith(
                            "GreatSword_Attack",
                            StringComparison.Ordinal))
                    {
                        isNormalGreatswordAttack = true;
                        break;
                    }
                }

                if (!isNormalGreatswordAttack)
                {
                    return true;
                }

                string twinClip = GetTwinClipName(fireId);

                if (twinClip == null)
                {
                    return true;
                }

                string statePath = FindStatePath(animator, twinClip);

                if (statePath == null)
                {
                    Debug.LogError(
                        $"[脑叶武器扩展][双锤动作] " +
                        $"动画层 {animator.GetLayerName(GreatswordLayer)} " +
                        $"中找不到状态：{twinClip}");

                    return true;
                }

                context.RedirectedFireId = fireId;

                // 服务器切换动画，保证服务器能够处理双锤动画事件。
                animator.Play(statePath, GreatswordLayer, 0f);

                Debug.Log(
                    $"[脑叶武器扩展][双锤动作] " +
                    $"fireId={fireId}，切换到 {statePath}");

                // 保留原始 BeginAttackAnimation 的初始化逻辑。
                return true;
            }
        }
    }
}