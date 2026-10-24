using System;
using HarmonyLib;
using UnityEngine;
using System.Linq;

[HarmonyPatch(
    typeof(UI_WeaponEnhancementButton),
    nameof(UI_WeaponEnhancementButton.SetWeaponMethod))]
internal static class RedEyeConfessionEnhancementButtonPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        UI_WeaponEnhancementButton __instance,
        WeaponSimple currentWeapon,
        EnhancementMetadata enhancementMetadata)
    {
        if (__instance == null || __instance.effectText == null)
        {
            return;
        }

        int registeredId = RedEyeConfessionWeapon.RegisteredWeaponId;

        bool isCurrentWeapon =
            currentWeapon != null &&
            currentWeapon.entityId == registeredId;

        bool isEnhancedWeapon =
            enhancementMetadata != null &&
            enhancementMetadata.enhanced != null &&
            enhancementMetadata.enhanced.id == registeredId;

        // 只处理「赤瞳&忏悔」，不影响其他武器。
        if (!isCurrentWeapon && !isEnhancedWeapon)
        {
            return;
        }

        string tierText = GetLocalizedDescription(
            RedEyeConfessionWeapon.Tier1TextKey);

        string flavorText = GetLocalizedDescription(
            RedEyeConfessionWeapon.FlavorTextKey);

        string customText = string.Join(
            "\n",
            new[] { tierText, flavorText }
                .Where(text => !string.IsNullOrWhiteSpace(text)));

        if (string.IsNullOrWhiteSpace(customText))
        {
            Debug.LogWarning(
                "[脑叶武器扩展] 赤瞳&忏悔的铁砧自定义描述为空。");
            return;
        }

        string originalText = __instance.effectText.text?.Trim();

        // 保留原版显示的强化效果，并补上自定义描述。
        if (string.IsNullOrWhiteSpace(originalText))
        {
            __instance.effectText.text = customText;
        }
        else if (!originalText.Contains(tierText) &&
                 !originalText.Contains(flavorText))
        {
            __instance.effectText.text =
                originalText + "\n" + customText;
        }
    }

    private static string GetLocalizedDescription(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        string localizedText = new LocalizedString(key).ToString();

        return KeywordDatabase.Convert(
            Loc.Convert(localizedText),
            useColor: true,
            useSprite: true,
            useDungeonManager: true);
    }
}