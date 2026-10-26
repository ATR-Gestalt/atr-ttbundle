# 第三方组件许可声明（THIRD-PARTY NOTICES）

ATR-TTbundle 自身代码以 MIT 许可证发布（见 [LICENSE](LICENSE)）。
本程序是自包含单文件发布，二进制中打包了下列第三方组件。按各组件许可证要求，
在此列出名称、版本与许可证类型。

> 完整许可证文本可在对应 NuGet 包内（`<包名>.nuspec` 同级目录）或下列项目主页查阅。

## 一、运行时打包的组件

以下组件会随 `atr-ttbundle` 二进制一起分发：

| 组件 | 版本 | 许可证 | 项目主页 |
|---|---|---|---|
| CWTools | 0.5.0-alpha | MIT | https://github.com/cwtools/cwtools |
| DotNet.Glob | 3.1.3 | MIT | https://github.com/dazinator/DotNet.Glob |
| FParsec | 1.0.4-RC3 | BSD-2-Clause | https://www.quanttec.com/fparsec/ |
| FSharp.Core | 10.1.401 | MIT | https://github.com/dotnet/fsharp |
| FSharp.Collections.ParallelSeq | 1.2.0 | Apache-2.0 | https://github.com/fsprojects/FSharp.Collections.ParallelSeq |
| FSharp.Data | 3.0.1 | Apache-2.0 | https://github.com/fsprojects/FSharp.Data |
| FSharpx.Collections | 2.0.0 | Apache-2.0 | https://github.com/fsprojects/FSharpx.Collections |
| Microsoft.Data.Sqlite | 10.0.12 | MIT | https://github.com/dotnet/efcore |
| Microsoft.EntityFrameworkCore | 10.0.12 | MIT | https://github.com/dotnet/efcore |
| Microsoft.EntityFrameworkCore.Abstractions | 10.0.12 | MIT | https://github.com/dotnet/efcore |
| Microsoft.EntityFrameworkCore.Relational | 10.0.12 | MIT | https://github.com/dotnet/efcore |
| Microsoft.EntityFrameworkCore.Sqlite.Core | 10.0.12 | MIT | https://github.com/dotnet/efcore |
| Microsoft.Extensions.Caching.Abstractions | 10.0.12 | MIT | https://github.com/dotnet/runtime |
| Microsoft.Extensions.Caching.Memory | 10.0.12 | MIT | https://github.com/dotnet/runtime |
| Microsoft.Extensions.Configuration.Abstractions | 10.0.12 | MIT | https://github.com/dotnet/runtime |
| Microsoft.Extensions.DependencyInjection | 10.0.12 | MIT | https://github.com/dotnet/runtime |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.12 | MIT | https://github.com/dotnet/runtime |
| Microsoft.Extensions.DependencyModel | 10.0.12 | MIT | https://github.com/dotnet/runtime |
| Microsoft.Extensions.Logging | 10.0.12 | MIT | https://github.com/dotnet/runtime |
| Microsoft.Extensions.Logging.Abstractions | 10.0.12 | MIT | https://github.com/dotnet/runtime |
| Microsoft.Extensions.Options | 10.0.12 | MIT | https://github.com/dotnet/runtime |
| Microsoft.Extensions.Primitives | 10.0.12 | MIT | https://github.com/dotnet/runtime |
| Sandwych.QuickGraph.Core | 1.0.0 | MS-PL | https://github.com/oldrev/Sandwych.QuickGraph |
| Spectre.Console | 0.57.2 | MIT | https://github.com/spectreconsole/spectre.console |
| Spectre.Console.Ansi | 0.57.2 | MIT | https://github.com/spectreconsole/spectre.console |
| Spectre.Console.Cli | 0.55.0 | MIT | https://github.com/spectreconsole/spectre.console |
| SQLitePCLRaw.batteries_v2 | 3.0.5 | Apache-2.0 | https://github.com/ericsink/SQLitePCL.raw |
| SQLitePCLRaw.core | 3.0.5 | Apache-2.0 | https://github.com/ericsink/SQLitePCL.raw |
| SQLitePCLRaw.provider.e_sqlite3 | 3.0.5 | Apache-2.0 | https://github.com/ericsink/SQLitePCL.raw |
| SourceGear.sqlite3（e_sqlite3 原生库） | 3.53.4 | SQLite 公有领域 | https://github.com/AaronRobinsonMSFT/SourceGear.sqlite3 |
| Tomlyn | 2.10.1 | BSD-2-Clause | https://github.com/xoofx/Tomlyn |
| VDS.Common | 2.0.0 | MIT | https://github.com/dotnetrdf/dotnetrdf |
| YamlDotNet | 18.1.0 | MIT | https://github.com/aaubry/YamlDotNet |

此外，自包含发布中还包含 **.NET 10 运行时**（`Microsoft.NETCore.App`），
其许可为 MIT，版权归 .NET Foundation 与 Microsoft 贡献者所有。

## 二、本项目基于的上游代码

| 项目 | 许可证 | 使用方式 |
|---|---|---|
| [Clazex/StlTechRelGen](https://github.com/Clazex/StlTechRelGen) | MIT | **代码主体 fork**；保留其 `Copyright (c) 2026 Clazex` |
| [codexvn/TechnologyTree](https://github.com/codexvn/stellaris-parser) | 仅参考 | 树形渲染的**表现形式**参考，未复制其代码 |

## 三、合规说明

1. **不含 copyleft 组件**：本项目曾依赖 `GameFinder`（GPL-3.0），因其会波及整体分发许可，
   已替换为零依赖的自行实现（`src/GameLocator.cs`）。当前依赖树中**无 GPL / LGPL / AGPL 组件**。
2. **MIT / BSD / MS-PL / Apache-2.0 均为宽松许可**，允许在保留声明的前提下闭源或开源分发。
3. Apache-2.0 组件如自带 `NOTICE` 文件，其内容随 NuGet 包分发，本清单不额外转述。
4. 本清单由 `dotnet list package --include-transitive` 与各包 `.nuspec` 的 `license` 字段
   逐项核对生成（2026-10-26）；升级依赖后应重新核对。
5. **开发方式声明**：本工具代码在 **DeepSeek** 辅助下编写（需求梳理、代码实现、
   依赖许可证审计、文档整理）。AI 辅助**未引入任何未列出的第三方代码**——
   `src/` 下的代码均为项目维护者审定的实现，外部代码仅限上表所列组件与第二节所列上游项目。

## 四、许可证全文

MIT、Apache-2.0、BSD-2-Clause、MS-PL 的完整文本均为通用版本，可在以下地址获取：

- MIT: https://opensource.org/license/mit
- Apache-2.0: https://www.apache.org/licenses/LICENSE-2.0
- BSD-2-Clause: https://opensource.org/license/bsd-2-clause
- MS-PL: https://opensource.org/license/ms-pl-html
