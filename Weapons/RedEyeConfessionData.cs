using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;

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

    [Serializable]
    internal sealed class RedEyeConfessionTexts
    {
        public RedEyeConfessionTextEntry[] entries;
    }

    [Serializable]
    internal sealed class RedEyeConfessionTextEntry
    {
        public string key;
        public string zhCN;
        public string zhTW;
        public string enUS;
        public string jaJP;
    }

    internal static class RedEyeConfessionData
    {
        internal const int DirectoryId = 372466;

        private static bool loadAttempted;
        private static bool loadSucceeded;
        private static bool textsLoadAttempted;
        private static bool textsLoadSucceeded;
        private static RedEyeConfessionTexts texts;

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

                Debug.Log(
    "[脑叶武器扩展][数据] 实际读取路径：" +
    manifestPath);

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

                // 兼容嵌套 art 对象未被整体反序列化的情况。
                if (loaded.art == null &&
                    TryExtractJsonObject(json, "art", out string artJson))
                {
                    loaded.art =
                        JsonUtility.FromJson<RedEyeConfessionArt>(artJson);

                    if (loaded.art != null)
                    {
                        Debug.Log(
                            "[脑叶武器扩展][数据] 已单独解析 art 对象。" +
                            " icon=" + loaded.art.icon +
                            "，mainHammer=" + loaded.art.mainHammer +
                            "，offhandHammer=" + loaded.art.offhandHammer);
                    }
                }

                if (loaded.art == null)
                {
                    Debug.LogError(
                        "[脑叶武器扩展][数据] art 对象解析失败，取消加载配置。");

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
    "，文本前缀=" + manifest.textPrefix +
    "，art=" + (manifest.art == null
        ? "null"
        : "非空"));

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

        internal static bool RegisterTexts(
    HorayModLocalizationContext context)
        {
            if (context == null)
                return false;

            if (!TryLoadTexts(out RedEyeConfessionTexts data))
                return false;

            if (data.entries == null)
            {
                Debug.LogError(
                    "[脑叶武器扩展][文本] texts.json 缺少 entries。");
                return false;
            }

            bool hasName = false;
            bool hasFlavor = false;

            foreach (RedEyeConfessionTextEntry entry in data.entries)
            {
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.key))
                {
                    continue;
                }

                if (entry.key == "Name")
                    hasName = true;

                if (entry.key == "Flavor")
                    hasFlavor = true;
            }

            if (!hasName || !hasFlavor)
            {
                Debug.LogError(
                    "[脑叶武器扩展][文本] texts.json 必须包含 Name 和 Flavor。");
                return false;
            }

            foreach (RedEyeConfessionTextEntry entry in data.entries)
            {
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.key))
                {
                    continue;
                }

                string localizationKey = GetTextKey(entry.key);

                AddTextIfPresent(
                    context, "zh-CN", localizationKey, entry.zhCN);

                AddTextIfPresent(
                    context, "zh-TW", localizationKey, entry.zhTW);

                AddTextIfPresent(
                    context, "en-US", localizationKey, entry.enUS);

                AddTextIfPresent(
                    context, "ja-JP", localizationKey, entry.jaJP);
            }

            Debug.Log(
                "[脑叶武器扩展][文本] 已注册 texts.json 中的本地化条目。");

            return true;
        }

        private static void AddTextIfPresent(
            HorayModLocalizationContext context,
            string language,
            string key,
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            context.AddText(language, key, value);
        }

        private static bool TryLoadTexts(
    out RedEyeConfessionTexts result)
        {
            if (textsLoadAttempted)
            {
                result = texts;
                return textsLoadSucceeded;
            }

            textsLoadAttempted = true;
            result = null;

            try
            {
                string assemblyPath =
                    typeof(LobCorpWeaponExpansionMod).Assembly.Location;

                string modDirectory =
                    Path.GetDirectoryName(assemblyPath);

                string textsPath = Path.Combine(
                    modDirectory,
                    "Weapons",
                    DirectoryId.ToString(),
                    "texts.json");

                Debug.Log(
                    "[脑叶武器扩展][文本] 实际读取路径：" +
                    textsPath);

                if (!File.Exists(textsPath))
                {
                    Debug.LogError(
                        "[脑叶武器扩展][文本] 找不到 texts.json：" +
                        textsPath);

                    return false;
                }

                string json = File.ReadAllText(textsPath);

                // 不再一次性反序列化整个 entries 数组。
                // 先提取数组，再逐个解析每个文本条目。
                if (!TryExtractJsonArray(
                        json,
                        "entries",
                        out string entriesJson))
                {
                    Debug.LogError(
                        "[脑叶武器扩展][文本] 无法提取 entries 数组。");

                    return false;
                }

                if (!TryExtractJsonObjectsFromArray(
                        entriesJson,
                        out List<string> entryObjects))
                {
                    Debug.LogError(
                        "[脑叶武器扩展][文本] entries 中没有可解析的对象。");

                    return false;
                }

                List<RedEyeConfessionTextEntry> parsedEntries =
                    new List<RedEyeConfessionTextEntry>();

                foreach (string entryJson in entryObjects)
                {
                    RedEyeConfessionTextEntry entry =
                        JsonUtility.FromJson<RedEyeConfessionTextEntry>(
                            entryJson);

                    if (entry == null ||
                        string.IsNullOrWhiteSpace(entry.key))
                    {
                        Debug.LogWarning(
                            "[脑叶武器扩展][文本] 跳过无效的文本条目：" +
                            entryJson);

                        continue;
                    }

                    parsedEntries.Add(entry);

                    Debug.Log(
                        "[脑叶武器扩展][文本] 已解析条目：" +
                        entry.key);
                }

                if (parsedEntries.Count == 0)
                {
                    Debug.LogError(
                        "[脑叶武器扩展][文本] 没有成功解析任何文本条目。");

                    return false;
                }

                RedEyeConfessionTexts loaded =
                    new RedEyeConfessionTexts
                    {
                        entries = parsedEntries.ToArray()
                    };

                texts = loaded;
                result = loaded;
                textsLoadSucceeded = true;

                Debug.Log(
                    "[脑叶武器扩展][文本] texts.json 解析成功。" +
                    " 条目数=" + loaded.entries.Length);

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展][文本] 读取 texts.json 失败：" +
                    exception);

                return false;
            }
        }
        private static bool TryExtractJsonArray(
    string json,
    string propertyName,
    out string arrayJson)
        {
            arrayJson = null;

            if (string.IsNullOrEmpty(json) ||
                string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            string token = "\"" + propertyName + "\"";

            int keyIndex = json.IndexOf(
                token,
                StringComparison.Ordinal);

            if (keyIndex < 0)
                return false;

            int colonIndex = json.IndexOf(
                ':',
                keyIndex + token.Length);

            if (colonIndex < 0)
                return false;

            int start = colonIndex + 1;

            while (start < json.Length &&
                   char.IsWhiteSpace(json[start]))
            {
                start++;
            }

            if (start >= json.Length || json[start] != '[')
                return false;

            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];

                if (inString)
                {
                    if (escaped)
                        escaped = false;
                    else if (c == '\\')
                        escaped = true;
                    else if (c == '"')
                        inString = false;

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                }
                else if (c == '[')
                {
                    depth++;
                }
                else if (c == ']')
                {
                    depth--;

                    if (depth == 0)
                    {
                        arrayJson = json.Substring(
                            start,
                            i - start + 1);

                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryExtractJsonObjectsFromArray(
            string arrayJson,
            out List<string> objects)
        {
            objects = new List<string>();

            if (string.IsNullOrEmpty(arrayJson))
                return false;

            bool inString = false;
            bool escaped = false;
            int depth = 0;
            int objectStart = -1;

            for (int i = 0; i < arrayJson.Length; i++)
            {
                char c = arrayJson[i];

                if (inString)
                {
                    if (escaped)
                        escaped = false;
                    else if (c == '\\')
                        escaped = true;
                    else if (c == '"')
                        inString = false;

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                }
                else if (c == '{')
                {
                    if (depth == 0)
                        objectStart = i;

                    depth++;
                }
                else if (c == '}')
                {
                    depth--;

                    if (depth < 0)
                        return false;

                    if (depth == 0 && objectStart >= 0)
                    {
                        objects.Add(
                            arrayJson.Substring(
                                objectStart,
                                i - objectStart + 1));

                        objectStart = -1;
                    }
                }
            }

            return depth == 0 &&
                   !inString &&
                   objects.Count > 0;
        }
        private static bool TryExtractJsonObject(
    string json,
    string propertyName,
    out string objectJson)
        {
            objectJson = null;

            if (string.IsNullOrEmpty(json) ||
                string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            string propertyToken = "\"" + propertyName + "\"";

            int keyIndex = json.IndexOf(
                propertyToken,
                StringComparison.Ordinal);

            if (keyIndex < 0)
                return false;

            int colonIndex = json.IndexOf(
                ':',
                keyIndex + propertyToken.Length);

            if (colonIndex < 0)
                return false;

            int start = colonIndex + 1;

            while (start < json.Length &&
                   char.IsWhiteSpace(json[start]))
            {
                start++;
            }

            if (start >= json.Length || json[start] != '{')
                return false;

            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];

                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                }
                else if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;

                    if (depth == 0)
                    {
                        objectJson = json.Substring(
                            start,
                            i - start + 1);

                        return true;
                    }
                }
            }

            return false;
        }

        internal static bool TryGetArtPath(
    string artKey,
    out string path)
        {
            path = null;

            if (!TryLoad(out RedEyeConfessionManifest data))
            {
                Debug.LogError(
                    "[脑叶武器扩展][贴图] manifest.json 加载失败。" +
                    " 请检查前面的 [数据] 日志。");
                return false;
            }

            if (data.art == null)
            {
                Debug.LogError(
                    "[脑叶武器扩展][贴图] manifest.json 已加载，" +
                    "但 art 解析结果为 null。" +
                    " 请检查实际读取的文件路径和 JSON 解析结果。");
                return false;
            }

            string relativePath;

            switch (artKey)
            {
                case "icon":
                    relativePath = data.art.icon;
                    break;

                case "mainHammer":
                    relativePath = data.art.mainHammer;
                    break;

                case "offhandHammer":
                    relativePath = data.art.offhandHammer;
                    break;

                default:
                    Debug.LogError(
                        "[脑叶武器扩展][贴图] 未知贴图配置项：" +
                        artKey);
                    return false;
            }

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                Debug.LogError(
                    "[脑叶武器扩展][贴图] 配置项为空：" +
                    artKey);
                return false;
            }

            try
            {
                string assemblyPath =
                    typeof(LobCorpWeaponExpansionMod)
                        .Assembly.Location;

                string modDirectory =
                    Path.GetDirectoryName(assemblyPath);

                string dataDirectory = Path.GetFullPath(
                    Path.Combine(
                        modDirectory,
                        "Weapons",
                        DirectoryId.ToString()));

                string fullPath = Path.GetFullPath(
                    Path.Combine(dataDirectory, relativePath));

                string rootPrefix =
                    dataDirectory.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;

                if (!fullPath.StartsWith(
                        rootPrefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError(
                        "[脑叶武器扩展][贴图] 贴图路径不能超出武器数据目录：" +
                        relativePath);
                    return false;
                }

                if (!File.Exists(fullPath))
                {
                    Debug.LogError(
                        "[脑叶武器扩展][贴图] 找不到贴图文件：" +
                        fullPath);
                    return false;
                }

                path = fullPath;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[脑叶武器扩展][贴图] 解析贴图路径失败：" +
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