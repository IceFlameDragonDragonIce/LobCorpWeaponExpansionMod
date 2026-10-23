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

        internal static string DisplayNameKey =>
            RedEyeConfessionData.GetTextKey("Name");

        internal static string FlavorTextKey =>
            RedEyeConfessionData.GetTextKey("Flavor");

        // 原版标准剑盾。
        private const int PrefabTemplateWeaponId = 0;

        // 标准剑盾强化路线。
        private const int EnhancementParentWeaponId = 0;

        private static bool registered;

        internal static int RegisteredWeaponId { get; private set; } = -1;

        internal static void RegisterLocalization(
    HorayModLocalizationContext context)
        {
            if (context == null)
                return;

            // 优先读取 Weapons/372466/texts.json。
            if (RedEyeConfessionData.RegisterTexts(context))
                return;

            // JSON 不可用时，保留原有翻译作为回退。
            string chineseName = "赤瞳&忏悔";

            if (RedEyeConfessionData.TryLoad(
                    out RedEyeConfessionManifest manifest))
            {
                chineseName = manifest.displayName;
            }

            context.AddText(
                "zh-CN",
                DisplayNameKey,
                chineseName);

            context.AddText(
                "en-US",
                DisplayNameKey,
                "Red Eye & Confession");

            context.AddText(
                "ja-JP",
                DisplayNameKey,
                "赤瞳と懺悔");

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

            if (!RedEyeConfessionData.TryLoad(
                    out RedEyeConfessionManifest manifest))
            {
                Debug.LogError(
                    "[脑叶武器扩展] 无法读取武器配置，取消注册。");
                return;
            }

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
                if (existingWeapon.id != manifest.entityId)
                {
                    Debug.LogError(
                        "[脑叶武器扩展] 已存在同名武器，但 ID 与 manifest.json 不一致。" +
                        " 当前 ID=" + existingWeapon.id +
                        "，配置 ID=" + manifest.entityId);

                    return;
                }

                RegisteredWeaponId = existingWeapon.id;

                Debug.LogWarning(
                    "[脑叶武器扩展] 数据库中已经存在赤瞳&忏悔，" +
                    "跳过重复注册。实际 ID=" + RegisteredWeaponId);

                registered = true;
                return;
            }

            // 检查预定 ID 是否被其他武器占用。
            WeaponEntity idConflict = weapons.FirstOrDefault(
                weapon =>
                    weapon != null &&
                    weapon.id == manifest.entityId);

            if (idConflict != null)
            {
                Debug.LogError(
                    "[脑叶武器扩展] 武器 ID 已被其他武器占用，取消注册。" +
                    " ID=" + manifest.entityId +
                    "，占用者=" + idConflict.name);

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

            int customId = manifest.entityId;

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
    }
}