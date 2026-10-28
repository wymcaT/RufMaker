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

当前正式分支为 `main`，已合入 `codex/bullet-control-beta`；测试版历史功能提交为 `88853a8`。具体功能版本和推送状态见 `desktop/Updater.cs` 与协作状态。以实际 Git 状态为准，不为了对齐本文擅自 checkout。`projects/` 下已有本地编辑内容，见协作状态。

弹幕控制、生成模板、整体形状及撤销修复均已进入正式版。接手仍需核对实际分支和版本，不用旧的 Beta EXE 验证新源码。

## 2. 技术栈与版本来源

- C# WinForms/GDI+，目标 x86，系统 .NET Framework 编译器；项目使用显式源码列表，没有必须安装的 .NET SDK/NuGet 工程。
- Node.js ESM，使用随库 `runtime/node.exe`，当前本机检查为 v20.18.0。后台以进程管道通信；不需要开发网站、端口或 `npm install`。
- GM8.2 用于原生编译/试玩。只做桌面构建与大部分测试时可以没有它。通过编辑器“引擎设置”配置，或为命令行指定 `RUFMAKER_GAMEMAKER`。
- 应用版本查 `desktop/Updater.cs`，不是 `studio/package.json`；本机产物与远程发行版可能处于不同版本，见协作状态。
- 根目录 `RufMaker.exe` 是正式编辑器；旧的忽略文件 `RufMaker.Beta.exe` 可能仍是历史产物，不作为正式入口。

## 3. 构建与启动

先在自己的代理记录里登记构建输出占用，再构建：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Build-Native.ps1 -Output D:\rufmaker\RufMaker.exe
```

产物为根目录 `RufMaker.exe`。启动它时保留旁边的 `runtime/`、`studio/`、`assets/` 和 `templates/`。EXE 依赖这些随附文件，不是可以单独搬走的单文件应用。

只检查能否编译、又不想写共同的根目录 EXE，可指定自己的构建路径：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Build-Native.ps1 -Output D:\rufmaker\build\argon4\RufMaker.compile-check.exe
```

这个自定义位置的 EXE 是编译检查产物；直接启动会把它所在目录当程序根目录。需要运行编辑器时使用根目录的完整布局。

正式版保存 `.rufmaker-view.json`，升级时可继承更新时间较新的 Beta 布局；语言和引擎配置在 `user-data/settings.json`。旧版无法无损回写新行为字段，验证回退兼容性请复制工程。

## 4. 按修改范围选择验证

### 已知不需要 GM 的后台检查

```powershell
.\runtime\node.exe --test studio/template-system.test.mjs studio/shapes.test.mjs studio/bullet-system.test.mjs studio/random-parameters.test.mjs studio/player-expressions.test.mjs studio/presets.test.mjs studio/style-presets.test.mjs studio/gml.test.mjs studio/native-compositor.test.mjs
```

这组在 1.12.0 集成检查中有 24 项通过。不要把所有 `*.test.mjs` 都当成纯单元测试：例如 `timeline-regression.test.mjs` 还复制工程并调用编译流程。先读脚本，按任务选择。

### 对应桌面检查

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-BulletSystem.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-PlayerExpressions.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-Presets.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-Localization.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-RandomTimeline.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-FormationShapes.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-TemplateSystem.ps1
pwsh -NoProfile -File desktop/Test-EditorPerformance.ps1
```

不要求每轮全跑：行为/分组改动优先第一项；公式改动优先前两项；复制/预设改动加第三项；UI 文案与布局改动加第四项。`Test-RandomTimeline.ps1` 包含撤销后的物件/关键帧/多选/房间实例选中恢复；`Test-FormationShapes.ps1` 检查形状几何、参数关键帧和预览/GML 参考坐标。其余 `Test-*.ps1` 按模块选择。部分测试会创建离屏原生控件并截图，不等于完整的交互试玩验收。

`Test-MotionEditing.ps1` 验证14种逐帧动作、参数面板/撤销、密集路径索引、同轨覆盖裁剪（含分组与共用模板）、手持拖动稳定和跨轨吸附。生成的隔离夹具在build/motion-editing-check；设置RUFMAKER_TEST_BUILD=motion-editing-check、RUFMAKER_TEST_SAMPLER=MotionEditing.tests.exe后，可复用desktop/tests/verify-generation-gml.mjs核对真实GM运行，含裁剪后的负时间原点和循环片段。参数仅用于测试，不是产品启动参数。

`Test-EditorPerformance.ps1` 检查属性表控件复用、重绑后的编辑/撤销、历史深复制与去重、保存后的房间比较，并记录合成场景的编辑/撤销耗时。`-ProfileOnly -Report NAME` 可单独记录数据，输出在 `build/editor-performance-check/`；绝对耗时不作为测试断言。此测试不测真实游戏 FPS。

`Test-BulletSystem.ps1` 输出 `build/bullet-system-check/scene.json`、测试 EXE 和截图。数值导出接口属于这个测试 EXE：

```powershell
.\build\bullet-system-check\BulletSystem.tests.exe --sample INPUT.json FRAME OUTPUT.json
```

先从当前源码编译测试 EXE，再使用该接口；不要把 `--sample` 当成产品 EXE 的参数。`INPUT.json`、`FRAME`、`OUTPUT.json` 是需要替换的占位值。

`Test-KeyframeWorkspace.ps1` 验证产品下栏只显示当前物件的标记、旧展开布局不再露出属性子行，以及右栏逐属性编辑区的选帧/框选/边界拖动/单次Undo/加删键/数值绑定/时间同步。包括多选、共用模板、自动采样路径和中英文窄栏；所有夹具与截图位于build/keyframe-workspace-check，不加载用户工程。

游标磁吸入口为PreviewPlayback.cs的CursorSnapping与CursorScrub：Capture在每次手势开始时收集歌曲边界/属性键/GM代码帧并排序一次，拖动时二分查找候选，松手才提交；没有停留自动吸附计时器。Alt隐藏候选并跳过松手吸附。TimelineRendering.cs缓存波形/频谱/刻度/轨道/关键帧静态层，PlayheadMoved和候选变化只标记窄条覆盖层；普通Invalidate、滚动、缩放、选择、数据、尺寸变化仍重建底图。拖动游标用独立的小数帧位置跟随鼠标像素，即使预览整数Frame没变也InvalidateCursor并同步Update，不能用游戏FPS限制输入反馈。Bitmap和手势在Dispose时释放，新增CS已列入Build-Native。下栏与右栏关键帧尺共享虚线；SelectionTimeControl通过箭头显示候选并同步两处虚线。WorkspaceView.cursorSnap是全局开关，旧布局null默认开，旧keySnap兼容写出。片段仍以[start,end+1]吸附共享Frame与其他片段。Test-CursorSnapping.ps1含输入、Alt、共享、像素图与稠密数据绘制计数/成本；图和cursor-paint-profile.txt在build/cursor-snap-check。离屏Update不会保证实际WM_PAINT，测试验证同步请求并用自身窗口PrintWindow/离屏OnPaint确认绘制，不能将此成本当屏幕Hz或游戏FPS。按变更选动作、关键帧、音频统计与语言回归；纯UI无需GM探针。

右栏停靠由InspectorDockWorkspace.cs管理两个常驻页面，KeyframeWorkspace.cs负责列表及独立keyProperties。WorkspaceView.inspectorDock只存界面mode/first/active、rows/columns/details比例；旧keyframesHeight迁移为上下布局高度。CommitInspectorEdits同时提交常规和关键帧输入；键盘焦点、F5/导出/保存、切工程/语言均要覆盖两端。运行Test-InspectorDocking.ps1验证真实标题MouseDown/Move/Up的四向停靠、页签拆合、取消、旧/新布局恢复、独立详情/Undo及控件复用，截图在build/inspector-docking-check。无工程或GML语义变化。

rightTime（SelectionTimeControl）属于关键帧页顶部，不挂在两个停靠页面的公共头部。暗色原生滚动条由DarkScrollbars.cs处理：仅Windows10 build18362以上动态查询uxtheme私有ordinal133/135，启用进程暗色偏好，再为原生滚动控件/AutoScroll面板/列表树/文本框及组合框列表调用[SetWindowTheme](https://learn.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-setwindowtheme)；旧系统或缺少入口时保留原生回退。Program提前初始化，MainForm与Language.Attach覆盖句柄重建和动态/弹窗控件；不替换滚动算法或修改系统设置。Test-ScrollbarTheme.ps1在不激活的离屏测试窗口检查横纵滚动条像素、动态控件、句柄重建及滚动值/事件，图与日志在build/scrollbar-theme-check。停靠页截图使用自身离屏窗口PrintWindow，避免DrawToBitmap对隐藏页签产生漏绘。

### GM8.2 验证

独立轨道物件的时段互斥由desktop/TimelineLayout.cs的EnsureExclusive处理，仅用于编辑器的工程打开和改动提交，不用于展开后运行态。生成组内引用模板的局部时间不参与歌曲轨道冲突。检测到旧工程重叠时先保留原执行数组并设置preserveTrackOrder，再仅变更lane；拖动覆盖仍调用CutOverlaps裁剪。运行desktop/Test-LaneExclusivity.ps1后再运行runtime/node.exe desktop/tests/verify-lane-sync.mjs，隔离工程核对保存/重开、时间/父子关系/执行顺序和真实生成GML不变。

复制obj与时间轴编辑入口为desktop/CopyGeneration.cs、TemplateRuntime.cs、TimelineEditing.cs及studio/copy-generation.mjs、linked-copy.mjs、template-system.mjs。BulletAction.copyLinked默认true；track级复制按实际来源图和当前局部时钟取样，一组链接持续到来源结束，不能每次触发再叠加整组。关掉copyLinked或使用弹幕年龄行为时仍走独立模板。联动网格逐成员在旋转格点坐标内取模，锚定实际房间中心、范围至少覆盖房间；禁止按整体中心出屏裁掉成员。shapeSetup与stateCode都要保留来源所在的父级/模板上下文，避免父角度重复进入形状phase。临时实例/位置字段只参与模拟快照；rf_copy_started用普通GM数组，F5重放及原生保存可恢复。先运行desktop/Test-CopyEditing.ps1，再用runtime/node.exe desktop/tests/verify-copy-gml.mjs核对独立GM工程；Node入口studio/copy-generation.test.mjs。夹具在build/copy-editing-check，覆盖来源后续编辑、数量/半径/角度、屏外成员/旋转网格、父级及生成实例内链接、颜色、释放、独立模式和实际F5目标帧状态。UI还覆盖无缝接线、整理/Undo、拉伸模式与中英文。preserveTrackOrder保留执行顺序；Track.keyframeCycle保存周期与独立快照。产品新增CS必须在Build-Native显式列出。

事件预设入口为desktop/EventPresets.cs和studio/event-presets.mjs（复用control轨道，以eventPreset区分）。运行desktop/Test-EventPresets.ps1覆盖事件语义和高字号分隔条/时间按钮；随后用runtime/node.exe desktop/tests/verify-event-gml.mjs核对独立GM工程的弹幕、整体显隐及额外原生父子对象。Node单元测试为studio/event-presets.test.mjs。夹具均位于build/event-presets-check；测试对象事件头使用工程标准action_id/applies_to，不使用id/kind字段。

2.1.0 的生成模板与全局变量定向入口：`desktop/Test-GenerationPresets.ps1`，通过后运行 `runtime/node.exe desktop/tests/verify-generation-gml.mjs`。前者覆盖5种循环模板、移动母弹、寿命/拖影/子弹行为入口、变量历史与复制预设；后者创建独立 GM 工程，对照弹幕状态、拖影参数、全局变量与 F5 中途启动。Node 的对应单元测试为 `studio/global-variables.test.mjs`。

生成模板有单独的隔离验证入口，先运行 `Test-TemplateSystem.ps1`，再执行：

```powershell
.\runtime\node.exe desktop/tests/verify-template-gml.mjs
```

它在 `build/template-system-check/` 新建工程，编译运行隐藏探针，对照 8 个时间点的完整弹幕/模板状态。覆盖替换母体、编号筛选、循环、分组内发射器、原心形/星星的模板、释放、代数限制和局部时钟。

整体形状有独立的可重复验证入口。在 `Test-FormationShapes.ps1` 通过后执行：

```powershell
.\runtime\node.exe desktop/tests/verify-formation-gml.mjs
```

它只在 `build/formation-shapes-check/` 新建隔离工程，编译并运行隐藏探针，检查运行日志及 1,560 个输出坐标；其中直接调用随库的原始 `heart` / `star2` 脚本，并校验同名用户脚本不会被同步覆盖。需要已配置的 GM8.2；其他弹幕行为的旧探针交接仍见 STATUS。

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

轨道共享组由TimelineGroups.cs与studio/timeline-groups.mjs管理，EditorState.timelineGroups保存稳定id/name/color/lanes；lane是归属的唯一来源，父子parentId仍是旧坐标变换。模式展开用scopeGroup/_scopeGroup继承外层轨道，模板与分裂后代保存来源scope。StageCanvas.SceneChanged必须复制组定义。GroupEvents.cs与studio/group-events.mjs负责组内快照复制、清除和属性设置；复制在当前帧普通生成/控制/移动之后取快照，每个事件只在start执行一次。ScalarExpressions.cs与studio/scalar-expressions.mjs处理行为数值公式与换FPS单位，BulletAction.expressions进入快照、预设和导出。Test-TimelineGroups.ps1及Node timeline-groups.test.mjs为定向入口；verify-groups-gml.mjs在build隔离工程核对实际GM及Create/F5预热。使用说明见[轨道组](../timeline-groups.md)。

## 5. 从哪里改

本地保存/预览/历史优化见[使用说明](../performance-and-saving.md)及[验证记录](OPTIMIZATION-STAGE-2026-10-28.md)。保存由`studio/project-sync.mjs`准备文件计划、提交和恢复，`project.mjs`与`plain-room.mjs`通过其虚拟文件入口生成；不要在计划期间直接写真实工程。预览修订由`PreviewRevision.cs`判定受影响时间，`PreviewCache.UseScene`保留世界并从早期检查点重算；保持未修改生成动作的引用身份。`UndoHistory.cs`管理共享估算预算，不能在压入另一端后再弹出当前端，以免预算淘汰待恢复记录。

定向入口为`desktop/Test-Optimizations.ps1`、`runtime/node.exe --test studio/project-sync.test.mjs`和`runtime/node.exe desktop/tests/verify-project-sync.mjs`。后者产生build/optimization-check/save-project.json；随后`verify-optimization-gml.mjs large`核对1000物件真实Draw，`verify-optimization-gml.mjs native`使用本机non-ruf projects/Verse3原件的只读导入副本做同步/编译兼容检查。实时自机狙先跑Test-PlayerAim.ps1，再跑verify-player-aim-gml.mjs。运行器探针设置隐藏窗口、错误写日志并自动中止，保留失败日志。新增UndoHistory/PreviewRevision已进入显式构建清单。

- UI/渲染：读 `desktop/AGENTS.md` 的入口表。
- 后台/GML/原生导入：读 `studio/AGENTS.md` 的入口表。
- 用户操作语义：读 [Beta 使用说明](../beta-bullet-control.md) 和根 README。
- 功能优先级：读 [耐久控制路线图](../research/avoidance-control-roadmap-2026-10-23.md)。路线图是候选设计，不是全部已实现的规格。
- 原有目录说明：[目录与开发说明](../目录与开发说明.md)。

新增状态字段时，一次核对：模型默认值、归一化、保存/读取、快照/撤销、复制 ID、预设资源、预览、GML、语言和定向用例。参数变化必须覆盖出生帧、阶段切换帧和倒拖；多人编辑时整项契约由一方负责或先约定接口。

## 6. 发布边界

Git 推送以根 `AGENTS.md` 为准。`.github/workflows/release.yml` 在推送 `v*` 标签或手动触发时发布正式 Release。

正式版通过 `Package-Release.ps1` 生成完整便携包、兼容更新包和 SHA256；它要求纯数字 Version，并核对 ProductVersion。推送前确认标签 `v版本号` 与 `AppVersion.Current` 一致，发布说明放 `docs/releases/v版本号.md`。若以后制作 Beta 包，需要单独验证预发布流程并取得用户授权，不能去掉 Beta 后缀绕过校验。
