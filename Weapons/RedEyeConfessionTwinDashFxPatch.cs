using HarmonyLib;
using UnityEngine;

namespace LobCorpWeaponExpansion
{
    /// <summary>
    /// 将赤瞳＆忏悔的冲刺攻击特效替换为普通棍子的冲刺特效。
    ///
    /// 不修改动画片段、不修改伤害、不修改普通攻击连段。
    /// 使用克隆后的 NewWeaponFireData，避免直接修改共享的原版数据。
    /// </summary>
    [HarmonyPatch(
        typeof(WeaponControllerSimple),
        nameof(WeaponControllerSimple.CreateDashAttackSwing),
        new[] { typeof(int) }
    )]
    internal static class RedEyeConfessionTwinDashFxPatch
    {
        private const int StaffWeaponEntityId = 500;

        private static GameObject staffDashSwingFx;

        private static bool warnedMissingStaffFx;

        [HarmonyPrefix]
        private static void Prefix(
            WeaponControllerSimple __instance,
            int idx)
        {
            if (__instance == null)
                return;

            WeaponSimple weapon = __instance.currentWeapon;

            if (!RedEyeConfessionCombatPatchHelper.IsTargetWeapon(
                    weapon))
            {
                return;
            }

            // 取得普通棍子的冲刺特效。
            GameObject staffFx = FindStaffDashSwingFx();

            if (staffFx == null)
                return;

            NewWeaponFireData[] dashAttacks = weapon.dashAttacks;

            if (dashAttacks == null || dashAttacks.Length == 0)
                return;

            bool changed = false;

            // 替换该武器全部冲刺攻击数据中的挥击特效。
            // 这样 idx=0 与追加的 idx=1 都能使用正确的特效，
            // 同时保留每个攻击数据自身的伤害等属性。
            for (int i = 0; i < dashAttacks.Length; i++)
            {
                NewWeaponFireData originalData = dashAttacks[i];

                if (originalData == null)
                    continue;

                if (originalData.swingFxPrefab == staffFx)
                    continue;

                // 不能直接修改原始数据，因为它可能被其他武器共享。
                NewWeaponFireData clonedData =
                    UnityEngine.Object.Instantiate(originalData);

                if (clonedData == null)
                    continue;

                clonedData.name =
                    originalData.name + "_LobCorpTwinDash";

                // 只替换特效引用，保留原数据的其他属性。
                clonedData.swingFxPrefab = staffFx;

                dashAttacks[i] = clonedData;
                changed = true;

                Debug.Log(
                    "[脑叶武器扩展][双锤冲刺特效] " +
                    "Index=" + i +
                    ", Data=" + clonedData.name +
                    ", SwingFx=" + clonedData.swingFxPrefab.name);
            }

            if (changed)
            {
                Debug.Log(
                    "[脑叶武器扩展][双锤冲刺特效] " +
                    "已切换为普通棍子的 StaffSwingFx_DashAttack。");
            }
        }

        /// <summary>
        /// 通过日志已确认的 EntityId=500 获取普通棍子。
        /// </summary>
        private static GameObject FindStaffDashSwingFx()
        {
            if (staffDashSwingFx != null)
                return staffDashSwingFx;

            foreach (WeaponEntity entity in
                     Resources.FindObjectsOfTypeAll<WeaponEntity>())
            {
                if (entity == null ||
                    entity.id != StaffWeaponEntityId)
                {
                    continue;
                }

                GameObject prefab = entity.mainWeaponPrefab;

                if (prefab == null)
                    continue;

                WeaponSimple staff =
                    prefab.GetComponent<WeaponSimple>();

                if (staff == null ||
                    staff.dashAttacks == null)
                {
                    continue;
                }

                foreach (NewWeaponFireData data in staff.dashAttacks)
                {
                    if (data == null ||
                        data.swingFxPrefab == null)
                    {
                        continue;
                    }

                    staffDashSwingFx = data.swingFxPrefab;

                    Debug.Log(
                        "[脑叶武器扩展][双锤冲刺特效] " +
                        "特效来源=" + entity.name +
                        ", Prefab=" + prefab.name +
                        ", SwingFx=" + staffDashSwingFx.name);

                    return staffDashSwingFx;
                }
            }

            if (!warnedMissingStaffFx)
            {
                warnedMissingStaffFx = true;

                Debug.LogWarning(
                    "[脑叶武器扩展][双锤冲刺特效] " +
                    "无法获取 EntityId=500 的普通棍子冲刺特效。");
            }

            return null;
        }
    }
}