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
        private const string MainResource =
            "LobCorpWeaponExpansion.Textures.RedEyeConfession_MainHammer.png";

        private const string OffhandResource =
            "LobCorpWeaponExpansion.Textures.RedEyeConfession_OffhandHammer.png";

        private const string IconResource =
            "LobCorpWeaponExpansion.Textures.RedEyeConfession_Icon.png";

        private static Sprite mainHammerSprite;
        private static Sprite offhandHammerSprite;
        private static Sprite iconSprite;

        private static bool mainLoadAttempted;
        private static bool offhandLoadAttempted;
        private static bool iconLoadAttempted;

        /// <summary>
        /// 替换游戏内主、副手的武器外观。
        /// 通过装备初始化钩子调用。
        /// </summary>
        internal static void ApplyWeaponSprites(
            WeaponSimple_SwordAndShield weapon)
        {
            if (weapon == null)
                return;

            int targetId = RedEyeConfessionWeapon.RegisteredWeaponId;

            if (targetId < 0 || weapon.entityId != targetId)
                return;

            try
            {
                // 主手：剑 -> 锤子。
                NewWeaponBody mainBody = weapon.mainWeaponBody;

                if (mainBody == null ||
                    mainBody.weaponSpriteRenderer == null)
                {
                    Debug.LogWarning(
                        "[脑叶武器扩展][贴图] 找不到主手 SpriteRenderer。"
                    );
                }
                else
                {
                    SpriteRenderer renderer =
                        mainBody.weaponSpriteRenderer;

                    Sprite replacement = LoadSprite(
                        MainResource,
                        "主手锤子",
                        renderer.sprite,
                        ref mainHammerSprite,
                        ref mainLoadAttempted
                    );

                    if (replacement != null)
                    {
                        renderer.sprite = replacement;

                        // 遮罩改成相同轮廓，避免残留剑的形状。
                        if (mainBody.weaponStencilRenderer != null)
                        {
                            mainBody.weaponStencilRenderer.sprite =
                                replacement;
                        }

                        Debug.Log(
                            "[脑叶武器扩展][贴图] 已替换主手锤子。"
                        );
                    }
                }

                // 副手：盾牌 -> 第二把锤子。
                SubWeapon offhand = weapon.shieldBody;

                if (offhand == null ||
                    offhand.weaponSpriteRenderer == null)
                {
                    Debug.LogWarning(
                        "[脑叶武器扩展][贴图] 找不到副手盾牌的 SpriteRenderer。"
                    );
                }
                else
                {
                    SpriteRenderer renderer =
                        offhand.weaponSpriteRenderer;

                    Sprite replacement = LoadSprite(
                        OffhandResource,
                        "副手锤子",
                        renderer.sprite,
                        ref offhandHammerSprite,
                        ref offhandLoadAttempted
                    );

                    if (replacement != null)
                    {
                        renderer.sprite = replacement;

                        if (offhand.weaponStencilRenderer != null)
                        {
                            offhand.weaponStencilRenderer.sprite =
                                replacement;
                        }

                        // 叠加层也采用锤子的轮廓。
                        // 这可避免盾牌外形残留在格挡特效中。
                        if (offhand.weaponAddon != null)
                        {
                            offhand.weaponAddon.sprite = replacement;
                        }

                        Debug.Log(
                            "[脑叶武器扩展][贴图] 已替换副手锤子。"
                        );
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展][贴图] 双锤外观替换失败：" +
                    exception
                );
            }
        }

        /// <summary>
        /// 替换自定义 WeaponEntity 中可识别的图标字段。
        /// 在武器数据库注册完成后调用。
        /// </summary>
        internal static void ApplyWeaponIcon()
        {
            int targetId = RedEyeConfessionWeapon.RegisteredWeaponId;

            if (targetId < 0)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][图标] 尚未获得自定义武器 ID。"
                );
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
                        if (candidate != null &&
                            candidate.id == targetId)
                        {
                            entity = candidate;
                            break;
                        }
                    }
                }

                if (entity == null)
                {
                    Debug.LogWarning(
                        $"[脑叶武器扩展][图标] 找不到 ID={targetId} 的 WeaponEntity。"
                    );
                    return;
                }

                List<MemberInfo> members =
                    FindIconMembers(entity.GetType());

                if (members.Count == 0)
                {
                    Debug.LogWarning(
                        "[脑叶武器扩展][图标] " +
                        "WeaponEntity 中没有找到可写入的 Sprite/Texture2D 图标成员。" +
                        $" 类型={entity.GetType().FullName}"
                    );

                    DumpPossibleIconMembers(entity.GetType());
                    return;
                }

                // 尽量使用原始图标的 PPU 与轴心作为参考。
                Sprite originalIcon = null;

                foreach (MemberInfo member in members)
                {
                    if (GetMemberType(member) != typeof(Sprite))
                        continue;

                    originalIcon = GetMemberValue(member, entity) as Sprite;

                    if (originalIcon != null)
                        break;
                }

                Sprite replacementIcon = LoadSprite(
                    IconResource,
                    "红眼忏悔物品图标",
                    originalIcon,
                    ref iconSprite,
                    ref iconLoadAttempted
                );

                if (replacementIcon == null)
                    return;

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
                                replacementIcon
                            );
                        }
                        else if (memberType == typeof(Texture2D))
                        {
                            SetMemberValue(
                                member,
                                entity,
                                replacementIcon.texture
                            );
                        }
                        else
                        {
                            continue;
                        }

                        appliedCount++;

                        Debug.Log(
                            "[脑叶武器扩展][图标] 已替换成员：" +
                            member.Name
                        );
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(
                            "[脑叶武器扩展][图标] 无法替换成员 " +
                            member.Name + "：" + exception.Message
                        );
                    }
                }

                if (appliedCount > 0)
                {
                    Debug.Log(
                        $"[脑叶武器扩展][图标] 已向 {appliedCount} 个图标成员写入资源，" +
                        $"WeaponId={targetId}。"
                    );
                }
                else
                {
                    Debug.LogWarning(
                        "[脑叶武器扩展][图标] 没有成功替换任何图标成员。"
                    );
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展][图标] 图标替换失败：" +
                    exception
                );
            }
        }

        private static Sprite LoadSprite(
            string resourceName,
            string description,
            Sprite originalSprite,
            ref Sprite cachedSprite,
            ref bool loadAttempted)
        {
            if (cachedSprite != null)
                return cachedSprite;

            if (loadAttempted)
                return null;

            loadAttempted = true;

            Assembly assembly =
                typeof(LobCorpWeaponExpansionMod).Assembly;

            using (Stream stream =
                   assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Debug.LogError(
                        $"[脑叶武器扩展][贴图] 找不到嵌入资源：{resourceName}\n" +
                        "请检查 PNG 文件路径和 csproj 中的 EmbeddedResource 配置。\n" +
                        "当前嵌入资源：" +
                        string.Join(", ", assembly.GetManifestResourceNames())
                    );

                    return null;
                }

                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    byte[] bytes = memory.ToArray();

                    var texture = new Texture2D(
                        2,
                        2,
                        TextureFormat.RGBA32,
                        false
                    );

                    if (!texture.LoadImage(bytes))
                    {
                        UnityEngine.Object.Destroy(texture);

                        Debug.LogError(
                            $"[脑叶武器扩展][贴图] PNG 解码失败：{description}"
                        );

                        return null;
                    }

                    texture.name = description;
                    texture.wrapMode = TextureWrapMode.Clamp;

                    texture.filterMode =
                        originalSprite != null &&
                        originalSprite.texture != null
                            ? originalSprite.texture.filterMode
                            : FilterMode.Point;

                    float pixelsPerUnit =
                        originalSprite != null
                            ? originalSprite.pixelsPerUnit
                            : 100f;

                    Vector2 pivot = new Vector2(0.5f, 0.5f);

                    if (originalSprite != null &&
                        originalSprite.rect.width > 0f &&
                        originalSprite.rect.height > 0f)
                    {
                        pivot = new Vector2(
                            originalSprite.pivot.x /
                                originalSprite.rect.width,
                            originalSprite.pivot.y /
                                originalSprite.rect.height
                        );
                    }

                    cachedSprite = Sprite.Create(
                        texture,
                        new Rect(
                            0f,
                            0f,
                            texture.width,
                            texture.height
                        ),
                        pivot,
                        pixelsPerUnit
                    );

                    cachedSprite.name = description;

                    Debug.Log(
                        $"[脑叶武器扩展][贴图] 已加载 {description}：" +
                        $"{texture.width}x{texture.height}，" +
                        $"PPU={pixelsPerUnit}"
                    );

                    return cachedSprite;
                }
            }
        }

        private static List<MemberInfo> FindIconMembers(Type type)
        {
            var result = new List<MemberInfo>();

            for (Type current = type;
                 current != null;
                 current = current.BaseType)
            {
                const BindingFlags flags =
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly;

                foreach (FieldInfo field in current.GetFields(flags))
                {
                    if (field.IsInitOnly || field.IsLiteral)
                        continue;

                    if (field.Name.Contains("k__BackingField"))
                        continue;

                    if (field.Name.IndexOf(
                            "icon",
                            StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    if (field.FieldType == typeof(Sprite) ||
                        field.FieldType == typeof(Texture2D))
                    {
                        result.Add(field);
                    }
                }

                foreach (PropertyInfo property in current.GetProperties(flags))
                {
                    if (!property.CanWrite ||
                        property.GetIndexParameters().Length != 0)
                    {
                        continue;
                    }

                    if (property.Name.IndexOf(
                            "icon",
                            StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    if (property.PropertyType == typeof(Sprite) ||
                        property.PropertyType == typeof(Texture2D))
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
                return field.FieldType;

            if (member is PropertyInfo property)
                return property.PropertyType;

            return null;
        }

        private static object GetMemberValue(
            MemberInfo member,
            object target)
        {
            if (member is FieldInfo field)
                return field.GetValue(target);

            if (member is PropertyInfo property)
                return property.GetValue(target);

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
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            foreach (FieldInfo field in type.GetFields(flags))
            {
                if (field.Name.IndexOf(
                        "icon",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.Log(
                        $"[脑叶武器扩展][图标诊断] 字段={field.Name}，" +
                        $"类型={field.FieldType.FullName}"
                    );
                }
            }

            foreach (PropertyInfo property in type.GetProperties(flags))
            {
                if (property.Name.IndexOf(
                        "icon",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.Log(
                        $"[脑叶武器扩展][图标诊断] 属性={property.Name}，" +
                        $"类型={property.PropertyType.FullName}"
                    );
                }
            }
        }
    }

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
                return;

            RedEyeConfessionSpriteAssets.ApplyWeaponSprites(__instance);
        }
    }
}