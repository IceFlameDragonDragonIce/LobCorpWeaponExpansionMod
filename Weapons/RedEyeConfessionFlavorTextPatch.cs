using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LobCorpWeaponExpansion
{
    /// <summary>
    /// 在武器详情的 UpgradesTab 中显示自定义描述，
    /// 并将自定义招式关键词详情注册到武器详情的 keywordViewer。
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

            MethodInfo openMethod = AccessTools.Method(
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

            MethodInfo keywordMethod = AccessTools.Method(
                typeof(UI_WeaponTooltip),
                "OpenKeywordsCoroutine");

            harmony = new Harmony(HarmonyId);

            try
            {
                // 原有功能：创建并显示强化页签的自定义说明。
                harmony.Patch(
                    openMethod,
                    prefix: new HarmonyMethod(
                        typeof(RedEyeConfessionFlavorTextPatch),
                        nameof(Prefix)),
                    postfix: new HarmonyMethod(
                        typeof(RedEyeConfessionFlavorTextPatch),
                        nameof(Postfix)));

                // 新增功能：把自定义说明中的招式关键词
                // 补充到当前武器详情的关键词查看器。
                if (keywordMethod != null)
                {
                    harmony.Patch(
                        keywordMethod,
                        postfix: new HarmonyMethod(
                            typeof(RedEyeConfessionFlavorTextPatch),
                            nameof(OpenKeywordsCoroutinePostfix)));

                    Debug.Log(
                        "[脑叶武器扩展][招式详情] " +
                        "OpenKeywordsCoroutine 补丁已安装。");
                }
                else
                {
                    Debug.LogError(
                        "[脑叶武器扩展][招式详情] " +
                        "找不到 OpenKeywordsCoroutine，" +
                        "无法补充武器详情的关键词悬停。");
                }

                Debug.Log(
                    "[脑叶武器扩展] 赤瞳&忏悔强化描述补丁已启用。");
            }
            catch (Exception exception)
            {
                harmony.UnpatchAll(HarmonyId);
                harmony = null;

                Debug.LogError(
                    "[脑叶武器扩展] 安装武器详情补丁失败：" +
                    exception);
            }
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

            state.DescriptionBox.SetText(string.Empty);
            state.DescriptionBox.gameObject.SetActive(false);
        }

        /// <summary>
        /// 只为赤瞳&忏悔准备强化页签的自定义描述。
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

            // 转换 <tag=...>，生成游戏可识别的关键词链接。
            displayText = ConvertKeywordText(displayText);

            UI_SimpleTextBox box = state.DescriptionBox;

            RectTransform boxRect =
                box.GetComponent<RectTransform>();

            float width = Mathf.Max(
                1f,
                state.UpgradesTabRect.rect.width - 16f);

            boxRect.anchorMin = new Vector2(0f, 1f);
            boxRect.anchorMax = new Vector2(1f, 1f);
            boxRect.pivot = new Vector2(0.5f, 1f);
            boxRect.sizeDelta = new Vector2(-16f, 30f);
            boxRect.anchoredPosition = Vector2.zero;

            box.SetText(displayText);
            box.gameObject.SetActive(true);

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
        /// 将本地化描述中的关键词标签转换为游戏链接。
        /// </summary>
        private static string ConvertKeywordText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            try
            {
                string converted = KeywordDatabase.Convert(
                    KeywordDatabase.Convert(text));

                return converted ?? text;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展][描述] 关键词转换失败：" +
                    exception);

                return text;
            }
        }

        /// <summary>
        /// 原版关键词协程完成后，补充我们自定义说明框中的关键词。
        /// </summary>
        private static void OpenKeywordsCoroutinePostfix(
            UI_WeaponTooltip __instance,
            ref IEnumerator __result)
        {
            if (__instance == null || __result == null)
                return;

            IEnumerator original = __result;

            __result = RunOriginalThenAddCustomKeywords(
                original,
                __instance);
        }

        private static IEnumerator RunOriginalThenAddCustomKeywords(
            IEnumerator original,
            UI_WeaponTooltip instance)
        {
            // 先完整执行原版逻辑，保留原版关键词的悬停详情。
            yield return original;

            try
            {
                AddCustomKeywordDetails(instance);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展][招式详情] " +
                    "补充自定义关键词失败：" +
                    exception);
            }
        }

        /// <summary>
        /// 只把赤瞳&忏悔自定义说明中实际出现的招式关键词
        /// 加入当前武器详情的 keywordViewer。
        /// </summary>
        private static void AddCustomKeywordDetails(
            UI_WeaponTooltip instance)
        {
            if (instance == null ||
                !States.TryGetValue(
                    instance,
                    out TooltipState state) ||
                state == null ||
                state.DescriptionBox == null ||
                !state.DescriptionBox.gameObject.activeInHierarchy ||
                instance.keywordViewer == null)
            {
                return;
            }

            TMP_Text text = state.DescriptionBox.text;

            if (text == null)
                return;

            // 确保 TextMeshPro 的链接信息已经生成。
            text.ForceMeshUpdate();

            string customKeywordId =
                RedEyeConfessionActionKeywordPatch.Keyword;

            bool foundCustomKeyword = false;

            for (int i = 0; i < text.textInfo.linkCount; i++)
            {
                string linkId =
                    text.textInfo.linkInfo[i].GetLinkID();

                if (string.Equals(
                    linkId,
                    customKeywordId,
                    StringComparison.Ordinal))
                {
                    foundCustomKeyword = true;
                    break;
                }
            }

            if (!foundCustomKeyword)
            {
                Debug.Log(
                    "[脑叶武器扩展][招式详情] " +
                    "当前说明框没有找到自定义招式链接。");

                return;
            }

            KeywordEntity customKeyword =
                KeywordDatabase.GetEntity(customKeywordId);

            if (customKeyword == null)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][招式详情] " +
                    "自定义 KeywordEntity 不存在：" +
                    customKeywordId);

                return;
            }

            if (!customKeyword.displayDetails)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展][招式详情] " +
                    "自定义关键词的 displayDetails 未开启：" +
                    customKeywordId);

                return;
            }

            // 以自定义关键词作为起点，递归收集其描述中
            // 引用且允许显示详情的其他关键词。
            HashSet<string> keywordIds =
                new HashSet<string>();

            Queue<string> pendingIds =
                new Queue<string>();

            keywordIds.Add(customKeywordId);
            pendingIds.Enqueue(customKeywordId);

            while (pendingIds.Count > 0)
            {
                string currentId = pendingIds.Dequeue();

                KeywordEntity currentEntity =
                    KeywordDatabase.GetEntity(currentId);

                if (currentEntity == null)
                    continue;

                string rawDescription =
                    currentEntity.GetRawDescription();

                if (string.IsNullOrEmpty(rawDescription))
                    continue;

                MatchCollection matches =
                    Regex.Matches(
                        rawDescription,
                        @"<tag=(.*?)>");

                foreach (Match match in matches)
                {
                    if (!match.Groups[1].Success)
                        continue;

                    string childId = match.Groups[1].Value;

                    KeywordEntity childEntity =
                        KeywordDatabase.GetEntity(childId);

                    if (childEntity == null ||
                        !childEntity.displayDetails)
                    {
                        continue;
                    }

                    if (keywordIds.Add(childEntity.keyword))
                    {
                        pendingIds.Enqueue(childEntity.keyword);
                    }
                }
            }

            int addedCount = 0;

            foreach (string keywordId in keywordIds)
            {
                KeywordEntity keyword =
                    KeywordDatabase.GetEntity(keywordId);

                if (keyword == null)
                    continue;

                string rawDescription =
                    keyword.GetRawDescription();

                string detailText = KeywordDatabase.Convert(
                    rawDescription,
                    useColor: false,
                    useSprite: false);

                if (detailText == null)
                    detailText = string.Empty;

                // 与其他模组的 UI_WeaponTooltip 处理方式一致，
                // 将关键词和说明加入当前窗口的详情查看器。
                instance.keywordViewer.AddKeyword(
                    keyword.Convert_Details(
                        useColor: false,
                        useSprite: false),
                    detailText);

                addedCount++;
            }

            Debug.Log(
                "[脑叶武器扩展][招式详情] " +
                "已补充武器详情的自定义关键词。" +
                " Keyword=" + customKeywordId +
                "，LinkCount=" + text.textInfo.linkCount +
                "，Added=" + addedCount);
        }

        /// <summary>
        /// 确保自定义武器的强化页签可以切换。
        /// </summary>
        private static void EnsureUpgradesTabSelectable(
            UI_WeaponTooltip instance,
            TooltipState state)
        {
            if (instance == null ||
                state == null ||
                state.UpgradesTabRect == null ||
                instance.tabElements == null)
            {
                return;
            }

            int upgradeTabIndex = -1;

            for (int i = 0; i < instance.tabElements.Length; i++)
            {
                UI_WeaponTooltip.TabElement tab =
                    instance.tabElements[i];

                if (tab != null &&
                    tab.tabContent == state.UpgradesTabRect)
                {
                    upgradeTabIndex = i;
                    break;
                }
            }

            if (upgradeTabIndex < 0)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展] 未找到强化页签，" +
                    "无法设置默认页签。");

                return;
            }

            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.NonPublic;

            FieldInfo activeIndicesField =
                typeof(UI_WeaponTooltip).GetField(
                    "activeTabIndices",
                    flags);

            FieldInfo currentTabIndexField =
                typeof(UI_WeaponTooltip).GetField(
                    "currentTabIndex",
                    flags);

            MethodInfo applyTabSelectionMethod =
                typeof(UI_WeaponTooltip).GetMethod(
                    "ApplyTabSelection",
                    flags);

            if (activeIndicesField == null ||
                currentTabIndexField == null ||
                applyTabSelectionMethod == null)
            {
                Debug.LogError(
                    "[脑叶武器扩展] 无法访问 UI_WeaponTooltip 的" +
                    "页签字段或方法。");

                return;
            }

            List<int> activeTabIndices =
                activeIndicesField.GetValue(instance) as List<int>;

            if (activeTabIndices == null)
            {
                Debug.LogError(
                    "[脑叶武器扩展] activeTabIndices 获取失败。");

                return;
            }

            if (!activeTabIndices.Contains(upgradeTabIndex))
            {
                activeTabIndices.Add(upgradeTabIndex);
            }

            if (instance.tabMovementGuideObject != null)
            {
                instance.tabMovementGuideObject.SetActive(
                    activeTabIndices.Count > 1);
            }

            currentTabIndexField.SetValue(
                instance,
                upgradeTabIndex);

            applyTabSelectionMethod.Invoke(
                instance,
                null);
        }

        private static void PositionDescriptionRow(
            TooltipState state,
            float rowHeight)
        {
            RectTransform parent =
                state.UpgradesTabRect;

            RectTransform rowRect =
                state.DescriptionBox.GetComponent<RectTransform>();

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

                rowRect.anchoredPosition =
                    new Vector2(0f, -8f);

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
                    "[脑叶武器扩展][描述] " +
                    "找不到可用的 UI_SimpleTextBox。");

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

            // 自定义强化说明仅放在 UpgradesTab。
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