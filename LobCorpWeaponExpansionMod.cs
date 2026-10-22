using System;
using HarmonyLib;
using UnityEngine;

namespace LobCorpWeaponExpansion
{
    /// <summary>
    /// Native Sephiria AddOn entry point. This project does not require Sephiria ModMaker.
    /// </summary>
    public sealed class LobCorpWeaponExpansionMod : HorayModBase
    {
        private const string HarmonyId = "DragonIce.FrostFire.LobCorpWeaponExpansion";
        private static Harmony harmony;
        private static bool hooksRegistered;

        protected override void OnModLoaded()
        {
            try
            {
                base.OnModLoaded();

                if (!hooksRegistered)
                {
                    HorayModAPI.OnLocalizationReady += OnLocalizationReady;
                    HorayModAPI.OnLoadWeaponDatabase += OnLoadWeaponDatabase;
                    hooksRegistered = true;
                }

                if (harmony == null)
                {
                    harmony = new Harmony(HarmonyId);
                    harmony.PatchAll(typeof(LobCorpWeaponExpansionMod).Assembly);
                }

                Debug.Log("[脑叶武器扩展] AddOn 已加载；等待武器数据库初始化。版本：0.1.0");
            }
            catch (Exception exception)
            {
                Debug.LogError("[脑叶武器扩展] 初始化失败：" + exception);
            }
        }

        protected override void OnModUnloaded()
        {
            try
            {
                if (hooksRegistered)
                {
                    HorayModAPI.OnLocalizationReady -= OnLocalizationReady;
                    HorayModAPI.OnLoadWeaponDatabase -= OnLoadWeaponDatabase;
                    hooksRegistered = false;
                }

                if (harmony != null)
                {
                    harmony.UnpatchAll(HarmonyId);
                    harmony = null;
                }

                base.OnModUnloaded();
                Debug.Log("[脑叶武器扩展] AddOn 已卸载。");
            }
            catch (Exception exception)
            {
                Debug.LogError("[脑叶武器扩展] 卸载清理失败：" + exception);
            }
        }

        private void OnLocalizationReady(HorayModLocalizationContext context)
        {
            try
            {
                RedEyeConfessionWeapon.RegisterLocalization(context);
            }
            catch (Exception exception)
            {
                Debug.LogError("[脑叶武器扩展] 注册本地化文本失败：" + exception);
            }
        }


private void OnLoadWeaponDatabase()
{
    try
    {
        RedEyeConfessionWeapon.Register();
    }
    catch (Exception exception)
    {
        Debug.LogError("[脑叶武器扩展] 注册武器失败：" + exception);
    }

    // 独立替换物品图标，避免图标异常影响武器注册。
    try
    {
        RedEyeConfessionSpriteAssets.ApplyWeaponIcon();
    }
    catch (Exception exception)
    {
        Debug.LogError(
            "[脑叶武器扩展] 替换红眼忏悔物品图标失败：" +
            exception
        );
    }

    // 独立执行诊断，避免诊断异常影响武器注册。
    try
    {
        // 标准剑盾：用作对照。
        DumpWeaponDiagnostics(0);

        // 胡萝卜剑：原有对照目标，暂时保留。
        DumpWeaponDiagnostics(1000);
    }
    catch (Exception exception)
    {
        Debug.LogError("[脑叶武器扩展] 武器诊断失败：" + exception);
    }
}

        private static void DumpWeaponDiagnostics(int id)
        {
            WeaponEntity entity = WeaponDatabase.FindWeaponById(id);

            // 如果数据库查询失败，尝试从已加载的资源中查找。
            if (entity == null)
            {
                foreach (WeaponEntity candidate
                         in Resources.FindObjectsOfTypeAll<WeaponEntity>())
                {
                    if (candidate != null && candidate.id == id)
                    {
                        entity = candidate;
                        break;
                    }
                }
            }

            if (entity == null)
            {
                Debug.LogWarning(
                    $"[脑叶武器扩展][诊断] 找不到 WeaponEntity，ID={id}"
                );
                return;
            }

            Debug.Log(
                $"[脑叶武器扩展][诊断] " +
                $"ID={entity.id}, " +
                $"AssetName={entity.name}, " +
                $"DisplayName={entity.aName}, " +
                $"Enabled={entity.enabled}, " +
                $"MainPrefab={(entity.mainWeaponPrefab != null ? entity.mainWeaponPrefab.name : "<null>")}, " +
                $"SubPrefab={(entity.subWeaponPrefab != null ? entity.subWeaponPrefab.name : "<null>")}"
            );

            GameObject prefab = entity.mainWeaponPrefab;

            if (prefab == null)
            {
                Debug.LogWarning(
                    $"[脑叶武器扩展][诊断] ID={id} 没有主武器预制体。"
                );
                return;
            }

            WeaponSimple weapon = prefab.GetComponent<WeaponSimple>();

            if (weapon == null)
            {
                Debug.LogWarning(
                    $"[脑叶武器扩展][诊断] " +
                    $"预制体 {prefab.name} 上找不到 WeaponSimple。"
                );

                foreach (Component component in prefab.GetComponents<Component>())
                {
                    Debug.Log(
                        $"[脑叶武器扩展][诊断] 根组件：" +
                        $"{(component != null ? component.GetType().FullName : "<Missing Script>")}"
                    );
                }

                return;
            }

            Debug.Log(
                $"[脑叶武器扩展][诊断] " +
                $"Prefab={prefab.name}, " +
                $"ComponentType={weapon.GetType().FullName}"
            );

            Debug.Log(
                $"[脑叶武器扩展][诊断] " +
                $"WeaponType={weapon.weaponType}, " +
                $"AttackMoveSet={weapon.attackMoveSet}, " +
                $"AnimatorLayer={weapon.animatorLayer}, " +
                $"FinalComboIdx={weapon.finalComboIdx}, " +
                $"IsRangedWeapon={weapon.isRangedWeapon}, " +
                $"AttackWeightPerSwing={weapon.attackWeightPerSwing}, " +
                $"AttackSpeedAmplify={weapon.attackSpeedAmplify}, " +
                $"SpecialAttackIsRelatedToAttackSpeed={weapon.specialAttackIsRelatedToAttackSpeed}"
            );

            Debug.Log(
                $"[脑叶武器扩展][诊断] " +
                $"ActionsCount={(weapon.actions != null ? weapon.actions.Length : -1)}, " +
                $"BasicCount={(weapon.basicComboAttacks != null ? weapon.basicComboAttacks.Length : -1)}, " +
                $"DashCount={(weapon.dashAttacks != null ? weapon.dashAttacks.Length : -1)}, " +
                $"SpecialCount={(weapon.specialAttacks != null ? weapon.specialAttacks.Length : -1)}"
            );

            DumpFireData("普通攻击", weapon.basicComboAttacks);
            DumpFireData("冲刺攻击", weapon.dashAttacks);
            DumpFireData("特殊攻击", weapon.specialAttacks);

            // 如果是剑盾类，还要查看它的额外攻击配置。
            if (weapon is WeaponSimple_SwordAndShield swordShield)
            {
                Debug.Log(
                    $"[脑叶武器扩展][诊断] 剑盾专属配置：" +
                    $"GuardAvailable={swordShield.isGuardAvailable}, " +
                    $"SweepAvailable={swordShield.isSweepAvailable}, " +
                    $"GuardSweep={swordShield.guardSweep}, " +
                    $"GuardSweepNew={swordShield.guardSweep_New}, " +
                    $"AllElementalEnabled={swordShield.isAllElementalEnabled}"
                );

                if (swordShield.allElementalSets != null)
                {
                    for (int i = 0; i < swordShield.allElementalSets.Length; i++)
                    {
                        WeaponSimple_SwordAndShield.AllElementalFireDataSet set =
                            swordShield.allElementalSets[i];

                        if (set == null)
                        {
                            continue;
                        }

                        Debug.Log(
                            $"[脑叶武器扩展][诊断] 元素动作组[{i}]：" +
                            $"State={set.animatorState}, " +
                            $"BasicCount={(set.basicComboAttacks != null ? set.basicComboAttacks.Length : -1)}, " +
                            $"DashCount={(set.dashAttacks != null ? set.dashAttacks.Length : -1)}, " +
                            $"SpecialCount={(set.specialAttacks != null ? set.specialAttacks.Length : -1)}"
                        );

                        DumpFireData(
                            $"元素动作组[{i}] 普通攻击",
                            set.basicComboAttacks
                        );

                        DumpFireData(
                            $"元素动作组[{i}] 特殊攻击",
                            set.specialAttacks
                        );
                    }
                }
            }
        }

        private static void DumpFireData(
            string category,
            NewWeaponFireData[] dataArray)
        {
            if (dataArray == null)
            {
                Debug.Log(
                    $"[脑叶武器扩展][诊断] {category}：数组为 null。"
                );
                return;
            }

            for (int i = 0; i < dataArray.Length; i++)
            {
                NewWeaponFireData data = dataArray[i];

                if (data == null)
                {
                    Debug.Log(
                        $"[脑叶武器扩展][诊断] {category}[{i}]=<null>"
                    );
                    continue;
                }

                Debug.Log(
                    $"[脑叶武器扩展][诊断] {category}[{i}]：" +
                    $"AssetName={data.name}, " +
                    $"Type={data.GetType().FullName}, " +
                    $"DamageMultiplier={data.damageMultiplier}, " +
                    $"Element={data.damageElementalType}, " +
                    $"RelatedStatFormula={data.relatedStatFormula}, " +
                    $"SwingID={data.swingID}, " +
                    $"ForceFinalCombo={data.forceFinalCombo}, " +
                    $"StaggeringLevel={data.staggeringLevel}, " +
                    $"ExternalForce={data.externalForcePower}, " +
                    $"DashSpeed={data.attackDashSpeed}, " +
                    $"DashTime={data.attackDashTime}, " +
                    $"DashAngle={data.attackDashAngle}, " +
                    $"SwingFx={(data.swingFxPrefab != null ? data.swingFxPrefab.name : "<null>")}, " +
                    $"AttachFx={data.attachSwingFxToOwner}"
                );
            }
        }
    }
}
