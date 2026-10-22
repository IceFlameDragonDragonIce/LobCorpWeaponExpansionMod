using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LobCorpWeaponExpansion
{
    /// <summary>
    /// 三段连击的武器显示：
    /// Swing 0：只显示主手锤。
    /// Swing 1：只显示副手锤。
    /// Swing 2：显示两把锤。
    /// </summary>
    public sealed class RedEyeConfessionTwinHandVisibilityDriver
        : MonoBehaviour
    {
        private sealed class RendererState
        {
            internal SpriteRenderer Renderer;
            internal bool OriginalEnabled;
        }
        private bool lastWasDashAttack;
        private static readonly FieldInfo CurrentWeaponField =
            AccessTools.Field(
                typeof(WeaponControllerSimple),
                "currentWeapon");
        private static readonly FieldInfo AnimatorField =
            AccessTools.Field(
        typeof(WeaponControllerSimple),
        "animator");
        private readonly List<RendererState> mainRenderers =
            new List<RendererState>();

        private readonly List<RendererState> offhandRenderers =
            new List<RendererState>();

        private WeaponSimple_SwordAndShield boundWeapon;

        private int lastLoggedSwing = int.MinValue;

        private void LateUpdate()
        {
            WeaponControllerSimple controller =
                GetComponent<WeaponControllerSimple>();

            if (controller == null ||
                CurrentWeaponField == null)
            {
                ReleaseWeapon();
                return;
            }

            WeaponSimple current =
                CurrentWeaponField.GetValue(controller)
                as WeaponSimple;

            if (!RedEyeConfessionTwinComboHelper.IsTarget(current) ||
                !(current is WeaponSimple_SwordAndShield))
            {
                // 切换离开赤瞳＆忏悔时，恢复原始动画控制器。
                RedEyeConfessionTwinDashAnimationPatch
                    .RestoreOverride(controller);

                ReleaseWeapon();
                return;
            }

            WeaponSimple_SwordAndShield weapon =
                (WeaponSimple_SwordAndShield)current;

            if (boundWeapon != weapon)
            {
                BindWeapon(weapon);
            }

            bool isDashAttack = IsDashAttackPlaying(controller);

            int swing =
                RedEyeConfessionTwinComboHelper.GetCurrentSwing(
                    controller);

            if (isDashAttack)
            {
                // 冲刺攻击期间，两把锤子都必须显示。
                RestoreGroups();
            }
            else
            {
                switch (swing)
                {
                    case 0:
                        // 普通攻击第一段：主锤。
                        SetGroup(mainRenderers, true);
                        SetGroup(offhandRenderers, false);
                        break;

                    case 1:
                        // 普通攻击第二段：副锤。
                        SetGroup(mainRenderers, false);
                        SetGroup(offhandRenderers, true);
                        break;

                    default:
                        // 普通攻击第三段及非攻击状态。
                        RestoreGroups();
                        break;
                }
            }

            if (swing != lastLoggedSwing ||
                isDashAttack != lastWasDashAttack)
            {
                lastLoggedSwing = swing;
                lastWasDashAttack = isDashAttack;

                string mode;

                if (isDashAttack)
                {
                    mode = "冲刺期间双锤可见";
                }
                else if (swing == 0)
                {
                    mode = "仅主手可见";
                }
                else if (swing == 1)
                {
                    mode = "仅副手可见";
                }
                else if (swing == 2)
                {
                    mode = "双锤可见";
                }
                else
                {
                    mode = "恢复默认显示";
                }

                Debug.Log(
                    "[脑叶武器扩展][双锤显示] Swing=" +
                    swing + "，" + mode + "。");
            }
        }
        private static bool IsDashAttackPlaying(
    WeaponControllerSimple controller)
        {
            Animator animator =
                AnimatorField?.GetValue(controller) as Animator;

            if (animator == null)
                return false;

            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                AnimatorClipInfo[] clips =
                    animator.GetCurrentAnimatorClipInfo(layer);

                foreach (AnimatorClipInfo info in clips)
                {
                    if (info.clip == null)
                        continue;

                    if (info.clip.name.IndexOf(
                            "Dash",
                            System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        private void BindWeapon(
            WeaponSimple_SwordAndShield weapon)
        {
            ReleaseWeapon();

            boundWeapon = weapon;

            // 主手锤及其遮罩。
            if (weapon.mainWeaponBody != null)
            {
                AddRenderer(
                    mainRenderers,
                    weapon.mainWeaponBody.weaponSpriteRenderer);

                AddRenderer(
                    mainRenderers,
                    weapon.mainWeaponBody.weaponStencilRenderer);
            }

            // 副手锤、遮罩及武器叠加层。
            if (weapon.shieldBody != null)
            {
                AddRenderer(
                    offhandRenderers,
                    weapon.shieldBody.weaponSpriteRenderer);

                AddRenderer(
                    offhandRenderers,
                    weapon.shieldBody.weaponStencilRenderer);

                AddRenderer(
                    offhandRenderers,
                    weapon.shieldBody.weaponAddon);
            }
        }

        private static void AddRenderer(
            List<RendererState> group,
            SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            foreach (RendererState existing in group)
            {
                if (existing.Renderer == renderer)
                {
                    return;
                }
            }

            group.Add(new RendererState
            {
                Renderer = renderer,
                OriginalEnabled = renderer.enabled
            });
        }

        private static void SetGroup(
            List<RendererState> group,
            bool visible)
        {
            foreach (RendererState state in group)
            {
                if (state.Renderer != null)
                {
                    // 不覆盖这个渲染器原本的启用状态。
                    state.Renderer.enabled =
                        visible && state.OriginalEnabled;
                }
            }
        }

        private void RestoreGroups()
        {
            SetGroup(mainRenderers, true);
            SetGroup(offhandRenderers, true);
        }

        private void ReleaseWeapon()
        {
            RestoreGroups();

            mainRenderers.Clear();
            offhandRenderers.Clear();

            boundWeapon = null;
            lastLoggedSwing = int.MinValue;
        }

        private void OnDestroy()
        {
            RestoreGroups();
        }
    }

    /// <summary>
    /// 在普通攻击开始时安装双锤显示控制器。
    /// </summary>
    [HarmonyPatch(
        typeof(WeaponControllerSimple),
        nameof(WeaponControllerSimple.BeginAttackAnimation),
        new[] { typeof(int) })]
    internal static class RedEyeConfessionTwinHandVisibilityInstallPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            WeaponControllerSimple __instance)
        {
            Install(__instance);
        }

        internal static void Install(
            WeaponControllerSimple controller)
        {
            if (controller == null)
            {
                return;
            }

            FieldInfo field = AccessTools.Field(
                typeof(WeaponControllerSimple),
                "currentWeapon");

            WeaponSimple weapon =
                field?.GetValue(controller) as WeaponSimple;

            if (!RedEyeConfessionTwinComboHelper.IsTarget(weapon))
            {
                return;
            }

            if (controller.GetComponent<
                    RedEyeConfessionTwinHandVisibilityDriver>() == null)
            {
                controller.gameObject.AddComponent<
                    RedEyeConfessionTwinHandVisibilityDriver>();

                Debug.Log(
                    "[脑叶武器扩展][双锤显示] " +
                    "已安装单手/双手武器显示控制器。");
            }
        }
    }

    /// <summary>
    /// 在冲刺攻击开始时也安装双锤显示控制器，
    /// 避免首次攻击就是冲刺时未安装组件。
    /// </summary>
    [HarmonyPatch(
        typeof(WeaponControllerSimple),
        nameof(WeaponControllerSimple.BeginDashAttackAnimation),
        new[] { typeof(int) })]
    internal static class RedEyeConfessionTwinHandVisibilityDashInstallPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            WeaponControllerSimple __instance)
        {
            RedEyeConfessionTwinHandVisibilityInstallPatch
                .Install(__instance);
        }
    }
}