# 脑叶武器扩展

《Sephiria》的原生 AddOns C# 模组，不依赖 Sephiria ModMaker。

## 当前版本：0.1.0（第一阶段原型）

当前先验证最基础的原生模组链路：

- 使用游戏自带的 AddOns 加载器；
- 通过 `metadata.json` 加载 `LobCorpWeaponExpansion.LobCorpWeaponExpansionMod`；
- 监听游戏本地化与武器数据库初始化事件；
- 尝试复制 ID 为 `0` 的原版剑盾预制体，注册一把名称为“赤瞳&忏悔”的武器，并将它加入原版剑盾的常规强化列表；
- 为复制的预制体分配一个当前 Mirror 客户端字典中未占用的 AssetId；
- 暂时沿用原版剑盾图标和模型，不改变攻击、攻速、格挡或 MP 消耗。

**这只是加载与武器注册原型，尚未在实际游戏中验证，也不是最终玩法版本。** 若游戏版本中的武器 ID、AddOn API 或网络预制体规则有变化，日志会显示注册失败的原因；请先验证加载和注册结果，再加入自动格挡与特殊攻击。

## 环境要求

- Windows；
- 已安装《Sephiria》；
- .NET SDK，能运行 `dotnet build`；
- 可访问 NuGet 以还原 Harmony 依赖。

## 构建

在 PowerShell 中进入项目目录，然后执行：

```powershell
.\Build.ps1 -GameDir "D:\Steam\steamapps\common\Sephiria"
```

如果游戏装在其他路径，把参数换成实际安装目录。也可以设置 `SEPHIRIA_GAME_DIR` 环境变量。

本项目目录本身就应位于游戏的 AddOns 目录内，例如：

```text
D:\Steam\steamapps\common\Sephiria\AddOns\脑叶武器扩展\
```

运行 `Build.ps1` 后，脚本会把以下运行文件直接复制到当前项目目录（不会再套一层 `dist\AddOns`）：

```text
脑叶武器扩展\
├── metadata.json
├── LobCorpWeaponExpansion.dll
└── 0Harmony.dll
```

然后启动游戏、进入一次存档。AddOn 通常在游戏会话加载阶段运行；查看游戏日志中的 `[脑叶武器扩展]` 消息以确认是否加载。

## 下一阶段

1. 确认 AddOn 成功加载并能注册“赤瞳&忏悔”；
2. 加入 +15% 攻击速度；
3. 根据 `WeaponControllerSimple` 与 `WeaponSimple_SwordAndShield` 的实际行为实现攻击期间自动格挡；
4. 做第二阶段武器、能量累计、护盾值和「碎颅」特殊攻击。
