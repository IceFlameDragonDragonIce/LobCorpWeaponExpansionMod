# 脑叶武器扩展

《Sephiria》的原生 C# DLL 模组，使用游戏官方模组加载机制，不依赖 Sephiria ModMaker。

## 当前版本：0.2.0

模组包含赤瞳&忏悔武器及其相关战斗机制，具体功能以当前源码和游戏内测试结果为准。

## 环境要求

- Windows
- 已安装《Sephiria》
- .NET SDK 8 或更高版本
- 支持官方 DLL 模组的游戏版本

## 构建与部署

在 PowerShell 中进入项目目录，执行：

```powershell
.\Build.ps1 -GameDir "D:\Steam\steamapps\common\Sephiria"