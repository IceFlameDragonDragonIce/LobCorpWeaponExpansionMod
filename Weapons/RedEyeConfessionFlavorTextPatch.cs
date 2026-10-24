using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace LobCorpWeaponExpansion
{
    /// <summary>
    /// 在武器详情的 UpgradesTab 中显示自定义描述。
    /// 优先显示 Tier1，再显示 Flavor。
    /// 不向 ActionTab 添加自定义描述。
    /// </summary>
    internal static class RedEyeConfessionFlavorTextPatch
    {
        private const string HarmonyId =
            "LobCorpWeaponExpansion.RedEyeConfessionFlavorText";

        private static Harmony harmony;

        private sealed class TooltipState
        {
            public UI_SimpleTextBox DescriptionBox;
            public UI_SimpleTextBox[] OriginalBoxes;

            public RectTransform TooltipRect;
            public RectTransform ContentPanelRect;
            public RectTransform UpgradesTabRect;

            public Vector2 OriginalTooltipSize;
            public Vector2 OriginalContentPanelSize;
            public Vector2 OriginalUpgradesTabSize;
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
                "[脑叶武器扩展] 赤瞳&忏悔强化描述补丁已启用。");
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

            if (state.UpgradesTabRect != null)
            {
                state.UpgradesTabRect.sizeDelta =
                    state.OriginalUpgradesTabSize;
            }
        }

        /// <summary>
        /// 原版打开提示框前，恢复尺寸并隐藏上一轮描述。
        /// </summary>
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

        /// <summary>
        /// 只在目标武器的强化页签中准备自定义描述。
        /// </summary>
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

            // 将《强化》页签加入可切换列表。
            EnsureUpgradesTabSelectable(__instance, state);

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

            float width = Mathf.Max(
                1f,
                state.UpgradesTabRect.rect.width - 16f);

            // 描述框属于 UpgradesTab，不属于 ActionTab。
            boxRect.anchorMin = new Vector2(0f, 1f);
            boxRect.anchorMax = new Vector2(1f, 1f);
            boxRect.pivot = new Vector2(0.5f, 1f);
            boxRect.sizeDelta = new Vector2(-16f, 30f);
            boxRect.anchoredPosition = Vector2.zero;

            box.SetText(displayText);
            box.gameObject.SetActive(true);

            // 没有原版强化说明时，隐藏原版空状态提示。
            if (__instance.emptyUpgradeObject != null)
            {
                __instance.emptyUpgradeObject.SetActive(false);
            }

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

            float extraHeight = rowHeight + 12f;

            state.UpgradesTabRect.sizeDelta =
                new Vector2(
                    state.OriginalUpgradesTabSize.x,
                    state.OriginalUpgradesTabSize.y + extraHeight);

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
                state.UpgradesTabRect);

            LayoutRebuilder.ForceRebuildLayoutImmediate(
                state.ContentPanelRect);

            Canvas.ForceUpdateCanvases();

            Debug.Log(
                "[脑叶武器扩展][描述] 强化页签描述已处理。" +
                " Tier1=" + hasTier1 +
                "，Flavor=" + hasFlavor +
                "，文本宽度=" + width +
                "，文本高度=" + rowHeight +
                "，UpgradesTabActive=" +
                state.UpgradesTabRect.gameObject.activeInHierarchy);
        }

        /// <summary>
        /// 原版 SetupTabs 根据 WeaponAddon 数量决定页签是否可用。
        /// 赤瞳&忏悔使用自定义描述，因此手动将强化页签加入
        /// activeTabIndices，并重新应用当前页签选择。
        /// </summary>
        private static void EnsureUpgradesTabSelectable(
            UI_WeaponTooltip instance,
            TooltipState state)
        {
            if (instance == null ||
                state == null ||
                state.UpgradesTabRect == null)
            {
                return;
            }

            UI_WeaponTooltip.TabElement[] tabs =
                instance.tabElements;

            if (tabs == null || tabs.Length == 0)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][页签] tabElements 为空。");
                return;
            }

            // 查找 UpgradesTab 对应的原版页签索引。
            int upgradesIndex = -1;

            for (int i = 0; i < tabs.Length; i++)
            {
                UI_WeaponTooltip.TabElement tab = tabs[i];

                if (tab == null || tab.tabContent == null)
                    continue;

                if (tab.tabContent == state.UpgradesTabRect)
                {
                    upgradesIndex = i;
                    break;
                }
            }

            if (upgradesIndex < 0)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][页签] tabElements 中找不到 " +
                    "与 UpgradesTab 对应的页签。");
                return;
            }

            // 访问原版私有字段 activeTabIndices。
            var activeIndicesField = AccessTools.Field(
                typeof(UI_WeaponTooltip),
                "activeTabIndices");

            // 访问原版私有方法 ApplyTabSelection。
            var applyTabSelectionMethod = AccessTools.Method(
                typeof(UI_WeaponTooltip),
                "ApplyTabSelection");

            if (activeIndicesField == null ||
                applyTabSelectionMethod == null)
            {
                Debug.LogError(
                    "[脑叶武器扩展][页签] 无法访问原版页签控制成员。");
                return;
            }

            var activeIndices =
                activeIndicesField.GetValue(instance) as List<int>;

            if (activeIndices == null)
            {
                Debug.LogError(
                    "[脑叶武器扩展][页签] 无法读取 activeTabIndices。");
                return;
            }

            // 避免重复加入。
            if (!activeIndices.Contains(upgradesIndex))
            {
                activeIndices.Add(upgradesIndex);
            }

            // 原版只在多个页签可用时显示切换提示。
            if (instance.tabMovementGuideObject != null)
            {
                instance.tabMovementGuideObject.SetActive(
                    activeIndices.Count > 1);
            }

            // 刷新页签显示。
            // 保留原本选中的页签，不强制跳到《强化》。
            applyTabSelectionMethod.Invoke(instance, null);

            Debug.Log(
                "[脑叶武器扩展][页签] 已确保强化页签可切换。" +
                " Index=" + upgradesIndex +
                "，ActiveTabCount=" + activeIndices.Count +
                "，CurrentUpgradesTabActive=" +
                state.UpgradesTabRect.gameObject.activeSelf);
        }

        private static void PositionDescriptionRow(
            TooltipState state,
            float rowHeight)
        {
            RectTransform parent = state.UpgradesTabRect;

            RectTransform rowRect =
                state.DescriptionBox.GetComponent<RectTransform>();

            LayoutGroup layoutGroup =
                parent.GetComponent<LayoutGroup>();

            // 如果强化页签使用布局组件，则由 Unity 自动排列。
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

            // 没有布局组件时，放在原有强化文本的下方。
            bool foundActiveRow = false;
            float lowestBottom = float.PositiveInfinity;

            Vector3[] corners = new Vector3[4];

            foreach (UI_SimpleTextBox box in state.OriginalBoxes)
            {
                if (box == null ||
                    !box.gameObject.activeSelf)
                {
                    continue;
                }

                RectTransform rect =
                    box.GetComponent<RectTransform>();

                if (rect == null)
                    continue;

                rect.GetWorldCorners(corners);

                for (int i = 0; i < corners.Length; i++)
                {
                    float localY =
                        parent.InverseTransformPoint(corners[i]).y;

                    lowestBottom = Mathf.Min(
                        lowestBottom,
                        localY);
                }

                foundActiveRow = true;
            }

            if (!foundActiveRow)
            {
                // 没有其他强化行时，使用原版第一行的位置。
                foreach (UI_SimpleTextBox box in state.OriginalBoxes)
                {
                    if (box == null)
                        continue;

                    RectTransform rect =
                        box.GetComponent<RectTransform>();

                    if (rect == null)
                        continue;

                    rowRect.position = rect.position;
                    return;
                }

                rowRect.anchoredPosition = new Vector2(0f, -8f);
                return;
            }

            Vector3 localPosition =
                parent.InverseTransformPoint(rowRect.position);

            localPosition.y =
                lowestBottom - 8f -
                rowRect.rect.height * (1f - rowRect.pivot.y);

            rowRect.position =
                parent.TransformPoint(localPosition);
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

            Transform upgradesTab =
                instance.transform.Find(
                    "ContentPanel/UpgradesTab");

            if (contentPanel == null || upgradesTab == null)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][描述] 找不到 " +
                    "ContentPanel/UpgradesTab。");
                return null;
            }

            UI_SimpleTextBox[] boxes =
                instance.upgradeTextBoxes;

            if (boxes == null || boxes.Length == 0)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][描述] upgradeTextBoxes 为空，" +
                    "无法创建强化描述框。");
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

            RectTransform upgradesTabRect =
                upgradesTab as RectTransform;

            if (tooltipRect == null ||
                contentPanelRect == null ||
                upgradesTabRect == null)
            {
                return null;
            }

            // 关键修改：描述框创建在 UpgradesTab 下。
            GameObject clone = UnityEngine.Object.Instantiate(
                template.gameObject,
                upgradesTab,
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
                OriginalBoxes = boxes,

                TooltipRect = tooltipRect,
                ContentPanelRect = contentPanelRect,
                UpgradesTabRect = upgradesTabRect,

                OriginalTooltipSize = tooltipRect.sizeDelta,
                OriginalContentPanelSize =
                    contentPanelRect.sizeDelta,
                OriginalUpgradesTabSize =
                    upgradesTabRect.sizeDelta
            };

            States[instance] = state;
            return state;
        }
    }
}