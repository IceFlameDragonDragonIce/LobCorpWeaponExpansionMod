using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace LobCorpWeaponExpansion
{
    /// <summary>
    /// 为赤瞳&忏悔的武器提示框增加独立风味文本行。
    /// 不修改原版文件，也不覆盖原有强化说明。
    /// </summary>
    internal static class RedEyeConfessionFlavorTextPatch
    {
        private const string HarmonyId =
            "LobCorpWeaponExpansion.RedEyeConfessionFlavorText";

        private static Harmony harmony;

        private sealed class TooltipState
        {
            public UI_SimpleTextBox FlavorBox;
            public UI_SimpleTextBox[] OriginalBoxes;
            public RectTransform TooltipRect;
            public Vector2 OriginalTooltipSize;
            public RectTransform RowParent;
            public Vector2 OriginalParentSize;
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
                    "风味文本补丁未启用。");
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
                "[脑叶武器扩展] 赤瞳&忏悔风味文本补丁已启用。");
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

                // 恢复原来的 UI 尺寸。
                if (state.TooltipRect != null)
                {
                    state.TooltipRect.sizeDelta =
                        state.OriginalTooltipSize;
                }

                if (state.RowParent != null)
                {
                    state.RowParent.sizeDelta =
                        state.OriginalParentSize;
                }

                if (state.FlavorBox != null)
                {
                    UnityEngine.Object.Destroy(
                        state.FlavorBox.gameObject);
                }
            }

            States.Clear();
        }

        /// <summary>
        /// 在原版 Open 之前恢复尺寸并隐藏上一轮风味文本。
        /// </summary>
        private static void Prefix(UI_WeaponTooltip __instance)
        {
            if (__instance == null)
                return;

            TooltipState state = GetOrCreateState(__instance);

            if (state == null)
                return;

            state.TooltipRect.sizeDelta =
                state.OriginalTooltipSize;

            state.RowParent.sizeDelta =
                state.OriginalParentSize;

            state.FlavorBox.SetText("");
            state.FlavorBox.gameObject.SetActive(false);
        }

        /// <summary>
        /// 原版 Open 完成后，为目标武器显示风味文本。
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
                weapon.id !=
                    RedEyeConfessionWeapon.RegisteredWeaponId)
            {
                return;
            }

            TooltipState state = GetOrCreateState(__instance);

            if (state == null)
                return;

            string flavor = new LocalizedString(
                RedEyeConfessionWeapon.FlavorTextKey
            ).ToString();

            if (string.IsNullOrWhiteSpace(flavor) ||
                flavor ==
                    RedEyeConfessionWeapon.FlavorTextKey)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展] 风味文本键尚未得到有效的本地化文本：" +
                    RedEyeConfessionWeapon.FlavorTextKey);
                return;
            }

            UI_SimpleTextBox box = state.FlavorBox;

            box.SetText(flavor);
            box.gameObject.SetActive(true);

            // 没有强化说明时，隐藏原版的空状态提示。
            if (__instance.emptyUpgradeObject != null)
            {
                __instance.emptyUpgradeObject.SetActive(false);
            }

            Canvas.ForceUpdateCanvases();

            RectTransform boxRect =
                box.GetComponent<RectTransform>();

            float width = box.text.rectTransform.rect.width;

            if (width <= 1f)
                width = boxRect.rect.width;

            float preferredHeight = box.text.GetPreferredValues(
                flavor,
                width,
                0f
            ).y;

            float rowHeight = Mathf.Max(
                boxRect.rect.height,
                preferredHeight + 10f
            );

            boxRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                rowHeight
            );

            PositionFlavorRow(state);

            // 为新增文本行留出空间。
            float extraHeight = rowHeight + 8f;

            state.RowParent.sizeDelta =
                new Vector2(
                    state.OriginalParentSize.x,
                    state.OriginalParentSize.y + extraHeight
                );

            state.TooltipRect.sizeDelta =
                new Vector2(
                    state.OriginalTooltipSize.x,
                    state.OriginalTooltipSize.y + extraHeight
                );

            LayoutRebuilder.ForceRebuildLayoutImmediate(
                state.RowParent
            );
        }

        private static WeaponEntity ResolveWeapon(ITooltip data)
        {
            if (data is WeaponEntity entity)
                return entity;

            if (data is WeaponSimple simple)
            {
                return WeaponDatabase.FindWeaponById(
                    simple.entityId
                );
            }

            if (data is WeaponPreview preview)
                return preview.Entity;

            return null;
        }

        private static TooltipState GetOrCreateState(
            UI_WeaponTooltip instance)
        {
            if (States.TryGetValue(instance, out TooltipState existing))
            {
                if (existing.FlavorBox != null)
                    return existing;
            }

            UI_SimpleTextBox[] boxes =
                instance.upgradeTextBoxes;

            if (boxes == null || boxes.Length == 0)
            {
                Debug.LogWarning(
                    "[脑叶武器扩展] upgradeTextBoxes 为空，" +
                    "无法创建风味文本行。");
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
                    "[脑叶武器扩展] 找不到可复制的 UI_SimpleTextBox。");
                return null;
            }

            RectTransform rowParent =
                template.transform.parent as RectTransform;

            RectTransform tooltipRect =
                instance.GetComponent<RectTransform>();

            if (rowParent == null || tooltipRect == null)
                return null;

            GameObject clone = UnityEngine.Object.Instantiate(
                template.gameObject,
                rowParent,
                false
            );

            clone.name =
                "LobCorpWeaponExpansion_FlavorText";

            UI_SimpleTextBox flavorBox =
                clone.GetComponent<UI_SimpleTextBox>();

            if (flavorBox == null)
            {
                UnityEngine.Object.Destroy(clone);
                return null;
            }

            clone.SetActive(false);
            clone.transform.SetAsLastSibling();

            TooltipState state = new TooltipState
            {
                FlavorBox = flavorBox,
                OriginalBoxes = boxes,
                TooltipRect = tooltipRect,
                OriginalTooltipSize = tooltipRect.sizeDelta,
                RowParent = rowParent,
                OriginalParentSize = rowParent.sizeDelta
            };

            States[instance] = state;

            return state;
        }

        private static void PositionFlavorRow(TooltipState state)
        {
            RectTransform rowRect =
                state.FlavorBox.GetComponent<RectTransform>();

            RectTransform parent = state.RowParent;

            // 有布局组件时交给 Unity 的布局系统排列。
            if (parent.GetComponent<LayoutGroup>() != null)
            {
                state.FlavorBox.transform.SetAsLastSibling();

                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
                return;
            }

            bool foundActiveRow = false;
            float lowestBottom = float.PositiveInfinity;

            Vector3[] corners = new Vector3[4];

            foreach (UI_SimpleTextBox box in state.OriginalBoxes)
            {
                if (box == null || !box.gameObject.activeSelf)
                    continue;

                RectTransform rect =
                    box.GetComponent<RectTransform>();

                rect.GetWorldCorners(corners);

                for (int i = 0; i < corners.Length; i++)
                {
                    float localY = parent
                        .InverseTransformPoint(corners[i]).y;

                    lowestBottom = Mathf.Min(
                        lowestBottom,
                        localY
                    );
                }

                foundActiveRow = true;
            }

            if (!foundActiveRow)
            {
                // 原武器没有强化说明时，将风味文本放在第一行位置。
                foreach (UI_SimpleTextBox box in state.OriginalBoxes)
                {
                    if (box == null)
                        continue;

                    rowRect.position =
                        box.GetComponent<RectTransform>().position;

                    return;
                }

                return;
            }

            Vector3 localPosition =
                parent.InverseTransformPoint(rowRect.position);

            float height = rowRect.rect.height;

            // 将新文本行放到所有当前显示的强化说明下方。
            localPosition.y =
                lowestBottom - 8f - height * (1f - rowRect.pivot.y);

            rowRect.position =
                parent.TransformPoint(localPosition);
        }
    }
}