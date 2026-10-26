# ATR-TTbundle

把「科技树 MOD 的树形展示」与「科技关联 MOD 的标签引用」合并进**同一条科技描述**，
一个工具、一份本地化，不再需要同时装两个互相覆盖的 MOD。

- 可执行文件名：`atr-ttbundle`（Windows 为 `.exe`）
- 单文件、免安装，**玩家不需要装 .NET 运行时**
- 带图形界面（浏览器操作），也支持命令行

## 下载

预编译版本见 [Releases](https://github.com/ATR-Gestalt/atr-ttbundle/releases)：

| 平台 | 下载 | 使用 |
|---|---|---|
| Windows x64 | `atr-ttbundle-win-x64.zip` | 解压后双击 `一键启动.bat` |
| Linux x64 | `atr-ttbundle-linux-x64.zip` | 解压后运行 `./start.sh` |

附件 `SHA256SUMS.txt` 是各下载文件的**完整性校验值**（SHA-256）：下载后可用
`sha256sum -c SHA256SUMS.txt`（Linux/macOS）或 `Get-FileHash`（Windows）
核对压缩包是否完整、有没有下载坏；不核对也不影响使用。
`atr-ttbundle.example.toml` 是配置示例。

## 快速开始

### 方式一：图形界面（推荐）

| 平台 | 入口 |
|---|---|
| Windows | 双击 `一键启动.bat` |
| Linux | 运行 `./start.sh`（或 `./atr-ttbundle --ui`） |

界面会起一个只监听 `127.0.0.1` 的本地服务并自动打开浏览器，可以：

- 填游戏目录与用户数据目录
- 从启动器数据库里**下拉选择播放集与目标模组**
- 勾选要生成的语言、标签项、段落与树的画法
- 点「开始生成」并实时看日志

关闭服务：结束 `atr-ttbundle` 进程（Windows 可在任务管理器结束，或关掉控制台窗口）。

### 方式二：命令行

```bash
# 原版模式：只解析原版科技
./atr-ttbundle -g "<游戏目录>" -d "<文档目录>" -p '' -m "<目标模组目录>" --no-check-update

# 播放集模式：原版 + 播放集里所有 MOD 的科技
./atr-ttbundle -g "<游戏目录>" -d "<文档目录>" -p "<播放集名>" -m "<目标模组展示名>" --no-check-update
```

`-p` 传空字符串即原版模式，此时 `-m` 是一个**目录**；
播放集模式下 `-m` 是**模组展示名**（启动器里显示的名字）。

## 配置

所有可选项见 [`atr-ttbundle.example.toml`](atr-ttbundle.example.toml)。
命令行参数优先于配置文件。

配置分段：

| 段 | 作用 |
|---|---|
| `[game]` | 游戏安装目录与用户数据目录 |
| `[data]` | 数据范围：`mode`（vanilla/playset）、`playset`、`source_mod`、`include_vanilla`、三个排除开关 |
| `[output]` | 输出目标：`target_mod`（MOD 展示名）/ `target_dir`（目录）、文件名前缀、语言、片段 |
| `[labels]` | 领域图标、T 级、T0 文本、类别、稀有/危险/可重复、成本、着色、分隔符 |
| `[lists]` | 前置/解锁列表的标题、条数上限、是否带图标 |
| `[tree]` | 树方向（下游/上游/双向）、缩进样式与宽度、行数上限、标题、节点模板、截断文本、着色 |
| `[sections]` | 段落顺序：`requires` / `unlocks` / `tree` |

### 数据来源与输出目标是两件事

- **数据来源**由 `[data]` 决定：
  - `mode = "vanilla"`：只解析原版；
  - `mode = "playset"`：解析原版 + 播放集里的 MOD；`source_mod` 非空时只解析该 MOD；
  - `include_vanilla = false`：只生成播放集（某）MOD 的科技，原版科技只作为科技树节点被引用、不写入本地化。
- **输出目标**由 `[output]` 决定：`target_mod` 非空时写入播放集里该 MOD 的 `localisation/`；为空时写入 `target_dir` 目录。

常见组合：

| 数据来源 | 输出目标 | 配置 |
|---|---|---|
| 仅原版 | 目录 | `mode = "vanilla"` + `target_dir` |
| 仅原版 | 播放集里的 MOD | `mode = "vanilla"` + `playset` + `target_mod` |
| 原版 + 播放集全部 MOD | 播放集里的 MOD | `mode = "playset"` + `playset` + `target_mod` |
| 原版 + 播放集某个 MOD | 播放集里的 MOD | `mode = "playset"` + `playset` + `source_mod` + `target_mod` |
| 仅播放集某个 MOD | 播放集里的 MOD | 上一行 + `include_vanilla = false` |

> 旧配置的 `[playset].name` / `[playset].target` 仍会被读取并自动回退，保存一次后即迁移到新字段。

> 以上全部可选配置项在本地 Web 界面里都有对应控件（数据范围/来源 MOD/包含原版、输出目标 MOD/目录、
> 三个排除开关、树的标题/缩进/模板、标签的 T0 文本/分隔符/着色、列表的标题与条数等），
> 一般不需要手改 TOML。仅 `[update]` 的 `check_update` 属于兼容旧配置的保留项（工具已不执行更新检查）。

### 关于文件名与加载顺序

`[output].file_prefix` 默认 `zz_techtree`。`zz` 前缀是为了让文件在加载顺序里靠后，
从而覆盖原版与其它本地化 MOD。想让它优先级更高，把前缀改成更靠后的名字
（Paradox 按文件名字母序加载，越靠后越优先）。

### 关于冲突

本工具的输出是**替换型本地化**（`localisation/replace/`），会覆盖 `tech_<id>_desc`。
如果玩家同时装了其它改科技描述的 MOD，后加载者生效。
建议在 MOD 说明里告知玩家「本 MOD 与同类科技描述 MOD 二选一」。

## 从源码编译

需要 .NET SDK 10。本仓库自带一份在 `toolchain/dotnet/`（免安装、不污染系统）。

一句话构建双平台并打包 `dist/`：

```powershell
./scripts/build.ps1                  # Windows + Linux
./scripts/build.ps1 -Rid win-x64     # 只构建一个平台
./scripts/build.ps1 -KeepPdb         # 保留调试符号（默认不留）
```

脚本会完成「publish → 复制 README/LICENSE/配置示例/启动器 → 清理 pdb」，
`dist/` 完全由脚本生成，不要手工往里加文件。

手工 publish 也可以：

```bash
./toolchain/dotnet/dotnet publish src/atr-ttbundle.csproj -c Release -r win-x64 -o dist/win-x64
```

> SDK 由 `scripts/dotnet-install.ps1` 安装到 `toolchain/dotnet`；换机器要重装时跑该脚本。

## 仓库结构

```
atr-ttbundle/
├── README.md                 本文件（使用说明）
├── LICENSE                   本项目许可证（MIT）
├── THIRD-PARTY-NOTICES.md    第三方组件许可清单（分发时必须一起带）
├── src/                      C# 源码与项目文件
├── config/                   配置模板（构建时复制进 dist）
├── scripts/
│   ├── build.ps1             一键构建双平台并打包 dist
│   └── dotnet-install.ps1    重新安装自带 SDK 用
├── toolchain/dotnet/         自带 .NET SDK 10（仅编译期用）
├── dist/                     构建产物（每个平台一个可直接分发的目录）
└── output/target-mod/        本地试跑用的目标模组（生成结果落这里）
```

### 为什么这样分

- **`src/` 与 `dist/` 分离**：源码是人改的、产物是机器生成的。分开放可以避免误改产物，
  也方便 `dist/` 整个被 `.gitignore` 排除。
- **`config/` 放模板**：正式配置 `atr-ttbundle.toml` 由工具在运行时生成，模板只作为默认值参考；
  构建脚本负责把它复制进 `dist/`，避免两处维护。
- **`scripts/` 放自动化**：一条命令完成「编译 → 打包 → 复制文档 → 清理调试符号」，
  比手工敲 publish 更不容易漏步骤。
- **`toolchain/` 自带 SDK**：这叫 vendoring（自带依赖）。好处是换机器不用装 .NET SDK、
  编译结果可复现；代价是占地 700+ MB，所以不进版本库。
- **`output/` 只用来试跑**：工具生成时会**清空目标模组的整个 `localisation/`**，
  所以试参数时指向这里，确认效果后再复制进真正的 MOD。

## 已知限制

- **进度条不动**：上游用 Harmony 给 CWTools 挂进度回调，而 Harmony 在自包含单文件下会崩，
  这里已改为空实现，换取「免安装」。
- **游戏内尚未实测**：树形文本、概念引用悬停、图标显示需要进游戏确认。
- **`unicode` 缩进未验证**：`├─ └─` 是否被游戏字体正确渲染尚未确认，默认用 `ascii`（`|--`）。
- **`[labels].weight` 暂不支持**：科技权重不在输出范围内。

## 灵感来源与致谢

- **代码主体**：fork 自 [Clazex/StlTechRelGen](https://github.com/Clazex/StlTechRelGen)（MIT），
  保留原作者版权声明。
- **科技树渲染样式**：参考 [codexvn/TechnologyTree](https://github.com/codexvn/stellaris-parser)
  （Stellaris 工坊 MOD「TechnologyTree」配套解析器）的表现规则，未复制其代码。
- **前置/解锁提示样式**：参考 Stellaris 工坊 MOD「Tech Relations」（工坊 id 3491493772）。

## 开发说明

本工具在开发过程中使用了 **DeepSeek** 作为 AI 编程助手，辅助完成需求梳理、方案设计、
代码编写、依赖许可证审计与文档整理。所有设计决策与最终交付内容由项目维护者审定。

AI 辅助不改变代码的许可方式：本项目的许可证仍由维护者以 MIT 授予，
不引入任何额外的第三方代码或授权义务。

## 许可证与合规

- 本项目以 **MIT** 发布，见 [LICENSE](LICENSE)。
- 代码主体 fork 自 [Clazex/StlTechRelGen](https://github.com/Clazex/StlTechRelGen)（MIT），
  保留原作者版权声明；树形渲染的**表现形式**参考
  [codexvn/TechnologyTree](https://github.com/codexvn/stellaris-parser)，未复制其代码。
- 二进制中打包的第三方组件全部为**宽松许可**（MIT / Apache-2.0 / BSD-2-Clause / MS-PL / 公有领域），
  **不含任何 GPL / LGPL / AGPL 组件**。逐项清单见
  [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)，分发时请与 `LICENSE` 一并带上。
