# ATR-TTbundle 一键构建
#
# 用法：
#   .\scripts\build.ps1                      # 同时构建 Windows 与 Linux
#   .\scripts\build.ps1 -Rid win-x64         # 只构建指定平台
#   .\scripts\build.ps1 -KeepPdb             # 保留调试符号（默认不留）
#   .\scripts\build.ps1 -CwToolsDir <路径>    # 指定 CWTools 目录（默认用 vendor/，见下）
#
# 前置条件：CWTools 2.39.0 不在 NuGet 上（最高只有 0.6.0-alpha，实测不认 Stellaris 4.x
# 的 `key? =` 可选赋值，会整文件丢读巨构与 Force of Will 科技）。
# 程序集已随仓库放在 vendor/cwtools-2.39.0/，无需额外准备；
# 只有该目录缺失时才回退到本机 VS Code 扩展目录。
#
# 产出：
#   dist/<rid>/            可直接分发的目录，结构为「启动器在外层、程序本体在 app/」
#   dist/atr-ttbundle-<rid>.zip   上面目录的压缩包（解压后得到一个同名文件夹）
#
# 为什么程序本体放子文件夹：自包含发布会产生 200+ 个 dll，
# 全铺在外层会让玩家找不到该双击哪个文件。

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

# CWTools 前置检查：优先用仓库自带的 vendor 目录，缺失时回退到本机扩展
if ([string]::IsNullOrWhiteSpace($CwToolsDir)) {
    $vendored = Join-Path $root 'vendor/cwtools-2.39.0'
    if (Test-Path (Join-Path $vendored 'CWTools.dll')) {
        $CwToolsDir = $vendored
    } else {
        $CwToolsDir = Join-Path $env:USERPROFILE '.vscode/extensions/foreverskywalker.foreverskywalker-stellaris-cwtools-2.39.0/bin/server/win-x64'
    }
}

if (-not (Test-Path (Join-Path $CwToolsDir 'CWTools.dll'))) {
    throw @"
找不到 CWTools.dll：$CwToolsDir

正常情况下应使用仓库自带的 vendor/cwtools-2.39.0 目录。
若该目录缺失，可从 VS Code 扩展 foreverskywalker.foreverskywalker-stellaris-cwtools
（2.39.0）的 bin/server/win-x64/ 重新拷贝，或用 -CwToolsDir 指向一份手动拷贝。
"@
}

Write-Host "CWTools : $CwToolsDir"
Write-Host ""

$publishArgs = @($project, '-c', 'Release')

foreach ($runtime in $Rid) {
    # 目录名 = 压缩包名，这样解压后直接得到一个同名文件夹，
    # 不会出现「解压出一堆文件散落一地」或「解压出 win-x64 这种看不懂的名字」
    $outDir = Join-Path $root "dist/atr-ttbundle-$runtime"
    $appDir = Join-Path $outDir 'app'
    Write-Host ""
    Write-Host "=== 构建 $runtime → dist/atr-ttbundle-$runtime ===" -ForegroundColor Cyan

    if (Test-Path $outDir) {
        Remove-Item -Recurse -Force $outDir
    }

    # 程序本体（含 200+ 个依赖 dll）先发布到 app/，再往下整理
    & $dotnet publish $project -c Release -r $runtime -o $appDir "-p:CwToolsDir=$CwToolsDir"
    if ($LASTEXITCODE -ne 0) {
        throw "构建失败：$runtime（退出码 $LASTEXITCODE）"
    }

    # 外层：给人看的文档与配置示例
    Copy-Item (Join-Path $root 'README.md') $outDir -Force
    Copy-Item (Join-Path $root 'LICENSE') $outDir -Force
    Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.md') $outDir -Force
    Copy-Item (Join-Path $root 'config/atr-ttbundle.example.toml') $outDir -Force
    # CWTools 的 MIT 声明，随二进制一起分发（vendor 目录里那份）
    $cwtoolsLicense = Join-Path $CwToolsDir 'LICENSE.cwtools.txt'
    if (Test-Path $cwtoolsLicense) {
        Copy-Item $cwtoolsLicense $appDir -Force
    }

    # 启动器放外层：玩家只需看到这一个可双击的文件
    if ($runtime -like 'win-*') {
        # 注意：bat 按 ASCII 写出（cmd 默认代码页是 936/GBK，写 UTF-8 中文会乱码），
        # 因此注释一律用英文，避免出现问号方块
        $bat = @'
@echo off
rem ATR-TTbundle launcher
rem Program files live in app\; config file is app\atr-ttbundle.toml
cd /d "%~dp0"
start "" "app\atr-ttbundle.exe" --ui
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
# 程序本体在 app/ 下；配置写在 app/atr-ttbundle.toml
cd "$(dirname "$0")" || exit 1
chmod +x ./app/atr-ttbundle 2>/dev/null
./app/atr-ttbundle --ui
'@
        # sh 必须是 LF
        $sh = ($sh -replace "`r`n", "`n")
        [System.IO.File]::WriteAllText(
            (Join-Path $outDir 'start.sh'), $sh,
            [System.Text.UTF8Encoding]::new($false)
        )
    }

    if (-not $KeepPdb) {
        Get-ChildItem $appDir -Filter '*.pdb' -ErrorAction SilentlyContinue |
            Remove-Item -Force
    }

    # 打成 zip：压缩包内带顶层文件夹（dist/atr-ttbundle-<rid>/），
    # 解压后得到一个干净的同名目录，不会散落一地
    $zipPath = Join-Path $root "dist/atr-ttbundle-$runtime.zip"
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }
    Compress-Archive -Path $outDir -DestinationPath $zipPath -CompressionLevel Optimal

    $size = [math]::Round(((Get-ChildItem $outDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
    $zipSize = [math]::Round(((Get-Item $zipPath).Length / 1MB), 1)
    Write-Host ("完成：{0} 个文件，{1} MB（压缩包 {2} MB）" -f `
        (Get-ChildItem $outDir -Recurse -File).Count, $size, $zipSize) -ForegroundColor Green
}

Write-Host ""
Write-Host "全部完成。" -ForegroundColor Green
