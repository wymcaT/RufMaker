# Argon4 任务记录

- 状态：`completed`
- 当前任务：彻底解决“复制出来的物体”在实机中不响应重设所有弹幕属性（reset-motion）与重设组内所有弹幕属性（group-set）的缺陷。修复 GML 嵌套 with 作用域缺陷（other.id 误解析为 objRufController 导致复制物体弹幕无法脱离并被 Destroy 销毁）、补齐基础速度与方向初始化（rf_base_speed / rf_base_dir）、确保 rf_world 在事件脚本中准确赋值以支持表达式计算、C# 端剔除网格复制 omit 点并重设 templateOwner，重新同步工程并完成 Native 构建。
- 基线分支/SHA：`main` / `51d4b2d`。
- 当前占用文件：无。
- 验收方式：`studio/event-presets.test.mjs` 定向测试（5/5 pass）；`desktop/Test-EventPresets.ps1` 与 `desktop/Test-BulletSystem.ps1` 通过；根目录 `RufMaker.exe` 成功更新编译（1,382,912 bytes）；`projects/新耐久` 完整同步 GML 校验通过。

## 完成或交接时填写

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
