# 优化实施与验证记录：2026-10-28

用户要求阶段性收尾后再次要求继续。本轮范围已完成本地交付，不推送、不发布。下面保留暂停时的记录，并追加最终结果。

## 当前产物

- 根目录 `RufMaker.exe` 已替换为本轮优化版本：2.4.0，1,362,944 bytes。保存工程并重启编辑器后使用。
- 旧入口备份：`build/optimization-check/RufMaker.before-optimization-20261028-163807.exe`，1,349,632 bytes。后台源码也有变化，单独替换旧EXE不代表完整回退。
- `build/optimization-check/RufMaker.stage.exe` 为暂停时的构建检查产物，不作为用户启动入口；最终构建产物已通过 File.Replace 移至根入口。
- 本地 `studio/` 和 `desktop/` 已有本阶段未提交改动。重新启动根 EXE 会读取本地后台源码，不能把“EXE 未替换”理解成整个目录已回退到旧版本。继续开发时保留这些改动。
- 基线仍为 `main / a0491ba` 加前轮本地功能及用户工程改动；没有切分支、重置、提交或推送。

## 已实现并做过定向验证

1. **保存一致性**：`studio/project-sync.mjs` 先准备虚拟文件计划，提交前检查外部修改，再提交整个生成资源集合；失败回滚，进程中断后恢复待提交记录。`readState()` 和同步入口接入恢复。音乐检查提前，房间迁移也进入同一计划。恢复目录为工程内 `.rufmaker-sync`，正常完成会移除；发生未完成回滚时应保留。
2. **过时渲染**：预览按请求序号、场景、帧检查结果；旧请求取消并丢弃，统计与画面一起检查。模拟在完整游戏帧之间取消，继续定位不会留下半帧状态。绘图失败写状态栏和 `user-data/logs/preview.log`，相同连续错误去重。
3. **模拟缓存**：改名称、无需参与出视野寿命的镜头修改复用世界。后段关键帧只从受影响的插值区段重算；随机种子、玩家轨迹、模板定义等历史依赖改变时完整重建。保持未变化的动作对象身份，防止联动复制随机槽位改变。检查点保留早期锚点和近期密集点，最多 48 份、估算预算 32 MiB。
4. **数量限制**：移除简化预览静默 20,000 颗限制；48000 颗夹具确认后半段能够显示。移除 200 物件/轨道限制，并同步处理生成、复制、预设、覆盖裁剪、轨道组归一化与拖动上下界。201/1000 物件的保存、重开及高编号轨道组已验证。
5. **撤销预算**：Undo/Redo 共用估算内存预算，默认 128 MiB，在“工具 → 撤销内存预算”选择 32/64/128/256 MiB，保存于布局设置。超额淘汰最早记录；单步本身超过预算时保留最近一步。保持独立快照、选择、控件复用与原撤销语义。

## 已有验证记录

全部临时工程和日志位于 `build/optimization-check/`，没有写入用户 `projects/`。

- `studio/project-sync.test.mjs`：准备期间无真实写入、无变更不改时间戳、中途失败回滚、子进程中断恢复、外部编辑冲突；5 项通过。
- `desktop/tests/verify-project-sync.mjs`：缺失歌曲时全工程文件保持原值，修复路径后正常保存，201/1000 物件和高编号组保存重开；最终日志 `save-integration-final.log`。
- `Test-Optimizations.ps1`：预算、缓存、镜头寿命、局部重算与冷重算一致、取消恢复、远距离倒拖检查点、联动动作身份、历史变量、超过 200 的剪裁和预设、48000 颗渲染、实际异步 StageCanvas 仅提交最新帧；通过，见 `optimization-complex.log`。
- `Test-EditorPerformance.ps1 -Report optimized`：通过。40 物件/6400 键，编辑约 1.39ms，撤销约 3.93ms，控件保留；加键约 89.30ms，尚无显著改善。
- `Test-RandomTimeline.ps1`、`Test-LaneExclusivity.ps1`：通过，选择/多选/关键帧/房间 Undo、时间轴剪裁等保持。
- Node 同步/玩家表达式/全局变量 3 文件合计 16 项通过，见 `node-initial.log`。
- 最新源码 `Build-Native.ps1` 构建通过，见 `stage-build.log`。早期 C# 方法组 `Sum` 重载编译错误已改为显式 lambda；保留初次失败日志。
- 测量 `cache-profile.json`：同一隔离场景冷定位7000帧约 **1256ms**，改名后约 **12ms**、没有新增模拟步；后段关键帧修改约 **257ms**，仅重算1200帧。不是实际耐久游戏的FPS保证。

## 暂停时的续做清单（现已完成）

1. 先读代理记录并核对 Git，保持所有用户工程和既有改动。
2. 完成最终定向回归：最后两处大编号轨道拖动上界、预览错误去重、索引缺少末尾换行的拼接是在前述回归后补的；已编译，尚未针对这些最终细节补跑。
3. 检查剩余 200/199 限制仅保留无关的 JSON 长度、坐标或测试常量，验证生成模板/复制/导入大预设和大编号轨道组的实际界面入口。
4. 跑相关 Node（同步事务、组、模板、预设）及必要 C#（实时自机狙、模板、生成、复制、语言/布局）定向回归，按新失败修正，不无关全量重跑。
5. **尚未完成本阶段真实 GM 编译/运行。** 在 `build/` 下验证新事务输出可编译、原生导入保存兼容、前轮实时自机狙数值不回退；必要时为大物件工程补启动/Draw 检查。
6. 完成用户文档和开发入口，更新本报告结果，再构建最终 EXE，备份并替换根入口。当前根 EXE 不包含本阶段 C# 优化。
7. 用户未要求推送或 Release；默认仅本地交付。

新增产品源码 `desktop/UndoHistory.cs`、`PreviewRevision.cs` 已进入 `Build-Native.ps1` 显式清单。具体占用和全部文件见 Codex 代理记录；本阶段结束后释放占用，后续接手应重新登记。

## 继续后的最终结果

- 最后细节：增加原生鼠标多选拖动到第203/204显示轨道、无末尾换行的资源索引、连续预览错误去重/修复后恢复的自动用例，均通过。测试早期曾因反射取公开ClipBounds的方法标记错误失败，已改用公开入口；不是产品空引用。失败日志保留为optimization-final.log，最终为optimization-final-2.log。
- Node相关6文件22项通过；索引新用例加入后保存事务6项通过。CopyEditing、GenerationPresets、TemplateSystem、PlayerAim、Localization、ShellLayout全部通过。相关输出为build/optimization-check/*-final.log；暂停前已通过且没有新涉及改动的回归未无关重跑。
- 保存集成重新通过：失效歌曲不改变任何工程文件、有效重试、201和1000物件及高编号组保存重开。日志save-integration-resumed.log。
- 真实GM实时玩家数值对照通过：build/player-aim-check/GM Player Aim 1793176451003/GameMaker.gm82，61帧/1502条状态，player-aim-gm-verified.log。最初沙箱内编译器以3221225794退出，自动审核允许隔离隐藏执行后通过；未控制桌面，未遗留测试错误弹窗。
- 真实GM大工程通过：build/optimization-check/Sync 1793176523019/GameMaker.gm82，1000物件编译、启动并执行普通Draw，large-gm-final.log。新增入口desktop/tests/verify-optimization-gml.mjs large。
- 原生兼容通过：将non-ruf projects/Verse3/inverse.gm82只读导入build/optimization-check/Native Sync 1793176577609/GameMaker.gm82；纯原生保存不改其他文件，添加叠加弹幕及lane300组后保存/重开、原控制代码保留及GM编译通过。原件GML/设置/索引摘要不变。日志native-gm-final.log；本轮未重录整段底片/完整原生试玩。入口verify-optimization-gml.mjs native依赖本机该原件。
- 最终合成测量：冷定位7000帧1268ms、改名复用12.47ms/0新增步、后段改键239ms/1200重算步。40物件6400键的编辑/Undo数据仍采用暂停前的1.39/3.93ms；加键89.30ms仍未显著改善。不是游戏FPS承诺。
- 双语README、开发入口及docs/performance-and-saving.md已说明预算、缓存失效、日志和保存恢复的实际边界。没有新增永久版本备份、差量Undo、质量档位或CI重构。
- 最终Build-Native及git diff --check通过；已备份并替换根EXE，版本仍2.4.0/1,362,944 bytes。用户demo维持12个tracked文件817增236删，其余用户projects未操作。没有提交、推送或Release；本轮五类优化无未完成必做项。
