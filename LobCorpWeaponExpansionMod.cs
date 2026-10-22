using System;
using HarmonyLib;
using UnityEngine;

namespace LobCorpWeaponExpansion
{
    /// <summary>
    /// Native Sephiria AddOn entry point.
    /// This project does not require Sephiria ModMaker.
    /// </summary>
    public sealed class LobCorpWeaponExpansionMod : HorayModBase
    {
        private const string HarmonyId =
            "DragonIce.FrostFire.LobCorpWeaponExpansion";

        private static Harmony harmony;
        private static bool hooksRegistered;

        protected override void OnModLoaded()
        {
            try
            {
                base.OnModLoaded();

                if (!hooksRegistered)
                {
                    HorayModAPI.OnLocalizationReady +=
                        OnLocalizationReady;

                    HorayModAPI.OnLoadWeaponDatabase +=
                        OnLoadWeaponDatabase;

                    hooksRegistered = true;
                }

                if (harmony == null)
                {
                    harmony = new Harmony(HarmonyId);
                    harmony.PatchAll(
                        typeof(LobCorpWeaponExpansionMod).Assembly);
                }

                Debug.Log(
                    "[脑叶武器扩展] AddOn 已加载，等待武器数据库初始化。");
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展] 初始化失败：" + exception);
            }
        }

        protected override void OnModUnloaded()
        {
            try
            {
                if (hooksRegistered)
                {
                    HorayModAPI.OnLocalizationReady -=
                        OnLocalizationReady;

                    HorayModAPI.OnLoadWeaponDatabase -=
                        OnLoadWeaponDatabase;

                    hooksRegistered = false;
                }

                if (harmony != null)
                {
                    harmony.UnpatchAll(HarmonyId);
                    harmony = null;
                }

                base.OnModUnloaded();

                Debug.Log("[脑叶武器扩展] AddOn 已卸载。");
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展] 卸载清理失败：" + exception);
            }
        }

        private void OnLocalizationReady(
            HorayModLocalizationContext context)
        {
            try
            {
                RedEyeConfessionWeapon.RegisterLocalization(context);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展] 注册本地化文本失败：" +
                    exception);
            }
        }

        private void OnLoadWeaponDatabase()
        {
            try
            {
                RedEyeConfessionWeapon.Register();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展] 注册武器失败：" +
                    exception);
            }

            // 图标替换与武器注册分开处理，避免互相影响。
            try
            {
                RedEyeConfessionSpriteAssets.ApplyWeaponIcon();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展] 替换武器图标失败：" +
                    exception);
            }
        }
    }
}
