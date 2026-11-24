# ATR-TTbundle 一键构建
#
# 用法：
#   .\scripts\build.ps1                      # 同时构建 Windows 与 Linux
#   .\scripts\build.ps1 -Rid win-x64         # 只构建指定平台
#   .\scripts\build.ps1 -KeepPdb             # 保留调试符号（默认不留）
#   .\scripts\build.ps1 -CwToolsDir <路径>    # 指定 CWTools 目录（默认取 VS Code 扩展自带的）
#
# 前置条件：CWTools 2.39.0 不在 NuGet 上（最高只有 0.6.0-alpha，实测不认 Stellaris 4.x
# 的 `key? =` 可选赋值，会整文件丢读巨构与 Force of Will 科技），因此改为引用
# VS Code 扩展 foreverskywalker.foreverskywalker-stellaris-cwtools 自带的私有构建。
# 构建机需安装该扩展（版本 2.39.0），或用 -CwToolsDir 指向一份拷贝。
#
# 产出：dist/<rid>/ 下一个可直接分发的目录：
#   atr-ttbundle(.exe) + 一键启动.bat / start.sh + README.md + 配置示例

param(
    [string[]]$Rid = @('win-x64', 'linux-x64'),
    [switch]$KeepPdb,
    [string]$CwToolsDir = ''
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src/atr-ttbundle.csproj'

# 定位自带 SDK（Windows 版 dotnet.exe 与 Linux 版 dotnet 都兼容）
$dotnet = Join-Path $root 'toolchain/dotnet/dotnet.exe'
if (-not (Test-Path $dotnet)) {
    $dotnet = Join-Path $root 'toolchain/dotnet/dotnet'
}

if (-not (Test-Path $dotnet)) {
    throw "找不到 .NET SDK：$dotnet`n先运行 scripts/dotnet-install.ps1 安装到 toolchain/dotnet"
}

Write-Host "SDK     : $dotnet"
Write-Host "项目    : $project"

# CWTools 前置检查：缺了会在编译期报「找不到 CWTools.dll」，提前给出可读提示
if ([string]::IsNullOrWhiteSpace($CwToolsDir)) {
    $CwToolsDir = Join-Path $env:USERPROFILE '.vscode/extensions/foreverskywalker.foreverskywalker-stellaris-cwtools-2.39.0/bin/server/win-x64'
}

if (-not (Test-Path (Join-Path $CwToolsDir 'CWTools.dll'))) {
    throw @"
找不到 CWTools.dll：$CwToolsDir

CWTools 需取自 VS Code 扩展 foreverskywalker.foreverskywalker-stellaris-cwtools（2.39.0）：
  1. 在 VS Code 里安装该扩展；或
  2. 用 -CwToolsDir 指向一份手动拷贝的目录
"@
}

Write-Host "CWTools : $CwToolsDir"
Write-Host ""

$publishArgs = @($project, '-c', 'Release')

foreach ($runtime in $Rid) {
    $outDir = Join-Path $root "dist/$runtime"
    Write-Host ""
    Write-Host "=== 构建 $runtime → dist/$runtime ===" -ForegroundColor Cyan

    if (Test-Path $outDir) {
        Remove-Item -Recurse -Force $outDir
    }

    & $dotnet publish $project -c Release -r $runtime -o $outDir "-p:CwToolsDir=$CwToolsDir"
    if ($LASTEXITCODE -ne 0) {
        throw "构建失败：$runtime（退出码 $LASTEXITCODE）"
    }

    # 分发目录里的公共文件
    Copy-Item (Join-Path $root 'README.md') $outDir -Force
    Copy-Item (Join-Path $root 'LICENSE') $outDir -Force
    Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.md') $outDir -Force
    Copy-Item (Join-Path $root 'config/atr-ttbundle.example.toml') $outDir -Force

    # 启动器就地生成，不再维护模板文件
    if ($runtime -like 'win-*') {
        $bat = @'
@echo off
rem ATR-TTbundle launcher
cd /d "%~dp0"
start "" "atr-ttbundle.exe" --ui
exit
'@
        # bat 必须是 CRLF
        $bat = ($bat -replace "`r?`n", "`r`n")
        [System.IO.File]::WriteAllText(
            (Join-Path $outDir '一键启动.bat'), $bat,
            [System.Text.Encoding]::ASCII
        )
    } else {
        $sh = @'
#!/bin/sh
# ATR-TTbundle launcher
cd "$(dirname "$0")" || exit 1
chmod +x ./atr-ttbundle 2>/dev/null
./atr-ttbundle --ui
'@
        # sh 必须是 LF
        $sh = ($sh -replace "`r`n", "`n")
        [System.IO.File]::WriteAllText(
            (Join-Path $outDir 'start.sh'), $sh,
            [System.Text.UTF8Encoding]::new($false)
        )
    }

    if (-not $KeepPdb) {
        Get-ChildItem $outDir -Filter '*.pdb' -ErrorAction SilentlyContinue |
            Remove-Item -Force
    }

    $size = [math]::Round(((Get-ChildItem $outDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
    Write-Host ("完成：{0} 个文件，{1} MB" -f (Get-ChildItem $outDir -File).Count, $size) -ForegroundColor Green
}

Write-Host ""
Write-Host "全部完成。" -ForegroundColor Green
