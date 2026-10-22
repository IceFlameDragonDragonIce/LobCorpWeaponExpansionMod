using HarmonyLib;
using UnityEngine;

namespace LobCorpWeaponExpansion
{
    /// <summary>
    /// 双锤冲刺时，为副手锤增加独立的挥动视觉。
    /// 不修改伤害、冲刺特效或普通攻击连段。
    /// </summary>
    internal sealed class RedEyeConfessionTwinDashOffhandMotionDriver
        : MonoBehaviour
    {
        private const int GreatswordLayer = 2;

        private const string DashClipName =
            "Crossbow_DashAttack";

        // 副手挥动角度。
        // 如果游戏内挥动方向相反，将 105 改为 -105。
        private const float SwingAngle = 105f;

        // Crossbow_DashAttack 长度约 0.483 秒。
        // 对齐主锤的斩击起始、命中和收招时间。
        private const float SwingStart = 0.24f;
        private const float SwingPeak = 0.35f;
        private const float SwingEnd = 0.83f;

        private WeaponControllerSimple controller;
        private Transform offhandTransform;

        private Quaternion originalLocalRotation =
            Quaternion.identity;

        private bool isAnimating;

        private void LateUpdate()
        {
            if (controller == null)
            {
                controller =
                    GetComponent<WeaponControllerSimple>();
            }

            if (controller == null)
                return;

            WeaponSimple currentWeapon =
                controller.currentWeapon;

            if (!RedEyeConfessionCombatPatchHelper.IsTargetWeapon(
                    currentWeapon) ||
                !(currentWeapon is WeaponSimple_SwordAndShield weapon))
            {
                RestoreAndUnbind();
                return;
            }

            if (weapon.shieldBody == null)
            {
                RestoreAndUnbind();
                return;
            }

            Transform currentOffhand =
                weapon.shieldBody.transform;

            if (offhandTransform != currentOffhand)
            {
                RestoreAndUnbind();

                offhandTransform = currentOffhand;
                originalLocalRotation =
                    offhandTransform.localRotation;
            }

            Animator animator = controller.animator;

            if (!TryGetDashProgress(animator, out float progress))
            {
                RestoreRotation();

                // 非冲刺期间持续记录正常姿态，
                // 避免干扰普通攻击或其他动作。
                if (offhandTransform != null)
                {
                    originalLocalRotation =
                        offhandTransform.localRotation;
                }

                return;
            }

            if (!isAnimating)
            {
                // 记录冲刺开始时的真实姿态。
                originalLocalRotation =
                    offhandTransform.localRotation;

                isAnimating = true;

                Debug.Log(
                    "[脑叶武器扩展][副手冲刺动作] " +
                    "开始同步挥动副手锤。");
            }

            float swing = CalculateSwing(progress);

            Quaternion swingRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    SwingAngle * swing);

            offhandTransform.localRotation =
                originalLocalRotation * swingRotation;
        }

        private static bool TryGetDashProgress(
            Animator animator,
            out float progress)
        {
            progress = 0f;

            if (animator == null ||
                animator.layerCount <= GreatswordLayer)
            {
                return false;
            }

            AnimatorClipInfo[] clips =
                animator.GetCurrentAnimatorClipInfo(
                    GreatswordLayer);

            bool playingDash = false;

            foreach (AnimatorClipInfo info in clips)
            {
                if (info.clip == null)
                    continue;

                if (info.clip.name == DashClipName &&
                    info.weight > 0.01f)
                {
                    playingDash = true;
                    break;
                }
            }

            if (!playingDash)
                return false;

            AnimatorStateInfo state =
                animator.GetCurrentAnimatorStateInfo(
                    GreatswordLayer);

            progress = Mathf.Clamp01(state.normalizedTime);

            return true;
        }

        private static float CalculateSwing(float time)
        {
            if (time <= SwingStart || time >= SwingEnd)
                return 0f;

            if (time < SwingPeak)
            {
                float t = Mathf.InverseLerp(
                    SwingStart,
                    SwingPeak,
                    time);

                return Mathf.SmoothStep(0f, 1f, t);
            }

            float recovery = Mathf.InverseLerp(
                SwingPeak,
                SwingEnd,
                time);

            return 1f - Mathf.SmoothStep(
                0f,
                1f,
                recovery);
        }

        private void RestoreRotation()
        {
            if (!isAnimating)
                return;

            if (offhandTransform != null)
            {
                offhandTransform.localRotation =
                    originalLocalRotation;
            }

            isAnimating = false;
        }

        private void RestoreAndUnbind()
        {
            RestoreRotation();
            offhandTransform = null;
        }

        private void OnDisable()
        {
            RestoreAndUnbind();
        }

        private void OnDestroy()
        {
            RestoreAndUnbind();
        }
    }

    /// <summary>
    /// 在冲刺攻击开始时安装副手动作控制器。
    /// </summary>
    [HarmonyPatch(
        typeof(WeaponControllerSimple),
        nameof(WeaponControllerSimple.BeginDashAttackAnimation),
        new[] { typeof(int) })]
    internal static class
        RedEyeConfessionTwinDashOffhandMotionInstallPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            WeaponControllerSimple __instance)
        {
            if (__instance == null ||
                !RedEyeConfessionCombatPatchHelper.IsTargetWeapon(
                    __instance.currentWeapon))
            {
                return;
            }

            if (__instance.GetComponent<
                    RedEyeConfessionTwinDashOffhandMotionDriver>() == null)
            {
                __instance.gameObject.AddComponent<
                    RedEyeConfessionTwinDashOffhandMotionDriver>();

                Debug.Log(
                    "[脑叶武器扩展][副手冲刺动作] " +
                    "已安装副手挥动控制器。");
            }
        }
    }
}