using System;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace LobCorpWeaponExpansion
{
    [HarmonyPatch(
        typeof(UI_WeaponEnhancementButton),
        nameof(UI_WeaponEnhancementButton.SetWeaponMethod))]
    internal static class RedEyeConfessionEnhancementButtonPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            UI_WeaponEnhancementButton __instance,
            EnhancementMetadata enhancementMetadata)
        {
            if (__instance == null ||
                __instance.effectText == null ||
                enhancementMetadata == null ||
                enhancementMetadata.enhanced == null)
            {
                return;
            }

            // 使用武器注册时从 manifest.json 读取的实际 ID。
            int registeredId =
                RedEyeConfessionWeapon.RegisteredWeaponId;

            if (registeredId < 0 ||
                enhancementMetadata.enhanced.id != registeredId)
            {
                return;
            }

            string tierKey =
                RedEyeConfessionWeapon.Tier1TextKey;

            string flavorKey =
                RedEyeConfessionWeapon.FlavorTextKey;

            // 读取本地化文本，并转换其中的 <tag=...> 标签。
            string tierText =
                GetLocalizedDescription(tierKey);

            string flavorText =
                GetLocalizedDescription(flavorKey);

            bool hasTier =
                !string.IsNullOrWhiteSpace(tierText);

            bool hasFlavor =
                !string.IsNullOrWhiteSpace(flavorText);

            if (!hasTier && !hasFlavor)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][铁砧] 赤瞳&忏悔的自定义文本无效。" +
                    " WeaponId=" + registeredId +
                    "，Tier1Key=" + tierKey +
                    "，FlavorKey=" + flavorKey);

                return;
            }

            string originalText =
                __instance.effectText.text?.Trim() ?? string.Empty;

            StringBuilder builder =
                new StringBuilder(originalText);

            // 保留原版已经生成的新增强化效果。
            // 仅补充尚未显示的自定义文本，避免重复。
            if (hasTier && !originalText.Contains(tierText))
            {
                AppendText(builder, tierText);
            }

            string currentText = builder.ToString();

            if (hasFlavor && !currentText.Contains(flavorText))
            {
                AppendText(builder, flavorText);
            }

            __instance.effectText.text = builder.ToString();

            Debug.Log(
                "[脑叶武器扩展][铁砧] 已处理赤瞳&忏悔强化按钮。" +
                " WeaponId=" + registeredId +
                "，Tier1=" + hasTier +
                "，Flavor=" + hasFlavor);
        }

        private static string GetLocalizedDescription(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            string text = new LocalizedString(key).ToString();

            // 本地化未命中时，LocalizedString 可能直接返回键名。
            if (string.IsNullOrWhiteSpace(text) ||
                text == key)
            {
                return string.Empty;
            }

            // 必须在写入 TextMeshPro 前转换关键词标签。
            return ConvertKeywordTags(text);
        }

        private static string ConvertKeywordTags(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            try
            {
                // 与 Star's Sephiria Mod 的武器说明处理方式一致：
                // 连续转换两次，以处理转换过程中产生的嵌套关键词。
                string converted = KeywordDatabase.Convert(
                    KeywordDatabase.Convert(text));

                return converted ?? text;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展][铁砧] 关键词文本转换失败：" +
                    exception);

                // 转换失败时保留原文本，避免整段描述丢失。
                return text;
            }
        }

        private static void AppendText(
            StringBuilder builder,
            string text)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            builder.Append(text);
        }
    }
}