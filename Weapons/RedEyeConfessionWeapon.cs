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

        internal static string Tier1TextKey =>
            RedEyeConfessionData.GetTextKey("Tier1");

        // 原版标准剑盾。
        private const int PrefabTemplateWeaponId = 0;

        // 标准剑盾强化路线。
        private const int EnhancementParentWeaponId = 0;

        private static bool registered;

        internal static int RegisteredWeaponId { get; private set; } = -1;
        internal const int Tier2WeaponId = 372467;

        internal static int RegisteredTier2WeaponId { get; private set; } = -1;

        internal static string Tier2NameKey =>
            RedEyeConfessionData.GetTextKey("Tier2Name");

        internal static string Tier2TextKey =>
            RedEyeConfessionData.GetTextKey("Tier2");

        internal static bool IsTier2WeaponId(int id)
        {
            return RegisteredTier2WeaponId >= 0
                && id == RegisteredTier2WeaponId;
        }

        internal static bool IsManagedWeaponId(int id)
        {
            return id >= 0
                && (id == RegisteredWeaponId
                    || id == RegisteredTier2WeaponId);
        }

        internal static bool IsManagedWeapon(WeaponSimple weapon)
        {
            return weapon != null
                && IsManagedWeaponId(weapon.entityId);
        }
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
            {
                if (RegisteredWeaponId >= 0)
                {
                    RegisterTier2(
                        WeaponDatabase.FindWeaponById(
                            RegisteredWeaponId));
                }

                return;
            }

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
                RegisterTier2(existingWeapon);
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
            RegisterTier2(customWeapon);
            Debug.Log(
                "[脑叶武器扩展] 赤瞳&忏悔注册完成。" +
                " WeaponId=" + customId +
                "，TemplateId=" + PrefabTemplateWeaponId +
                "，EnhancementParentId=" + EnhancementParentWeaponId +
                "，MainPrefab=" +
                customWeapon.mainWeaponPrefab.name +
                "，使用原版网络预制体，不创建额外实例。");
        }
        private static void RegisterTier2(WeaponEntity baseWeapon)
        {
            if (baseWeapon == null
                || baseWeapon.id != RegisteredWeaponId)
            {
                return;
            }

            try
            {
                const string tier2Suffix =
                    "_" + InternalName + "_Tier2";

                WeaponEntity tier2 =
                    WeaponDatabase.FindWeaponById(Tier2WeaponId);

                // 防止误覆盖其他模组或游戏原版的武器。
                if (tier2 != null
                    && (string.IsNullOrEmpty(tier2.name)
                        || !tier2.name.EndsWith(
                            tier2Suffix,
                            StringComparison.Ordinal)))
                {
                    Debug.LogError(
                        "[脑叶武器扩展] 第二阶段武器 ID 已被占用。" +
                        " ID=" + Tier2WeaponId +
                        "，占用者=" + tier2.name);

                    return;
                }

                if (tier2 == null)
                {
                    // 继承基础版武器数据和网络预制体引用。
                    tier2 = UnityEngine.Object.Instantiate(baseWeapon);

                    tier2.name =
                        Tier2WeaponId + tier2Suffix;

                    tier2.id = Tier2WeaponId;
                    tier2.enabled = true;

                    tier2.aName =
                        new LocalizedString(Tier2NameKey);

                    tier2.mainWeaponPrefab =
                        baseWeapon.mainWeaponPrefab;

                    tier2.subWeaponPrefab =
                        baseWeapon.subWeaponPrefab;

                    tier2.icon = baseWeapon.icon;
                    tier2.bigIcon = baseWeapon.bigIcon;

                    tier2.activeState =
                        WeaponEntity.EActiveState.Active;

                    tier2.isDefaultWeapon = false;
                    tier2.enhanceFromId = baseWeapon.id;
                    tier2.unlockMiniscript = string.Empty;

                    // 第二阶段的强化列表独立于基础版。
                    tier2.standardEnhancements =
                        new List<EnhancementMetadata>();

                    WeaponDatabase.Register(tier2);

                    tier2 =
                        WeaponDatabase.FindWeaponById(Tier2WeaponId);

                    if (tier2 == null)
                    {
                        Debug.LogError(
                            "[脑叶武器扩展] 第二阶段武器注册失败。" +
                            " ID=" + Tier2WeaponId);

                        return;
                    }
                }

                tier2.aName =
                    new LocalizedString(Tier2NameKey);

                tier2.enhanceFromId = baseWeapon.id;

                if (baseWeapon.standardEnhancements == null)
                {
                    baseWeapon.standardEnhancements =
                        new List<EnhancementMetadata>();
                }

                bool alreadyLinked =
                    baseWeapon.standardEnhancements.Any(
                        enhancement =>
                            enhancement != null
                            && enhancement.enhanced != null
                            && enhancement.enhanced.id == Tier2WeaponId);

                if (!alreadyLinked)
                {
                    baseWeapon.standardEnhancements.Add(
                        new EnhancementMetadata
                        {
                            enabled = true,
                            enhanced = tier2
                        });
                }

                RegisteredTier2WeaponId = tier2.id;

                Debug.Log(
                    "[脑叶武器扩展] 第二阶段武器注册完成。" +
                    " WeaponId=" + RegisteredTier2WeaponId +
                    "，EnhanceFromId=" + baseWeapon.id);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展] 注册第二阶段武器失败：" +
                    exception);
            }
        }
    }
}