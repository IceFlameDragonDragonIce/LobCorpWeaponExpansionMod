using System;
using HarmonyLib;
using UnityEngine;

namespace LobCorpWeaponExpansion
{
    /// <summary>
    /// 赤瞳＆忏悔的冲刺攻击双击补丁。
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
            // 只在第一段攻击结束后追加第二段。
            // idx=1 再次进入时会直接退出，防止递归。
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

            // 仅对原版单段冲刺动作追加第二击。
            AnimatorClipInfo[] clips =
                animator.GetCurrentAnimatorClipInfo(GreatswordLayer);

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
                Debug.Log(
                    "[脑叶武器扩展][双锤冲刺] " +
                    "原始冲刺事件 idx=0 已执行，同帧追加 idx=1。");

                // 使用游戏现有流程生成第二次攻击。
                __instance.CreateDashAttackSwing(1);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展][双锤冲刺] " +
                    "第二次冲刺攻击执行失败：" + exception);
            }
        }
    }
    /// <summary>
    /// 对比赤瞳＆忏悔和棍子的冲刺特效。
    /// 只记录日志，不修改攻击行为。
    /// </summary>
    [HarmonyPatch(
        typeof(WeaponControllerSimple),
        nameof(WeaponControllerSimple.CreateDashAttackSwing),
        new[] { typeof(int) })]
    internal static class RedEyeConfessionStaffDashFxDiagnosticPatch
    {
        private static bool dumpedStaffCandidates;

        [HarmonyPrefix]
        private static void Prefix(
            WeaponControllerSimple __instance,
            int idx)
        {
            if (__instance == null ||
                !RedEyeConfessionCombatPatchHelper.IsTargetWeapon(
                    __instance.currentWeapon))
            {
                return;
            }

            WeaponSimple weapon = __instance.currentWeapon;

            if (weapon.dashAttacks != null &&
                idx >= 0 &&
                idx < weapon.dashAttacks.Length)
            {
                NewWeaponFireData data = weapon.dashAttacks[idx];

                Debug.Log(
                    "[脑叶武器扩展][当前冲刺特效] " +
                    "Index=" + idx +
                    ", FireData=" +
                    (data != null ? data.name : "<null>") +
                    ", SwingFx=" +
                    (data != null && data.swingFxPrefab != null
                        ? data.swingFxPrefab.name
                        : "<null>"));
            }

            if (!dumpedStaffCandidates)
            {
                dumpedStaffCandidates = true;
                DumpStaffCandidates();
            }
        }

        private static void DumpStaffCandidates()
        {
            bool found = false;

            foreach (WeaponEntity entity in
                     Resources.FindObjectsOfTypeAll<WeaponEntity>())
            {
                if (entity == null)
                    continue;

                GameObject prefab = entity.mainWeaponPrefab;

                string identity =
                    entity.name + " " +
                    (prefab != null ? prefab.name : "");

                if (identity.IndexOf(
                        "Staff",
                        StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                found = true;

                WeaponSimple staff =
                    prefab != null
                        ? prefab.GetComponent<WeaponSimple>()
                        : null;

                Debug.Log(
                    "[脑叶武器扩展][棍子冲刺特效候选] " +
                    "EntityId=" + entity.id +
                    ", Entity=" + entity.name +
                    ", Prefab=" +
                    (prefab != null ? prefab.name : "<null>"));

                if (staff == null || staff.dashAttacks == null)
                {
                    Debug.LogWarning(
                        "[脑叶武器扩展][棍子冲刺特效候选] " +
                        "该预制体没有可读取的冲刺攻击数据。");
                    continue;
                }

                for (int i = 0; i < staff.dashAttacks.Length; i++)
                {
                    NewWeaponFireData data = staff.dashAttacks[i];

                    Debug.Log(
                        "[脑叶武器扩展][棍子冲刺特效候选] " +
                        "Index=" + i +
                        ", FireData=" +
                        (data != null ? data.name : "<null>") +
                        ", SwingFx=" +
                        (data != null && data.swingFxPrefab != null
                            ? data.swingFxPrefab.name
                            : "<null>"));
                }
            }

            if (!found)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][棍子冲刺特效候选] " +
                    "没有找到名称包含 Staff 的武器预制体。");
            }
        }
    }
}