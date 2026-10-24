# desktop/：原生编辑器与预览

继承根目录 `AGENTS.md`。这里使用系统 .NET Framework 编译器，保持当前语言/API 兼容；不要仅为了新增功能引入 SDK、框架迁移或网页 UI。

## 常用入口

| 内容 | 文件 |
| --- | --- |
| 数据模型、快照、桥接、贴图读取 | `EditorCore.cs` |
| 主窗口与操作路由 | `MainForm.cs` 的各个 partial 文件 |
| 参数值、属性关键帧、运动、缓动 | `Motion.cs`、`RandomParameterUI.cs`、`ParameterInspector.cs` |
| 表达式与种子上下文 | `RandomParameters.cs`、`ExpressionContext.cs`、`ExpressionEditor.cs` |
| 时间轴布局、框选、移动/拉伸 | `TimelineCanvas.cs`、`TimelineLayout.cs` |
| 画布取景、拖动、曲线控制点 | `EditorCanvas.cs`、`CurveControls.cs` |
| 预览绘制、缓存、合成 | `PreviewRenderer.cs`、`PreviewEffects.cs`、`Camera.cs`、`StyleEffects.cs` |
| Beta 行为模拟与界面 | `BulletSystem.cs`、`BulletBehaviorForm.cs`、`BulletTools.cs` |
| 常规飞行、拖影、彩虹 | `BulletFlight.cs`、`Trails.cs`、`Rainbow.cs` |
| 预设与携带资源 | `MotionPresets.cs`、`BuiltinPresets.cs`、`StylePresets.cs`、`PresetLibrary.cs`、`PresetBrowser.cs` |
| 原生底片、试玩、玩家位置与轨迹 | `NativeFilm.cs`、`NativeGamePreview.cs`、`NativeTestPlayer.cs`、`PlayerTrace.cs` |
| 原生音频与播放定位 | `NativeAudio.cs`、`PreviewPlayback.cs` |

## 改动时注意

- `Track` 表示物件，`lane` 只是轨道编号。组引用和来源引用使用稳定 ID；复制时处理 `parentId`、`targetSource`、行为 `templateId`。
- 新数据不能只存在于控件中。同步检查 `Track.Snapshot()`、JSON 字段、`Motion.Normalize()`、预设保存与实例化。
- `Motion.EditValue()` 保持“无键改基础值、有键改当前键”；不要所有编辑都偷偷插键。
- 同步语义对应 `studio/`；取整用现有 `BulletSystem.Round` 等约定，注意 C# 默认银行家舍入与 JS/GM 的差异。
- 渲染在后台工作。修改场景后经 `SceneChanged()` 更新快照/缓存，不从后台线程操作 WinForms 控件，不让旧渲染结果覆盖新场景。
- 条件显示属性和新增按钮时检查窄侧栏、窗口缩小和高 DPI；优先沿用 `WheelSafe*`、`ParameterInspector`、原生弹窗。
- 中文原文保留为存储标识，通过 `Language` 呈现英文；新增词条写 `assets/locales/en.json`。不要翻译公式、用户名称或 GML 源码。
- 新增 `.cs` 要加入 `Build-Native.ps1`；测试扫描源码与产品构建的文件收集方式不同。

## 验证

根目录运行 `desktop/Build-Beta.ps1` 构建当前测试版。按改动选择 `Test-BulletSystem.ps1`、`Test-PlayerExpressions.ps1`、`Test-Presets.ps1`、`Test-Localization.ps1` 等；完整命令见开发入口。不要将测试产物写进用户工程。
