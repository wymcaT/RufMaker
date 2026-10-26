# Codex 任务记录

- 状态：`in_progress`
- 任务：用户明确确认“保留原更新方式，继续推送发布”；发布main/v2.2.0，保留原SHA256与旧更新器兼容。
- 开始时基线：`b88164b`，分支 `main`；保留已有用户工程改动（12 文件，817 增/236 删）。
- 当前占用文件：README中英文、v2.2.0说明、交接文档、RufMaker.exe及发布产物；集成此前已验证的全部2.1.x/2.2.0产品源码和测试。基线b88164b/main；保留用户工程。AGENTS.md、Package-Release.ps1与release.yml保持原流程，Updater.cs仅版本变化。
- 本轮发布计划：重新构建两个ZIP，检查完整包/清单/旧更新器兼容；精确提交产品改动并推送main/v2.2.0，由Actions发布；最后验证线上附件下载与检测更新。当前尚未远程写入。公共API此前遭共享IP限速，必要时用GitHub网页和连接器查Actions。
- 本轮约定：用户明确选择裁短被覆盖的其他物件；中间覆盖保留前后两段、全覆盖移出时间轴，同轨执行，跨轨仅吸附。验收含连续拖动稳定性/吸附开关/裁剪的时间历史与Undo，14种预设逐帧位置/参数/保存复制、C#/GM及构建。
- 本轮验收计划：原生重叠选择候选/取消/房间实例/多选与拖动定向测试；独立方向和自定义范围分布、倒拖、C#/GML对照；中英文和本地EXE构建。
- 本轮验收：五种拖放入口/快捷参数/循环与不同方向模式；变量赋值/重复/引用/初值/随机历史/倒拖/保存复制预设；C#/Node定向测试、隔离GM运行数值对照、中英布局、EXE构建。仅本地交付，无推送授权。
- 验收结果：Test-MotionEditing及Presets/RandomTimeline/GenerationPresets/Localization/EditorPerformance/ShellLayout通过；Node20/20；本轮201条GM弹幕状态、17帧变量历史、F5第85帧恢复对照通过；2.2.0本地EXE构建通过。
- 本轮发布：2.0.0 已正式发布；本轮继续工作默认本地，不自动追加 Git 推送或新 Release。
- 本机产物：根目录 `RufMaker.exe`，ProductVersion `2.2.0`，FileVersion `2.2.0.0`，1,212,928 bytes；已构建并File.Replace安全替换，未结束用户进程。原2.1.1在build/motion-editing-check/RufMaker.2.1.1.previous.exe；用户须保存并重启旧进程使用新版。

## 2026-10-26：2.2.0逐帧动作与时间轴覆盖剪辑（完成，本地未推送）

- 用户先要求推开重叠物件，经异步问答明确选择“裁短其他物件被覆盖的部分”。最终语义：同lane手中物件保留鼠标落点，边缘覆盖裁短、中间覆盖保留前后两段、全覆盖移出时间轴；跨lane仅吸附，不裁剪。没有额外发布授权。
- MotionPresets改为MotionPresetData参数（name/easing、时间域、起终点/峰、中心、振幅、起止半径、圈数、初相角、停顿占比）+原生X/Y关键帧。14种动作每帧生成线性连接的位置键；参数本身不加关键帧，绑定期间隐藏X/Y输入、禁止位置键拖动/插入/粘贴，切“手动关键帧”解除绑定并保留结果。圆周仅显示单个半径，螺旋分别显示起止半径；圈数/停顿拖动步长.01。画布及批量位置平移会改路径参数；时段拉伸重铺，复制/预设/换FPS/片段展开/撤销保留参数。旧动作没有可靠的预设标识，不猜测迁移，须重新应用一次。
- Track新增motionPreset、可空clockStart/clockEnd、clipSourceId；Snapshot深复制预设，keyIndex为ScriptIgnore运行缓存。密集路径按属性建立排序键索引，预览二分查找，导出GML用平衡分支树（不逐帧扫描长else-if链）。新数据仍有真实可执行的逐帧XY键，JS归一化保留参数元数据；预设和工程对绑定路径的键数上限调整为2,010,000，普通物件仍10,000。
- Timeline拖动期间冻结布局，单独绘制手持物件覆盖层，鼠标跨行以手势开始时的行映射确定lane，不受重叠Stack重排影响；松手一次性裁剪并提交一次Undo。切分保留原时间域与隐藏关键帧（移动到零帧可出现合法的内部负帧），后半段不重置运动/角速度/发射波次/变量赋值/特效缓动/滚动彩虹。分组随子树切分、父ID重映射，避免无父后代；若来源也是被引用模板，保留原模板定义并裁剪可见副本。clipSourceId让原控制事件仍能匹配切分的后段。超过现有200物件限制时保留其他物件并提示，未扩大项目物件数量上限。
- 跨轨物件边缘吸附默认开，8像素捕捉范围，黄色引导线；同轨优先首尾衔接到end+1，避免同一边界帧重复发射。吸附菜单开关保存在TimelineView.clipSnap，旧布局缺字段默认为true；Alt暂时绕过。其他轨道不裁剪。
- 时间原点同步核对：C# Motion/ExpressionContext/BulletSystem/TemplateRuntime/GlobalVariables/PreviewEffects/StyleEffects/Rainbow；JS motion/bullet-schema/bullet-system/template-system/global-variables/effects/rainbow/shapes/random-parameters及project脚本。裁剪片段强制走历史弹幕演算，周期片段用原时间域展开后再裁范围，负字面量减法加括号。未改GM原生导入底片、用户歌曲或项目。
- 验证：Test-MotionEditing通过，含14种逐帧位置、5101帧路径索引复用、参数面板无XY、单参数与覆盖Ctrl+Z选中保持、FPS翻倍重新铺键、同lane多次往返拖动不跳行/未松手不剪、边缘/中间/全覆盖、分组、共用模板、首尾/跨轨吸附和偏好保存。Presets、RandomTimeline、GenerationPresets、Localization、EditorPerformance、ShellLayout通过；Node定向20/20。中英动作参数页离屏截图已查看，未控制用户桌面。性能合成测试40物件/6400键，标量编辑中位1.68ms、撤销3.87ms，参数控件复用；不当作实际游戏FPS。
- GM最终隔离项目build/motion-editing-check/GM Generators 1792990110969/GameMaker.gm82：201条实际弹幕状态、17帧变量值、F5第85帧预热一致，包含14条密集路径、原发射时钟/控制来源别名、循环片段裁剪和负时钟/隐藏负关键帧。日志gm-final2.log。探针增加可选RUFMAKER_TEST_BUILD/SAMPLER，原默认生成模板检查不变；修正源索引按展开后的轨道表读取。
- 过程问题：首次新测试有held局部变量命名冲突（测试编译），后修复；GM第一次把展开后的索引映射到原片段表导致source不符（gm.log保留），修正探针后通过；参数步长尝试只传Property的ParameterActions导致测试UI构造NRE，已撤回这种用法，改为参数标签识别步长，最终原生面板测试通过。失败均发生于隔离测试，根EXE交付前未替换。最终日志和截图均在build/motion-editing-check；部分C#日志重跑更新，以本记录说明原因，不将初始失败冒充通过。
- 构建2.2.0，根EXE1,212,928 bytes，File.Replace安全替换，旧2.1.1留在build备份，用户进程未关闭。README中英文、docs/motion-timeline-editing.md、docs/releases/v2.2.0.md及开发入口已更新。没有新增产品.cs文件，无需额外Build-Native源码项；新增测试脚本/测试.cs未纳入产品。
- 没有暂存/提交/push/Release。原demo12文件817增236删保持；用户项目、个人耐久、歌曲和参考资料均未修改。无必做待办，待用户重启后交互验收。

## 2026-10-26：2.1.1重叠选择与独立随机方向（完成，本地未推送）

- StageCanvas统一点击/拖放的命中列表，过滤模板专用、禁用、时间段外或隐藏父级物件；局部模板预览仍可选模板根。支持物件中心、整体成员、按大小/旋转变换的贴图区域，以及未被底片隐藏的房间实例。点按重叠位置在MouseUp打开原生暗色候选菜单，显示名称、lane+1、类型/实例坐标和序号；确认后选中明确目标，取消保持原选择。Ctrl/Shift可多选，直接拖动优先已选候选，保留拖动单次Undo、曲线/端点控制和中键取景。
- 360随机模板默认angle=random_range(0,360)，randomScope=bullet。快捷行为页新增“新弹幕方向 / 度或表达式”，提交前验证，支持自定义random_range/choose或固定角度；编辑子模板原字段，不新增工程字段。
- 根因：出生随机种子直接加i*9176，Park-Miller第一抽样使相邻方向呈等差；只变波次不能解决。对randomPerInstance=true的生成模板出生取样增加确定性MixSeed，GML用rf_random_mix对应（正31位XOR/整数除法/模乘，中间乘积低于2^53）。实例偏移补birth*104729+serial*13007，解决同帧各成员/母弹共用子模板时复用同一方向。只在标记出生上下文启用，旧普通轨道与静态参数随机保持原算法；倒拖checkpoint仍恢复serial，固定种子可复现。全局变量/生命周期/行为执行顺序未改。
- 修改：desktop/EditorCanvas.cs、GenerationPresets.cs、ExpressionContext.cs、RandomParameters.cs、BulletSystem.cs、Updater.cs、tests/GenerationPresetTests.cs；studio/random-parameters.mjs、bullet-system.mjs及random-parameters.test.mjs；assets/locales/en.json，README中英文、教程及v2.1.1本地说明。此前未提交的2.1.0改动保留；未新增产品.cs，无需调整构建源列表。
- 验证：Test-GenerationPresets通过（跨成员/每颗独立、范围/choose、倒拖、候选底层/房间实例/取消/多选/明确主选择/拖动/缩放/中英文等）；Node定向20/20；Test-RandomTimeline及Test-Localization通过。中文/英文候选菜单和随机方向参数页离屏截图已查看，均暗色可读，未操作用户桌面。
- 原生GM：build/generation-presets-check/GM Generators 1792985836073/GameMaker.gm82，2254条实际弹幕状态、17个变量历史时刻以及F5第85帧恢复对照通过，含自定义30..150、choose四方向和同帧成员分别取样。日志direction-gm.log，非人工试玩。测试新增辅助脚本后首次Node回归有旧测试固定脚本数不符，改为仅需要时生成rf_random_mix后20/20通过，初始日志保留。
- 构建2.1.1通过，根EXE1,195,520 bytes；旧2.1.0保留build备份，正在运行的旧进程未关闭。没有暂存/提交/push/Release；用户demo12文件原差异（817增236删）保持。无必做待办，待用户实操验收。

## 2026-10-26：2.1.0 生成模板与全局变量（完成，本地未推送）

- 左栏“预设”新增“弹幕生成模板”，五项为保持形状扩散、统一速度扩散、360°随机弹、对玩家自机狙和溅射。双击应用到选择物件，或拖到画布/时间轴物件。自动建 templateOnly 发射器及生成规则，来源同轨绑定、Undo与复制/预设沿用完整依赖。
- 普通对象/整体/分组使用来源时间段；拖到 emitter 默认绑定每颗飞行弹幕的 bulletActions，右栏“生成来源”可切回发射器本体。默认每 fps/2 帧、持续0表示至来源结束。形状保持速度为 source-distance*speed/100；统一速度仅按径向定向。继承出生外观，保持形状/统一扩散继承大小。溅射为360/n等距，支持固定、按波重新随机、每轮递增角度。
- 右栏对标记生成规则使用紧凑面板，直接改重复间隔、首次延迟、子弹数量/速度/寿命模式。新弹幕寿命支持 fixed/room/view、边界余量、销毁过渡；完整拖影6种样式与衰减/漂移字段保存在子模板。“新弹幕行为…”直接打开子模板的 BulletBehaviorForm，应用后仍选来源；子弹新增分裂/继续生成默认允许到4代。寿命/拖影高级分组默认折叠，控件保留和暗色样式沿用原实现。
- 新 Track 字段 variableName/variableExpression/variableDefault/variableRepeat，kind=variable；工具栏/时间轴右键新增入口。命名 global.name（统一小写，rf_保留），初值在赋值前生效；开始帧赋值，repeat0一次、正数间隔至end含边界，之后保持。同帧按轨道顺序赋值，在本帧发射前执行。支持自身上次值、变量间依赖、random/choose/floor/point_distance等原有数学；未定义引用报错。变量重命名更新参数公式，不改手写GML；仍被引用时阻止误删最后定义。
- C# GlobalVariableTimeline 按事件生成缓存历史、旧帧读取原样本；ExpressionContext 按歌曲时间取值，生成模板使用globalSource/instance shift而非局部年龄。循环片段会展开变量时间，FPS/时间倍率保留正重复间隔至少1帧。JS同实现/归一化/公式编译，生成 rf_global_init/step；所有变量强制进历史演算路径，F5预热逐帧赋值，循环结束补一次镜头/固定队形姿态。GML可读真实global字段。C#缓存/owner/shift用ScriptIgnore且快照不共享缓存。
- 新持久字段：BulletAction.generationPreset、spawnDirectionMode(fixed/random/add)、spawnWaveAngleStep；Track.randomPerInstance（只对新模板默认true）。后者每生成实例加入出生帧随机偏移，解决子模板各轮局部第0帧重复同一随机结果；旧物件缺省false。随机相位使用已有种子/slot算法，与C#/GML一致。基础值/新字段均经JSON/Snapshot/预设保存，Build-Native显式列表已加入GenerationPresets/GlobalVariables。
- 保存自定义预设与Ctrl+C会带生成/分裂模板依赖以及参数引用的变量；导入/复制遇同名变量自动改名并重写复制内容中的引用。变量依赖保留为真实赋值轨道。PresetLibrary和schema支持variable，发射方向/随机数不靠临时UI状态。
- 定向验证：Test-GenerationPresets（5种默认值、源生命周期、移动母弹、两种溅射相位、倒拖、变量依赖/默认/循环/自身递增/片段局部时钟、快照、预设FPS/重名、左栏/拖放/紧凑字段、子寿命/拖影指向和子行为代数）；Node global-variables/template-system/bullet-system/random-parameters/player-expressions 19/19；Test-TemplateSystem、Test-PlayerExpressions、Test-Presets、Test-Localization、Test-EditorPerformance、Test-ShellLayout通过。性能检查单值/关键帧仍保留控件，未将合成耗时当真实FPS。
- GM对照最终项目 build/generation-presets-check/GM Generators 1792984144714/GameMaker.gm82：1777条实际弹幕状态，含fixed/room/view、fade/shrink、custom/shrink-fade拖影参数、转向和移动母弹；17个时刻的变量值与F5在85帧历史恢复均匹配C#，无runtime错误日志。位置容差.01，队形PointF派生角沿用原探针.002度，其余数值.00001。不是人工试玩。
- 过程记录：早期GM探针取样用了控制器同名局部变量而非self字段，错误读为speed0/index0；修正探针后遇到PointF派生角约1e-5差值，按既有模板探针标准处理，最终通过。最初截图因隐藏父容器为空白，改为独立Panel离屏绘制并查看；中英紧凑生成页、变量页已检查。测试button.Visible因父窗口隐藏为false，改检查Available；真实入口逻辑不因此改变。日志均保留在 build/generation-presets-check。
- 交付根EXE2.1.0（1,190,912 bytes），先构建临时再File.Replace，旧进程未关闭。README中英文、本地发布说明docs/releases/v2.1.0.md、完整教程docs/generation-presets.md和开发入口已更新。本轮修改包括 ParameterInspector 新分组默认折叠；没有修改用户耐久、歌曲、自定义预设或原始引擎安装。
- Git仍以b88164b/main为基线，功能改动未本地提交/暂存、未push/Release；原demo12文件817增236删保持。新源码、测试和文档需要下次提交时精确暂存，不能git add .混入用户工程。无必做待办；未获用户新界面实操验收。

## 2026-10-25：可编辑工程与导出版本不一致修正（完成，未推送）

- 用户询问工程是否修改，读取发现原 Neko Pulse/GameMaker.gm82/rufmaker.json 在19:12:42已变为旧63物件/固定种子版，晚于19:07最终EXE导出；文件回写符合仍打开旧状态时自动保存的情况，但未借UI确认来源，不断言具体进程。
- 保留当前原工程，使用自带 createProject 新建 projects/examples/Neko Pulse Mix/Neko Pulse Mix.rufmaker，从本轮 compose 配方重建130物件、13组随机层、全曲持续出弹的最终版；避免再次向原工程写回。临时脚本改为接收 RUFMAKER_PROJECT 或明确资产路径，新建阶段不导入默认 project.mjs。无产品源码变动。
- 通过 studio/bridge.mjs 真实 load/返回/退出核对 descriptor、130物件、randomizeOnPlay=true、13组随机层；与已通过 GM runner-project-3 的全部130个轨道 deepEqual。随后新工程编译成功，独立游戏 游玩/Neko Pulse.exe 为4,871,545 bytes，ZIP12,852,428 bytes，源状态 SHA256 写交付校验.json。只是新工程ID/路径，没有更改已验证弹幕，不重复整曲探针。
- 新旧 README 标明最新可编辑入口及原工程回写状况；原63物件用户时间轴、其他工程和正在运行的编辑器均未操作。无UI控制、无本地提交、无push/release。当前后续编辑应选择 Mix 工程，不再把原 Neko Pulse 当最终可编辑版本。

## 2026-10-25：人是猫 · Neko Pulse 本地完整关卡（完成，未推送）

- 用户最初要求简单耐久，随后连续要求加快节奏、弹速、密度、增加单源随机方向扩散和其他随机弹，不要空隙段。最终交付高速混合版，全长 136.28 秒、50 FPS、130 个可编辑物件；148 BPM/-405 ms 来自歌曲旁 maidata 元数据，FMOD 自动检测的 98.9 BPM 未直接采用。音乐复制到新工程 Data/Music/song-2eef7530f2c7.mp3，原件只读。沒有改用户其他工程。
- 工程入口 projects/examples/Neko Pulse/Neko Pulse.rufmaker；真实文本 GM 工程在 GameMaker.gm82/rufmaker-demo.gm82。游玩/Neko Pulse.exe 为最终独立游戏（4,871,724 bytes），配套 Data 必须保留；便携 ZIP 12,852,594 bytes。README 记录控制、10 段时间、编辑入口、随机设置、来源和验证边界；预览目录含 6 张 GM 实际输出及关卡总览。
- 弹型为移动缺口快雨、错拍交叉扇形、快旋转环、star2/heart 周期复制扩散、双源蛇形、随机初相位双环、随机角速扇形、减速后加速环。三条全曲随机底层覆盖开场至尾奏；无段落清弹，上段尾弹自然延续。星/心使用随库原脚本，来源保留，副本冻结出生彩虹帧及大小，速度按 source.x/y 距离 *0.035。随机表达式保存在参数字段，游玩默认新随机种子，预览可重现。
- 关卡配置全用 RufMaker 字段/关键帧/生成行为，少量本工程 GML 处理 R 从当前耐久重来（room_goto，无 game_restart）、死亡记录和原生 CLEAR 房间；未修改 C#/JS 引擎公式或构建根编辑器。临时 compose/create/assets/分析/探针/导出脚本均在 build/neko-pulse。用户明确不要桌面控制后，改走 RufMaker 自身 Node 接口和 C# 预览演算，无后续键鼠操作。
- 参考视频：BV1Hjt4zeENo《春天～The Spring》、BV1w1421X7w6 Noisepike、BV1RJ411W7pz Noisepike。公开低清文件和联系表在 build/neko-pulse/references；第三份后半样本不完整，仅参考前段。没有复制参考影片/立绘/原关卡代码到交付包。
- 定向验证：build/neko-pulse/AnalyzeAudio.exe 得到 136.28 s；Probe.exe 调实际 C# Motion/PreviewRenderer/BulletWorld 渲染 12 时刻并逐帧检查 6815 帧，参考种子飞行弹峰值 350，保守矩形避让的地面路线存在（X73..433，最多每帧3像素）。未声称所有随机种子无死。最终 export.mjs 使用 studio/native.mjs 成功编译、打包。未改语义，不重跑无关产品测试。
- GM 隔离副本 runner-project-3 固定种子20261025，程序设置参考路线并冻结玩家物理/键鼠输入；原碰撞仍有效，自动执行 R 重试、强制死亡后 R 重试，再跑完整曲到 CLEAR，verify.ini 含 run=3/forced_death=1/clear=3，无 game_errors.log。不是人工试玩或完整输入物理验证。真实帧保存后已查看星形段与 CLEAR，并形成总览。
- 失败记录保留：第一次受限编译 0xC0000142，获审核后本机编译成功；runner-project 初版探针文本文件句柄报错，改 INI/内嵌路线；runner-project-2 未冻结玩家物理的测试在1643帧死亡，未将其称通过。最终碰撞/流程探针和首次 C# 演算通过，后来仅修改 CLEAR 的 EASY→MIX 文案及音频缓存，最终游戏已重编译。旧日志不删除。
- 本轮无本地提交、无推送、无 Release；音乐、个人关卡、录像和缓存未暂存入 Git。根 RufMaker.exe 仍2.0.2，用户原 demo 12文件/817增236删保留。交付无必做待办，难度体验待用户实际游玩反馈。

## 2026-10-25：2.0.2 右栏原地展开、模板完整编辑与暗色 UI（完成，未推送）

- 用户明确要求三项修复。复现发现普通 PerformClick 测试不足：用不激活的屏外原生测试窗取得真实标题焦点，WinForms 滚动从 -626 跳至 -391，点击后又至 -6。ParameterInspector 改用 InspectorScrollPanel，仅在标题焦点/折叠布局期间保持 DisplayRectangle 偏移；ToggleSection 在提交输入前保存偏移，保留标题焦点而非 ActiveControl=null。实际标题视觉锚点及滚动前后保持 -626；普通参数的键盘聚焦仍能自动滚入视野。控件保留，工作区折叠格式不变。
- 模板引用卡片新增可选回调 TimelineCanvas.EditTemplateRule(source,bullet,index)，双击按绑定来源、@spawn/@bullet 与规则序号调用。MainForm 选择来源、定位对应行为，再调用与右栏“展开编辑”共用的 ExpandBehaviorEditor；直接打开现有 BulletBehaviorForm 完整编辑器，不跳转歌曲时间或进入局部预览。未绑定模板卡片沿用定义编辑回调，主窗接到完整行为入口。局部动画预览移到模板右键“预览模板局部动画”，双语提示/README/范例教程同步。新回调是 UI 接口，无持久工程字段或后台变化。
- DarkStripRenderer 统一工具栏、菜单及子菜单的边线、分隔线、箭头、选中/按下/勾选背景和文字；Language.Attach 给 ToolStrip 应用并处理后续动态项目。子菜单可继承 renderer 却尚未样式化，故按控件实例弱表登记，保证各自项目实际处理一次。排除 ToolStripControlHost 的自定义前景，保留粉色导出按钮的深色文字。NumericUpDown 原生增减按钮/边框和可输入 ComboBox 箭头补绘深色，保留原生输入与按钮逻辑；行为窗口按钮去除系统浅色边框。系统滚动条未重写。
- ShellLayout 增加屏外原生焦点/滚动位置/标题锚点、展开控件复用与正常键盘滚动；两种模板引用双击实际打开正确完整窗口、模式与规则序号，取消即关闭，检查右栏同入口、局部预览保留、工程不变；常规/经典工具栏底边像素、递归/动态菜单前景、暗色数字框/增减值、可输入组合框绘制。临时测试对话框自动取消，无遗留错误弹窗。单纯 Panel/PerformClick 的旧探针曾通过，屏外焦点探针确实失败后才修正；日志保留。
- 通过 Test-ShellLayout.ps1 -OutputDirectory build/ui-interaction/check（shell-keyboard-final.log）、Test-EditorPerformance -Report ui-interaction（performance-final.log）、Test-Localization（localization-final.log）、Test-TemplateSystem（templates-final.log）和 Build-Native（build.log）。中英主界面及暗色控件截图已查看；测试场景、日志在 build/ui-interaction。性能用例确认单值/关键帧撤销保留控件，不将合成耗时当用户工程 FPS。未新增 .cs，无需改构建列表。
- 根 EXE 已更新 2.0.2；原用户 demo 12 文件/817 增/236 删及其他个人工程保留。本轮仅本地提交，不 push/Release；GitHub latest 仍为 2.0.0，待用户自行重启验收，无剩余必做项。

## 2026-10-25：2.0.1 本地 UI 细节修复（完成，未推送）

- 用户“继续”后，限定在已确认 UI 整理范围。先复现：展开外观 → 切换到没有此分组的对象 → 保存/切回，展开标记丢失；原恢复默认布局又调用了“展开全部”。ParameterInspector 现在将已知分组的偏好与临时标题控件分开保存，记住出现过的展开/折叠分类；现有 ! 前缀布局格式不变。新增 ResetDefaultSections 供菜单使用，保留 ResetSections 供展开全部/既有性能用例使用。
- 实际放大字体截图还发现吸附弹出面板的 BPM/延迟/关键帧选项使用黑字，以及分音框截断。EditorShell 的面板指定 Theme.Text；ProjectCommands 的分音框按本地化文字和当前字体测宽，FontChanged/弹出时更新。未修改节拍参数、吸附算法、工程数据、后台或 GML。
- 扩展既有 ShellLayoutTests：类型往返与保存恢复的偏好、折叠/展开/默认重置的控件保留、真实“恢复默认布局”菜单及工程不变、吸附设置布局边界/文字亮度/选择框宽度、中英 125%/150% 字体截图。首个失败 sections-before.log 证实偏好丢失；随后测试复用 NativeProperties 导致原夹具被 Reconcile，改为每次新建测试 bag。Popup 初次恢复用只读 Controls 集合失败，改为移除/重加 ToolStripControlHost。失败日志保留，没有应用错误弹窗。
- Test-ShellLayout 最终通过（snap-contrast.log）；Test-EditorPerformance 通过（performance.log），单值/关键帧撤销仍保留控件；Test-Localization 通过；Build-Native 通过并安全替换根 EXE 2.0.1。截图已查看。测试写 build/ui-polish/，没有同步或打开用户工程，原 demo 12 文件/817 增/236 删保留。
- 本轮按独立本地提交收口，未推送、未发布；GitHub 正式版仍为 2.0.0。没有新增 .cs，无需更改显式构建列表；没有未完成必做项。使用 stop-that-shit 技能限制在已复现 UI 问题和相应验证内。

## 2026-10-25：2.0.0 原生界面整理与发布（完成）

- 已实施六菜单和固定 ToolStrip 主工具/播放工具，使用原生 overflow；左栏素材/预设分类、搜索与管理菜单；预览显示/测试/缩放菜单、条件随机和底片状态；右栏属性/行为/统计、紧凑共享进度条和起止输入、折叠分组及原位参数按钮。保留完整行为编辑器，常用规则参数可在行为页编辑；时间轴双击规则直达对应行。用户已明确授权 v2.0.0 推送/Release。
- 新增 EditorShell.cs、BehaviorWorkspace.cs，并加入 Build-Native 显式列表。WorkspaceView 版本 2，新增 recentProjects、collapsedSections（! 前缀表示展开）、leftHidden/rightHidden、inspectorPage；TimelineView 新增 focusGenerationGroups。仅工作区字段；工程模型/后台协议/导出 GML 未改。语言绑定补 ToolStripControlHost 控件与用户文本保留，语言菜单替代旧顶栏组合框。
- 模板双击设置 StageCanvas.TemplatePreviewId，在渲染快照中单独激活模板及分组成员，保持生成依赖；原生底片与原生视野在局部预览中暂停使用。模板编辑限制游标至模板区间、静音局部播放，返回时恢复歌曲帧与来源；F5 自动退出局部上下文。预览状态不写入工程。测试检查隔离快照及工程不变，没有改变游戏运行公式。
- Test-ShellLayout.ps1 通过：命令可达、固定工具行、共享时间、行为页、模板隔离/返回、语言/工程不变、关键帧撤销和中/英/1100×740 截图；未启动主窗 Shown/后台 load，所有数据写 build/ui-v2/check。Test-Localization、Test-RandomTimeline、Test-TemplateSystem、Test-Presets 通过。Test-EditorPerformance（ui-v2-final）通过，40 轨/6400 键合成用例单值编辑中位约 1.46ms、撤销 3.76ms、原控件保留；加键约 83.35ms、撤销 7.73ms，共用控件保留。这些不是实际工程 FPS 或 GUI 用户验收。
- 初次本地化测试暴露 popup 内 checkbox 未翻译，已补 hosted control 递归绑定；性能测试按新分类展开后复测通过，保留失败日志。相关现有测试调整为 ToolStrip 菜单入口与“外观”原位分类，没有删掉状态/控件复用断言。第一次 Shell 运行遗留测试目录语言影响重跑，测试显式重置自身隔离语言设置。无遗留错误弹窗。
- 发布说明、双语 README 与设计状态已更新；根 EXE 版本一致。构建/截图/测试日志在 build/ui-v2/。未修改 projects 用户文件；仍为 12 个已有追踪文件，817 增/236 删，未纳入暂存。
- Package-Release 产出 final-distribution：完整便携 ZIP 58,785,959 bytes、更新 ZIP 56,954,131 bytes 及各自 SHA256。检查必需入口和 2,639 个便携文件，不含个人工程/设置。初次检查错误假定模板入口为 avoidance.gm82，已核实实际 rufmaker-demo.gm82 存在并修正检查。远程正式 1.13.1 EXE 的旧更新器实际解压/校验最终 2.0.0 更新包通过，AppPaths 新素材整理通过。
- Test-Updater 通过：最新版/双版本提示、资源选择，以及隔离应用真实替换、保留资料、备份和重启。Test-PreviewPlayback 通过：逐帧/长按/倒放、倍速、GM 标记吸附和静音 FMOD 播放；原测试的 [short] 在 Windows PowerShell 无法解析，改为等价 [int16] 后通过。没有修改音频实现。新测试/测试修正属于本轮占用。
- 本地功能提交 `1f37928`（31 文件），已精确暂存并检查未混入 projects；main 与 v2.0.0 已原子推送。Actions `40175140036` 构建/打包/发布全部 success；v2.0.0 为 latest、非草稿/非预发布，四附件 uploaded：便携 ZIP 59,901,555 bytes、兼容更新 ZIP 57,991,912 bytes 和两个 SHA256。线上包由 Windows CI 构建，与本地压缩大小不同，版本一致。
- `Test-Updater.ps1 -Online -Version 2.0.0` 实际从 latest API 读取版本，下载公开更新 ZIP、校验 SHA256、验证并解压全部 manifest 文件通过，记录 build/ui-v2/updater-online.log。本轮高 DPI 仅采用适应字体尺寸/overflow 的设计，实际多 DPI 屏幕未验收；没有将隔离控件截图或构建通过当成用户试玩。发布后仅补交接文档到 main，不移动发布标签。旧窗口需用户保存并重启，无剩余必做项。

## 2026-10-25：界面整理提案（尚未实施）

- 用户要求先给方案。新增 `docs/design/ui-reorganization-2026-10-25.md` 与 `docs/design/images/ui-reorganization-2026-10-25.png`，明确菜单迁移表、常驻/情境入口、阶段顺序及验收要求；布局保留四区和原生 WinForms，不引入 HTML。
- 建议菜单 + 一行主工具，左栏素材/预设，右栏属性/行为/统计，时间轴单行播放工具。保留右栏共享时间条、物件起止输入、参数行关键帧、同轨生成组、可读模板引用、波形/频谱高度与快捷键；补充模板局部编辑上下文和窄窗口/DPI/双语约束。均为提案，不是已上线功能。
- 对照读取 MainForm、ProjectCommands、SelectionTimeControl、BulletTools、PreviewPlayback、NativeTestPlayer、PlayerTrace、StatisticsTools 等入口；参考现有 `build/verified-english-layout.png` 确认拥挤位置。System.Drawing 生成器在 `build/ui-plan/render-ui-plan.cs`，用系统 Framework64 csc 编译并成功输出 PNG，已查看修正起止按钮文字截断；初次生成器编译因隐式多变量声明失败，已修正，没有启动应用或遗留弹窗。
- 未修改应用源码、语言资源、版本、根 EXE 或用户工程；未跑应用测试/GM 运行，不把静态图检查当成交互验收。未本地提交、未推送；后续实施等待用户对方案的反馈，本轮提案无必做剩余项。

## 2026-10-25：同轨生成组与可读模板卡片

- 时间轴按引用关系把来源物件、@spawn / @bullet 规则和 @template 引用卡片组织在来源轨道，生成组默认展开，能从来源箭头整体折叠。展开时来源长条位于自己的组头，只显示一次；它仍支持拖动、裁剪与框选。被普通来源可达的 templateOnly 模板收进组内，原模板独立轨道隐藏；未引用模板保持独立宽卡片。
- 同一个模板被多源引用时，每个来源组显示同一份定义的引用；新增引用循环的路径停止检查，避免无限展开。模板的 lane、start/end、关键帧、行为和模板 ID 都不因视图重排而改写；原生房间/GML、预览运动和导出公式没有修改。拖动来源实际改变来源时间，触发标记和引用卡片随源显示，模板局部时钟保留。
- 卡片独立于动作时长，以可读宽度显示名称、类型、单次发射/局部动画、局部帧范围、关键帧数和共用提示；零时长单发也清晰可见。宽度不表示运行时持续时间。单击绑定真实模板的右栏参数；双击展开原模板关键帧并定位局部起点，继续使用原参数/关键帧编辑路径。右栏引用模板显示“所在生成组轨道”，避免原数据 lane 与视觉位置混淆。
- 新 UI 布局字段 `TimelineView.collapsedGenerationGroups`，旧布局缺省为空；折叠状态随工作区保存。共享模板的展开/修改仍作用于同一个 ID。行高键新增 generation:绑定路径，防止共用模板的不同引用行冲突。主窗接线 `EditTemplateDefinition` 只设置选中/播放头，不改工程数据。没有新增工程持久字段或新的 .cs 文件。
- 实际文件：desktop/{TimelineCanvas,SpawnTimeline,TimelineLayout,TimelineAudio,WorkspaceView,MainForm,Updater}.cs；TemplateSystemTests、LocalizationTests；assets/locales/en.json、双语 README、范例教程及交接文档、根 EXE。TemplateTools.cs 登记过但最终未修改。
- 通过 `Test-TemplateSystem.ps1`：自动绑定同轨、宽卡片、两个来源共享、循环引用有界、折叠保存/恢复、选择真实模板、双击原关键帧入口、实际鼠标拖动生成组、不改局部模板数据；原模板/寿命/外观/颜色/像素用例也通过。`Test-RandomTimeline.ps1` 通过：混合轨道、多选、拖动/裁剪、关键帧边界、撤销选中、复制粘贴时钟。`Test-Localization.ps1` 通过，含新增标签。日志均在 `build/generation-groups-check/`。
- 新分组用例初次用 JSON 文本直接比较出现序列化字段顺序差异；改用已有 SceneComparison 比较持久字段，保存了 generation-before/after.json，数据语义相同。初始测试夹具的 laneCount 也在截图前先走既有 Normalize，与应用实际入口一致。`template-first.log` 保留初次失败。
- 最终截图 `build/generation-groups-check/generation-groups.png` 已检查：来源、触发标记、完整模板卡片处于同轨；共享模板在两个来源组出现，未引用单发仍为宽卡片。所有用户 projects 原文件未修改，既有 demo 仍为 12 文件/817 增/236 删。范例和游戏不需要重新同步；这轮仅视图/编辑入口变化，未重跑 GM 数值探针。
- 构建 `desktop/Build-Native.ps1 -Output D:\rufmaker\build\generation-groups-check\RufMaker.exe` 通过；File.Replace 安全替换根入口并比对 SHA256，未结束用户进程，旧映像备份在同一 build 目录。源码/测试/说明/EXE 随所属修复本地提交，未推送/发布；无剩余必做项，待用户交互验收。

## 2026-10-25：生成外观、波次颜色与歌曲时间定位

- BulletAction 新增 `inheritAppearance=false`、`inheritScale=false`、`spawnColorExpression=""`；Track 新增 `colorMode="fixed"`、`colorExpression="random(360)"`、`colorScope="wave"`。旧字段缺省保留原表现；UI 新建规则默认勾选大小继承。原示例偏小来自模板 scale=.48/.7，修订第 01/02 章明确继承母体大小与外观。
- C# 新增 BirthAppearance 和运行时冻结 sprite/image、颜色覆盖、scaleMultiplier；生成时保存来源的贴图/混色/实际彩虹帧，之后固定。大小按母体 scale/模板根锚点 scale 换算，保留后续缩放动画。模板固定成员、释放、检查点和下一代生成使用同一快照。预览后代在固定队形之后绘制，按代数/出生顺序排列；GML 后代 depth=controller.depth-generation。拖影始终在本体后方，未增加拖影 obj。
- GML `rf_template_spawn` 内部 ABI 从 8 扩至 14 参数，末六项为继承 sprite(-1 关闭)、color、image、继承大小开关、来源大小、颜色覆盖(-1 无)。新增 `rf_spawn_appearance` 统一应用；GM 更新碰撞 sprite_index 会清掉 image_index，已用独立 `rf_frozen_image` 在 Tick/Draw 恢复。旧工程同步时需一起重建内部脚本；表达式的 16 参数 ABI 没有变化。
- 颜色入口把 `choose("#FF75AD","#6DE8FF")` 转为 GM RGB 整数后复用原表达式/RNG；普通数值语法不新增字符串。`random(360)` / `choose(0,120,240)` 按色相映射全饱和/全亮度 RGB。波次 offset=wave*1009，逐颗再加 index*9176；生成规则逐成员错开仍共用同一轮颜色，出生后固定。沿用固定种子、每次游玩随机和重新随机。颜色是贴图混色，任意着色应使用白色底图；保留彩虹则用出生外观继承。
- 展开来源物件后增加 @spawn:index / @bullet:index 行，显示歌曲时间的重复/逐成员触发标记；单击定位，双击编辑准确规则。弹窗增加“首次触发歌曲帧”，模板条标为“局部时间”。飞行弹幕标记按配置寿命推算计划，提前越界/销毁会取消实际触发；模板内更深层生成使用实例时钟，不把模板第 0 帧伪装成真实歌曲出场。未新增大量持久轨道。
- 实际源码：desktop/{EditorCore,SpawnAppearance,SpawnTimeline,TemplateRuntime,TemplateTools,BulletSystem,Trails,PreviewRenderer,BulletBehaviorForm,BulletTools,MainForm,TimelineCanvas,Updater}.cs 与 Build-Native.ps1；studio/{spawn-appearance,template-system,bullet-system,bullet-schema,project,random-parameters}.mjs。两个新增 .cs 已进显式构建清单。另改英文词条、TemplateSystemTests/template-fixtures/verify-template-gml/PresetTests/LocalizationTests、studio/template-system.test.mjs、范例配方/教程、双语 README、本交接及根 EXE。
- 通过：`Test-TemplateSystem.ps1`（冻结外观/大小/动画、同波颜色、标记平移/双击、叠层像素）；`Test-Presets.ps1`（随机颜色保存）；`Test-Localization.ps1`；`Test-EditorPerformance.ps1`（171 基础控件保留）；Node template-system/bullet-system/bullet-lifetime 9/9；范例测试 4/4。日志在 `build/spawn-appearance-check/`，实际截图 `build/template-system-check/spawn-timeline.png`、`spawn-layer.png` 已检查。
- 真实 GM8.2 对照新增 sprite/color/depth：原夹具 22 时刻 1,489 状态通过，目录 `build/template-system-check/Template GM 1792903822718/`；`--gallery` 33 时刻 1,329 状态通过，目录 `build/gallery-check/Template GM 1792904036882/`。相应 `gm.log` / `gallery-gm.log` 保留。初次外观检查先发现夹具省略 color 时 C#/GML 默认值不同，给夹具显式白色后保持测试含义；随后实际发现 GM 碰撞贴图重置彩虹帧并修复。未留下错误弹窗。
- 其他过程诊断：时间轴 marker 从 Int32.MinValue 相减溢出导致初次截图无标记，已改像素哨兵并补像素断言；绘图检查补丁一度把 nativeView 引出作用域导致编译失败，已复用原绘图上下文；新语言断言起初在中文状态执行，已移到英文切换后。最终测试/构建全部通过。收尾文档更新曾被自动审批系统因额度限制拒绝执行，用户续接后核实原文件未改，再完成交接。
- 新范例 `projects/examples/Pattern Gallery 2/Pattern Gallery 2.rufmaker`：第 01/02 章同尺寸彩虹副本，第 06 章按波选择颜色。教程和 actual PreviewRenderer 总览放同目录；独立游戏 `build/release/rufmaker-game.exe` 4,751,501 bytes，保留 Data。旧 Pattern Gallery/Star Echo 保留，projects 不提交。
- 构建 `desktop/Build-Native.ps1 -Output D:\rufmaker\build\spawn-appearance-check\RufMaker.exe` 通过，根入口 File.Replace 安全替换并比对 SHA256；旧映像备份在同一 build 目录。没有关闭用户进程或覆盖既有用户工程。源码/配方/测试/教程/EXE 随所属修复本地提交，未推送/发布，无剩余必做项，待用户验收。

## 2026-10-25：深色右栏与 11 章弹幕范例

- 截图中的白框来源可复现为 Windows 原生 TabControl 框架和 DropDownList 默认绘制。`StatisticsTools.cs` 新增原生 WinForms `InspectorTabs`（仍使用原属性/统计控件，仅切换可见性），用深色按钮/Panel 替代系统页签框。主窗口的 SelectedIndex/布局保存接口保持不变，没有重建属性页或改为网页。
- `WheelSafeComboBox` 保留 ComboBox 的选择、键盘、滚轮保护和本地化；下拉项 owner-draw，关闭状态在 WM_PAINT/WM_PRINT/WM_PRINTCLIENT 补绘深色背景、文字和箭头，解决部分 Windows 主题忽略 BackColor。`ParameterInspector.cs` 新增 ColorParameterButton，白色参数仅显示为小色块，按钮背景保持深色；颜色值、对话框及现有绑定不变。新类放在已构建的源文件中，不需要更改显式清单。
- `EditorPerformanceTests` 增加普通/经典 Windows 主题、420/720 宽度、页签切换、色块/下拉框像素检查与截图，仍执行撤销、加键、布局和控件复用检查。第一次检查确实抓到白色 ComboBox，随后补绘关闭状态修复；日志 `build/gallery-check/inspector-first.log`，最终 `inspector.log` 全通过。最终加键中位约 46 ms、单项编辑/撤销约 1.27/3.84 ms；基础 168 控件全部保留，这些是本轮合成场景数值，不作为跨版本速度提升结论。
- 新增配方 `tools/examples/create-pattern-gallery.mjs`，运行时创建唯一新目录，不覆盖既有 Star Echo/用户工程。`projects/examples/Pattern Gallery/Pattern Gallery.rufmaker` 已在本机创建，包含 11×8 秒、50 FPS 的真实工程：周期星星扩散、逐成员循环等速射击、移动母弹每 6 帧留点、双向螺旋、移动自机狙、固定种子散雨、停顿瞄准冲刺、延迟开花、圆环切线释放、移动缺口弹墙、原 heart 脚本心形波纹。既有 star2/heart 引擎脚本继续使用，未新增运动或表达式语义。
- 单色弹使用 sprCherryWhite 再着色，彩虹使用原 sprrainbowcherry；最初截图使用红色 Cherry 底图导致颜色相乘偏红，已改正并重新同步本轮新工程、渲染和编译。没有修改用户之前的 Star Echo 或 demo 工程。
- 教程 `docs/examples/pattern-gallery.md` 及本机 `示例教程.md` 包含章节秒/帧索引、右键入口、每项具体参数、关键帧、年龄/歌曲时钟、循环发射公式和扩展练习。来源链接为 Sparen 自述的发射密度、角度与环/扇形/弹墙教程；未宣称完整观看无法访问的 Bilibili 视频。中文/英文 README 增加创建入口，并注明教程目前为中文；用户工程名称不翻译。
- 新增 `tools/examples/pattern-gallery.test.mjs`，调用真实 C# BulletWorld 的批量取样：11 章均有可见输出且下一章前清空；星星多批各 60 颗/来源中心冻结；成员三轮各 30 颗/等速和方向递增；留点间隔、速度 0 和原地存活均通过。首次留点坐标严格相等断言遇到 PointF 约 0.000002 像素舍入差异，已改为 0.0001 像素误差边界；没有改产品运动。
- `TemplateSystemTests` 新增 `--samples`（一次世界演算、多帧输出）与 `--render-gallery`（真实 PreviewRenderer、贴图、房间实例与总览）。`verify-template-gml.mjs --gallery` 使用 build/gallery-check 下隔离工程；正常模式仍用原有夹具。首次 0.002 像素检查发现长飞行最大 X 差 0.004827，其他数值/数量/时序一致；诊断 `build/gallery-check/compare.mjs` 及首份 GM CSV 保留。gallery 的 x/y 允许 0.01 世界像素，其余数值仍为 0.002、离散状态严格相同。
- 通过：`Test-EditorPerformance.ps1`、`Test-Localization.ps1`、`Test-TemplateSystem.ps1`；Node `--test tools/examples/pattern-gallery.test.mjs`（4/4）。真实 GM 最终 33 时刻 1,329 状态通过，目录 `build/gallery-check/Template GM 1792902940992/`，日志 `build/gallery-check/gm-check.log`；随后只有单色底图替换，未改运动参数。无残留故障弹窗。
- 总览 `build/gallery-check/previews/gallery-overview.png` 已目视检查，并复制到新示例 `预览/`。该图来自实际编辑器预览。示例独立游戏 `projects/examples/Pattern Gallery/build/release/rufmaker-game.exe`，4,750,086 bytes，保留旁边 Data；编译日志在示例 build/compiler/latest.log，任务结果 `build/gallery-check/export.log`。这是编译与数值验证，未声称完整玩家通关验收。
- 正式构建 `desktop/Build-Native.ps1 -Output D:\rufmaker\build\gallery-check\RufMaker.exe` 通过；File.Replace 安全替换根入口、SHA256 相同，旧映像备份留在该 build 目录。未终止用户进程。源码、配方、测试、教程、EXE 随所属修复本地提交；projects 示例及用户工程不提交，未推送/发布，当前无剩余必做项。

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
