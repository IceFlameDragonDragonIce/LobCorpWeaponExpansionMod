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
    throw "找不到 Assembly-CSharp.dll。请检查 GameDir：$GameDir"
}

if (-not (Test-Path -LiteralPath (Join-Path $gameManaged '0Harmony.dll'))) {
    throw "找不到游戏内置的 0Harmony.dll：$gameManaged"
}

& dotnet build $projectFile -c $Configuration "-p:GameDir=$GameDir"

if ($LASTEXITCODE -ne 0) {
    throw "dotnet build 失败，退出代码：$LASTEXITCODE"
}

$assembly = Join-Path $projectRoot "bin\$Configuration\LobCorpWeaponExpansion.dll"

if (-not (Test-Path -LiteralPath $assembly)) {
    throw "构建完成后找不到 DLL：$assembly"
}

$deployRoot = Join-Path $GameDir 'WorkshopLocal\lobcorp-weapon-expansion'
$deployBin = Join-Path $deployRoot 'bin'
$weaponDataSource = Join-Path $projectRoot 'Weapons\372466'
$weaponDataTarget = Join-Path $deployBin 'Weapons\372466'

New-Item -ItemType Directory -Force -Path $deployBin | Out-Null
New-Item -ItemType Directory -Force -Path $weaponDataTarget | Out-Null

Copy-Item -LiteralPath $assembly `
    -Destination (Join-Path $deployBin 'LobCorpWeaponExpansion.dll') `
    -Force

Copy-Item -LiteralPath (Join-Path $projectRoot 'metadata.json') `
    -Destination (Join-Path $deployRoot 'metadata.json') `
    -Force

$assetFiles = @(
    'manifest.json',
    'texts.json',
    'icon.png',
    'main_hammer.png',
    'offhand_hammer.png'
)

foreach ($fileName in $assetFiles) {
    $source = Join-Path $weaponDataSource $fileName

    if (-not (Test-Path -LiteralPath $source)) {
        throw "缺少武器资源：$source"
    }

    Copy-Item -LiteralPath $source `
        -Destination (Join-Path $weaponDataTarget $fileName) `
        -Force
}

# 清理旧版部署遗留文件。
# 新版 DLL 只应位于 bin/ 中，且不应打包游戏自带的 Harmony。
$obsoletePaths = @(
    (Join-Path $deployRoot 'LobCorpWeaponExpansion.dll'),
    (Join-Path $deployRoot '0Harmony.dll'),
    (Join-Path $deployBin '0Harmony.dll')
)

foreach ($obsoletePath in $obsoletePaths) {
    if (Test-Path -LiteralPath $obsoletePath) {
        Remove-Item -LiteralPath $obsoletePath -Force
        Write-Host "已清理旧文件：$obsoletePath"
    }
}

Write-Host ''
Write-Host '构建与部署完成：' -ForegroundColor Green
Write-Host $deployRoot
Write-Host ''
Write-Host '已部署 metadata.json、DLL 和 Weapons/372466 运行资源。'
Write-Host '没有将游戏内置的 0Harmony.dll 打包进模组。'