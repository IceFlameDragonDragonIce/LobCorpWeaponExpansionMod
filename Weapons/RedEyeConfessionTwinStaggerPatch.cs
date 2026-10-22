using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace LobCorpWeaponExpansion
{
    /// <summary>
    /// 双锤第三段交错攻击补丁。
    /// 连击顺序保持为：1 -> 2 -> 12。
    /// 第三段先执行 idx=4，再延迟执行 idx=5。
    /// </summary>
    [HarmonyPatch]
    internal static class RedEyeConfessionTwinStaggerPatch
    {
        private const int ThirdComboSwing = 2;
        private const int DelayedAttackIndex = 5;
        private const float SecondHitDelaySeconds = 0.12f;

        private static readonly FieldInfo CurrentWeaponField =
            AccessTools.Field(
                typeof(WeaponControllerSimple),
                "currentWeapon");

        private static readonly FieldInfo CurrentWeaponSwingField =
            AccessTools.Field(
                typeof(WeaponControllerSimple),
                "currentWeaponSwing");

        [ThreadStatic]
        private static bool replayingDelayedAttack;

        private static bool loggedMissingControllerFields;

        /// <summary>
        /// 通过反射查找目标方法，避免依赖 HarmonyPatch 的重载声明语法。
        /// </summary>
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(WeaponSimple),
                "CreateBasicAttackProjectile",
                new Type[]
                {
                    typeof(int),
                    typeof(Vector3),
                    typeof(List<CombatBehaviour>),
                    typeof(float),
                    typeof(int),
                    typeof(string)
                });
        }

        /// <summary>
        /// 使用 __args 获取原方法参数，避免依赖参数名称。
        /// </summary>
        private static bool Prefix(
            WeaponSimple __instance,
            object[] __args)
        {
            // 延迟回放时放行原始方法，避免再次截留。
            if (replayingDelayedAttack)
            {
                if (__args != null &&
                    __args.Length > 0 &&
                    __args[0] is int replayIndex &&
                    replayIndex == DelayedAttackIndex)
                {
                    Debug.Log(
                        "[脑叶武器扩展][双锤交错] " +
                        "延迟回放已进入 Harmony，放行 idx=5。");
                }

                return true;
            }

            // 当前只处理服务器端的第三段第二次攻击。
            if (!NetworkServer.active ||
                __instance == null ||
                __args == null ||
                __args.Length < 6)
            {
                return true;
            }

            if (!(__args[0] is int idx) ||
                idx != DelayedAttackIndex)
            {
                return true;
            }

            if (!IsThirdComboSwing(__instance))
            {
                return true;
            }

            try
            {
                Vector3 aimedDelta = (Vector3)__args[1];

                List<CombatBehaviour> sharedTarget =
                    (List<CombatBehaviour>)__args[2];

                float attackDashScale = Convert.ToSingle(__args[3]);
                int mpConsumed = Convert.ToInt32(__args[4]);
                string parameter = __args[5] as string;

                RedEyeConfessionTwinStaggerRunner runner =
                    __instance.GetComponent<
                        RedEyeConfessionTwinStaggerRunner>();

                if (runner == null)
                {
                    runner =
                        __instance.gameObject.AddComponent<
                            RedEyeConfessionTwinStaggerRunner>();
                }

                if (runner == null)
                {
                    Debug.LogWarning(
                        "[脑叶武器扩展][双锤交错] " +
                        "无法创建延迟调度器，恢复原始攻击。");

                    return true;
                }

                bool scheduled = runner.TrySchedule(
                    __instance,
                    idx,
                    aimedDelta,
                    sharedTarget,
                    attackDashScale,
                    mpConsumed,
                    parameter,
                    SecondHitDelaySeconds);

                if (!scheduled)
                {
                    return true;
                }

                Debug.Log(
                    "[脑叶武器扩展][双锤交错] " +
                    "已截留第三段 idx=5，延迟 " +
                    SecondHitDelaySeconds.ToString("0.00") +
                    " 秒执行。");

                // 本次动画事件先不直接执行 idx=5。
                // idx=4 不受影响，仍按动画原本的时间执行。
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][双锤交错] " +
                    "延迟调度失败，恢复原始攻击：" +
                    exception);

                return true;
            }
        }

        /// <summary>
        /// 确认当前 WeaponSimple 对应的武器控制器正在执行第三段。
        /// </summary>
        private static bool IsThirdComboSwing(
            WeaponSimple weapon)
        {
            if (weapon == null ||
                CurrentWeaponField == null ||
                CurrentWeaponSwingField == null)
            {
                if (!loggedMissingControllerFields)
                {
                    loggedMissingControllerFields = true;

                    Debug.LogWarning(
                        "[脑叶武器扩展][双锤交错] " +
                        "无法读取 WeaponControllerSimple 的当前武器或连击段。" +
                        "为避免影响其他攻击，本次不进行延迟。");
                }

                return false;
            }

            WeaponControllerSimple[] controllers =
                UnityEngine.Object.FindObjectsOfType<
                    WeaponControllerSimple>();

            for (int i = 0; i < controllers.Length; i++)
            {
                WeaponControllerSimple controller = controllers[i];

                if (controller == null)
                {
                    continue;
                }

                object currentWeapon =
                    CurrentWeaponField.GetValue(controller);

                if (!ReferenceEquals(currentWeapon, weapon))
                {
                    continue;
                }

                object swingValue =
                    CurrentWeaponSwingField.GetValue(controller);

                if (swingValue == null)
                {
                    return false;
                }

                return Convert.ToInt32(swingValue) ==
                    ThirdComboSwing;
            }

            return false;
        }

        /// <summary>
        /// 延迟结束后调用原始攻击方法。
        /// </summary>
        internal static void InvokeDelayed(
            WeaponSimple weapon,
            int idx,
            Vector3 aimedDelta,
            List<CombatBehaviour> sharedTarget,
            float attackDashScale,
            int mpConsumed,
            string parameter)
        {
            if (weapon == null ||
                !weapon.gameObject.activeInHierarchy)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][双锤交错] " +
                    "武器已销毁或已停用，跳过延迟攻击。");

                return;
            }

            replayingDelayedAttack = true;

            try
            {
                Debug.Log(
                    "[脑叶武器扩展][双锤交错] " +
                    "开始执行延迟攻击：idx=" + idx + "。");

                weapon.CreateBasicAttackProjectile(
                    idx,
                    aimedDelta,
                    sharedTarget,
                    attackDashScale,
                    mpConsumed,
                    parameter);

                Debug.Log(
                    "[脑叶武器扩展][双锤交错] " +
                    "延迟攻击方法已返回：idx=" + idx + "。");
            }
            finally
            {
                replayingDelayedAttack = false;
            }
        }
    }

    /// <summary>
    /// 负责等待指定时间，再执行第三段的第二次攻击。
    /// </summary>
    public sealed class RedEyeConfessionTwinStaggerRunner :
        MonoBehaviour
    {
        private readonly HashSet<int> pendingIndices =
            new HashSet<int>();

        internal bool TrySchedule(
            WeaponSimple weapon,
            int idx,
            Vector3 aimedDelta,
            List<CombatBehaviour> sharedTarget,
            float attackDashScale,
            int mpConsumed,
            string parameter,
            float delaySeconds)
        {
            // 防止同一个攻击事件在等待期间被重复安排。
            if (pendingIndices.Contains(idx))
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][双锤交错] " +
                    "idx=" + idx + " 已在等待执行，忽略重复调度。");

                return false;
            }

            pendingIndices.Add(idx);

            try
            {
                StartCoroutine(
                    InvokeAfterDelay(
                        weapon,
                        idx,
                        aimedDelta,
                        sharedTarget,
                        attackDashScale,
                        mpConsumed,
                        parameter,
                        delaySeconds));

                return true;
            }
            catch
            {
                pendingIndices.Remove(idx);
                throw;
            }
        }

        private IEnumerator InvokeAfterDelay(
            WeaponSimple weapon,
            int idx,
            Vector3 aimedDelta,
            List<CombatBehaviour> sharedTarget,
            float attackDashScale,
            int mpConsumed,
            string parameter,
            float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);

            pendingIndices.Remove(idx);

            if (weapon == null ||
                !weapon.gameObject.activeInHierarchy)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][双锤交错] " +
                    "等待结束时武器已不可用，取消延迟攻击。");

                yield break;
            }

            if (!NetworkServer.active)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][双锤交错] " +
                    "等待结束时服务器不可用，取消延迟攻击。");

                yield break;
            }

            Debug.Log(
                "[脑叶武器扩展][双锤交错] " +
                "延迟时间已到，准备执行第二锤：idx=" +
                idx + "。");

            try
            {
                RedEyeConfessionTwinStaggerPatch.InvokeDelayed(
                    weapon,
                    idx,
                    aimedDelta,
                    sharedTarget,
                    attackDashScale,
                    mpConsumed,
                    parameter);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展][双锤交错] " +
                    "延迟攻击执行异常：" + exception);
            }
        }
    }
}