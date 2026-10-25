# Codex 任务记录

- 状态：`completed`
- 任务：已完成飞行弹幕寿命模式（固定/房间外/当前视野外）、缩小/淡出销毁过渡，以及模板生成的初始方向和逐成员方向递增；补充周期扩散与逐颗等速发射说明。仅本地实现、构建，不推送。
- 开始时基线：`d278c0b`，分支 `main`；保留已有用户工程及示例。
- 当前占用文件：无（已完成并释放）。
- 验收结果：C#/JS 定向测试、复制预设与 FPS、右栏控件复用和中英切换通过；隔离 GM8.2 22 时刻 1,130 个完整状态对照通过。待用户交互验收。
- 本轮发布：仅本地 1.13.6 构建/提交，未推送。远程 main 为 `a83e764`，Release/latest 仍为 v1.13.1（Actions `40114921642`）。
- 本机产物：根目录 `RufMaker.exe`，ProductVersion `1.13.6`，FileVersion `1.13.6.0`，1,085,952 bytes。已安全替换路径，运行中的旧进程需保存并重启。

## 2026-10-25：弹幕寿命、销毁过渡与顺序发射

- 持久字段：Track 增加 `lifetimeMode="fixed"`（fixed/room/view）、`deathEffect="none"`（none/shrink/fade/shrink-fade）、`deathFrames=15`、`despawnMargin=0`；BulletAction 增加 `spawnAngle=0`、`spawnAngleStep=0`。旧工程默认保持固定寿命、无过渡与原生成方向；现有 Memberwise 快照、复制、JSON 保存/读取和预设涵盖这些值。预设和循环片段按 FPS/时间倍率换算 deathFrames，角度/像素余量不缩放。
- 新增 `desktop/BulletLifetime.cs`，已加入 Build-Native 显式清单；新增 `studio/bullet-lifetime.mjs`，同步登记 `rf_lifetime_inside/request/tick` 三个内部 GML 脚本。发射器和整体对象右栏增加“弹幕寿命”，条件字段沿用增量更新，不清空整页。寿命数值仅固定模式显示，已有 life 关键帧保留。
- 房间外/游戏视野外模式不受数值 life 限制；边界以弹幕中心计算，出生即越界也触发。余量正值扩大、负值缩小允许范围。当前 RufMaker 拉镜优先，随后使用原生底片/游戏 view0，最后回退房间；编辑器自身缩放/平移不改变判定。C# 按模拟帧缓存镜头边界。
- 固定寿命在到期前消退，按原时刻移除；越界、RufMaker clear 和生成后销毁触发后播放过渡，结束才移除。待销毁实例保持移动、禁用碰撞和后续行为/生成，统计保留到真正移除。缩放/透明度以原参数乘过渡系数，历史拖影保存最终外观，避免逐帧重复衰减；已开始固定寿命消退后再 clear 不会突然变亮。
- 此寿命设置用于发射弹幕及已释放成员；未释放的整体成员仍由队形/物件时段控制。任意用户 GML 中直接 instance_destroy 不会被拦截。这些边界已写进双语说明与工具提示。
- 生成方向为 `(继承 ? 母体方向 : 0) + spawnAngle + 成员编号 × spawnAngleStep`，成员编号从 0 开始。C#/GML 同步。现有 stagger 和 spawnRepeat 分别控制逐成员错开与重复；重复时每名成员保持自己的方向，不按重复次数继续加角度。README/英文指南写出周期星形副本、逐颗等速发射配方；未改写用户上一轮 Star Echo 工程。
- 实际源码：desktop/{BulletLifetime,EditorCore,BulletSystem,TemplateRuntime,BulletBehaviorForm,MainForm,BulletTools,PreviewRenderer,PresetLibrary,Updater}.cs、desktop/Build-Native.ps1；studio/{bullet-lifetime,bullet-schema,bullet-system,template-system,project}.mjs；assets/locales/en.json。测试修改：TemplateSystemTests、template-fixtures、verify-template-gml、LocalizationTests、PresetTests，新增 studio/bullet-lifetime.test.mjs；另有双语 README、交接和根 EXE。
- 通过：Node `--test studio/bullet-lifetime.test.mjs studio/template-system.test.mjs studio/bullet-system.test.mjs`（8/8）；`Test-TemplateSystem.ps1`（固定/越界/旋转缩放视野/原生 view/缩小透明/销毁重叠/负余量/出生在外/两轮错开发射及新 UI 参数写入）；`Test-Presets.ps1`；`Test-Localization.ps1`；`Test-EditorPerformance.ps1`。最终桌面日志在 `build/lifetime-check/{template,localization,inspector}.log`。
- 真实 GM8.2：`runtime/node.exe desktop/tests/verify-template-gml.mjs` 通过，22 个时刻 1,130 个完整状态含碰撞、待销毁年龄、视觉缩放/透明度、生成方向与 C# 一致。隔离目录 `build/template-system-check/Template GM 1792900814285/`；沙箱外运行已配置引擎，无残留错误弹窗。
- 右栏性能回归：40 物件/6400 键，单项编辑/撤销中位数 1.75/3.87 ms，加键/撤销加键 4.57/5.74 ms，168 个基础控件全部保留。仅本轮合成场景的回归结果，不用于推断实际游戏 FPS。
- 最终构建 `desktop/Build-Native.ps1 -Output D:\rufmaker\build\lifetime-check\RufMaker.exe` 通过，根入口通过 File.Replace 安全替换、SHA256 相同，备份 `build/lifetime-check/RufMaker.previous-*.exe`。未关闭用户进程。本地提交随本节所属修复保存；未推送/发布，无剩余必做项，待用户验收。

## 2026-10-25：生成来源中心与星星复制扩散示例

- 新表达式变量 `source.x / source.y`：对生成模板发出的弹幕，表示触发生成时直接来源中心的世界坐标快照；逐成员生成时传整体中心，而非成员坐标。包含父分组/生成实例的旋转和平移。飞行弹幕生成下一代时，下一代记录母弹当时位置；普通弹幕/释放/分裂也记录对应出生来源。母体继续移动或消失不改变已捕获的值。
- C# `TemplateInstance` 和 `SimBullet` 增加运行时 sourceX/sourceY；捕获、倒拖、检查点通过现有成员快照保留。`ExpressionContext`/解析变量表和出生/行为/控制/飞行的弹幕上下文读取此值。模板尚未生成时，参数公式参考预览使用其基础 X/Y；没有增加工程持久字段或改写用户已有表达式。
- GML `rf_template_spawn` 内部调用从 6 扩至 8 参数，`rf_pattern_spawn` 从 14 扩至 16 参数，统一传递冻结来源中心；全部生成调用已一起更新。为满足 GM8.2 的 16 参数限制，内部 `rf_expr_*` 调用重排：argument10 改传原算法计算好的 RNG 状态，FPS 编入脚本常量，腾出 argument11/15 传来源坐标；随机种子/取样时钟公式不变。没有使用共享全局来源变量，多个生成实例不会互相串中心。旧项目保存/加载同步时整体重建这些内部脚本。
- UI 公式选择增加来源 X/Y、从来源中心向外、按来源距离设置弹速（弹幕行为）；行为公式说明与中英词条同步。带来源变量的发射器表达式进入历史模拟。README/英文指南解释来源快照和用法。
- 实际源码文件：desktop/{ExpressionContext,RandomParameters,ExpressionEditor,BulletBehaviorForm,BulletSystem,TemplateRuntime,Updater}.cs；studio/{random-parameters,bullet-system,template-system,bullet-schema,project}.mjs；assets/locales/en.json。测试为 PlayerExpressionTests、TemplateSystemTests、LocalizationTests、expression/template-fixtures、verify-template-gml 和 studio/player-expressions.test；另有示例脚本/说明与交接文档。
- 可重复创建入口：`runtime/node.exe tools/examples/create-star-echo.mjs`。新建目录 `projects/examples/Star Echo/`，已有同名目录时递增后缀，不覆盖。完整可编辑入口 `Star Echo.rufmaker`，GM 文本工程在相邻 `GameMaker.gm82/`；说明在同目录 `示例说明.md`。这份用户要求的新示例留本地，不提交 projects；生成配方及 docs/examples/star-echo.md 入库。
- 示例使用原 `star2` 轮廓、60 颗彩虹弹，中心 `(230,190)` 通过关键帧移动；第 100 帧中心 `(287.5,215)`，逐成员生成单发模板，继承径向但不销毁母体。新弹幕年龄 0、持续 0 的弹速公式为 `point_distance(x,y,source.x,source.y)*0.02`。50 帧后副本相对出生中心放大两倍，原星星继续移动和旋转。
- 通过：`Test-TemplateSystem.ps1`（含母体保留、一次生成/出生位置、源中心冻结、等比例扩散、多来源与分组、倒拖）；`Test-PlayerExpressions.ps1`；`Test-Localization.ps1`；`Test-RandomTimeline.ps1`；Node player-expressions/random-parameters/template-system/bullet-system 共 15 项。
- 真实 GM8.2：`runtime/node.exe desktop/tests/verify-template-gml.mjs` 通过，11 个时刻 647 个完整弹幕/模板状态一致，包含来源 X/Y 和新扩散示例；隔离目录 `build/template-system-check/Template GM 1792898918114/`，无运行错误。既有随机表达式与行为一并对照。
- 示例独立游戏编译、打包通过，`projects/examples/Star Echo/build/release/rufmaker-game.exe` 4,738,923 bytes，保留旁边 Data；编译日志 `projects/examples/Star Echo/build/compiler/latest.log`。这是编译和同配方数值验证，没有宣称用户交互试玩验收。
- 第 150 帧真实预览渲染已检查：`build/source-center-check/star-echo-150.png`，彩虹原星星与青色扩散副本可区分。测试截图入口 `TemplateSystem.tests.exe --render STATE FRAME PROJECT OUTPUT` 读取贴图元数据；最初辅助器缺少帧数、重编时发生 lambda 变量遮蔽，均仅为测试辅助器并已修复，记录在 `render-helper-fix.log`。
- 正式构建 `desktop/Build-Native.ps1 -Output D:\rufmaker\build\source-center-check\RufMaker.exe` 通过，File.Replace 安全替换根入口且 SHA256 相同；未关闭用户进程。源码、测试、说明、EXE 随本节修复本地提交保存，未推送/发布；无剩余必做项，待用户验收。

## 2026-10-25：物件粘贴到播放头

- `MainForm.PasteSelection()` 对复制的物件统一加 `当前帧 - 最早起始帧` 偏移：开始、结束、属性关键帧和 releaseFrame 一起平移；多选及分组子项保持时间间隔，已有 ID/分组引用复制机制沿用。超过工程结尾自动延长时长，空间坐标、属性值、弹幕年龄行为和片段 children 的局部时间保持原样。连续粘贴重新读取当前帧，不修改剪贴板或原件。
- 同步复制/粘贴状态提示、中英 README 与英文词条；版本 1.13.4。未改关键帧剪贴板、房间实例粘贴、JS/GML、数据字段或运动公式。
- 实际文件：desktop/MainForm.cs、desktop/Updater.cs、desktop/tests/RandomTimelineTests.cs、assets/locales/en.json、README.md、docs/README.en.md、RufMaker.exe 及本交接文档和 STATUS。
- `pwsh -NoProfile -File desktop/Test-RandomTimeline.ps1` 通过：新用例覆盖单物件、混合物件多选、组/子项、属性键/释放时刻、连续粘贴、帧 0、工程末尾、片段局部时钟、撤销/重做；原参数/撤销/关键帧边界回归也通过。日志 `build/paste-playhead-check/random-timeline.log`。
- 首次新用例误把“重做应强制切换到恢复的副本”当作约束，与已有“优先保留仍存在的选中对象”冲突。已改为核对恢复后的副本时间数据及有效选中引用，未改撤销策略；初次失败记录保存在 `build/paste-playhead-check/first-test-failure.txt`。
- 构建 `desktop/Build-Native.ps1 -Output D:\rufmaker\build\paste-playhead-check\RufMaker.exe` 通过，根入口通过 File.Replace 替换并比对 SHA256；未结束用户进程，旧映像备份在同一目录。
- 本地提交随本节所属修复提交保存；未推送/发布，无剩余必做事项，待用户交互验收。

## 2026-10-25：关键帧边界、负高度比例与空间彩虹

- `TimelineCanvas.KeyMoveBounds()` 在开始拖动时计算所选键各自物件的起止限制，取共同位移范围；鼠标移动时直接钳制。单键、多属性和跨物件多选均保留相对间隔，松手不再延长物件。`MoveSelectedTo()` 共用范围与碰撞合并规则。旧工程或裁剪留下的越界键不会在加载时被批量改写；无法整体移入的选择保持原位。
- `shapeAspect` 原先在参数编辑、表达式和三端几何中被限制最小 0.05，现统一 -10～10。负值翻转局部高度，0 压成直线，关键帧/动态表达式可以连续过零；没有改用户提供的原始 heart/star2 脚本。涉及 `MainForm`、`RandomParameters`、`FormationShapes` 及 JS 同名路径和生成的 `rf_formation_point`。
- Track 新增持久字符串 `rainbowMode="sequence"`，值 `sequence/distance/angle`；C#/JS 缺省与未知值回落原有模式。整体对象彩虹开关下增加“彩虹分配方式”。距离以物件自身 X/Y 为中心，每像素推进 density 个颜色帧；角度一周对应 32 帧，density=1 为一轮彩虹。保留反转、动态、密度/相位/速度关键帧和固定白色/image_speed=0。
- `Rainbow.RodSequence()` 可接收已算出的成员坐标/中心，避免重复生成整组点；模板额外计入实例旋转。C# 与 GML 对空间序号舍入至 0.0001，避免 PointF 误差使等距圆环产生杂色。同步普通对象本体、空间拖影、模板生成及释放后的出生配色；释放后沿用出生配色序号，动态色彩继续随时间变化。
- 模板历史新增内存 `BulletSample.sequence` 和 GML `rf_himage`，拖影读取历史样本配色。模板本体参数按当前局部时钟取样，释放后的弹幕按出生时取样。`Track.Snapshot` 的成员复制、JSON/预设与撤销涵盖新模式；未新增工程版本或破坏旧工程默认表现。
- 测试改动：RandomTimelineTests、FormationShapeTests、formation-fixtures、TemplateSystemTests、template-fixtures、两个 verify-*-gml 探针、LocalizationTests、studio/shapes.test。中英文 README 与 en.json 增补用户选项和操作说明。
- 通过：`Test-RandomTimeline.ps1`（含折叠/展开、左右边界、跨属性/物件多选、移到播放头、零长度及冲突合并）；`Test-FormationShapes.ps1`；`Test-TemplateSystem.ps1`；`Test-Localization.ps1`；`Test-Presets.ps1`；`Test-EditorPerformance.ps1`；Node shapes/template-system/bullet-system/random-parameters 共 15 项。
- GM8.2 隔离探针最终通过：`runtime/node.exe desktop/tests/verify-formation-gml.mjs`，4,040 条坐标/颜色记录（负比例、过零、表达式、滚动与圆环配色），目录 `build/formation-shapes-check/Formation GM 1792896461188/`；`verify-template-gml.mjs`，8 个时刻 188 个完整状态含配色一致，目录 `build/template-system-check/Template GM 1792896478733/`。需沙箱外启动已安装引擎，未访问用户工程。
- 性能回归仍保留参数控件，合成 40 物件/6400 键：编辑/撤销中位 6.51/12.98 ms，加键/撤销加键 17.39/20.47 ms。仅作回归记录，不代表游戏 FPS。
- 早期扩充形状夹具时，参考代码曾对关闭滚动的新测试物件也计算滚动相位，导致 Track X ellipse 断言失败；已修正为检查 t.rolling，后续 C#/GM 最终对照通过。没有留下测试进程或错误弹窗。
- 构建 `desktop/Build-Native.ps1 -Output D:\rufmaker\build\key-bounds-check\RufMaker.exe` 通过；File.Replace 安全替换根目录 EXE，SHA256 与构建产物相同，旧映像备份留在同一 build 目录。未关闭用户进程。
- 本地提交随本节所属修复提交保存，未推送/发布；无剩余必做事项，待用户交互验收。

## 2026-10-25：属性原位标记、局部更新与语义拖动步长

- 按用户新要求，`AnimatedFields` 固定归入“参数”，不再按有无关键帧拆组。◆/◇ 在原参数行标动画状态；数值编辑规则仍为无键改全局、有键改当前键。
- `NativeProperties.Reconcile()` 按类别/名称及兼容性复用 `ValueProperty` 与动作绑定。`ParameterInspector.UpdateRows()` 只创建/销毁变化行，兼容字段保留 Label、编辑器、事件与滚动；组标题也复用，单/双列切换调整已有控件单元格。K 自动选择关键帧时只追加详情，删键移除详情；常规参数行不再重建。
- `QueueInspectorUpdate()` 合并同一 UI 轮次的布局请求，接入运动/飞行/发射形状/控制动作/拖影/特效等下拉更新。启用表达式时删除一次多余 MarkChanged，避免同一操作重复制作预览快照。
- 新增 GUI 元数据 `ParameterActions.Property`（不进入工程保存），供 `ScrubNumber.StepFor()` 按字段 ID 选拖动步长；重绑已有“数值”编辑器时同步更新。缩放、镜头缩放、透明度与比例 0.01；坐标、长度、角度、数量/帧 1；速度 0.1；加速度 0.01；拖影衰减 0.001。步长不随当前数值数量级跳变；Ctrl ×0.1、Shift ×10 保持原行为。无元数据的原生/普通数字按标签和整数类型回退。
- `Test-EditorPerformance.ps1` 扩展：实际 K/删键/动画开关/关键帧选择及重绑、父控件身份与原行、增删拖影/运动字段、宽窄布局和单元格无重叠；关键帧详情切换 x/scale 时验证步长；比例/坐标在 0～1000 范围验证稳定步长。
- 与 `a83e764` 的 1.13.1 源码编译基线对照（均为 40 物件/6400 关键帧、7 次中位数，不含游戏渲染）：加键约 59.88 → 18.45 ms，撤销加键 77.29 → 20.59 ms；原页共有控件保留由 false 变为 true。基线在 `build/inspector-incremental-baseline/before.json`，新结果在 `build/editor-performance-check/after.json`。
- 通过：`Test-EditorPerformance.ps1`、`Test-RandomTimeline.ps1`、`Test-Localization.ps1`、`Test-FormationShapes.ps1`、`Test-Presets.ps1`。本地化用例改为比较持久字段语义，避免 .NET 反射字段顺序改变导致相同 JSON 数据误报；没有改语言切换产品行为。
- 离屏截图 `build/editor-performance-check/incremental-parameters.png` 已检查。构建 `desktop/Build-Native.ps1 -Output D:\rufmaker\build\inspector-incremental-check\RufMaker.exe` 通过，File.Replace 安全替换根入口，旧映像备份在同一 build 目录；未关闭用户进程。
- 未改 JS/GML、运动语义或用户 `projects/`；未发布 1.13.2，待用户验收和另行推送授权。README/英文指南及 desktop/AGENTS 同步了新交互约定。

## 2026-10-25：1.13.1 发布与线上验证

- 发布范围为 `9884eed` 的性能修复，以及上次尚未发布 Release 的频谱、轨道高度和对象统计。新增 `docs/releases/v1.13.1.md`，首页双语版本说明更新；修正开发入口中已过时的 Beta 发布描述。
- `desktop/Package-Release.ps1 -Version 1.13.1 -OutputDirectory D:\rufmaker\build\distribution\v1.13.1` 通过，生成完整便携包、兼容更新包及两个 SHA256。
- 本地审计：更新包 2612 项/57,949,920 bytes，完整包 2631 项/59,627,708 bytes；两个 EXE 均为 1.13.1，更新清单中全部文件逐项 SHA256 一致，无重复路径、用户工程、设置或缓存。模板中的 objRufController 由建工程时生成，审计核对的是模板入口 `rufmaker-demo.gm82`。
- `desktop/Test-Updater.ps1` 通过：版本提示、包选择、等待退出、替换/备份、保留个人资料和重启隔离程序。1.12.0 原版更新器成功解包/校验 1.13.1 包，并完成资产目录迁移。
- 发布方式为现有 `.github/workflows/release.yml` 的 `v1.13.1` 标签触发。发布前已确认远程不存在该标签。后续记录应补充 Actions 和线上 latest/下载校验的真实结果。
- 发布准备提交 `6ed3764`，功能修复 `9884eed`；main 和注释标签通过一次 atomic push 同步。Actions [40114921642](https://github.com/wymcaT/RufMaker/actions/runs/40114921642) 构建、打包、上传与发布成功，未重试或改写标签。
- [v1.13.1 正式发行版](https://github.com/wymcaT/RufMaker/releases/tag/v1.13.1) 为 latest，`draft=false`、`prerelease=false`，4 个下载资产名称与大小正确。发布说明包含 1.13.0 的频谱/统计功能和 1.13.1 编辑性能修复。
- 公开完整包实下载验证：59,628,720 bytes，2631 项，EXE 1.13.1，所需运行文件/模板/形状脚本/图标/英文词条/说明齐全，无用户工程或缓存；SHA256 `a98648fae26c177dcd46b6b4ad1075533ff8862423003944a6f32c5c6f2cd8f1` 与公开校验文件、GitHub 资产 digest 一致。
- `desktop/Test-Updater.ps1 -Online -Version 1.13.1` 通过：软件自己的 latest API、版本识别、公开更新 ZIP 下载、SHA256、全部清单文件解包均验证成功。公开更新包 57,950,554 bytes，SHA256 `ad4be61b878c902b492a5c2f246df1eef604a8b51b450474873e1b2973c008fe`。本地包仅为预检，公开包由 CI 重建，哈希以线上为准。
- 过程中的匿名 Actions API 一度限流，改用连接的 GitHub 工具读取 Actions/Release；后续软件匿名 latest/下载测试正常。没有改变软件更新策略或注入凭据。
- 最终交接记录作为文档提交再推送 main，不移动已发布标签。个人工程未提交、未覆盖；本轮授权仅用于这次推送/发布。

## 2026-10-25：属性页撤销与编辑性能

- `NativeProperties.TryRebind()` 先核对字段顺序、名称、类别、类型、只读状态、说明、选项和动作布局；相同时更新现有 `ValueProperty` 的 getter/setter 和 `ParameterActions`，复用控件。撤销后闭包绑定恢复后的对象；字段/选项/类别变化时仍重建。保留选中、滚动和原有输入控件。
- `ParameterInspector.Refresh()` 只更新字段；`ScrubNumber.RefreshValue()` 比较显示值/表达式，只对改变的行发起绘制，不调用整页同步 `base.Refresh()`。提交表达式、取消输入与拖数值均更新显示缓存。
- 新增 `EditorState.Snapshot()`、`NativeProject.Snapshot()` 等类型化深复制，用于 `UndoEntry` 和保存。复制涵盖嵌套片段、关键帧、行为、表达式、曲线、terrain、玩家/轨迹、原生事件和选中信息；恢复时再复制，历史之间不共享可变数据。terrain 保留 JSON 兼容复制，其余大块模型不再用整工程 JSON 往返。
- `UndoEntry.SameScene()` 用持久字段比较做去重，跳过 `[ScriptIgnore]` 的 parent/owner 等引用；仍能识别未经过 MarkChanged 的直接属性/房间修改。歌曲未变的撤销不重新加载音频。
- `SaveNow()` 用独立快照，并通过 `Task.Run` 在后台发送桥接 JSON。`RoomData.SameContent()` / `ReplaceRoom()` 跳过数据相同的保存响应，避免不必要的场景快照、缓存清空及整段历史弹幕重演。没有改 JS/GML 或弹幕运动语义。
- 性能记录：`build/editor-performance-check/before.json` 和 `after-final.json`；同一合成场景 40 个物件、6400 个关键帧，预热后 7 次中位数。单项编辑 248.81 → 4.17 ms，撤销 746.58 → 12.54 ms；属性页 164 个控件全部保留，原有子控件销毁由 163 降至 0。测试不包含实际游戏渲染/音频播放，不能换算为游戏 FPS。
- 新入口：`pwsh -NoProfile -File desktop/Test-EditorPerformance.ps1`；可用 `-ProfileOnly -Report NAME` 单独记录数据。用例覆盖值/表达式/关键帧/重做的绑定有效性、滚动、删除恢复、结构变化、历史去重、保存房间比较及全部可变快照隔离；持久 JSON 保持一致。
- 通过：上述性能/快照测试、`Test-RandomTimeline.ps1`、`Test-AudioStatistics.ps1`、`Test-TemplateSystem.ps1`、`Test-Localization.ps1`、`Test-PreviewRendering.ps1`。预览缝隙回归的未覆盖像素为 0。本轮未运行 GM 运动探针，运动逻辑未修改。
- 构建：`desktop/Build-Native.ps1 -Output D:\rufmaker\build\editor-performance-check\RufMaker.exe` 通过。根 EXE 被用户运行中的旧编辑器占用，直接覆盖编译失败；Windows Restart Manager 只读确认为 RufMaker（PID 50940）。随后使用 `File.Replace` 安全替换路径成功，未结束用户进程，旧映像备份留在 `build/editor-performance-check/RufMaker.previous-*.exe`。
- 本地提交随本轮修复保存；不推送，不改用户 `projects/`，不改 Argon4 记录。尚无 1.13.1 用户验收结论。

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
