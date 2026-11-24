# CWTools 2.39.0（vendored）

本目录存放构建所需的 CWTools 运行时程序集，取自 VS Code 扩展
`foreverskywalker.foreverskywalker-stellaris-cwtools` 版本 `2.39.0`
的 `bin/server/win-x64/` 目录。

## 为什么不用 NuGet

NuGet 上 CWTools 最高只有 `0.6.0-alpha`。实测 `0.5.0-alpha` 与 `0.6.0-alpha`
都不认 Stellaris 4.x 的 `key? =` 可选赋值语法，遇到下面两个文件会整文件解析失败：

- `common/technology/00_megastructures.txt` 第 204 行 `space_owner? = {`
- `common/technology/00_forceofwill_tech.txt` 第 53 行 `ideology? = {`

结果是 12 个巨构科技与 4 个 Force of Will 科技全部缺席生成产物。
`2.39.0` 能正确解析这两个文件，但它只在扩展里私有打包，未发布到 NuGet。

## 许可证

CWTools 及其配套程序集均为 **MIT** 许可，版权归 Thomas Boby 与各位贡献者所有，
许可证全文见同目录的 `LICENSE.cwtools.txt`。

MIT 允许在保留版权声明与许可声明的前提下再分发二进制，
因此这里随项目一起携带这些程序集是合规的。

扩展整体（含 LSP、规则集等）另见扩展自身的 `LICENSE.md`；
本项目只取用 CWTools 运行时程序集，不包含扩展的其他部分。

## 文件清单

| 文件 | 来源 | 说明 |
|---|---|---|
| `CWTools.dll` | CWTools 本体 | 解析与校验核心 |
| `Shared.dll` | CWTools 2.39.0 | 新增的共享程序集 |
| `CSharpHelpers.dll` | CWTools 2.39.0 | 新增的 C# 互操作辅助 |
| `FParsec.dll` / `FParsecCS.dll` | FParsec 1.1.1 | 解析器组合子 |
| `FSharp.Core.dll` | FSharp.Core | F# 运行时 |
| `FSharp.Data.dll` | FSharp.Data 3.1.1 | CWTools 依赖 |
| `FSharp.Collections.ParallelSeq.dll` | 1.2.0 | 并行序列 |
| `FSharpx.Collections.dll` | 2.0.0 | 函数式集合 |
| `Sandwych.QuickGraph.Core.dll` | 1.0.0 | 图算法（依赖图） |
| `DotNet.Glob.dll` | 3.1.3 | 路径 glob |
| `VDS.Common.dll` | 3.0.0 | 字典/集合实现 |
| `FsPickler.dll` | 5.3.2 | 序列化（缓存） |
| `ICSharpCode.SharpZipLib.dll` | 1.4.2 | 压缩包处理 |
| `KTrie.dll` | 1.0.0 | 前缀树 |
| `ZString.dll` | 2.6.0 | 零分配字符串 |

各组件许可详见项目根目录的 `THIRD-PARTY-NOTICES.md`。

## 更新方法

升级扩展后重新拷贝即可，然后同步修改 `src/atr-ttbundle.csproj` 里的
`CwToolsDir` 默认路径中的版本目录名：

```powershell
$src = "$env:USERPROFILE\.vscode\extensions\foreverskywalker.foreverskywalker-stellaris-cwtools-<版本>\bin\server\win-x64"
Get-ChildItem vendor\cwtools-2.39.0\*.dll | ForEach-Object { Copy-Item (Join-Path $src $_.Name) . -Force }
```

升级后务必重跑一次生成，并确认 `00_megastructures.txt` 里的巨构科技仍在产物中。