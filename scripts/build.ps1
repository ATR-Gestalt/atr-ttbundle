# ATR-TTbundle 一键构建
#
# 用法：
#   .\scripts\build.ps1                      # 同时构建 Windows 与 Linux
#   .\scripts\build.ps1 -Rid win-x64         # 只构建指定平台
#   .\scripts\build.ps1 -KeepPdb             # 保留调试符号（默认不留）
#
# 产出：dist/<rid>/ 下一个可直接分发的目录：
#   atr-ttbundle(.exe) + 一键启动.bat / start.sh + README.md + 配置示例

param(
    [string[]]$Rid = @('win-x64', 'linux-x64'),
    [switch]$KeepPdb
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

foreach ($runtime in $Rid) {
    $outDir = Join-Path $root "dist/$runtime"
    Write-Host ""
    Write-Host "=== 构建 $runtime → dist/$runtime ===" -ForegroundColor Cyan

    if (Test-Path $outDir) {
        Remove-Item -Recurse -Force $outDir
    }

    & $dotnet publish $project -c Release -r $runtime -o $outDir
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
