using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LobCorpWeaponExpansion
{
    /// <summary>
    /// 赤瞳&忏悔。
    /// 第一阶段直接复用原版标准剑盾预制体，
    /// 不创建额外的运行时 NetworkIdentity。
    /// </summary>
    internal static class RedEyeConfessionWeapon
    {
        internal const string InternalName =
            "LobCorpWeaponExpansion_RedEyeConfession";

        internal const string DisplayNameKey =
            "Weapon_LobCorpWeaponExpansion_RedEyeConfession_Name";

        // 武器风味文本。本地化键采用 MekkReForge 的 _Flavor 命名方式。
        internal const string FlavorTextKey =
            "Weapon_LobCorpWeaponExpansion_RedEyeConfession_Flavor";

        // 原版标准剑盾。
        private const int PrefabTemplateWeaponId = 0;

        // 标准剑盾强化路线。
        private const int EnhancementParentWeaponId = 0;

        private const int FirstCustomWeaponId = 14000;

        private static bool registered;

        internal static int RegisteredWeaponId { get; private set; } = -1;

        internal static void RegisterLocalization(
            HorayModLocalizationContext context)
        {
            if (context == null)
                return;

            // 武器名称
            context.AddText(
                "zh-CN",
                DisplayNameKey,
                "赤瞳&忏悔");

            context.AddText(
                "en-US",
                DisplayNameKey,
                "Red Eye & Confession");

            context.AddText(
                "ja-JP",
                DisplayNameKey,
                "赤瞳と懺悔");

            // 武器风味文本
            context.AddText(
                "zh-CN",
                FlavorTextKey,
                "赤瞳凝视罪人，忏悔则为其降下最后的判决。");

            context.AddText(
                "en-US",
                FlavorTextKey,
                "The crimson eye watches the condemned; penance delivers the final verdict.");

            context.AddText(
                "ja-JP",
                FlavorTextKey,
                "赤き瞳は罪人を見据え、懺悔が最後の審判を下す。");
        }

        internal static void Register()
        {
            if (registered)
                return;

            List<WeaponEntity> weapons = WeaponDatabase.GetAll();

            if (weapons == null)
            {
                Debug.LogError(
                    "[脑叶武器扩展] WeaponDatabase.GetAll() 返回 null。");
                return;
            }

            string expectedName = "_" + InternalName;

            WeaponEntity existingWeapon = weapons.FirstOrDefault(
                weapon =>
                    weapon != null &&
                    !string.IsNullOrEmpty(weapon.name) &&
                    (
                        weapon.name == InternalName ||
                        weapon.name.EndsWith(
                            expectedName,
                            StringComparison.Ordinal)
                    ));

            if (existingWeapon != null)
            {
                RegisteredWeaponId = existingWeapon.id;

                Debug.LogWarning(
                    "[脑叶武器扩展] 数据库中已经存在赤瞳&忏悔，" +
                    "跳过重复注册。实际 ID=" + RegisteredWeaponId);

                registered = true;
                return;
            }

            WeaponEntity template = weapons.FirstOrDefault(
                weapon =>
                    weapon != null &&
                    weapon.id == PrefabTemplateWeaponId);

            if (template == null ||
                template.mainWeaponPrefab == null)
            {
                Debug.LogError(
                    "[脑叶武器扩展] 找不到原版标准剑盾模板，ID=" +
                    PrefabTemplateWeaponId);

                return;
            }

            WeaponEntity enhancementParent = weapons.FirstOrDefault(
                weapon =>
                    weapon != null &&
                    weapon.id == EnhancementParentWeaponId);

            if (enhancementParent == null)
            {
                Debug.LogError(
                    "[脑叶武器扩展] 找不到强化路线父武器，ID=" +
                    EnhancementParentWeaponId);

                return;
            }

            int customId;

            try
            {
                customId = FindUnusedWeaponId(weapons);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展] 分配武器 ID 失败：" +
                    exception);

                return;
            }

            WeaponEntity customWeapon = null;

            try
            {
                // 复制完整的武器数据。
                customWeapon =
                    UnityEngine.Object.Instantiate(template);

                customWeapon.name =
                    customId + "_" + InternalName;

                customWeapon.id = customId;
                customWeapon.enabled = true;

                customWeapon.aName =
                    new LocalizedString(DisplayNameKey);

                // 直接引用原版预制体资产，不 Instantiate 游戏物体。
                customWeapon.mainWeaponPrefab =
                    template.mainWeaponPrefab;

                customWeapon.subWeaponPrefab =
                    template.subWeaponPrefab;

                customWeapon.icon = template.icon;
                customWeapon.bigIcon = template.bigIcon;

                customWeapon.activeState =
                    WeaponEntity.EActiveState.Active;

                customWeapon.isDefaultWeapon = false;
                customWeapon.enhanceFromId = -1;
                customWeapon.unlockMiniscript =
                    string.Empty;

                // 不继承原版武器自身的强化子项。
                customWeapon.standardEnhancements =
                    new List<EnhancementMetadata>();

                WeaponDatabase.Register(customWeapon);

                // 确认数据库确实保存了这份武器数据。
                WeaponEntity check =
                    WeaponDatabase.FindWeaponById(customId);

                if (check != customWeapon)
                {
                    Debug.LogError(
                        "[脑叶武器扩展] 自定义武器未能写入数据库，ID=" +
                        customId);

                    return;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展] 创建或注册武器数据失败：" +
                    exception);

                return;
            }

            // 建立标准剑盾的强化关联。
            if (enhancementParent.standardEnhancements == null)
            {
                enhancementParent.standardEnhancements =
                    new List<EnhancementMetadata>();
            }

            bool alreadyLinked =
                enhancementParent.standardEnhancements.Any(
                    enhancement =>
                        enhancement != null &&
                        enhancement.enhanced != null &&
                        enhancement.enhanced.id == customId);

            if (!alreadyLinked)
            {
                enhancementParent.standardEnhancements.Add(
                    new EnhancementMetadata
                    {
                        enabled = true,
                        enhanced = customWeapon
                    });
            }

            RegisteredWeaponId = customId;
            registered = true;

            Debug.Log(
                "[脑叶武器扩展] 赤瞳&忏悔注册完成。" +
                " WeaponId=" + customId +
                "，TemplateId=" + PrefabTemplateWeaponId +
                "，EnhancementParentId=" + EnhancementParentWeaponId +
                "，MainPrefab=" +
                customWeapon.mainWeaponPrefab.name +
                "，使用原版网络预制体，不创建额外实例。");
        }

        private static int FindUnusedWeaponId(
            List<WeaponEntity> weapons)
        {
            HashSet<int> usedIds = new HashSet<int>(
                weapons
                    .Where(weapon => weapon != null)
                    .Select(weapon => weapon.id));

            int candidate = FirstCustomWeaponId;

            while (usedIds.Contains(candidate))
            {
                if (candidate == int.MaxValue)
                {
                    throw new InvalidOperationException(
                        "找不到可用的自定义武器 ID。");
                }

                candidate++;
            }

            return candidate;
        }
    }
}