using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LobCorpWeaponExpansion
{
    internal static class RedEyeConfessionSpriteAssets
    {
        private static Sprite mainHammerSprite;
        private static Sprite offhandHammerSprite;
        private static Sprite iconSprite;

        private static bool mainLoadAttempted;
        private static bool offhandLoadAttempted;
        private static bool iconLoadAttempted;

        /// <summary>
        /// 替换基础版与强化版的主、副手武器外观。
        /// </summary>
        internal static void ApplyWeaponSprites(
            WeaponSimple_SwordAndShield weapon)
        {
            if (weapon == null
                || RedEyeConfessionWeapon.RegisteredWeaponId < 0
                || !RedEyeConfessionWeapon.IsManagedWeaponId(
                    weapon.entityId))
            {
                return;
            }

            try
            {
                // 主手：剑 -> 锤子。
                NewWeaponBody mainBody = weapon.mainWeaponBody;

                if (mainBody == null
                    || mainBody.weaponSpriteRenderer == null)
                {
                    Debug.LogWarning(
                        "[脑叶武器扩展][贴图] 找不到主手 SpriteRenderer。");
                }
                else
                {
                    SpriteRenderer renderer =
                        mainBody.weaponSpriteRenderer;

                    Sprite replacement = LoadSprite(
                        "mainHammer",
                        "主手锤子",
                        renderer.sprite,
                        ref mainHammerSprite,
                        ref mainLoadAttempted);

                    if (replacement != null)
                    {
                        renderer.sprite = replacement;

                        // 遮罩使用相同轮廓，避免残留剑的形状。
                        if (mainBody.weaponStencilRenderer != null)
                        {
                            mainBody.weaponStencilRenderer.sprite =
                                replacement;
                        }

                        Debug.Log(
                            "[脑叶武器扩展][贴图] 已替换主手锤子。");
                    }
                }

                // 副手：盾牌 -> 第二把锤子。
                SubWeapon offhand = weapon.shieldBody;

                if (offhand == null
                    || offhand.weaponSpriteRenderer == null)
                {
                    Debug.LogWarning(
                        "[脑叶武器扩展][贴图] 找不到副手盾牌的 SpriteRenderer。");
                }
                else
                {
                    SpriteRenderer renderer =
                        offhand.weaponSpriteRenderer;

                    Sprite replacement = LoadSprite(
                        "offhandHammer",
                        "副手锤子",
                        renderer.sprite,
                        ref offhandHammerSprite,
                        ref offhandLoadAttempted);

                    if (replacement != null)
                    {
                        renderer.sprite = replacement;

                        if (offhand.weaponStencilRenderer != null)
                        {
                            offhand.weaponStencilRenderer.sprite =
                                replacement;
                        }

                        // 叠加层也采用锤子的轮廓。
                        if (offhand.weaponAddon != null)
                        {
                            offhand.weaponAddon.sprite = replacement;
                        }

                        Debug.Log(
                            "[脑叶武器扩展][贴图] 已替换副手锤子。");
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展][贴图] 双锤外观替换失败："
                    + exception);
            }
        }

        /// <summary>
        /// 替换武器数据库中基础版与强化版的物品图标。
        /// 强化版使用基础版替换完成后的图标。
        /// </summary>
        internal static void ApplyWeaponIcon()
        {
            int targetId =
                RedEyeConfessionWeapon.RegisteredWeaponId;

            if (targetId < 0)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][图标] 尚未获得自定义武器 ID。");

                return;
            }

            try
            {
                WeaponEntity entity =
                    WeaponDatabase.FindWeaponById(targetId);

                if (entity == null)
                {
                    foreach (WeaponEntity candidate in
                             Resources.FindObjectsOfTypeAll<WeaponEntity>())
                    {
                        if (candidate != null
                            && candidate.id == targetId)
                        {
                            entity = candidate;
                            break;
                        }
                    }
                }

                if (entity == null)
                {
                    Debug.LogWarning(
                        $"[脑叶武器扩展][图标] 找不到 ID={targetId} 的 WeaponEntity。");

                    return;
                }

                List<MemberInfo> members =
                    FindIconMembers(entity.GetType());

                if (members.Count == 0)
                {
                    Debug.LogWarning(
                        "[脑叶武器扩展][图标] "
                        + "WeaponEntity 中没有找到可写入的 Sprite/Texture2D 图标成员。"
                        + $" 类型={entity.GetType().FullName}");

                    DumpPossibleIconMembers(entity.GetType());
                    return;
                }

                // 获取原始图标，作为新图标的 PPU 与轴心参考。
                Sprite originalIcon = null;

                foreach (MemberInfo member in members)
                {
                    if (GetMemberType(member) != typeof(Sprite))
                    {
                        continue;
                    }

                    originalIcon =
                        GetMemberValue(member, entity) as Sprite;

                    if (originalIcon != null)
                    {
                        break;
                    }
                }

                Sprite replacementIcon = LoadSprite(
                    "icon",
                    "红眼忏悔物品图标",
                    originalIcon,
                    ref iconSprite,
                    ref iconLoadAttempted);

                if (replacementIcon == null)
                {
                    return;
                }

                int appliedCount = 0;

                foreach (MemberInfo member in members)
                {
                    try
                    {
                        Type memberType = GetMemberType(member);

                        if (memberType == typeof(Sprite))
                        {
                            SetMemberValue(
                                member,
                                entity,
                                replacementIcon);
                        }
                        else if (memberType == typeof(Texture2D))
                        {
                            SetMemberValue(
                                member,
                                entity,
                                replacementIcon.texture);
                        }
                        else
                        {
                            continue;
                        }

                        appliedCount++;

                        Debug.Log(
                            "[脑叶武器扩展][图标] 已替换成员："
                            + member.Name);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(
                            "[脑叶武器扩展][图标] 无法替换成员 "
                            + member.Name
                            + "："
                            + exception.Message);
                    }
                }

                if (appliedCount <= 0)
                {
                    Debug.LogWarning(
                        "[脑叶武器扩展][图标] 没有成功替换任何图标成员。");

                    return;
                }

                Debug.Log(
                    $"[脑叶武器扩展][图标] 已向 {appliedCount} 个图标成员写入资源，"
                    + $"WeaponId={targetId}。");

                // 基础版图标替换完成后，再同步到强化版。
                // 避免在替换之前复制到原版图标。
                int tier2Id =
                    RedEyeConfessionWeapon.RegisteredTier2WeaponId;

                if (tier2Id >= 0)
                {
                    WeaponEntity tier2Entity =
                        WeaponDatabase.FindWeaponById(tier2Id);

                    if (tier2Entity != null)
                    {
                        tier2Entity.icon = entity.icon;
                        tier2Entity.bigIcon = entity.bigIcon;

                        Debug.Log(
                            "[脑叶武器扩展][图标] 已同步强化版图标。");
                    }
                    else
                    {
                        Debug.LogWarning(
                            "[脑叶武器扩展][图标] "
                            + $"找不到强化版 WeaponEntity，ID={tier2Id}。");
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展][图标] 图标替换失败："
                    + exception);
            }
        }

        /// <summary>
        /// 从配置目录加载 PNG，并缓存 Sprite。
        /// </summary>
        private static Sprite LoadSprite(
            string artKey,
            string description,
            Sprite originalSprite,
            ref Sprite cachedSprite,
            ref bool loadAttempted)
        {
            if (cachedSprite != null)
            {
                return cachedSprite;
            }

            if (loadAttempted)
            {
                return null;
            }

            loadAttempted = true;

            try
            {
                if (!RedEyeConfessionData.TryGetArtPath(
                        artKey,
                        out string artPath))
                {
                    Debug.LogError(
                        "[脑叶武器扩展][贴图] 无法解析贴图路径："
                        + artKey);

                    return null;
                }

                byte[] bytes = File.ReadAllBytes(artPath);

                Texture2D texture = new Texture2D(
                    2,
                    2,
                    TextureFormat.RGBA32,
                    false);

                if (!texture.LoadImage(bytes))
                {
                    UnityEngine.Object.Destroy(texture);

                    Debug.LogError(
                        "[脑叶武器扩展][贴图] PNG 解码失败："
                        + description);

                    return null;
                }

                texture.name = description;
                texture.wrapMode = TextureWrapMode.Clamp;

                texture.filterMode =
                    originalSprite != null
                    && originalSprite.texture != null
                        ? originalSprite.texture.filterMode
                        : FilterMode.Point;

                float pixelsPerUnit =
                    originalSprite != null
                        ? originalSprite.pixelsPerUnit
                        : 100f;

                Vector2 pivot = new Vector2(0.5f, 0.5f);

                if (originalSprite != null
                    && originalSprite.rect.width > 0f
                    && originalSprite.rect.height > 0f)
                {
                    pivot = new Vector2(
                        originalSprite.pivot.x
                            / originalSprite.rect.width,
                        originalSprite.pivot.y
                            / originalSprite.rect.height);
                }

                cachedSprite = Sprite.Create(
                    texture,
                    new Rect(
                        0f,
                        0f,
                        texture.width,
                        texture.height),
                    pivot,
                    pixelsPerUnit);

                cachedSprite.name = description;

                Debug.Log(
                    "[脑叶武器扩展][贴图] 已从配置目录加载 "
                    + description
                    + "："
                    + artPath
                    + "，"
                    + texture.width
                    + "x"
                    + texture.height);

                return cachedSprite;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展][贴图] 加载 "
                    + description
                    + " 失败："
                    + exception);

                return null;
            }
        }

        /// <summary>
        /// 查找 WeaponEntity 中名称包含 icon 的可写 Sprite 或 Texture2D 成员。
        /// </summary>
        private static List<MemberInfo> FindIconMembers(Type type)
        {
            var result = new List<MemberInfo>();

            for (Type current = type;
                 current != null;
                 current = current.BaseType)
            {
                const BindingFlags flags =
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly;

                foreach (FieldInfo field in current.GetFields(flags))
                {
                    if (field.IsInitOnly || field.IsLiteral)
                    {
                        continue;
                    }

                    if (field.Name.Contains("k__BackingField"))
                    {
                        continue;
                    }

                    if (field.Name.IndexOf(
                            "icon",
                            StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    if (field.FieldType == typeof(Sprite)
                        || field.FieldType == typeof(Texture2D))
                    {
                        result.Add(field);
                    }
                }

                foreach (PropertyInfo property in
                         current.GetProperties(flags))
                {
                    if (!property.CanWrite
                        || property.GetIndexParameters().Length != 0)
                    {
                        continue;
                    }

                    if (property.Name.IndexOf(
                            "icon",
                            StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    if (property.PropertyType == typeof(Sprite)
                        || property.PropertyType == typeof(Texture2D))
                    {
                        result.Add(property);
                    }
                }
            }

            return result;
        }

        private static Type GetMemberType(MemberInfo member)
        {
            if (member is FieldInfo field)
            {
                return field.FieldType;
            }

            if (member is PropertyInfo property)
            {
                return property.PropertyType;
            }

            return null;
        }

        private static object GetMemberValue(
            MemberInfo member,
            object target)
        {
            if (member is FieldInfo field)
            {
                return field.GetValue(target);
            }

            if (member is PropertyInfo property)
            {
                return property.GetValue(target);
            }

            return null;
        }

        private static void SetMemberValue(
            MemberInfo member,
            object target,
            object value)
        {
            if (member is FieldInfo field)
            {
                field.SetValue(target, value);
                return;
            }

            if (member is PropertyInfo property)
            {
                property.SetValue(target, value);
            }
        }

        private static void DumpPossibleIconMembers(Type type)
        {
            const BindingFlags flags =
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic;

            foreach (FieldInfo field in type.GetFields(flags))
            {
                if (field.Name.IndexOf(
                        "icon",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.Log(
                        $"[脑叶武器扩展][图标诊断] 字段={field.Name}，"
                        + $"类型={field.FieldType.FullName}");
                }
            }

            foreach (PropertyInfo property in type.GetProperties(flags))
            {
                if (property.Name.IndexOf(
                        "icon",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.Log(
                        $"[脑叶武器扩展][图标诊断] 属性={property.Name}，"
                        + $"类型={property.PropertyType.FullName}");
                }
            }
        }
    }

    /// <summary>
    /// 武器初始化完成后替换双锤外观。
    /// </summary>
    [HarmonyPatch(
        typeof(WeaponSimple_SwordAndShield),
        "OnInitialized"
    )]
    internal static class RedEyeConfessionSpritePatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            WeaponSimple_SwordAndShield __instance,
            WeaponControllerSimple controller)
        {
            if (__instance == null || controller == null)
            {
                return;
            }

            RedEyeConfessionSpriteAssets.ApplyWeaponSprites(
                __instance);
        }
    }
}