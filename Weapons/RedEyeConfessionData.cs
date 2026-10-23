using System;
using System.IO;
using UnityEngine;

namespace LobCorpWeaponExpansion
{
    [Serializable]
    internal sealed class RedEyeConfessionManifest
    {
        public int entityId;
        public string displayName;
        public string textPrefix;
        public RedEyeConfessionArt art;
    }

    [Serializable]
    internal sealed class RedEyeConfessionArt
    {
        public string icon;
        public string mainHammer;
        public string offhandHammer;
    }

    internal static class RedEyeConfessionData
    {
        internal const int DirectoryId = 372466;

        private static bool loadAttempted;
        private static bool loadSucceeded;

        private static RedEyeConfessionManifest manifest;

        internal static bool TryLoad(
            out RedEyeConfessionManifest result)
        {
            if (loadAttempted)
            {
                result = manifest;
                return loadSucceeded;
            }

            loadAttempted = true;
            result = null;

            try
            {
                string assemblyPath =
                    typeof(LobCorpWeaponExpansionMod)
                        .Assembly.Location;

                string modDirectory =
                    Path.GetDirectoryName(assemblyPath);

                string dataDirectory = Path.Combine(
                    modDirectory,
                    "Weapons",
                    DirectoryId.ToString());

                string manifestPath = Path.Combine(
                    dataDirectory,
                    "manifest.json");

                if (!File.Exists(manifestPath))
                {
                    Debug.LogError(
                        "[脑叶武器扩展][数据] 找不到 manifest.json：" +
                        manifestPath);

                    return false;
                }

                string json = File.ReadAllText(manifestPath);

                RedEyeConfessionManifest loaded =
                    JsonUtility.FromJson<
                        RedEyeConfessionManifest>(json);

                if (loaded == null)
                {
                    Debug.LogError(
                        "[脑叶武器扩展][数据] manifest.json 解析失败。");

                    return false;
                }

                if (loaded.entityId != DirectoryId)
                {
                    Debug.LogError(
                        "[脑叶武器扩展][数据] entityId 与目录编号不一致。" +
                        " 目录=" + DirectoryId +
                        "，配置=" + loaded.entityId);

                    return false;
                }

                if (string.IsNullOrWhiteSpace(
                        loaded.displayName) ||
                    string.IsNullOrWhiteSpace(
                        loaded.textPrefix))
                {
                    Debug.LogError(
                        "[脑叶武器扩展][数据] displayName 或 textPrefix 为空。");

                    return false;
                }

                manifest = loaded;
                result = loaded;
                loadSucceeded = true;

                Debug.Log(
                    "[脑叶武器扩展][数据] 已读取武器配置。" +
                    " ID=" + manifest.entityId +
                    "，名称=" + manifest.displayName +
                    "，文本前缀=" + manifest.textPrefix);

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展][数据] 读取 manifest.json 失败：" +
                    exception);

                return false;
            }
        }

        internal static string GetTextKey(string suffix)
        {
            if (TryLoad(out RedEyeConfessionManifest data))
            {
                return data.textPrefix + "_" + suffix;
            }

            return "WP372466_" + suffix;
        }
    }
}