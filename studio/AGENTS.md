# studio/：后台、GML 生成与原生兼容

继承根目录 `AGENTS.md`。这是 Node.js ESM 后台，不是需要启动网站的前端。使用根目录 `runtime/node.exe`；`package.json` 的旧 `version` 不是应用版本来源。

## 常用入口与 C# 对应

| 后台 | 职责 / 对应桌面端 |
| --- | --- |
| `bridge.mjs` | 逐行 JSON 协议；对应 `NativeBridge` |
| `project-files.mjs` | 工程定位、新建、歌曲与初始地形 |
| `project.mjs` | 工程同步、生成对象/脚本与资源索引 |
| `motion.mjs` | 归一化、属性与缓动代码；对应 `Motion.cs` |
| `random-parameters.mjs` | 表达式、种子、脚本生成；对应 `RandomParameters.cs`、`ExpressionContext.cs` |
| `bullet-schema.mjs` | Beta 默认值、分组、循环片段展开 |
| `bullet-system.mjs` | Beta 出生、行为、控制和分裂 GML；对应 `BulletSystem.cs` |
| `bullet-flight.mjs` | 常规追踪与飞行；对应 `BulletFlight.cs` |
| `shapes.mjs`、`rainbow.mjs`、`trails.mjs` | 整体对象、彩虹与拖影 |
| `camera.mjs`、`effects.mjs`、`style-effects.mjs` | 镜头与屏幕合成；对应桌面同类模块 |
| `native.mjs` | GM 编译、游戏打包、试玩进程 |
| `native-import.mjs`、`native-blocks.mjs` | 外部工程识别与原生代码块编辑 |
| `native-preview.mjs`、`native-play-session.mjs`、`native-checkpoint.mjs` | 原生试玩、回退测试点 |
| `native-film.mjs`、`native-compositor.mjs` | 原生底片准备、合成 |

文件不存在或已改名时先搜索实际实现，不按表格臆造 API。

## 约束

- stdout 是协议通道。日志使用 stderr 或项目的日志目录；保持每个请求 ID 的响应格式。
- `project.mjs` 导入时定位工程；`bridge` 的 `load` 会同步工程。测试必须先选隔离工程，再动态导入。不要默认操作 `projects/rufmaker-demo.gm82/`。
- 保存的是原始可编辑片段与引用；展开循环片段只用于渲染/导出，不把展开后的轨道覆盖回源 `rufmaker.json`。
- 新字段的默认值、归一化和校验与桌面模型一起改；不要因为缺少新字段拒绝所有旧工程。
- 新生成脚本/对象要写入相应 `index.yyd`、`tree.yyd`，避免磁盘有文件但 GameMaker 不收录。
- GML 数值使用 `gmlNumber`；方向以度为单位，三角函数按现有弧度约定。避免局部变量覆盖内建量；不要使用当前 GM8.2 扩展禁止的重启接口。
- JS、C#、GM 的出生年龄、取整、随机取样与事件边界保持一致；完整试玩仍读取真实玩家，录制轨迹服务于编辑器预览。
- 外部原生代码中可能有先定义随机变量、后续使用的历史依赖。原生底片必须完整执行历史；不要用孤立求值替代原游戏逻辑。
- `native.mjs` 当前编译顺序是 `[projectFile, '--build', output]`，工作目录是已选 GM 安装目录。若另一个安装不支持该参数，先核对版本与路径，不猜另一套调用顺序。

## 验证

优先运行修改模块的 `*.test.mjs`，并执行对应桌面测试。预览公式测试通过之后，改变游戏行为的任务还要验证隔离 GM 工程；具体入口与当前验证边界见开发入口和交接状态。
