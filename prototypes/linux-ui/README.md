# RufMaker Linux UI 原型

这是 2026-11-13 文案调整版的隔离源码快照，不替换仓库根目录 Windows 编辑器，也不是正式 Linux 游戏版本。

界面包含：物件搜索、对象概览、行为摘要与编辑入口、父子/模板关系导航、紧凑布局和深色主题。保留独立物件/轨道与高级参数。共享采样表和模拟相关源码是该已交付原型的依赖，一并保留；未宣称已合入正式版或经过 GM 原生验证。

## 构建

需要 Python 3、Mono/mcs、libgdiplus、FFmpeg/ffplay、Node.js，以及运行 GUI 所需的 Linux 桌面。源码不附带第三方运行库、用户示例工程、歌曲、底片、截图或编译产物。

```sh
python3 prototypes/linux-ui/build.py --tests
prototypes/linux-ui/run.sh /absolute/path/to/a-copy.rufmaker
```

构建脚本在 `engine/` 下创建到仓库公共 `assets/`、`templates/` 的链接。使用非系统 Mono 时，构建传入 `--runtime /absolute/runtime`，运行设置 `RUFMAKER_MONO_RUNTIME=/absolute/runtime`。测试使用生成的 2 秒正弦音，不需要私有音频。

请只打开工程副本；加载及保存可能同步修改关联 GameMaker 文本工程。

## 验证范围

本轮重新编译通过；结构搜索完整性、祖先保留、循环引用保护、非修改性以及音频元数据/波形解析测试通过。Node 定向测试 21 项通过（命名采样表、编辑/保存安全和 GML 保存）。Fontconfig 报缓存不可写警告，不影响本次无窗口测试。

此前原型曾实际验证对象选择、10→12 扇区编辑、预览、保存和撤销。最近中文文案版没有重新取得 GUI 截图。本轮没有重新完成桌面交互回归，也没有 Windows/GM8.2、碰撞、音频听感或长期同步验证。

GM8.2 原生运行/导出在 Linux 未移植，相关入口被禁用；GML 预览不完整警告保留。不应以模拟预览代替原生游戏验收。

## 与上游的关系

快照放在 `prototypes/linux-ui`，主产品源码保持 9bcc4d4 不变，避免把原型覆盖到正式版。归档无 Git 历史，不能据此宣称它与主线的祖先关系；后续正式整合仍须逐项审查与双端回归。

音频波形缓存及原工程派生的 oracle JSON 未纳入仓库；依赖这些资料的历史测试不能视为开箱即跑。本轮所列 21 项测试与合成音频测试不依赖它们。
