# Codex 任务记录

- 状态：`completed`
- 任务：音频轨右键切换波形/频谱、独立轨道高度、当前帧对象数量与分类统计；完成本地构建后，用户另行授权推送 main。
- 开始时基线：`5afccbb`，分支 `main`；保留用户 projects 下既有改动。
- 当前占用文件：无，已完成并释放。实际改动见本轮 Git 提交；未改用户 `projects/`。
- 本轮推送：1.13.0 功能提交 `7100ed0` 同步至 main，附本次推送交接记录；未创建标签或 Release，正式发行版仍为 v1.12.0。
- 本机产物：根目录 `RufMaker.exe`，ProductVersion `1.13.0`，FileVersion `1.13.0.0`，1,064,448 bytes。

## 2026-10-25：频谱、轨道高度与对象统计

- `AudioSpectrogram.cs` 通过同一 FMOD 解码器做完整歌曲 STFT；Hann 窗、声道功率平均、192 个对数频带、-90～0 dB 亮度。反相立体声不会相互抵消。缓存按路径/大小/修改时间/格式版本生成，放在工程 `cache/audio-spectrum/`。
- `TimelineAudio.cs` 提供原生音频右键菜单和频率轴，显示与时间轴缩放/横移一致。音频区底边、左侧轨道名区底边可拖高度，双击还原；`TimelineCanvas` 的绘制、命中测试、框选、关键帧与跨轨拖动全部使用可变行高。布局新增 `TimelineView.audioDisplay/audioHeight/rowHeights` 和 `WorkspaceView.objectStatistics`。
- `ObjectStatistics.cs` / `StatisticsTools.cs` 在右栏增加属性/对象统计页，底部 obj 总数可点击。按 GM 类型和 RufMaker 来源名称分组；复用 `PreviewCache.bulletWorld`，覆盖延迟生成、寿命、隐藏成员、整体对象、生成模板和倒拖。纯绘制装饰单列，拖影不计实例。保留用户名称，中英文切换即时更新。
- `TemplateRuntime.InstanceCount` 只读暴露模板实例数；`BulletSystem.Expand` 和 Stage 场景快照保留 `nativeProject` 元数据。没有改变运动语义或生成游戏的弹幕代码。
- 原生底片录制器新增 `rf_film_counts`：`with(all)` 按 `object_index` 计数，排除录制器自身，随帧状态原子发布；清理列表补充 `objRufTemplate`。底片 manifest 新增可选 `objectCounts` 变化记录，二分定位。旧底片继续兼容；无记录明确显示仅 RufMaker 部分，由用户重新生成补全。普通房间原生代码的额外实例不可推算，明确标为预览估算。
- 修改/新增文件还包括 `AudioAnalyzer` 解码复用、`AudioTimelineTools` 后台分析与取消、`MainForm`/`EngineSetupForm` 接线、构建显式清单、`Updater` 版本、双语 README、英文词条及测试。
- 通过：`desktop/Test-AudioStatistics.ps1`（合成 100/1000/5000 Hz、20 dB 差、反相双声道、305 秒尾段、缓存/取消、菜单/独立高度/布局保存/拉伸、简单和高级计数/模板/倒拖/双语）；`Test-RandomTimeline.ps1`；`Test-NativeFilm.ps1`；`Test-Localization.ps1`；`pwsh -NoProfile -File desktop/Test-PreviewPlayback.ps1`；`runtime/node.exe --test studio/native-film.test.mjs`（4/4）。
- GM8.2 真机脚本探针：`runtime/node.exe desktop/tests/verify-film-counts-gml.mjs` 通过。沙箱内 GM 退出 3221225794；提权运行隔离工程后，生成/销毁数量、排除录制器和原子状态均正确。产物在 `build/audio-statistics-check/Film counts GM 1792858453650/`，未改用户工程。
- 截图已检查：`build/audio-statistics-check/spectrogram-timeline.png`、`object-statistics.png`、`object-statistics-en.png`。构建：`desktop/Build-Native.ps1 -Output D:\rufmaker\RufMaker.exe` 通过。播放回归脚本在 Windows PowerShell 5.1 的 `[short]` 类型别名失败，改用 PowerShell 7 原脚本通过；无需修改产品。
- 功能提交：`7100ed0`。用户于后续消息明确授权推送，已按 main 分支同步；个人工程未提交。尚无 1.13.0 用户验收结论。

## 上轮发布记录：1.12.0

- 功能提交 `4d8b1a2`；合并提交 `f554136` 已进入远程 main，标签 `v1.12.0` 已发布。GitHub Actions `40079194400` 成功；完整便携包、更新包及 SHA256 位于 Releases 和本地 `build/distribution/v1.12.0/`。

## 本轮实现与集成

- 新增 `TemplateRuntime.cs` / `TemplateTools.cs` / `studio/template-system.mjs`。`Track.spawnActions` 为物件生成行为，`bulletActions` 中 `type=spawn` 为飞行弹幕生成；`templateOnly` 隐藏自动出场的模板。
- 支持普通物件、整体对象、发射器和可视分组模板；局部关键帧时钟、重复/窗口/编号筛选、偏移、方向继承、生成成功后销毁母体与最多 4 代的后续生成。成员隐藏状态、实例和序号纳入预览检查点。
- 复制、预设依赖收集、跨 FPS 换算及片段 ID 映射覆盖新行为。模板名称选择框保留用户名称原文。
- GML 新增 `objRufTemplate` 和 `rf_template_*` 内部运行脚本。内部 `rf_pattern_spawn` 调用扩为 14 个参数（末两项为局部时钟偏移、模板 owner）；普通弹幕与旧分裂路径一起适配。
- 转为 1.12.0 正式程序名、互斥锁、布局入口；可继承更新的 Beta 布局。更新包通过旧版允许的模板路径传送 GML，由 `AppPaths` 在启动时安排到 `assets/gml/`；原始 GML 以 -text 属性保持字节。

- 保留并集成 Argon4 已完成的 `EditorCore.cs`、`MainForm.cs`、`ParameterInspector.cs`、`EditorCanvas.cs` 和 `RandomTimelineTests.cs` 撤销修复；未改写其代理记录。
- 撤销快照按 ID 恢复当前物件、关键帧、多选及房间实例；右栏保持上下文。补充撤销删除与房间实例回归，非属性输入框沿用文本快捷键。
- 新增 `desktop/FormationShapes.cs` 并加入 `Build-Native.ps1` 的显式清单。整体对象增加 heart、star2、星形轮廓、椭圆、花朵、∞ 形，原有转转棒/圆环/多边形保留。
- `heart.gml` / `star2.gml` 按字节复制自用户 `non-ruf projects/Verse3/scripts/`；校验值与原件一致。详情与参数对应见 `assets/gml/formations/README.md`。新形状没有替换这两种原有算法。
- 新数值字段 `shapeAspect=1`、`shapeInner=.45` 同时进入 C# 模型、关键帧、随机表达式与 JS 归一化/GML；复制、保存和预设使用现有快照路径。
- `studio/shapes.mjs` 的生成几何服务于固定弹幕、拖影和队形释放；修正分组下非圆轮廓重复添加相位和滚动速度缩放的偏差。
- `studio/project.mjs` 登记生成的 `rf_formation_point` / `rf_formation_perimeter`；缺少公共 heart/star2 时补入，有同名脚本时保留原内容。
- 同步中英词条、Beta 使用说明、开发入口和 STATUS。用户 `projects/` 下的既有改动未清理或纳入测试工程。

## 本轮执行的验证

- 正式集成：Node 9 个纯测试文件共 24 项通过；TemplateSystem、BulletSystem、Presets、PlayerExpressions、FormationShapes、RandomTimeline、Localization、Updater 的桌面定向测试通过。
- 新模板 GM 探针：8 个时间点、125 个完整状态与 C# 一致。旧弹幕 GM 探针：歌曲第 150 帧 120 个状态一致，覆盖分裂/控制/释放。两类测试均在 build 隔离工程运行。
- 旧公式测试改为修改场景参考玩家，而非仅改派生缓存，避免与新场景上下文不一致。早期模板 GML 分支括号错误已修正，最终真实运行通过。
- 正式构建与 Package-Release 成功；1.11.0 旧更新器反射解包、校验新包并完成资产迁移通过，heart/star2 的 SHA256 与原文件一致。发行包约为完整便携 58.6 MB、更新包 56.9 MB。
- 发布后执行 `Test-Updater.ps1 -Online -Version 1.12.0` 通过：实际 latest API 为 v1.12.0，应用更新器下载公开 ZIP，验证 SHA256 并解出全部清单文件。CI 的发行包大小因打包环境有所不同（完整包约 59.6 MB，更新包约 57.9 MB）。
- 分支切换与合并后，553 个个人工程文件与操作前快照的 SHA256 全部相同；只提交产品代码、说明和正式 EXE，个人工程仍保留在工作区。

- `desktop/Test-RandomTimeline.ps1`：通过，包含单项属性、连续撤销/重做、关键帧、多选、撤销删除、房间实例选中恢复。
- `desktop/Test-FormationShapes.ps1`：通过，C#/Node 几何与滚动对照、独立关键帧、分组旋转/缩放、属性栏、复制和新形状撤销选中。
- `desktop/Test-Localization.ps1`：通过，包含新增形状与属性词条。
- Node `shapes.test.mjs`、`random-parameters.test.mjs`、`bullet-system.test.mjs`：10 项通过；修复分组滚动后另复跑 shapes 的 3 项通过。
- `desktop/tests/verify-formation-gml.mjs`：隔离 GM 编译/运行通过，1,560 个坐标对照通过；包括执行原 heart/star2 脚本和校验自定义同名脚本不会被同步覆盖。
- `desktop/Build-Beta.ps1`：通过，根 Beta EXE 已更新。编译检查不等于用户交互验收。

GM 首次在沙箱内无法启动（3221225794），使用已安装引擎在沙箱外运行后完成验证。分组滚动的早期样本失败已修复，最终对照全部通过。日志和形状图保留在 `build/formation-shapes-check/`。

## 使用边界与后续

新闭合曲线按参数排列，非等弧长分布；star2 保留原脚本逐段次序。整形旋转用轮廓朝向。一般弹幕行为的旧 GM 探针整理工作仍在 STATUS 的 A1 中，本轮新增的是形状专用正式验证入口。

启动根目录 `RufMaker.exe` 使用正式功能；历史 `RufMaker.Beta.exe` 不作为入口。用户本轮已授权推送，本轮以外仍遵守默认不推送约定。
