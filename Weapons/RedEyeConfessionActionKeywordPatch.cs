using System;
using UnityEngine;

namespace LobCorpWeaponExpansion
{
    /// <summary>
    /// 为赤瞳&忏悔注册原生招式关键词。
    /// 通过 <tag=关键词ID> 触发游戏自己的悬停详情。
    /// </summary>
    internal static class RedEyeConfessionActionKeywordPatch
    {
        internal const string Keyword =
            "WeaponAction_LobCorpWeaponExpansion_RedEyeConfession";

        private const string NameKey =
            "LobCorpWeaponExpansion_RedEyeConfession_BasicAttack_Name";

        private const string DescriptionKey =
            "LobCorpWeaponExpansion_RedEyeConfession_BasicAttack_Description";

        internal static void RegisterLocalization(
            HorayModLocalizationContext context)
        {
            if (context == null)
                return;

            context.AddText(
                "zh-CN",
                NameKey,
                "普通攻击：赤瞳&忏悔");

            context.AddText(
                "zh-TW",
                NameKey,
                "普通攻擊：赤瞳&懺悔");

            context.AddText(
                "en-US",
                NameKey,
                "Basic Attack: Red Eye & Confession");

            context.AddText(
                "ja-JP",
                NameKey,
                "通常攻撃：赤瞳と懺悔");

            context.AddText(
                "zh-CN",
                DescriptionKey,
                "挥舞武器造成3次攻击\n第3次武器攻击是2连击");

            context.AddText(
                "zh-TW",
                DescriptionKey,
                "揮舞武器造成3次攻擊\n第3次武器攻擊是2連擊");

            context.AddText(
                "en-US",
                DescriptionKey,
                "Swing the weapon for 3 attacks\nThe third attack hits twice");

            context.AddText(
                "ja-JP",
                DescriptionKey,
                "武器を振るって3回攻撃する\n3段目の攻撃は2連撃になる");
        }

        internal static void RegisterKeyword()
        {
            KeywordEntity entity =
                KeywordDatabase.GetEntity(Keyword);

            bool isNew = entity == null;

            if (isNew)
            {
                entity =
                    ScriptableObject.CreateInstance<KeywordEntity>();
            }

            entity.name = Keyword;
            entity.keyword = Keyword;

            entity.visualText =
                new LocalizedString(NameKey);

            entity.description =
                new LocalizedString(DescriptionKey);

            entity.displayDetails = true;

            // 复用原版普通攻击的颜色与图标，
            // 使自定义招式的视觉效果与其他招式一致。
            KeywordEntity basicAttack =
                KeywordDatabase.GetEntity(
                    "WeaponAction_BasicAttack");

            if (basicAttack != null)
            {
                entity.textColor = basicAttack.textColor;
                entity.keywordImage = basicAttack.keywordImage;
            }

            if (isNew)
            {
                KeywordDatabase.Register(entity);
            }

            Debug.Log(
                "[脑叶武器扩展][招式详情] 关键词注册完成。" +
                " Keyword=" + Keyword +
                "，DisplayDetails=" + entity.displayDetails);
        }
    }
}