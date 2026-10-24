using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace LobCorpWeaponExpansion
{
    /// <summary>
    /// 在武器详情的 ActionTab 中显示自定义描述。
    /// 优先显示 Tier1，再显示 Flavor。
    /// </summary>
    internal static class RedEyeConfessionFlavorTextPatch
    {
        private const string HarmonyId =
            "LobCorpWeaponExpansion.RedEyeConfessionFlavorText";

        private static Harmony harmony;

        private sealed class TooltipState
        {
            public UI_SimpleTextBox DescriptionBox;
            public RectTransform TooltipRect;
            public RectTransform ContentPanelRect;
            public RectTransform ActionTabRect;

            public Vector2 OriginalTooltipSize;
            public Vector2 OriginalContentPanelSize;
            public Vector2 OriginalActionTabSize;
        }

        private static readonly Dictionary<
            UI_WeaponTooltip,
            TooltipState
        > States = new Dictionary<UI_WeaponTooltip, TooltipState>();

        internal static void Apply()
        {
            if (harmony != null)
                return;

            var openMethod = AccessTools.Method(
                typeof(UI_WeaponTooltip),
                "Open",
                new Type[]
                {
                    typeof(IUITooltipOpener),
                    typeof(RectTransform),
                    typeof(Vector2),
                    typeof(ITooltip)
                });

            if (openMethod == null)
            {
                Debug.LogError(
                    "[脑叶武器扩展] 找不到 UI_WeaponTooltip.Open，" +
                    "描述补丁未启用。");
                return;
            }

            harmony = new Harmony(HarmonyId);

            harmony.Patch(
                openMethod,
                prefix: new HarmonyMethod(
                    typeof(RedEyeConfessionFlavorTextPatch),
                    nameof(Prefix)),
                postfix: new HarmonyMethod(
                    typeof(RedEyeConfessionFlavorTextPatch),
                    nameof(Postfix)));

            Debug.Log(
                "[脑叶武器扩展] 赤瞳&忏悔描述文本补丁已启用。");
        }

        internal static void Unapply()
        {
            if (harmony != null)
            {
                harmony.UnpatchAll(HarmonyId);
                harmony = null;
            }

            foreach (TooltipState state in States.Values)
            {
                if (state == null)
                    continue;

                RestoreSizes(state);

                if (state.DescriptionBox != null)
                {
                    UnityEngine.Object.Destroy(
                        state.DescriptionBox.gameObject);
                }
            }

            States.Clear();
        }

        private static void RestoreSizes(TooltipState state)
        {
            if (state.TooltipRect != null)
            {
                state.TooltipRect.sizeDelta =
                    state.OriginalTooltipSize;
            }

            if (state.ContentPanelRect != null)
            {
                state.ContentPanelRect.sizeDelta =
                    state.OriginalContentPanelSize;
            }

            if (state.ActionTabRect != null)
            {
                state.ActionTabRect.sizeDelta =
                    state.OriginalActionTabSize;
            }
        }

        private static void Prefix(UI_WeaponTooltip __instance)
        {
            if (__instance == null)
                return;

            TooltipState state = GetOrCreateState(__instance);

            if (state == null)
                return;

            RestoreSizes(state);

            state.DescriptionBox.SetText("");
            state.DescriptionBox.gameObject.SetActive(false);
        }

        private static void Postfix(
            UI_WeaponTooltip __instance,
            ITooltip data)
        {
            if (__instance == null || data == null)
                return;

            WeaponEntity weapon = ResolveWeapon(data);

            if (weapon == null ||
                RedEyeConfessionWeapon.RegisteredWeaponId < 0 ||
                weapon.id != RedEyeConfessionWeapon.RegisteredWeaponId)
            {
                return;
            }

            TooltipState state = GetOrCreateState(__instance);

            if (state == null)
                return;

            string tier1Key =
                RedEyeConfessionWeapon.Tier1TextKey;

            string flavorKey =
                RedEyeConfessionWeapon.FlavorTextKey;

            string tier1 =
                new LocalizedString(tier1Key).ToString();

            string flavor =
                new LocalizedString(flavorKey).ToString();

            bool hasTier1 =
                !string.IsNullOrWhiteSpace(tier1) &&
                tier1 != tier1Key;

            bool hasFlavor =
                !string.IsNullOrWhiteSpace(flavor) &&
                flavor != flavorKey;

            if (!hasTier1 && !hasFlavor)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][描述] 没有有效的自定义文本。" +
                    " Tier1Key=" + tier1Key +
                    "，FlavorKey=" + flavorKey);
                return;
            }

            string displayText;

            if (hasTier1 && hasFlavor)
            {
                displayText = tier1 + "\n\n" + flavor;
            }
            else
            {
                displayText = hasTier1 ? tier1 : flavor;
            }

            UI_SimpleTextBox box = state.DescriptionBox;
            RectTransform boxRect =
                box.GetComponent<RectTransform>();

            // 先确定正确的宽度，避免 TMP 文本区域只有几个像素。
            float width = Mathf.Max(
                1f,
                state.ActionTabRect.rect.width - 16f);

            boxRect.anchorMin = new Vector2(0f, 1f);
            boxRect.anchorMax = new Vector2(1f, 1f);
            boxRect.pivot = new Vector2(0.5f, 1f);
            boxRect.sizeDelta = new Vector2(-16f, 30f);
            boxRect.anchoredPosition = Vector2.zero;

            box.gameObject.SetActive(true);
            box.SetText(displayText);

            Canvas.ForceUpdateCanvases();

            float preferredHeight =
                box.text.GetPreferredValues(
                    displayText,
                    width,
                    0f).y;

            float rowHeight = Mathf.Max(
                30f,
                preferredHeight + 12f);

            boxRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                rowHeight);

            // 为文字增加额外空间，随后重新计算位置。
            float extraHeight = rowHeight + 12f;

            state.ActionTabRect.sizeDelta =
                new Vector2(
                    state.OriginalActionTabSize.x,
                    state.OriginalActionTabSize.y + extraHeight);

            state.ContentPanelRect.sizeDelta =
                new Vector2(
                    state.OriginalContentPanelSize.x,
                    state.OriginalContentPanelSize.y + extraHeight);

            state.TooltipRect.sizeDelta =
                new Vector2(
                    state.OriginalTooltipSize.x,
                    state.OriginalTooltipSize.y + extraHeight);

            Canvas.ForceUpdateCanvases();

            PositionDescriptionRow(state, rowHeight);

            LayoutRebuilder.ForceRebuildLayoutImmediate(
                state.ActionTabRect);

            LayoutRebuilder.ForceRebuildLayoutImmediate(
                state.ContentPanelRect);

            Canvas.ForceUpdateCanvases();

            Debug.Log(
                "[脑叶武器扩展][描述] 自定义描述已处理。" +
                " Tier1=" + hasTier1 +
                "，Flavor=" + hasFlavor +
                "，文本宽度=" + width +
                "，文本高度=" + rowHeight +
                "，ActionTabActive=" +
                state.ActionTabRect.gameObject.activeInHierarchy);
        }

        private static void PositionDescriptionRow(
            TooltipState state,
            float rowHeight)
        {
            RectTransform parent = state.ActionTabRect;
            RectTransform rowRect =
                state.DescriptionBox.GetComponent<RectTransform>();

            // 存在布局组件时，让 Unity 负责纵向排列。
            LayoutGroup layoutGroup =
                parent.GetComponent<LayoutGroup>();

            if (layoutGroup != null)
            {
                LayoutElement layoutElement =
                    state.DescriptionBox.GetComponent<LayoutElement>();

                if (layoutElement == null)
                {
                    layoutElement =
                        state.DescriptionBox.gameObject
                            .AddComponent<LayoutElement>();
                }

                layoutElement.minHeight = rowHeight;
                layoutElement.preferredHeight = rowHeight;
                layoutElement.flexibleHeight = 0f;

                rowRect.SetAsLastSibling();

                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
                return;
            }

            // 没有布局组件时，找出普通攻击、冲刺、防御等
            // SubWeapon 行的最下沿。
            float lowestBottom = float.PositiveInfinity;
            bool foundRow = false;

            Vector3[] corners = new Vector3[4];

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);

                if (child == state.DescriptionBox.transform ||
                    !child.name.StartsWith(
                        "SubWeapon",
                        StringComparison.Ordinal) ||
                    !child.gameObject.activeSelf)
                {
                    continue;
                }

                RectTransform rect = child as RectTransform;

                if (rect == null)
                    continue;

                rect.GetWorldCorners(corners);

                for (int j = 0; j < corners.Length; j++)
                {
                    float localY =
                        parent.InverseTransformPoint(corners[j]).y;

                    lowestBottom = Mathf.Min(
                        lowestBottom,
                        localY);
                }

                foundRow = true;
            }

            if (!foundRow)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][描述] 没有找到可定位的 SubWeapon 行。");
                rowRect.anchoredPosition = new Vector2(0f, -8f);
                return;
            }

            // 描述框采用顶部锚点，因此其顶部放在最后一行动作下方。
            float targetTopY = lowestBottom - 8f;

            rowRect.anchoredPosition = new Vector2(
                0f,
                targetTopY - parent.rect.yMax);
        }

        private static WeaponEntity ResolveWeapon(ITooltip data)
        {
            if (data is WeaponEntity entity)
                return entity;

            if (data is WeaponSimple simple)
            {
                return WeaponDatabase.FindWeaponById(
                    simple.entityId);
            }

            if (data is WeaponPreview preview)
                return preview.Entity;

            return null;
        }

        private static TooltipState GetOrCreateState(
            UI_WeaponTooltip instance)
        {
            if (States.TryGetValue(
                    instance,
                    out TooltipState existing))
            {
                if (existing.DescriptionBox != null)
                    return existing;
            }

            Transform contentPanel =
                instance.transform.Find("ContentPanel");

            Transform actionTab =
                instance.transform.Find("ContentPanel/ActionTab");

            if (contentPanel == null || actionTab == null)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][描述] 找不到 ContentPanel/ActionTab。");
                return null;
            }

            UI_SimpleTextBox[] boxes =
                instance.upgradeTextBoxes;

            if (boxes == null || boxes.Length == 0)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][描述] upgradeTextBoxes 为空，" +
                    "无法创建描述框。");
                return null;
            }

            UI_SimpleTextBox template = null;

            foreach (UI_SimpleTextBox candidate in boxes)
            {
                if (candidate != null)
                {
                    template = candidate;
                    break;
                }
            }

            if (template == null)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][描述] 找不到可用的 UI_SimpleTextBox。");
                return null;
            }

            RectTransform tooltipRect =
                instance.GetComponent<RectTransform>();

            RectTransform contentPanelRect =
                contentPanel as RectTransform;

            RectTransform actionTabRect =
                actionTab as RectTransform;

            if (tooltipRect == null ||
                contentPanelRect == null ||
                actionTabRect == null)
            {
                return null;
            }

            GameObject clone = UnityEngine.Object.Instantiate(
                template.gameObject,
                actionTab,
                false);

            clone.name =
                "LobCorpWeaponExpansion_Description";

            UI_SimpleTextBox descriptionBox =
                clone.GetComponent<UI_SimpleTextBox>();

            if (descriptionBox == null)
            {
                UnityEngine.Object.Destroy(clone);
                return null;
            }

            clone.SetActive(false);

            TooltipState state = new TooltipState
            {
                DescriptionBox = descriptionBox,
                TooltipRect = tooltipRect,
                ContentPanelRect = contentPanelRect,
                ActionTabRect = actionTabRect,

                OriginalTooltipSize = tooltipRect.sizeDelta,
                OriginalContentPanelSize =
                    contentPanelRect.sizeDelta,
                OriginalActionTabSize =
                    actionTabRect.sizeDelta
            };

            States[instance] = state;
            return state;
        }
    }
}