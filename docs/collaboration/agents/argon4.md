# Argon4 任务记录

- 状态：`idle`
- 当前任务：无
- 基线分支/SHA：`main` / `b1affa1`
- 当前占用文件：无
- 验收方式：无

## 2026-11-07 本地集成与交接

```text
状态：completed
任务：
1. 队形释放（kind == "rod" 的释放队形）添加“保持原形状”（releaseKeepShape）选项：在径向散开模式下支持等比几何放大散开，弹速正比于到队形中心的距离，关闭时维持统一弹速；C# 实时预览与 GML 导出双端逻辑及参数编辑完全对齐。
2. 修复添加全局变量时弹窗报错“未将对象引用设置到对象的实例”（NullReferenceException）；时间轴关键帧右键交互支持“删除关键帧 / 删除所选关键帧（Delete）”与复制/缓动菜单，保留物件操作且快捷键文案互不冲突。
3. 暂停按钮优化为标准⏸（\u23F8）；新增“所有弹幕绕点旋转”（orbit-bullets）与“组内弹幕绕点旋转”（group-orbit）事件预设（C# 与 GML 双端对齐）；运动→圆周运动公转圆心与参数独立分类展示、多边形刚体自转与沿边滚动解耦。
4. 仅作用于预览的轨道/物件隐藏/显示选项（previewHidden，支持右键菜单、属性栏开关、时间轴视觉标记，不影响游戏导出和试玩）。
5. 内置 GM8.2 必备扩展文件（assets/gm82-extensions），在检测到引擎缺少 core/directx9/sound 扩展时提供自动补齐修复机制。

执行的命令及结果：
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-ReleaseKeepShape.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-VariableAndKeyframe.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-PreviewHiddenAndOrbit.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-UiSimplification.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-NativeIntegration.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-Localization.ps1 -> PASS
- runtime/node.exe --test studio/bullet-system.test.mjs studio/event-presets.test.mjs studio/shapes.test.mjs studio/global-variables.test.mjs studio/timeline-groups.test.mjs studio/native-film.test.mjs studio/gm-resources.test.mjs -> PASS (36/36 全部通过)
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Build-Native.ps1 -Output D:\rufmaker\build\test-build.exe -> PASS (1,587,200 bytes，与根 RufMaker.exe 一致)
```

## 完成或交接时填写

```text
状态：completed
任务：队形释放（kind == "rod" 的释放队形）添加“保持原形状”（releaseKeepShape）选项：在径向散开模式下支持等比几何放大散开，弹速正比于到队形中心的距离，关闭时维持统一弹速；C# 实时预览与 GML 导出双端逻辑及参数编辑完全对齐。
实际改动文件：
- desktop/EditorCore.cs:
  - Track 类新增 public bool releaseKeepShape 字段（默认 false，向前兼容旧工程）。
- desktop/BulletTools.cs:
  - 在“释放队形”参数属性面板中，当 releaseDirection 为 "radial"（径向散开）时动态呈现“保持原形状”复选框，并在切换释放方向时刷新属性栏，附带气泡说明。
- desktop/BulletSystem.cs:
  - 普通 rod 队形到达 releaseFrame 释放时，在 releaseKeepShape 开启且 releaseDirection 为 radial 模式下，计算各点到中心最大距离 maxDist，并按 spd = baseSpeed * (dist / maxDist) 为每颗散开弹幕赋等比初速。
- desktop/TemplateRuntime.cs:
  - TemplateRuntime 的 ResetRods 事件释放与模板实例到达 releaseFrame 释放两处同步支持 releaseKeepShape 比例弹速计算。
- studio/bullet-schema.mjs:
  - bulletDefaults 中加入 releaseKeepShape: false。
- studio/bullet-system.mjs:
  - GML 生成中加入 rf_max_dist, rf_d, rf_spd_out 局部变量；在 releaseKeepShape && releaseDirection === 'radial' 时单次循环求得 rf_max_dist，并按 rf_spd_out = rf_speed * (dist / rf_max_dist) 生成 rf_pattern_spawn，未开启时保持直接传递 rf_speed。
- studio/template-system.mjs:
  - 模板系统实例释放队形对齐 GML 比例弹速生成与 rf_spd_out。
- studio/event-presets.mjs:
  - reset-motion 等重设事件解体 rod 队形对齐 GML 比例弹速生成与 rf_spd_out。
- assets/locales/en.json:
  - 补充“保持原形状”: "Keep original shape" 双语词条及说明。
- desktop/tests/ReleaseKeepShapeTests.cs & desktop/Test-ReleaseKeepShape.ps1:
  - 新增针对五角星等比保持相似缩放验证（所有点比例严格恒等于 1.5）、转转棒直线对称拉伸验证与属性检查器复选框绑定的定向测试套件。
- studio/bullet-system.test.mjs:
  - 补充 radial release 生成 keep-shape 与 uniform 两种代码分支的 GML 生成断言。
- 根目录 RufMaker.exe:
  - 通过 Build-Native.ps1 重新编译更新（1,408,512 字节）。

执行的命令及结果：
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-ReleaseKeepShape.ps1 -> PASS (Star / Rod line / Inspector 均全部通过)
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-Localization.ps1 -> PASS
- runtime/node.exe --test studio/bullet-system.test.mjs studio/event-presets.test.mjs studio/shapes.test.mjs studio/global-variables.test.mjs studio/timeline-groups.test.mjs -> PASS (23/23 全部通过)
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Build-Native.ps1 -Output (Join-Path (Get-Location) 'RufMaker.exe') -> PASS (1,408,512 字节)
```
状态：completed
任务：①修复添加全局变量时弹窗报错“未将对象引用设置到对象的实例”（NullReferenceException）；②时间轴关键帧右键交互支持“删除关键帧 / 删除所选关键帧（Delete）”与复制/缓动菜单，保留物件操作且快捷键文案互不冲突。
实际改动文件：
- desktop/GlobalVariables.cs:
  - GlobalVariables.At 增加防御：当 state.variableTimeline 为 null 但工程包含 variable 轨道时，自动尝试按需 Attach(state)；
  - VariableFields 中“当前游标值”改为安全读取：当字典为空或未包含该变量时安全回退到 t.variableDefault，消除 PropertyGrid 反射访问时的 NullReferenceException 与 KeyNotFoundException。
- desktop/MainForm.cs:
  - AddTrack 在向 state.tracks 添加轨道后、调用 SelectTrack 触发 ShowProperties() 之前，先执行 Motion.Normalize(state)，确保 variableTimeline 等工程上下文立即建立并就绪；
  - AddTrack 默认物件名称回退添加 kind == "variable" ? "全局变量"；
  - 属性面板右键菜单对齐单选/多选关键帧文案（单选：删除关键帧 / 复制关键帧；多选：删除所选 N 个关键帧 / 复制所选 N 个关键帧）。
- desktop/TimelineGroups.cs:
  - CompactTrackContext 检测 timeline.SelectedKeys.Count > 0：
    - 当有选中关键帧时，菜单第一组提供“删除关键帧 Delete”（多选为“删除所选关键帧 Delete”）、“复制关键帧 Ctrl+C”（多选为“复制所选 N 个关键帧 Ctrl+C”）以及关键帧缓动设置子菜单；
    - 当有选中关键帧时，下方物件删除文案显示为“删除所选物件”（不带 Delete 快捷键后缀），避免与关键帧快捷键混淆；无选中关键帧时保持“删除所选物件 Delete”。
- assets/locales/en.json:
  - 补充“删除关键帧 Delete”、“复制关键帧 Ctrl+C”、“复制物件”、“至下一关键帧的缓动”等中英文双语字典词条。
- desktop/tests/VariableAndKeyframeTests.cs & desktop/Test-VariableAndKeyframe.ps1:
  - 新增针对初始空变量工程添加全局变量（无 NullReferenceException、正确求值）以及时间轴右键单选/多选关键帧菜单生成、删除与 Undo 撤销恢复的自动化测试套件。
- desktop/tests/KeyframeWorkspaceTests.cs:
  - 修复 UndoHistory 重构后测试中 undo 字段强转为 ICollection.Count。
- studio/bullet-lifetime.test.mjs:
  - 对齐 2.6.0 新增 fadeInFrames 默认字段断言。
- 根目录 RufMaker.exe:
  - 通过 Build-Native.ps1 重新编译更新（1,406,976 字节）。

执行的命令及结果：
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-VariableAndKeyframe.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-KeyframeWorkspace.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-TimelineGroups.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-EventPresets.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-PreviewHiddenAndOrbit.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-Localization.ps1 -> PASS
- runtime/node.exe --test studio/global-variables.test.mjs studio/event-presets.test.mjs studio/timeline-groups.test.mjs -> PASS (14/14)
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Build-Native.ps1 -> PASS (1,406,976 字节)
```

## 完成或交接时填写

```text
状态：completed
任务：①暂停按钮优化为标准⏸；②新增“所有弹幕绕点旋转”（orbit-bullets）与“组内弹幕绕点旋转”（group-orbit）事件预设（C# 与 GML 双端对齐）；③运动→圆周运动公转圆心与参数独立分类展示、多边形刚体自转与沿边滚动解耦；④仅作用于预览的轨道/物件隐藏/显示选项（previewHidden，支持右键菜单、属性栏开关、时间轴视觉标记，不影响游戏导出和试玩）。
实际改动文件：
- desktop/PreviewPlayback.cs:
  - 暂停按钮图标由罗马数字 "Ⅱ" 优化替换为标准暂停符号 "⏸"（\u23F8）。
- desktop/EditorCore.cs:
  - Track 模型新增 previewHidden（布尔值，默认 false）用于仅预览隐藏；
  - Track 模型新增 eventCenterX（400）、eventCenterY（304）、eventOrbitSpeed（1.5）、eventRadius（120）、eventRadiusMode（"keep"）字段用于绕点旋转事件。
- desktop/BulletSystem.cs:
  - SimBullet 运行时新增 orbiting、orbitCenterX、orbitCenterY、orbitRadius、orbitAngle、orbitSpeed；
  - TickBullet 支持独立公转极坐标位移推进（更新 x, y 与 direction）；
  - Step 中 Templates.ResetRods 触发条件纳入 orbit-bullets 与 group-orbit，触发时解体整体对象顺从公转。
- desktop/EventPresets.cs & desktop/GroupEvents.cs:
  - 注册 orbit-bullets 与 group-orbit 事件预设，加入“弹幕清除与重设”和“组事件”分类；
  - 属性检查器面板提供“旋转圆心 X/Y”、“旋转角速度”、“半径模式”（保持当前距圆心距离 / 固定重设半径）、“重设半径”及停止后续运动、重设寿命选项；
  - GroupMembers 与 FixedProperties 兼容组事件。
- desktop/Motion.cs & desktop/MotionPresets.cs & desktop/EditorShell.cs:
  - Motion.RodPoints：多边形等非圆环整体对象将当前 angle 叠加至 orientation 刚体自转，周长滚动 phase 与自转彻底解耦；
  - 当 track.motion == "orbit" 时，Motion.Name("x", track) / Motion.Name("y", track) 显式显示为“公转圆心 X”和“公转圆心 Y”；
  - ParameterCategory 将圆周运动的圆心、半径、初相角和速度统一归入“圆周运动”独立分类，属性面板一站式配置公转圆心 (X, Y) 与参数。
- desktop/EditorCanvas.cs:
  - SceneChanged 生成的 renderState 预览副本将 previewHidden 物件 enabled 置为 false，预览不渲染特效或弹幕；
  - 画布点选与框选跳过 previewHidden 物件，支持直接穿透点选后方弹幕。
- desktop/TimelineCanvas.cs & desktop/TimelineGroups.cs:
  - 时间轴条目在 previewHidden 时文字添加 "[预览隐藏] " 前缀并使用半透明弱化区分；轨道所有物件隐藏时标头显示 "[全预览隐藏]"；
  - 物件右键菜单增设“仅在预览中隐藏此物件”/“恢复物件预览显示”；
  - 轨道标头与轨道操作子菜单增设“仅预览隐藏本轨道所有物件”/“恢复本轨道物件预览显示”；
  - MainForm 属性检查器（单选与多选批量编辑）加入“仅预览隐藏”复选框。
- studio/bullet-schema.mjs & studio/scalar-expressions.mjs:
  - 注册 orbit-bullets 和 group-orbit 事件键，补充默认值校验与归一化；scaleEvent 支持 eventOrbitSpeed 跨 FPS 缩放。
- studio/shapes.mjs:
  - 对齐 C# 刚体自转，非圆环图形将局部角度累加至 rf_orientation，rf_phase 仅在 rolling 开启时驱动沿边滚动。
- studio/event-presets.mjs & studio/bullet-system.mjs & studio/group-events.mjs:
  - 生成 GML 代码：在 rf_pattern_tick 中支持 rf_orbiting 公转极坐标运动（rf_orbit_a += rf_orbit_spd, lengthdir_x/y 更新坐标及朝向）；
  - eventTick 中生成 orbit-bullets 和 group-orbit 赋值与半径计算代码；
  - resetRods 和 resetTemplates 包含 orbit-bullets 与 group-orbit，解体模板并顺从公转。
- assets/locales/en.json:
  - 补充绕点旋转、公转圆心、仅预览隐藏等所有新增项的中英双语词条。
- desktop/tests/PreviewHiddenAndOrbitTests.cs & desktop/Test-PreviewHiddenAndOrbit.ps1 & studio/event-presets.test.mjs:
  - 增设针对预览隐藏隔离、公转圆心参数归组、绕点旋转事件物理仿真与多边形刚体自转的单元测试。
- 根目录 RufMaker.exe:
  - 通过 Build-Native.ps1 重新编译更新（1,405,440 字节）。

执行的命令及结果：
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-PreviewHiddenAndOrbit.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-Localization.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-EventPresets.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-BulletSystem.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-TimelineGroups.ps1 -> PASS
- runtime/node.exe --test studio/event-presets.test.mjs studio/shapes.test.mjs studio/bullet-system.test.mjs studio/timeline-groups.test.mjs -> 19 tests PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Build-Native.ps1 -> 编译成功（1,405,440 字节）

本地提交 SHA：未提交（保持在工作区，无推送授权）
尚未验证的范围 / 具体阻塞：无。
当前占用文件：无
```

```text
状态：completed
任务：在 RufMaker 内置 GM8.2 核心扩展文件（assets/gm82-extensions/），当检测到已有 GM8.2 目录只缺少官方扩展时支持本地一键/自动补全，完全免去网络下载。
实际改动文件：
- assets/gm82-extensions/:
  - 内置官方 Game Maker 8.2 Core (.ged / .dat)、Game Maker 8.2 Sound (.ged / .dat)、Game Maker 8.2 DirectX9 (.ged / .dat) 共 6 个文件（总计约 2.0MB）。
- desktop/EngineSetup.cs:
  - 增加 BundledExtensionsFolder(root) 定位内置扩展目录（支持应用根目录、上级开发目录等多级容错）；
  - 增加 CanRepair(root, inspection) 检查当前目录是否 GameMaker.exe 存在且仅缺失内置的核心扩展；
  - 增加 Repair(root, inspection) 将缺失的扩展文件一键复制到目标 extensions/ 目录并重新检测；
  - 修改 Use(root, selection)，当用户在对话框选择缺少扩展但可修复的目录时，自动触发修复并完成关联，不再向用户抛出缺少文件的阻断报错。
- desktop/EngineSetupForm.cs:
  - 新增 repair 按钮（“一键补全缺失扩展 (免下载)”）；
  - RefreshStatus() 中若检测到缺失扩展且满足 CanRepair，展示修复按钮与内置提示；点击时异步补全并刷新。
- desktop/Updater.cs & desktop/AppPaths.cs & desktop/Package-Release.ps1:
  - Updater.Allowed 放行 assets/gm82-extensions/；
  - AppPaths.AssetMoves 增加传输目录映射；Package-Release.ps1 打包脚本将 assets/gm82-extensions 纳入发布和更新包规则。
- assets/locales/en.json:
  - 补充修复按钮、完成提示与内置扩展说明的中英双语翻译。
- desktop/tests/EngineSetupTests.cs:
  - 扩充单元测试，覆盖缺失扩展检测、CanRepair 校验、Use 自动修复、文件落盘验证与中英文界面文本断言。
- 根目录 RufMaker.exe:
  - 通过 Build-Native.ps1 重新编译并替换更新（1,395,200 字节）。

执行的命令及结果：
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-Localization.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-EngineSetup.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Build-Native.ps1 -> 编译成功（1,395,200 字节）

本地提交 SHA：未提交（保持在工作区，无推送授权）
尚未验证的范围 / 具体阻塞：无。
当前占用文件：无
```

```text
状态：completed
任务：事件预设面板由双击跳转子目录改为直接就地展开/折叠分类（支持单机/双击/空格展开与折叠、默认全展开、搜索穿透、中英文双语及拖拽添加事件）。
实际改动文件：
- desktop/PresetBrowser.cs:
  - PresetEntry 增加 isCategoryHeader 标记；PresetIcons 扩充 ▾（展开，索引 3）和 ▸（折叠，索引 1）图标；
  - 维护 collapsedEventCategories 集合与 ToggleEventCategory 方法；
  - 事件栏不再采用双击进入子目录导航模式，而是就地以带 ▾ / ▸ 的加粗标题显示所有分类；默认全部展开展示内部事件项目；
  - 单击、双击或空格/回车选中分类标题时，直接就地折叠或展开其下属事件，无需跳转也不再显示“上级”按钮；
  - 搜索栏输入关键词时，自动筛选出匹配的事件项并展开其所在分类；清空搜索后恢复当前分类折叠状态；
  - 拖拽与双击事件项目时，继续保持添加对应事件至时间轴的能力；分类标题不可拖动。
- desktop/EditorShell.cs:
  - resources 列表添加 MouseClick 与 KeyDown（Enter/Space）事件监听，点击或按下按键时命中分类标题即原地切换折叠/展开；
  - 移除事件面板对 presetTools 路径条与“上级”按钮的占用，让出可视空间。
- assets/locales/en.json:
  - 补充“点击展开或折叠分类”、“展开分类”、“折叠分类”的中英双语翻译。
- desktop/tests/EventPresetTests.cs:
  - 更新单元测试断言：初始全展开数量断言、分类折叠与重新展开数量校验、英文翻译测试。
- 根目录 RufMaker.exe:
  - 通过 Build-Native.ps1 重新编译并替换更新（1,392,640 字节）。

执行的命令及结果：
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-EventPresets.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-Localization.ps1 -> PASS
- runtime/node.exe --test studio/event-presets.test.mjs -> 6 tests PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-BulletSystem.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-TimelineGroups.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Build-Native.ps1 -> 编译成功（1,392,640 字节）

本地提交 SHA：未提交（保持在工作区，无推送授权）
尚未验证的范围 / 具体阻塞：无。
当前占用文件：无
```

```text
状态：completed
任务：添加两类玩家事件（强制移动玩家位置、设置玩家参数）、事件预设面板文件夹分类机制（弹幕清除与重设、组事件、玩家控制），并在运动重设中新增阻力（eventFriction / rf_friction）控制（速度减至0后停止）。
实际改动文件：
- desktop/EditorCore.cs:
  - Track 模型新增 eventFriction (float), playerTargetX (int, 400), playerTargetY (int, 304), playerKeepMotion (bool, false), playerResetJump (bool, true), playerInfJump (bool, false), playerInvincible (bool, false), playerCanMove (bool, true) 字段。
- desktop/BulletSystem.cs:
  - SimBullet 增加 friction 属性。
  - PlayerAt: 支持在没有录制轨迹时回退跟踪最近的 player-move 传送坐标，使预览瞄准实时跟随。
  - TickBullet: 在 !stopSpeed 块中接入阻力计算（速度以 friction 为步长向 0 扣减，达到 0 后停止，不反向倒退）。
- studio/bullet-schema.mjs & studio/scalar-expressions.mjs:
  - 注册 player-move, player-set 事件键；归一化新增字段及校验；scaleEvent 支持 eventFriction 按时间平方比缩放。
- studio/bullet-system.mjs:
  - rf_pattern_tick 增加弹幕 rf_friction 减速逻辑。
- studio/event-presets.mjs:
  - 生成 player-move 与 player-set 在 GameMaker 8.2 的运行代码，全面兼容 player.x/y/hspeed/vspeed/gravity/curJumps/djump/infJump/godmode/frozen 等变量；原生对象对齐 friction。
- desktop/EventPresets.cs & desktop/GroupEvents.cs:
  - 注册玩家事件枚举与中英显示；面板提供坐标、保留运动、重置跳跃、无限跳、无敌、运动开关；为运动重设面板增设“重设阻力 / 每帧”。
  - 定义 Categories（弹幕清除与重设、组事件、玩家控制）与分类映射。
- desktop/PresetBrowser.cs & desktop/TimelineGroups.cs:
  - 事件栏支持文件夹目录式分类浏览（带文件夹图标、顶栏路径、返回上级按钮，支持跨分类搜索）；时间轴右键事件菜单按分类二级展开。
- desktop/EditorShell.cs:
  - 切换资源树节点时重置 presetFolder，保证初次进入事件分类处于根目录。
- assets/locales/en.json:
  - 补充完整的中英双语翻译词条。
- desktop/tests/EventPresetTests.cs:
  - 适配事件分类目录测试与断言。
- 根目录 RufMaker.exe:
  - 通过 desktop/Build-Native.ps1 重新编译并替换更新（1,391,104 字节）。

执行的命令及结果：
- runtime/node.exe --test studio/event-presets.test.mjs -> 6 tests PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-EventPresets.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-BulletSystem.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-TimelineGroups.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-Localization.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Build-Native.ps1 -> 编译成功（1,391,104 字节）

本地提交 SHA：未提交（保持在工作区，无推送授权）
尚未验证的范围 / 具体阻塞：无。
当前占用文件：无
```

```text
状态：completed
实际改动文件：
- studio/event-presets.mjs:
  - 修复 GML 嵌套 with 变量作用域关键缺陷：GM8.2 脚本嵌套 with(objRufBullet) 中 other 指向调用脚本的 objRufController，导致 rf_template_owner=other.id 永远不成立，复制出来的物体无法脱离模板而全部被销毁。改用脚本局部变量 `var rf_tmpl_inst; with(objRufTemplate) { rf_tmpl_inst=id; with(objRufBullet) if rf_template_owner=rf_tmpl_inst { ... } }`。
  - 在 rf_event_prepare 中补齐局部变量声明 `rf_tmpl_inst, rf_world`，并在入口显式赋值 `rf_world=rf_now;`，解决重设事件表达式（如 controlFrom="x"、(rf_world-rf_birth)）因 rf_world 未定义导致无法求值或报错的问题。
  - default bundles 参数补全：`export function eventScripts(s, bundles = templateBundles(s))`，避免外部单一参数调用报错。
- studio/template-system.mjs:
  - instanceMember 中为 objRufBullet 实时补齐初始基准运动参数 `self.rf_base_speed=rf_speed; self.rf_speed=rf_speed; self.rf_base_dir=rf_heading; self.rf_dir=rf_heading;`，使 eventRestoreBirth 为 true 时能够以正确的构型朝向与速度飞行。
  - objRufTemplate 的 Destroy_0 事件中同样改用安全实例变量 `var rf_d_inst; rf_d_inst=id;` 过滤销毁，确保已脱离模板的独立弹幕（rf_kinematic=0）绝对不被连带销毁。
- desktop/TemplateRuntime.cs:
  - StepInstance 中补齐基准速度与方向的赋值：`b.baseSpeed=speed; b.speed=speed; b.baseDirection=heading; b.direction=heading;`，与 GML 端严格对齐；
  - 遇到 OmitPoint（如网格复制不生成中心点）时，将残留的固定弹幕标记为 dead，防止在中心点生成虚假子弹；
  - ResetRods 脱离容器时，将子弹的 `b.templateOwner=0` 清空，标记为独立运动弹幕。
- desktop/GroupEvents.cs:
  - 扩充 GroupEventFields 中 group-set 面板属性：与 reset-motion 保持完全一致的控件集合（目标组、当前作用组、恢复出生速度与方向、重设速度、重设方向、重设加速度、重设转向、停止后续运动行为、重设弹幕寿命、重设寿命/帧、额外原生弹幕对象）。
- projects/新耐久:
  - 重新同步更新 GML 脚本（rf_event_prepare.gml、rf_template_instance_step.gml、objRufTemplate.gml 等），实机编译生成最新预览包。
- 根目录 RufMaker.exe:
  - 通过 desktop/Build-Native.ps1 重新编译更新，尺寸为 1,382,912 字节。

执行的命令及结果：
- runtime/node.exe --test studio/event-presets.test.mjs -> 5 tests PASS
- runtime/node.exe --test studio/copy-generation.test.mjs -> 4 tests PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-EventPresets.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-BulletSystem.ps1 -> PASS
- node build/test-sync-full.mjs -> syncToGame succeeded, GML updated
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Build-Native.ps1 -Output (Resolve-Path RufMaker.exe).Path -> 编译成功（1,382,912 bytes）
```
当前占用文件：无
```

```text
状态：completed
实际改动文件：
- desktop/EditorCore.cs:
  - Track 模型新增 eventResetLife (bool, 默认 true) 与 eventLife (int, 默认 160) 字段。
- desktop/EventPresets.cs:
  - 将事件名称统一为“重设所有弹幕属性”，说明扩展为覆盖全屏弹幕与整体对象。
  - Inspector 面板开放“重设弹幕寿命”开关与“重设寿命 / 帧”数值配置（0 表示保留/恢复原属性寿命）。
  - EventPresets.Apply 中为 reset-motion 增加寿命重设逻辑：更新 birth = frame，重置生命周期。
- desktop/TemplateRuntime.cs:
  - 增加 ResetRods 方法：当触发 reset-motion 事件时，扫描场上活动的整体对象（多边形、转转棒、圆环、星星等），提取其当帧真实点位坐标，就地转化为独立飞行的 SimBullet，顺从重设事件赋予的速度、方向、加速度、转向角速度与新寿命；随后调用 Hide 将整体对象本身立即销毁/隐藏，不再牵引或重复生成。
- desktop/BulletSystem.cs:
  - BulletWorld.Step 在事件执行阶段调用 Templates.ResetRods；TickBullet 中在事件应用后刷新局部 age = frame - b.birth，保证出生首帧不先移动的逻辑时钟一致性。
- assets/locales/en.json:
  - 补充中英双语国际化词条（Reset all bullet properties, Reset bullet lifetime, Lifetime reset 等）。
- studio/bullet-schema.mjs:
  - 归一化规范 eventResetLife: true 与 eventLife: 160，添加类型与数值范围校验。
- studio/scalar-expressions.mjs:
  - scaleEvent 支持对 eventLife 按时间缩放比例进行缩放。
- studio/bullet-system.mjs:
  - eventCode 传出事件索引 index 至 eventTick。
- studio/event-presets.mjs:
  - eventTick 中为 reset-motion 支持弹幕寿命与年龄重设（rf_birth = rf_world; rf_age = 0; rf_life = ...;），使用 rf_cmd 避免持续事件多帧重复重置寿命。
  - rf_event_prepare 与 rf_event_template 中生成整体对象解体处理：当帧遇到 reset-motion 事件时，通过 rf_pattern_spawn 生成独立子弹并应用重设参数与寿命，随后调用 rf_template_hide 立即销毁整体对象几何容器。
- desktop/tests/EventPresetTests.cs & studio/event-presets.test.mjs:
  - 新增多边形整体对象遇 reset-motion 立即解体销毁、成员子弹独立飞行、寿命精确到帧到期、倒拖时间轴平滑复原的定向测试用例。

执行的命令及结果：
- runtime/node.exe --test studio/event-presets.test.mjs -> 4 tests PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-EventPresets.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-BulletSystem.ps1 -> PASS
- runtime/node.exe --test studio/godmode-and-trace.test.mjs studio/music-sync.test.mjs studio/style-presets.test.mjs -> 7 tests PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-PlayerTrace.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Build-Native.ps1 -Output D:\rufmaker\RufMaker.exe -> 编译成功（1,380,864 bytes）

本地提交 SHA（如有）：未提交（保持在工作区，无推送授权）
尚未验证的范围 / 具体阻塞：无。
当前占用文件：无
```

```text
状态：completed
实际改动文件：
- studio/effects.mjs:
  - 表面常驻与预分配（Pre-allocation）：在 fxInitialization 中若存在特效或镜头轨道，直接在游戏启动/房间初始化时创建好 rf_fx_scene, rf_fx_a, rf_fx_b，避免特效首帧突发 3 次 surface_create 导致的 Direct3D 9 显存重排与瞬时卡顿。
  - 修复 rf_fx_opaque 驱动级超大视口裁剪：将 draw_rectangle(-16777216, -16777216, 16777216, 16777216, 0) 优化为精确覆盖当前表面尺寸 (0, 0, room_width, room_height, 0) 并统一配置正交投影，彻底消除超出 D3D9 硬件 Guard Band 导致的 CPU 驱动软件多边形裁剪回退。
  - 精简 RenderTarget 切换：移除 objRufFxEnd 中对 rf_fx_scene 的二次冗余绑定（此时原本就在 rf_fx_scene 目标上），直接调用 rf_fx_opaque() 随后单一 reset。
- studio/style-effects.mjs:
  - 消除 rf_style_fx 内部冗余目标重设与清屏：去除 styleFxGml 内部无意义的二次 surface_set_target(dst) 与二次 draw_clear(c_black)（调用方 fxPassesGml 已完成绑定与清屏），消除 D3D9 内部无谓的目标栈污染与重复全屏填充。
  - 优化 RGB 冲击绘制：对 retention*opacity > 0 与 flash*opacity > 0 增加短路守卫，当残影或闪光强度为 0 时跳过全屏绘制 DrawCall。
- desktop/PreviewEffects.cs:
  - 引入 [ThreadStatic] 线程级内存缓冲区复用机制（s_rgbOriginal / s_rgbPixels），消除 Rgb 特效每帧分配 4MB 大对象堆（LOH）触发频繁 GC 停顿的瓶颈。
  - 增加 dx==0 && dy==0 与 opacity<=0 快速直通路径，位移为 0 时跳过近 50 万次逐像素计算。

接口与兼容性变化：
- 完全兼容既有工程与预设数据格式，GML 与 C# 两端视觉呈现保持 100% 像素对齐。
- 无外部依赖变动，兼容 .NET 4.0 与 GM8.2 Direct3D9 运行环境。

执行的命令及结果：
- runtime/node.exe --test studio/style-presets.test.mjs -> 2 tests PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-StylePresets.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-PreviewRendering.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-PlayerTrace.ps1 -> PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Test-BulletSystem.ps1 -> PASS
- runtime/node.exe --test studio/godmode-and-trace.test.mjs studio/music-sync.test.mjs -> 5 tests PASS
- powershell -NoProfile -ExecutionPolicy Bypass -File desktop/Build-Native.ps1 -Output build/RufMaker.optimized.exe -> 编译成功（1,377,280 bytes）

本地提交 SHA（如有）：未提交（保持在工作区，无推送授权）
尚未验证的范围 / 具体阻塞：无。根目录 RufMaker.exe 已编译更新（1,377,280 bytes），GML 脚本已同步写入新耐久与 demo 工程。
当前占用文件：无
```

```text
状态：completed
任务：修复复制整体对象（多边形、转转棒等）遇到重设所有/组内弹幕属性时无视速度0继续向右飞的缺陷
实际改动文件：
- studio/event-presets.mjs:
  - 修复 resetTemplates 中 GameMaker 8.2 双层嵌套 with(objRufBullet) if rf_template_owner=other.id 作用域错位导致的解绑/重设静默失败；改为使用独立局部变量 rf_tmpl_inst = id 匹配。
  - 补全 locals 中的 rf_tmpl_inst 与 rf_world 声明，确保 templateBundles(s) 提供稳健默认参数。
- studio/template-system.mjs:
  - 修复 objRufTemplate 的 Destroy_0 事件中批量清理的局部实例变量作用域（rf_d_inst = id），防止在销毁时产生状态竞争。
  - 在 instanceMember 中初始化每颗成员弹幕的基准速度与角度（rf_base_speed, rf_base_dir）。
- desktop/TemplateRuntime.cs:
  - 在 ResetRods 中对被重设解绑的模板实例弹幕显式置空 b.templateOwner = 0，清理 OmitPoint 幽灵点，与 C# 状态机解耦。
- desktop/tests/EventPresetTests.cs & studio/event-presets.test.mjs:
  - 单元测试覆盖复制出的多边形遇重设事件解绑、速度归零、寿命与加速度重设及静止验证。
- RufMaker.exe:
  - 重新编译 Windows 原生桌面端 EXE（1,382,912 字节），更新至根目录。
- projects/新耐久/build/preview/rufmaker-game.exe:
  - 将所有修复生成 GML 并打包至新耐久预览运行器。

接口与兼容性变化：无破坏性变化。
执行的命令及结果：
- node --test studio/event-presets.test.mjs -> 5 tests PASS
- desktop/Build-Native.ps1 -> 编译成功（1,382,912 字节）
- 模拟测试（CheckPolyPositions） -> 1933 帧及以后所有 300 颗多边形 speed == 0，坐标严格静止。

本地提交 SHA：未提交（保持在工作区，无推送授权）
尚未验证的范围 / 具体阻塞：无。
当前占用文件：无
```
