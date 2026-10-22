[CmdletBinding()]
param(
    [string]$GameDir = $env:SEPHIRIA_GAME_DIR,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $projectRoot 'LobCorpWeaponExpansion.csproj'

if ([string]::IsNullOrWhiteSpace($GameDir)) {
    $GameDir = 'D:\Steam\steamapps\common\Sephiria'
}
$gameManaged = Join-Path $GameDir 'Sephiria_Data\Managed'
if (-not (Test-Path -LiteralPath (Join-Path $gameManaged 'Assembly-CSharp.dll'))) {
    throw "找不到 Assembly-CSharp.dll。请使用 -GameDir 指定 Sephiria 安装目录。当前目录：$GameDir"
}

& dotnet build $projectFile -c $Configuration "-p:SephiriaGameDir=$GameDir"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build 失败，退出代码：$LASTEXITCODE"
}

$assembly = Join-Path $projectRoot "bin\$Configuration\netstandard2.1\LobCorpWeaponExpansion.dll"
if (-not (Test-Path -LiteralPath $assembly)) {
    throw "构建成功但找不到 DLL：$assembly"
}

$nugetRoot = if (-not [string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
    $env:NUGET_PACKAGES
} else {
    Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget\packages'
}
$harmony = Join-Path $nugetRoot 'lib.harmony\2.4.2\lib\net472\0Harmony.dll'
if (-not (Test-Path -LiteralPath $harmony)) {
    throw "找不到 Harmony 依赖：$harmony。请确认 NuGet 包已成功还原。"
}

# 此项目本身位于 <Sephiria安装目录>\AddOns\脑叶武器扩展，因此直接把 DLL 复制到项目目录。
# 不要再生成 dist\AddOns\脑叶武器扩展 的嵌套副本，以免部署到错误位置。
$deployFolder = $projectRoot
Copy-Item -LiteralPath $assembly -Destination (Join-Path $deployFolder 'LobCorpWeaponExpansion.dll') -Force
Copy-Item -LiteralPath $harmony -Destination (Join-Path $deployFolder '0Harmony.dll') -Force

Write-Host ''
Write-Host '构建完成。AddOn 文件已部署到：' -ForegroundColor Green
Write-Host $deployFolder
Write-Host '确认此目录同时包含 metadata.json、LobCorpWeaponExpansion.dll 和 0Harmony.dll。'
