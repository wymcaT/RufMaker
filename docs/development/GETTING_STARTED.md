# RufMaker 开发入口

这份文档用于首次接手和换代理；长期约束见根目录 `AGENTS.md`，当前进度见 [协作状态](../collaboration/STATUS.md)。适用于当前 Windows 仓库，不要求读历史聊天。

## 1. 先确认自己在哪个版本上

在仓库根目录执行：

```powershell
git status --short --branch
git log -3 --oneline
git diff --stat
.\runtime\node.exe --version
Test-Path -LiteralPath "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
```

当前功能基线是本地分支 `codex/bullet-control-beta`，功能提交 `88853a8`；后续文档或修复提交可能更新 HEAD。以实际命令输出为准，不为了对齐本文擅自 checkout。`projects/` 下已有本地编辑内容，见协作状态。

本地 Beta 尚未推送。从 GitHub 只取得 `main` 的工作区不一定具备这个基线，应先确认代码来源，不能把稳定分支当成已有 Beta 实现。

## 2. 技术栈与版本来源

- C# WinForms/GDI+，目标 x86，系统 .NET Framework 编译器；项目使用显式源码列表，没有必须安装的 .NET SDK/NuGet 工程。
- Node.js ESM，使用随库 `runtime/node.exe`，当前本机检查为 v20.18.0。后台以进程管道通信；不需要开发网站、端口或 `npm install`。
- GM8.2 用于原生编译/试玩。只做桌面构建与大部分测试时可以没有它。通过编辑器“引擎设置”配置，或为命令行指定 `RUFMAKER_GAMEMAKER`。
- 应用版本查 `desktop/Updater.cs`，不是 `studio/package.json`。当前数字版本为 `1.12.0`，ProductVersion 为 `1.12.0-beta.1`。
- `RufMaker.Beta.exe` 是本机生成的测试编辑器；根目录 `RufMaker.exe` 仍是此前 1.11.0 的二进制，不能拿它验证新增 Beta UI。

## 3. 构建与启动

先在自己的代理记录里登记构建输出占用，再构建：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Build-Beta.ps1
```

产物为根目录 `RufMaker.Beta.exe`。启动它时保留旁边的 `runtime/`、`studio/`、`assets/` 和 `templates/`。EXE 依赖这些随附文件，不是可以单独搬走的单文件应用。

只检查能否编译、又不想写共同的根目录 EXE，可指定自己的构建路径：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Build-Native.ps1 -Output D:\rufmaker\build\argon4\RufMaker.compile-check.exe
```

这个自定义位置的 EXE 是编译检查产物；直接启动会把它所在目录当程序根目录。需要运行测试编辑器时使用根目录的完整布局。

Beta 会单独保存 `.rufmaker-beta-view.json`，首次可继承原布局；语言和引擎配置仍在 `user-data/settings.json`。新行为还没有旧版的无损回写保证，验证功能请复制工程。

## 4. 按修改范围选择验证

### 已知不需要 GM 的后台检查

```powershell
.\runtime\node.exe --test studio/bullet-system.test.mjs studio/random-parameters.test.mjs studio/player-expressions.test.mjs studio/presets.test.mjs studio/style-presets.test.mjs studio/gml.test.mjs studio/native-compositor.test.mjs
```

这组在功能基线上有 18 个通过的检查。不要把所有 `*.test.mjs` 都当成纯单元测试：例如 `timeline-regression.test.mjs` 还复制工程并调用编译流程。先读脚本，按任务选择。

### 对应桌面检查

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-BulletSystem.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-PlayerExpressions.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-Presets.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-Localization.ps1
```

不要求每轮全跑：行为/分组改动优先第一项；公式改动优先前两项；复制/预设改动加第三项；UI 文案与布局改动加第四项。其余 `Test-*.ps1` 按模块选择。部分测试会创建离屏原生控件并截图，不等于完整的交互试玩验收。

`Test-BulletSystem.ps1` 输出 `build/bullet-system-check/scene.json`、测试 EXE 和截图。数值导出接口属于这个测试 EXE：

```powershell
.\build\bullet-system-check\BulletSystem.tests.exe --sample INPUT.json FRAME OUTPUT.json
```

先从当前源码编译测试 EXE，再使用该接口；不要把 `--sample` 当成产品 EXE 的参数。`INPUT.json`、`FRAME`、`OUTPUT.json` 是需要替换的占位值。

### GM8.2 验证

改变弹幕行为或 GML 生成时，还要核对真实运行器结果。使用隔离工程，保留所需 `Data/` 和扩展。当前调用约定见 `studio/native.mjs`：工程入口在前，随后 `--build`、输出 EXE，编译工作目录设为 GM 安装目录。

**在首次动态导入 `studio/project.mjs` 前设置工程选择：**

```javascript
import fs from 'node:fs';
import path from 'node:path';
import {createProject} from './studio/project-files.mjs';
// 在仓库根目录运行的示意；脚本文件中的 import 路径按实际位置调整。
const root = process.cwd();
const scratch = path.join(root, 'build', 'agent-check');
fs.mkdirSync(scratch, {recursive: true});
const created = createProject(root, {
  name: 'isolated-check-' + Date.now(), parent: scratch,
  preset: 'floor', width: 800, height: 608, fps: 50
});
process.env.RUFMAKER_ROOT = root;
process.env.RUFMAKER_PROJECT = created.path;
const project = await import('./studio/project.mjs');
```

示例先创建父目录并选唯一工程名，避免覆盖已有夹具。`RUFMAKER_PROJECT` 可指向 `.rufmaker` 描述文件；不要依赖默认用户工程。原生编译需要已配置 GM；不要为运行非 GM 单元测试而联网安装引擎。

当前三次 GM 数值对照的详情和未入库工具问题，见协作状态。一次编译成功不能替代运行检查；运行时需检查错误日志、完成标记和实际数值。

## 5. 从哪里改

- UI/渲染：读 `desktop/AGENTS.md` 的入口表。
- 后台/GML/原生导入：读 `studio/AGENTS.md` 的入口表。
- 用户操作语义：读 [Beta 使用说明](../beta-bullet-control.md) 和根 README。
- 功能优先级：读 [耐久控制路线图](../research/avoidance-control-roadmap-2026-10-23.md)。路线图是候选设计，不是全部已实现的规格。
- 原有目录说明：[目录与开发说明](../目录与开发说明.md)。

新增状态字段时，一次核对：模型默认值、归一化、保存/读取、快照/撤销、复制 ID、预设资源、预览、GML、语言和定向用例。参数变化必须覆盖出生帧、阶段切换帧和倒拖；多人编辑时整项契约由一方负责或先约定接口。

## 6. 发布边界

Git 推送以根 `AGENTS.md` 为准。`.github/workflows/release.yml` 在推送 `v*` 标签或手动触发时发布正式 Release。

**当前 Beta 不能直接拿 `Package-Release.ps1` 当正式版发布。** 该脚本目前要求纯数字 Version，且检查 ProductVersion 与它相同；当前 ProductVersion 带 `-beta.1`。若要发 Beta 包，应先单独实现并验证预发布流程，再由用户明确授权发布。不要简单去掉 Beta 标识绕过校验。
