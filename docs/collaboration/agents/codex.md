# Codex 任务记录

- 状态：`in_progress`
- 任务：按用户“推送release一下”授权，集成本地未发布修改，发布2.6.0完整便携版和兼容更新包。
- 基线：main/92bd058，v2.5.0已发布；继承下述区域生成及F5/复制/R的未提交实现与用户projects改动。本轮用户已明确授权推送和Release。
- 当前占用：desktop/Updater.cs、README.md、docs/releases/v2.6.0.md、协作记录/STATUS、根EXE和build/release-check-2.6.0、build/distribution/v2.6.0；精确提交本地应用修改，排除所有用户projects。沿用既有标签Actions发布流程，先核对版本/打包/更新器。
- 本机入口：RufMaker.exe，ProductVersion2.6.0，1,371,136 bytes；旧入口备份build/release-check-2.6.0/RufMaker.before-release.exe。

## 2026-10-29：2.6.0发布（准备与本地验收完成）

- 用户明确授权“推送release一下”。发布范围为此前未提交的F5/复制性能、R快照和快速重试血粒子修复，以及区域生成/淡入/默认白色/随机彩虹。没有新增额外产品功能，保护所有projects及用户资料。
- AppVersion及三个程序集版本同步2.6.0，双语README和docs/releases/v2.6.0.md按最终内容整理，性能指南转为正式版本说明。沿用SSH main/tag与Actions正式发布，连接器无创建Release操作。
- 本地Package-Release输出build/distribution/v2.6.0，完整包59,088,252 bytes/2677文件，兼容更新包57,083,963 bytes/2635文件及两个SHA256。ZIP唯一条目、必要文件、EXE/manifest版本、更新包逐文件SHA256、排除个人资料检查通过。实际原2.0.0更新器接受2634文件，当前更新器提示/替换/保留/备份/隐藏重启通过；完整包隔离无GM首次新建与重开通过。
- 功能与真实GM沿用上面各任务已通过的定向结果；没有在版本字符串修改后无关重跑全部模拟测试。根EXE已备份并更新为2.6.0/1,371,136 bytes。
- 待执行：精确暂存/提交应用文件，推送main及v2.6.0，确认Actions和Release资产，再验证公开更新与完整便携包。当前尚未声称远程发布成功；日志均在build/release-check-2.6.0。

## 2026-10-29：区域随机生成与随机彩虹（本地完成）

- 新模板random-region拖到来源后，默认按来源生命周期每25帧触发，覆盖当前房间；每颗独立矩形取样。可编辑中心/宽高、跟随来源、间隔/数量、速度/方向公式、出生淡入、固定/房间/视野寿命、销毁过渡、拖影及后续行为。选中来源绘制青色区域虚线。发射器来源默认整件触发，避免按所有已发弹幕成倍叠加；仍可主动切换来源模式。
- 新Track默认混色白色；新生成模板不再默认继承来源混色，显式继承/复制仍保留用户选择，既有颜色不改。彩虹增加rainbowRandom，发射弹出生时存一次调色板序号，动态颜色从该序号继续；整体对象按稳定成员索引取样，释放保持颜色。整体配色使用初始rf_rainbow_seed，R重新随机未来发射时不改变已有整体的配色。
- 字段：Track.fadeInFrames默认0、rainbowRandom默认false；BulletAction.spawnAtWorld默认false。区域模板设spawnAtWorld=true，中心复用spawnOffsetX/Y，矩形复用emitWidth/Height，新增emissionShape=rectangle。快照/复制/JSON沿用字段机制，出生淡入参与片段/FPS/预设缩放；组复制显式取事件fadeInFrames，保持C#/GML一致。模板普通偏移公式两端统一取样。
- 产品文件：desktop/GenerationPresets、EditorCore、ExpressionContext、BulletLifetime、BulletSystem、TemplateRuntime、MainForm、Rainbow、CurveControls、BulletBehaviorForm、Motion、PresetLibrary、GroupEvents；studio/bullet-schema、bullet-system、template-system、rainbow、bullet-lifetime、random-parameters；英文词条和根EXE。没有新增产品CS，不需变更显式构建清单。
- 新定向测试desktop/Test-RegionGeneration.ps1、tests/RegionGenerationTests.cs及studio/region-generation.test.mjs；现有GM模板探针增加可选build/sampler/frames环境参数，保留原gallery入口。既有生成测试文案改为7种。用户说明docs/generation-presets.md，开发入口/本记录/STATUS更新。
- 验证：C#区域测试覆盖分布/间隔/固定与跟随坐标、淡入中点、随机/倒拖/快照/预设FPS/白色默认/双语字段及区域线框；GenerationPresets、TemplateSystem、Localization、TimelineGroups通过。Node相关四文件15/15通过。最终真实GM18时刻2532条完整状态与C#一致，含三类寿命、静态/动态随机彩虹及整体释放。日志build/region-generation-check/gm-final.log，工程Template GM 1793238725395/GameMaker.gm82；不把构建或离屏UI检查当用户验收。
- Build-Native成功并File.Replace更新根EXE，备份路径见顶部。修正了开发中的room局部变量/测试编译及离屏截图问题；虚线角点断言改为检查整条预期边。诊断说明verification-notes.txt，最终日志均在build/region-generation-check；无遗留测试进程/错误窗。用户demo仍12文件817增236删，未操作用户工程。
- 未提交、推送或发布；重启本机RufMaker后生效，当前占用释放。

## 2026-10-29：快速R后死亡粒子残留（本地完成）

- 原因：bloodEmitter实例在game_load后消失，但其part_system/part_type独立资源未被释放；同帧R跳过原Step清理，以及恢复等待期间再次死亡，会留下持续喷血的系统。
- 文件：新增studio/retry-particles.mjs及test、desktop/tests/verify-retry-particles-gml.mjs；修改studio/native-checkpoint.mjs、project.mjs、native-preview.mjs、性能说明及本记录/STATUS。retryParticleCleanup仅识别bloodEmitter及常见obj前缀对象创建的粒子系统/类型，先执行实例Destroy再回收仍存在的资源；不清空场景装饰粒子。guardPendingDeath在恢复标记存在时阻止killPlayer/scrKillPlayer重复生成死亡特效。普通和原生试玩均接入，原生生成器摘要包含新模块使旧缓存失效；原生重试另加重复请求保护。
- 接口：plainCheckpointScripts新增可选第三参particles，checkpointScripts新增可选第二参particles；无工程字段/桥接协议变化，不改变预览或弹幕公式。只有F5测试期间启用死亡保护。
- 验证：实际GM隔离工程先复现8轮快速死亡/R后持续喷发，再验证修复后8轮连续重试及等待恢复时第二次死亡/R；结束120帧后死亡系统/类型/粒子均0，装饰粒子保留。最终日志build/retry-particles-check/fixed-final.log。R原回归固定/随机两模式均两次恢复34条状态，无Create/预热重跑，未来种子策略正确，checkpoint-regression.log。Node四文件12/12通过，unit.log；Build-Native输出RufMaker.check.exe成功，build.log；git diff --check通过。
- 实际GM本轮验证普通F5；原生接入有生成组合/定向测试，未重复完整原生导入试玩。基线失败与全部日志保留在build/retry-particles-check，无遗留测试进程或错误窗。
- 根EXE保持前轮2.5.0/1,363,456 bytes；此次后台源码更新，重启RufMaker后重新F5才会生成修复后的测试游戏。未提交、推送或发布，文件占用已释放。

## 2026-10-29：F5/复制性能与R直接恢复（本地完成）

- 用户报告中途F5等待和复制obj游玩卡顿，追加R不重算、不重新随机已有弹幕。基线92bd058；无本轮远程授权，保护用户工程与存档。
- 改动：studio/template-system.mjs、bullet-system.mjs、project.mjs、native.mjs、native-checkpoint.mjs；desktop/BulletSystem.cs、TemplateRuntime.cs、MainForm.cs、NativeGamePreview.cs；assets/locales/en.json；新studio/play-checkpoint.test.mjs、desktop/tests/verify-play-replay-gml.mjs、verify-plain-play.mjs、verify-play-checkpoint-gml.mjs，性能说明/开发入口/本记录与STATUS，根EXE。没有新产品CS；显式构建列表已包含现有文件。
- 实现和边界详见docs/development/PLAY-COPY-PERFORMANCE-2026-10-29.md。F5只跳过确定死亡且不影响后代/组复制的末端历史；成员数量键/表达式容量仍300。普通F5首次Begin Step保存，R在原进程game_load，不跑Create/模板初始化/预热；过去随机绑定/全局历史保留，随机模式只换未来种子，固定模式恢复序列。原生导入沿用已有快照并同步未来种子策略，其生成器摘要自动失效。
- 测量：同一1800成员60帧GM计算7156ms→3968ms（约45%减少），最终1800条位置/外观与基准相同。当前工程隔离副本第1899帧已编译F5 12322.6→8685.2ms；初次编译约2.3秒另计，运行器加载约6秒仍存在，非FPS保证。早期无收益的姿态缓存/depth检查/试玩文件复用均已撤去。
- 验证：CopyEditing/TemplateSystem/Optimizations/NativeDialogs/Localization通过；Node复制/模板/原生重放17项、R/保存相关14项通过。最终实际GM/C#复制15411状态+17特效/F5帧85一致；完整/优化重放9时刻2275行一致，含组复制、重设、分裂、三种寿命、数量超调与变量；普通F5真实进度/ready/取消在接入快照前后通过。
- R实际验证：原world KeyPress_82事件触发两次，固定/随机两模式均恢复34条已有弹幕/玩家/全局状态；Create文件计数始终1，历史预热文件计数始终41；固定未来随机一致、随机模式未来重采样。包括活动震屏表面与音乐恢复，无GM错误。checkpoint-gm-fixed.log。
- 诊断失败保留：600帧压力超时已结束；初次计时探针误改事件魔术头，导致用户截图的objRufController syntax error，仅build副本，已修正且原文件头完整。新夹具缺keyframes、GML探针other轮次误读、文本测试多算换行已修复，未遗留错误窗口或测试进程。没有将失败探针写进用户工程。
- Build-Native与diff检查通过，备份后File.Replace更新根EXE2.5.0/1,363,456 bytes；程序重启及新F5后生效。未提交、推送或发布，本轮占用释放。

## 2026-10-28：2.5.0正式发布（已完成）

- 用户明确授权推送/Release。集成本地共享组/组事件与公式、复制销毁及寿命统一、事件标记和名称裁剪、参数化运动/拉镜预设、实时玩家自机狙及五类性能/保存优化；保护所有projects。
- 版本四处来源已同步到2.5.0；双语README和docs/releases/v2.5.0.md按最终范围重写。旧阶段文档保留历史，性能指南改为正式版本说明。
- Package-Release在build/distribution/v2.5.0输出本体59,064,930 bytes/2671文件、兼容更新包57,073,456 bytes/2631文件及两个SHA256；包内逐文件/manifest/版本/私有目录排除检查通过。原2.0.0更新器接受2630文件；首次x64 PowerShell加载x86旧程序集格式不匹配，改x86宿主成功，失败日志保留。当前Test-Updater通过提示文本、版本比较、实际替换/保留/备份及隐藏重启。
- 完整包在隔离目录无GM首次建空工程、重新打开通过。语义与真实GM沿用此前已通过记录，未无关重跑。根EXE更新为2.5.0并备份；git diff --check通过。
- 精确暂存99个应用文件后提交8c13c7c，与v2.5.0原子SSH推送成功；不含任何projects。使用Windows OpenSSH绕过Git自带MSYS在受限环境的IPC失败。GitHub连接器无Release操作，沿用既有SSH与标签Actions流程。
- Actions40672949198成功，https://github.com/wymcaT/RufMaker/releases/tag/v2.5.0 为latest、非草稿/非预发布；本体60,120,152 bytes、更新包58,113,612 bytes及两个SHA256均uploaded。线上CI打包的压缩字节不同于本地包，按各自公开校验值核对，不混用。
- 线上Test-Updater实际latest查询、公开更新ZIP下载、SHA256及全部清单解包通过；另实际下载完整便携包、核对公开SHA256/版本/必要文件/个人资料排除，并无GM首次建空工程与重开通过。日志build/release-check-2.5.0/updater-online.log、portable-online.log和release.json。
- root EXE2.5.0/1,362,944 bytes；用户demo仍12文件817增236删，其余本地工程完整保留，未遗留测试进程。发布记录另提交，不移动已发布标签。

## 2026-10-28：优化续做与最终交付（本地完成）

- 用户明确继续；沿用main/a0491ba和暂停时源码，Argon4无占用，没有新远程授权。
- 完成暂停清单：测试新增高编号轨道原生多选拖动、索引末尾换行、错误去重/修复恢复，均通过。首个拖动探针反射标记错误导致测试自身空引用，改公开ClipBounds入口通过；失败日志保留。相关产品代码无须再改。
- Node组/模板/预设/复制/全局变量/保存22项通过；保存事务新增用例后6项通过。C# Optimizations、CopyEditing、GenerationPresets、TemplateSystem、PlayerAim、Localization、ShellLayout通过；原有EditorPerformance/RandomTimeline/LaneExclusivity阶段结果有效，未无关重跑。
- GM：61帧1502条实时玩家状态与C#一致；1000物件真实编译/启动/Draw；Verse3隔离导入的纯原生无副作用保存、overlay及lane300分组保存重开/原生控制器保留/真实编译通过。未重新完整录制底片/试玩。初次受限环境编译器3221225794退出，随后自动审核允许隐藏隔离执行通过；未使用电脑控制，未遗留测试进程错误窗。
- 文件：desktop/tests/OptimizationTests.cs、studio/project-sync.test.mjs补最终边界；新增desktop/tests/verify-optimization-gml.mjs；双语README、docs/performance-and-saving.md、开发入口/评估/阶段记录及本记录/STATUS。产品优化代码仍为暂停版本；新增产品CS显式构建清单已核对。
- 最新cache-profile冷定位1268ms、改名12.47ms/0步、后段改键239ms/1200步；不作为真实游戏FPS。完整结果与工程路径见阶段记录。
- Build-Native最终通过，File.Replace备份并更新根EXE为2.4.0/1,362,944 bytes；git diff --check通过。根EXE旧版本1,349,632 bytes备份如上。用户demo12文件817增236删保持，其他projects未操作；未提交/推送/Release。当前五类优化无未完成必做项；未来全量参数架构/CI等仍为候选，不擅自展开。

## 2026-10-28：实施评估优化（用户要求阶段收尾，待续）

- 基线main/a0491ba及全部既有本地功能，根EXE2.4.0/1,349,632 bytes。保护用户projects和前轮实时自机狙，用户授权实施评估结论，没有推送/Release授权。
- 范围：工程保存预检查/分阶段提交/失败回滚与恢复；旧帧不提交及取消过时模拟；按依赖复用预览世界与分层检查点；取消静默20k绘制/200物件保存限制；Undo/Redo可配置内存预算。保持既有JSON和GM语义，不做UI框架或引擎重写。
- 验收：build隔离工程的正常/失效歌曲/中途失败/恢复与201+物件同步；可确定的场景修订/缓存复用/参数和镜头依赖/倒拖数值对照；密集弹幕完整绘制；Undo预算/选择/控件复用；定向CS/Node与真实GM必要对照，最后构建并更新本机EXE、记录前后性能。
- 阶段交接：docs/development/OPTIMIZATION-STAGE-2026-10-28.md。五类优化均已有源码，主要定向检查通过；最终真实GM、部分最终细节回归及用户文档尚未完成。没有将整个优化任务标成已完成，也没有替换根EXE。
- 文件：studio/project-sync.mjs及test（新）、project.mjs、plain-room.mjs、timeline-groups.mjs；desktop/UndoHistory.cs、PreviewRevision.cs（新，已入Build-Native），MainForm/EditorCore/EditorCanvas/PreviewEffects/PreviewRenderer/BulletSystem/ObjectStatistics/WorkspaceView/EditorShell、TimelineCanvas/TimelineLayout/TimelineGroups/GenerationPresets/CopyGeneration/PresetLibrary/PresetBrowser；新Test-Optimizations.ps1、tests/OptimizationTests.cs和verify-project-sync.mjs；英文词条、本记录/STATUS与阶段文档。
- 已通过：保存事务5项，真实隔离工程失效歌曲不改任何文件/正常重试/201和1000物件及高编号组保存重开；C#优化用例包含共享Undo预算、取消恢复、局部重算/镜头依赖与冷重算一致、48检查点32MiB估算预算、联动动作引用/全局变量、202物件剪裁及预设、48000颗完整预览、异步Stage只提交最新帧。EditorPerformance、RandomTimeline、LaneExclusivity通过；Node相关3文件16项通过。日志全在build/optimization-check。
- 测量：7000帧冷定位1256ms，改名复用12ms/0新增模拟步，后段关键帧修改257ms/1200重算步；40物件6400键普通编辑1.39ms/Undo3.93ms、控件保留，加键89.30ms无明显提升。仅合成隔离测量，不是实际游戏FPS。
- 构建：最新源码Build-Native通过，build/optimization-check/RufMaker.stage.exe=1,362,944 bytes/2.4.0，仅构建产物不含运行依赖。根RufMaker.exe仍前轮1,349,632 bytes。早期方法组Sum重载编译错误已修，最终diff检查两处空行尾空格已清理。
- 待续：最后轨道拖动199上界取消/错误去重/无末尾换行索引的最终回归；补相关模板/复制/生成/语言与真实GM（含原生导入）验证，再完善说明、备份并更新根EXE。无推送/Release授权，无提交/推送/发布；用户projects保持原样。源码后台已在工作区，不应声称整个目录回退成旧版。

## 2026-10-28：性能/架构评估与自机狙实时玩家取样（本地完成）

- 基线main/a0491ba，含此前未提交的本地功能；仅评估和定向诊断，不扩展为实施全部优化。本轮无推送授权。
- 范围：编辑/撤销/保存链路、预览排队及模拟缓存、预览/GML双端维护、工程数据和打包测试；报告区分确认问题、性能设计限制与建议。
- 验证：源码路径/行号、已有定向测量及必要的隔离复现；不加载用户工程。
- 用户追加：所有自机狙统一使用实时玩家坐标；发射/释放按出生当帧，持续追踪按当前帧，编辑器按启用的试玩轨迹或编辑玩家位置，GML按真实玩家。同步审查C#/JS/GML，不把一次瞄准改为持续追踪。其余审查发现先交付评估，不顺带修复。
- 评估报告：docs/development/REVIEW-2026-10-28.md。隔离复现音乐失效时GML已改而JSON未改、200/201物件保存边界；源码确认过时渲染仍提交、每次编辑清历史、8个200帧检查点、简化预览单发射器20k绘制上限与完整统计不一致、Undo无内存预算。以上除自机狙外均未修改；未声称已发生用户数据损坏或测得实际复杂耐久FPS。
- 测量：240存活弹幕，6000帧历史首次定位891.8ms/下一帧0.1375ms/重建定位890.9ms；6001倒拖2000需301.9ms。40物件6400键保留64份Undo约34.6MiB托管内存；独立UI测量普通编辑1.97ms/Undo7.21ms/加键92.44ms，已有参数控件保留。日志及一次性探针build/review-20261028；不把冷启动重算与日常帧耗时混为FPS。
- 实现：根整体对象释放朝玩家与简化BulletFlight改按当帧玩家；ExpressionContext区分模板局部时间/歌曲时间，Motion表达式、生成相位、复制朝向正确映射到玩家轨迹。JS expressionContext和全局变量复用playerAt插值。GML延迟逐颗自机狙在实际出生第0帧再读取真实玩家，保持原方向偏移；Create初始化rf_birth_aim/rf_aim_offset，强制方向分裂/释放不覆写。一发瞄准继续单次，持续/限速追踪逐帧。GM数值对照发现并修复aim行为duration非零时GML插值起点与C#目标偏移不一致的问题。
- 接口：ExpressionContext.For/PlayerAt增加可选worldTime，CopyGeneration.Rotation同样传递时钟域；GenerationPresets.Phase明确接歌曲时间。没有新增持久字段或改变工程格式；旧工程、Undo/预设数据沿原链路。原生底片固定400,304规则保留。
- 文件：desktop/BulletSystem.cs、ExpressionContext.cs、BulletFlight.cs、TemplateRuntime.cs、GenerationPresets.cs、CopyGeneration.cs；studio/random-parameters.mjs、global-variables.mjs、bullet-system.mjs、project.mjs、player-expressions.test.mjs；新增desktop/Test-PlayerAim.ps1、tests/PlayerAimTests.cs、verify-player-aim-gml.mjs；README双语、生成教程、评估报告、本记录/STATUS与根EXE。没有新产品CS，原显式构建清单已涵盖全部改动。
- 验证：Test-PlayerAim覆盖移动玩家、逐颗延迟、根/模板释放、局部/世界时钟、嵌套生成、连续/限速/单次/全局瞄准、手工位置与倒拖；Test-PlayerExpressions、TemplateSystem、GenerationPresets通过；相关4个Node文件18项通过。最终真实GM隔离项目build/player-aim-check/GM Player Aim 1793162510053/GameMaker.gm82，61帧共1502条弹幕状态（位置/方向/速度）与C#容差0.002内一致。日志player-aim-gm-verified.log；不是用户实操验收。
- 早期验证：首次探针在with中读到控制器同名局部值，改用self字段；随后发现并修复产品aim目标偏移差异，1466条状态通过；追加嵌套生成用例后最终1502条通过。失败日志均保留。GM编译/运行使用获自动审核允许的隔离隐藏进程，没有桌面控制或遗留测试错误窗。
- 构建：Build-Native最终通过，File.Replace更新RufMaker.exe为2.4.0/1,349,632 bytes，旧入口build/player-aim-check/RufMaker.before-player-aim.exe；未关闭用户进程。定向git diff --check通过，用户demo仍12文件817增236删，未写用户projects。未本地提交、推送或发布。

## 2026-10-28：运动与拉镜预设参数驱动（本地完成）

- 基线：main / a0491ba，包含此前未提交的组事件、寿命和时间轴修复；保护所有用户 projects。本轮无远程授权。
- 目标：不再为运动/拉镜预设生成逐帧关键帧；预览和游戏按参数计算，右栏明确显示由预设控制并开放中心、幅度、速度、时长等适合预设的参数。兼容旧预设数据，保留非预设属性的手动关键帧及吸附。
- 实现：14种动作直接计算X/Y；12种拉镜直接按CameraPresetData计算位置、角度、缩放、模糊。右栏对应字段和关键帧页显示“由预设控制”，其他属性如镜头强度仍可单独加键；右键公式/全部加键/粘贴不会覆盖受控属性，游标吸附排除预设内部采样。动作提供持续时间、圈数/每秒速度，拉镜提供中心、基础缩放/角度、速度、时长及适用的水平/垂直、旋转、缩放、模糊幅度；中英文词条已补。手动模式保留当前取景/位置，不隐式生成大量关键帧。
- 兼容：旧动作逐帧X/Y先核对连续性、表达式和数值，未改动则转为参数计算；检测到用户改动则解除预设绑定并保留手动键。旧拉镜有可靠预设名称、曲线和中心完全匹配时才转换；旧曲线实际采样由不可序列化缓存和导出时的兼容曲线计算还原，时间轴不产生/吸附这些点。旧曲线被修改、有重复周期或无法确认时保留，不自动覆盖；右栏提示可重新应用预设。LegacyTime记录移动、裁剪缩放及跨FPS变换，快照深复制，缓存只读共享且修改后失效。修复时间轴拖动只有拉镜预设时其预设时间不跟随的问题，合并动作/拉镜时分别同步；密集周期清理只移除受控字段，保留其他手动键。
- 代码：desktop/MotionPresets.cs、Motion.cs、BuiltinPresets.cs、CameraPresetMotion.cs（新）、EditorCore.cs、MainForm.cs、EditorCanvas.cs、TimelineCanvas.cs、TimelineEditing.cs、PreviewPlayback.cs、PresetBrowser.cs、PresetLibrary.cs、BulletSystem.cs、Build-Native.ps1；studio/motion.mjs、motion-presets.mjs（新）、camera-presets.mjs（新）、bullet-schema.mjs；Test-PresetParameters.ps1及tests/PresetParameterTests.cs、verify-preset-numeric.mjs、verify-preset-gml.mjs（新），MotionEditing/KeyframeWorkspace/CopyEditing/StylePreset测试更新，studio/motion-presets.test.mjs（新）；assets/locales/en.json、README双语及时间轴教程、本记录/STATUS和根EXE。新CS已入显式构建列表，新MJS会随现有打包规则收录。
- 验证：Test-PresetParameters覆盖12种旧拉镜、9种时长和转折、用户改键/改中心保留、跨FPS复制、时间轴拖动、参数、缓存、JSON/Undo/关键帧/吸附；旧曲线最大误差5.69e-14。Test-MotionEditing、KeyframeWorkspace、CopyEditing、StylePresets、CursorSnapping、TimelineEventMarkers、Localization、ShellLayout、TemplateSystem通过；Node 6文件16项通过，C#/JS共768个预设姿态对照通过。日志build/preset-parameters-check/，含早期失败日志。
- 真实GM：在build/preset-parameters-check/GM Presets 1793156338384/GameMaker.gm82隔离工程，隐藏编译/运行并输出24帧×32轨道共768个姿态，14种动作、12种新拉镜、3种旧拉镜、2种旧拉镜变速复制和叠加动作均与C#对照，位置/角度/缩放/模糊/镜头强度误差阈值0.002，gm-final.log。首次沙箱内编译器等待60秒超时并已退出，后在允许的隔离运行环境成功；未控制用户桌面或改用户工程。
- Build-Native通过，File.Replace已替换根EXE并备份旧入口；git diff --check通过。tracked用户demo仍12文件817增236删；未提交、推送或发布，未声称用户实操验收。当前占用已释放。

## 2026-10-28：事件圆点与可视范围内的物件名称（本地完成）

- 基线：main / a0491ba，保留上一轮未提交产品改动和所有用户 projects；本轮没有推送授权。
- 目标：事件显示为时间点圆形与右侧名称；物件名称随可见左边缘移动，保持在物件范围内，处理横向滚动和裁剪；保留事件单击、拖动、长事件拉伸、缩放与多选。
- 实现：控制事件及GML事件以圆点中心标识开始帧，名称在右侧；可持续的控制事件显示细线及末端调整柄，单次组复制/GML仅显示圆点。圆点、名称与选框可选中、移动；相邻圆点按最近时间命中。普通物件名跟随视口内片段左缘并裁剪在片段内，垂直/水平越界时文字保留绘图裁剪；持续事件起点离屏后仍能看到范围和名称。没有改变项目数据、游戏语义或生成GML。
- 文件：desktop/TimelineCanvas.cs、desktop/SpawnTimeline.cs、desktop/tests/TimelineEventMarkerTests.cs（新）、desktop/Test-TimelineEventMarkers.ps1（新）、desktop/tests/ShellLayoutTests.cs、docs/event-presets.md、docs/motion-timeline-editing.md、本记录、STATUS及根EXE。ShellLayout原断言只检查菜单第一层，之前精简菜单已将模板局部预览移入子菜单，因此改为递归检查，保留该功能的验证。
- 验证：Test-TimelineEventMarkers（离屏像素、时间对齐、横纵滚动裁剪、点击/拖动/框选、相邻圆点、持续事件两端拉伸）、Test-KeyframeWorkspace、Test-MotionEditing、Test-CursorSnapping、Test-EventPresets、Test-CopyEditing及修正后的Test-ShellLayout全部通过；日志和截图在build/timeline-event-markers/。Build-Native.ps1构建通过，已用File.Replace更新根EXE并保留旧版；git diff --check通过。离屏测试不是用户验收；本轮纯UI，无GM试玩或新FPS结论。
- 未提交、未推送、未发布；用户demo仍12文件817增236删，未写入用户工程。当前占用已释放。

## 2026-10-27：组复制销毁原弹幕与统一寿命（本地完成）

- 基线main/a0491ba，保留上一轮未提交组功能与用户projects；本轮无远程授权，未提交/推送/发布。
- 新Track.eventCopyDestroyOriginal=false进入模型和JS默认/布尔校验，旧工程复制默认保留原弹幕；Snapshot/复制/预设/Undo沿用原持久字段链。
- GroupEvents在创建副本后按原快照逐个销毁：飞行弹直接移除，未释放原始/模板/联动成员用TemplateRuntime.DestroyMember记录精确source/index/owner隐藏。不会把副本、未来发射、其他组一并清除。GML对系统实例和解析生成的原始整体成员分别处理，复用rf_template_hide，默认false分支保留原行为。
- 副本复用标准lifetimeMode/despawnMargin，默认fixed，出屏幕room/出视野view忽略eventCopyLife。C#副本在出生第0帧检查边界；GML复用lifetimeSpawn和rf_lifetime_tick（复制仍无原销毁过渡）。旋转/缩放镜头与普通弹幕共用判断。没有新增另一套坐标或寿命公式。
- UI：副本寿命模式/固定帧数或出界余量；普通发射器life数值移到弹幕寿命，与模式放一起；释放后寿命同类；生成快捷页只在fixed显示帧数，出界显示余量，三种模式均可设置。拖影是绘制样本，不改变其时长规则。
- 文件：desktop/EditorCore.cs、GroupEvents.cs、TemplateRuntime.cs、BulletLifetime.cs、BulletTools.cs、EditorShell.cs、GenerationPresets.cs；studio/group-events.mjs、bullet-schema.mjs；TimelineGroupTests、verify-groups-gml、timeline-groups.test、locale、使用说明与根EXE。没有新增CS，现有显式Build-Native已覆盖。
- 测试通过：Test-TimelineGroups扩展三模式/替换/出生即越界、普通/模板/命名分裂/实时联动、原成员不重生、其他组与未来发射、倒拖/保存/预设换FPS、实际UI模式/开关/Undo及入口分类。三种夹具第6帧副本数fixed31、room28、view13，固定副本第8帧到期，边界副本忽略2帧设定。Test-GenerationPresets、Test-Presets、Test-Localization通过；Node相关6文件23项通过。日志build/copy-lifetime-check。
- 实际GM：用RUFMAKER_GROUP_FIXTURE选择copy-lifecycle-{fixed,room,view}.json，verify-groups-gml在build隔离工程通过各26时间点：fixed739、room957、view807条状态，总2,503；每份应用Create/F5预热第30帧28条一致。项目后缀分别1793114127306、1793114136128、1793114005304，gm-fixed/room/view.log。所有进程已退出，无用户桌面控制。
- 早期夹具的zoom2让所有房间内副本恰好也在视野内，区分断言失败；改为zoom2.5后显式覆盖视野内外，产品代码未因测试修改公式。早期日志保留。
- 正式Build-Native通过，根RufMaker.exe为2.4.0/1,324,544 bytes，File.Replace替换并备份旧入口build/copy-lifetime-check/RufMaker.before-lifetime.exe，没有关闭用户进程。用户demo仍12文件817增236删，无用户工程写入；无本轮用户验收结论。
- 当前占用：无。完成后默认继续不推送。

## 2026-10-27：轨道共享组与组事件（本地完成）

- EditorState.timelineGroups保存稳定id/name/color/lanes，每条轨道只属于一个组，轨道内所有物件共享。可多选轨道归组、改名、改色、解散/选择全部。TimelineCanvas绘制组底色与组名；普通物件移动采用目标轨道分组，自动分离重叠/整理时间轴保持分组。旧parentId坐标变换保留并标为旧工程，删除未使用的GroupSelection创建入口。
- 新TimelineGroups.cs与studio/timeline-groups.mjs负责归属；快照深复制、StageCanvas.SceneChanged后台场景、模式/嵌套模式展开、模板（含实时联动复制）和命名/默认分裂后代均保留scopeGroup。右键主菜单最多约10项，生成/预设/改轨/片段等收进子菜单；保留右栏详细关键帧上下文。
- 左栏事件新增group-copy/group-clear/group-set：目标默认事件轨道组，可显式选其他组。无组/已删除组不会作用于别组。复制在当前帧生成/控制/移动之后仅于start取快照，包括根整体成员、实例整体成员、联动副本及飞行弹幕；保留位置/贴图帧/颜色/大小/透明度，无原年龄运动/生成行为，速度默认0，可独设速度/方向/加速/转向、固定寿命与来源拖影。副本属于原组。清除保留发射器与未来出场内容；组设置支持速度/方向/加速/转向/scale/alpha/hue，未释放队形仍受自身位置动画控制。
- GroupEvents.cs/studio/group-events.mjs同步实现；静态队形属性按历史时刻重建，几何按帧缓存，避免每成员重复计算整组坐标。组复制使用成员快照，GM用临时DS list避免新副本再次参与当次复制。原生底片与普通装饰不属于组弹幕。全局事件的额外原生对象行为保留，组事件不会执行切换类型前遗留的nativeObjects。
- ScalarExpressions.cs/studio/scalar-expressions.mjs与BulletAction.expressions扩展运动输入：重设、全局控制起止、年龄行为起止、子弹速度/分裂间隔角、生成角/逐颗角/每波角、新弹幕速度/拖影方向及复制朝向支持random/choose/基础表达式。RandomParameters的JS字段/表达式脚本收录同步；按已有种子控制，组事件按目标弹幕取样；动作表达式和新事件的跨FPS/片段单位转换同步。事件输入使用适当拖动步长；同结构属性页继续复用。
- 主要改动：EditorCore/Motion/RandomParameterUI/MainForm/BulletSystem/TemplateRuntime/PreviewRenderer/EditorCanvas/BulletTools/TimelineCanvas/TimelineLayout/TimelineEditing、行为与生成/复制参数入口、PresetLibrary、Build-Native显式新CS清单及对应studio schema/bullet/template/copy/linked-copy/event/random/project模块；英文词条、双语README、docs/timeline-groups.md、event-presets/开发入口；新Test-TimelineGroups与CS/JS/GM定向测试。完整文件范围以git diff/status为准，所有projects不是本轮改动。
- 验证通过：Test-TimelineGroups（共享归组、隔离、静态/模板/联动/命名分裂复制、随机、清除未来、历史/倒拖、快照/保存/整理/FPS、实际Stage快照、菜单、属性公式与Undo）；Node相关6文件22项；Test-EventPresets、Test-BulletSystem、Test-Presets、Test-Localization、Test-CopyEditing、Test-CursorSnapping、Test-RandomTimeline。Cursor稠密夹具14.179ms全层/2.000ms输入覆盖层，仅离屏CPU成本，不代表屏幕Hz。
- 真实GM最终：desktop/tests/verify-groups-gml.mjs通过24时间点999条完整状态（含颜色、缩放、随机方向/速度/加速、source坐标、联动、模板/命名分裂归组），原应用Create/F5预热第30帧27条状态一致。最终项目build/timeline-group-check/GM Groups 1793111707165/GameMaker.gm82，日志gm-appearance-final.log。默认沙箱首次退出3221225794后改用提权隔离运行；没有用户桌面操作或留下测试窗口。
- 验证过程修正：新夹具加入同lane隐藏模板后索引假设失效，改按稳定ID；F5测试拼接多出括号，已修正并最终通过。RandomTimeline旧测试预期自由拖动30帧，却因先前已有的边缘磁吸落29帧；归档a0491ba源码在build/baseline只读复现同样失败，现为此原始拖动用例设置ClipSnap=false；磁吸另由CursorSnapping完整覆盖，未改产品吸附语义。早期失败日志保留。
- 正式Build-Native最终通过，File.Replace已更新根EXE并保留备份，没有关闭用户进程。未操作用户工程，tracked demo仍817增236删；没有本地提交或远程推送。尚无用户实操验收。
- 具体边界：未释放队形的速度方向不会代替它自身位置动画；属性重设不自动抹去后续年龄行为。自定义预设可保存事件参数/动作公式，跨工程显式组ID需重选目标组或跟随目标轨道；不是原生底片的视频后期改色/清除。

## 2026-10-27：2.4.0正式发布

- 本轮用户明确授权推送和Release。AppVersion、AssemblyVersion/FileVersion/InformationalVersion统一2.4.0，README双语首页、复制/排版/时间轴说明及docs/releases/v2.4.0.md对应本版最终行为。保留旧SHA256与兼容包，不更改标签发布工作流。
- 精确暂存70个应用源码/测试/文档/EXE文件，排除全部projects；提交54ae8ce4d12cfe0c5f84af524cce17798c117926。git push --atomic origin main refs/tags/v2.4.0成功。用户资料仍12个tracked文件817增236删，其余个人项目未跟踪。后续仅发布交接文档提交，不移动已发布标签。
- 本地Package-Release生成完整包58,985,200 bytes、更新包57,026,051 bytes；逐条检查manifest/SHA256、无个人资料、无重复文件、EXE版本及新增studio/linked-copy.mjs。原2.0.0更新器由32位CLR直接加载其旧二进制，接受2.4.0并校验2621文件；当前更新器替换、保留用户数据、隐藏重启通过；完整便携包首次建空工程并重开通过，无GM安装或游戏启动。
- 本版沿用各功能完成时已通过的UI/Node/真实GM数值对照，未在发布前无故重复游戏；更新器测试和发布包检查本轮重新运行。日志build/release-2.4.0-{package,package-check,updater-local,legacy-verified,portable-local}.log；本机根EXE使用同次Release构建。
- GitHub Actions任务122101163194全部步骤success，自动上传并公开4个附件。GitHub连接确认latest=v2.4.0，draft=false、prerelease=false、全部uploaded：完整包60,035,823 bytes，更新包58,063,037 bytes，以及各自102/85-byte SHA256文件。服务端ZIP digests分别042f66cb2d64c78fb7a498d02f09db3ca1aace7152cd16c380ca6ecf59b79548 / d022a85ab668378105b2d52fc72b4d5882a9b5a46e0c2fa11aece85cdb9607ca。CI平台编译/行尾压缩差异导致与本地产物大小不同，不据此认定相同字节。
- 在线验证边界：GitHub连接已确认发布元数据，公开附件HTTP302->200及长度正确。本机匿名REST/API和实际Updater.Latest返回403（rate limit），未通过在线更新检查；公开附件下载约60KB/s，curl90s只收到6,242,304/5,422,414 bytes后超时，不能宣称完整下载/SHA/解压通过。PowerShell下载校验长时间停滞，核对其具体命令与PID后停止了本轮build下的子进程；两个curl已超时退出，未留下下载任务。记录build/release-2.4.0-online-updater.log、published.log、download-headers.txt与published-curl的部分文件，未改应用网络逻辑或绕过校验。
- 发布目标已完成；没有功能待办或运行中的验证进程。若需再做线上完整下载，待网络/匿名配额恢复后运行Test-Updater.ps1 -Online -Version 2.4.0。不要把在线测试未通过写成已通过。此前标注“本地未推送”的历史功能均已包含于此发布，以下文字保留实施过程。

## 2026-10-27：松手吸附预告与游标绘制（本地完成，未推送）

- 用户本轮覆盖前一轮“停留160ms吸附”需求：拖动/停留始终raw，黄色虚线标出松手目标，只有松手提交。CursorScrub移除计时器，GuideChanged同步两处时间尺与右进度条标题；Alt立即隐藏候选且松手不追补。开始时Capture候选快照并排序一次，Move二分查询；关闭选项时不会显示假目标。保留所有物件起止/右边界/属性键及GM代码帧。
- 根因：旧PlayheadMoved虽只失效窄条，OnPaint仍重算波形/频谱、刻度、全部可见行和键；同时Frame整数更新会导致放大后10px一级的游标跳动。新增TimelineRendering.cs将底图与游标/虚线覆盖层分开，游标-only invalidation不重建底图；拖动使用独立小数位置，像素变化而游戏Frame未变时仍请求同步Update。预览、显示帧数和实际工程继续50FPS整数时间。
- 普通Invalidate、选择/数据/视口/大小变更重建底图；缩放/滚动仍需绘图，未做图块滚动缓存。手势结束清浮点位置、去虚线，覆盖旧指针列避免残影。Build-Native显式列表已加入TimelineRendering。
- 右栏进度条保持原生操作，标题用箭头提示候选帧；其PrepareCursorTargets/GuideChanged与上下时间尺相连。下栏与关键帧尺在拖动中共享虚线。布局和工程保存格式本轮未变，无GML/弹幕语义变化。
- 文件：desktop/PreviewPlayback.cs、TimelineRendering.cs（新）、TimelineCanvas.cs、TimelineAudio.cs、MotionPresets.cs、SelectionTimeControl.cs、EditorShell.cs、KeyframeWorkspace.cs、MainForm.cs、Build-Native.ps1；Test-CursorSnapping.ps1、tests/CursorSnappingTests.cs、tests/TimelineCursorRenderingTests.cs（新）；locale、README双语、动作/开发指南、本记录/STATUS和根EXE。
- 验证：Test-CursorSnapping通过候选查找与旧Snap全范围对比、索引快照/每手势一次、停留不移动、松手命中虚线、Alt/捕获/换工程/偏好、多选/拉伸/接线及原生右滑条。自身不激活离屏窗口像素验证615处raw游标与610处金色虚线、松手到610且615无残影、Alt无金线。图片cursor-guide-held/released/alt.png已检查，位于build/cursor-snap-check。
- 密集夹具20,002手工键与400,000波形采样：全底图平均12.916ms，输入+覆盖层离屏绘制1.455ms；240次同步绘制请求/离屏绘制，0次静态重建、每手势1次索引。数据在cursor-paint-profile.txt。该数据不代表屏幕刷新率或游戏FPS。初始测试将屏幕外Update误当实际WM_PAINT计数，已用请求计数+实际离屏绘图替代；初始失败日志保留，没有修改产品去强制画不可见窗口。
- Test-KeyframeWorkspace、Test-MotionEditing、Test-Localization、Test-AudioStatistics均通过；日志build/cursor-guide-{paint,keys,motion,localization,audio}.log。Build-Native最终通过，build/cursor-guide-build-final.log；git diff --check通过。File.Replace已覆盖根EXE并备份旧入口，用户demo保持12文件817增236删。
- 本轮未运行GM、未测用户复杂工程的实际屏幕Hz；游戏逻辑/导出未变。没有提交/推送/Release，远端v2.3.0仍不含本地后续修复。当前占用已释放，无其他必做待办，待用户实操反馈。

## 2026-10-27：游标/片段磁吸与平滑拖动（本地完成，未推送）

- CursorSnapping从选中单物件改成全部歌曲物件start/end/end+1和范围内所有属性keyframes、原生代码events；隐藏模板及其后代不混入歌曲时钟，选中模板可供局部预览使用。目标优先于BPM，直接输入帧数/逐帧/左右键保持原来精确导航。
- CursorScrub位于原有PreviewPlayback.cs，Build-Native既有列表已包含；MouseMove只传raw frame并去重，不查找/排序吸附目标。160ms无移动或松手才吸附一次；移动后重新回到raw。Alt样本与停留/松手时的Alt均禁止补吸附，之后松开Alt不追补；已停住吸附后按Alt可恢复原raw位置。换工程、捕获丢失、Dispose均取消或结束计时。
- TimelineCanvas（下栏与右栏关键帧尺）及SelectionTimeControl的进度条复用此逻辑。进度条也处理原生ValueChanged早于MouseDown的顺序，反馈SetFrame不重新启动吸附。SnapTolerance按实际进度条范围/宽度计算。
- SnapClipDelta新增共享Frame为优先目标，按[start,end+1]可见边缘对齐，移动、左右拉伸和多选同量变化；保留同轨无缝衔接、跨轨边缘与关闭开关。Alt跳过这些目标，松手不再补吸附。移除了每对物件新建候选List的开销。
- WorkspaceView新增nullable cursorSnap，仅保存UI；旧布局缺失时按本次需求默认开启，用户关闭后可保存恢复。旧keySnap字段继续写出兼容旧版；全局开关不再因没有选择/多选而禁用。中文/英文复选框和说明已更新。
- 文件：desktop/PreviewPlayback.cs、TimelineCanvas.cs、TimelineAudio.cs、SelectionTimeControl.cs、MotionPresets.cs、EditorShell.cs、MainForm.cs、WorkspaceView.cs；新Test-CursorSnapping.ps1和tests/CursorSnappingTests.cs；现有PreviewPlaybackTests与LocalizationTests调整有效夹具/新文案；assets/locales/en.json、README双语、动作/开发指南、交接及根EXE。没有工程字段/GML/弹幕运行变化。
- 定向：Test-CursorSnapping通过任意物件/隐藏局部排除/原生帧、连续raw事件、真正UI计时器、停留/松手/中途Alt/松Alt后不追补、捕获/换工程、主时间尺与右进度条、移动/双侧拉伸/多选/跨轨、偏好迁移/保存。Test-MotionEditing、Test-KeyframeWorkspace、Test-Localization、Test-PreviewPlayback均通过。原生控件以自身测试窗口/反射消息验证，不控制用户桌面；静音合成音频仅供既有播放回归。未测试实际复杂工程预览FPS，不将事件连续性写成FPS提升。
- 日志build/cursor-snap-verified.log、cursor-snap-motion.log、cursor-snap-keys.log、cursor-snap-localization.log、cursor-snap-playback.log，初始测试日志保留。Build-Native最终通过（cursor-snap-build-final.log），File.Replace覆盖根EXE并备份。git diff --check通过；用户demo仍12文件817增236删，无新增测试写入用户资料。
- 未进行本轮GM编译/试玩（纯编辑交互），未提交、推送或发布。当前EXE继续包含前述全部未发布本地改动，远端v2.3.0无此更新。无其他必做待办，待用户实操反馈。

## 2026-10-27：实时联动复制与逐成员网格铺满（本地完成，未推送）

- 新字段BulletAction.copyLinked默认true，C#字段初始化与JS copyDefaults一致，保存/快照/复制/预设保留。旧规则缺失字段时进入联动模式。关闭后仍走原有独立模板；弹幕年龄行为继续独立生成，当前联动入口是track.spawnActions。
- TemplateRuntime按实际来源图和当前时钟建立一组链接，复用来源当前坐标、形状、数量、角度、外观与子物件动画。linkKey去重，来源结束/隐藏时清掉链接；保留owner serial和上下文，检查点恢复不依赖旧实例引用。独立模板继续使用原始anchor与出生时钟。
- CopyGeneration.LivePlacements/GridPoint按实际房间中心、旋转后房间投影及最小宽高计算覆盖格点，对每个成员取模。无来源中心出屏裁剪；原成员位置处省略重复副本，避免叠加。最多300份，过密需增大间距。整体释放前先在原形状计算方向，再映射位置，保留副本颜色/大小，并省略原位置重复成员。
- studio/linked-copy.mjs生成rf_copy_link_spawn/update/point；template-system的来源姿态、激活条件与shapeSetup同时保留原父级/生成实例上下文，防止父角度二次进入形状phase。更新函数显式传入当前帧，避免脚本局部时钟泄漏。rf_copy_started为普通GM数组，不引入DS映射；F5目标帧直接验证实际弹幕。
- UI在行为页/展开编辑暴露“实时联动来源”，联动时隐藏独立模板/重复生成/出生继承设置。下栏显示链接生效条，收起失效的快照模板卡片；关掉链接后可继续编辑独立模板。同步中文、英文、README与复制教程，产品版本未变。
- 改动：desktop/EditorCore.cs、CopyGeneration.cs、TemplateRuntime.cs、BulletSystem.cs、GenerationPresets.cs、BehaviorWorkspace.cs、BulletBehaviorForm.cs、SpawnTimeline.cs、tests/CopyEditingTests.cs；studio/copy-generation.mjs、linked-copy.mjs（新）、template-system.mjs、bullet-system.mjs、copy-generation.test.mjs；desktop/tests/verify-copy-gml.mjs；assets/locales/en.json、README/docs及本记录与根EXE。未改用户工程、原件、歌曲或底片。
- 验证：Test-CopyEditing通过来源应用后编辑、当前半径/数量/角度/颜色、旋转与屏外逐成员覆盖、倒拖/200帧检查点、结束清理、JSON/预设及中英文参数UI。Test-TemplateSystem、Test-GenerationPresets、Test-Localization通过；Node copy/template 8/8及bullet/random/event 11/11。CopyEditing自身离屏参数图已检查。
- GM：verify-copy-gml.mjs新增移到-1720的整体、数量/半径动画、旋转网格朝向/颜色/释放、父级旋转、生成实例内部链接及旧独立复制，逐条对照15,411状态、17特效值；F5 85帧进入后的实际弹幕与C#一致。隔离工程build/copy-editing-check/GM Copy 1793084904096/GameMaker.gm82。所有探针隐藏且错误弹窗关闭，失败日志保留，成功探针已退出。日志build/copy-live-gm-verified.log；初始时钟/父级/释放问题日志也保留。
- 构建：Build-Native ->build/copy-live-check/RufMaker.exe通过，File.Replace已更新根入口并备份旧EXE；build/copy-live-build-final.log。当前根入口仍2.3.0开发修复版，不代表远端v2.3.0包含本轮修改。无本轮提交与推送。未验收用户真实工程交互及原生R存档恢复；已完成隔离GM/F5状态数值检查，无其他必做待办。

## 2026-10-27：关键帧页内时间条与暗色滚动条（本地完成，未推送）

- 用户截图指右栏上方共享时间条并追加白色滚动条反馈。BuildShellInspector由标题/共享时间/停靠区三行改为标题/停靠两行；BuildKeyframeProperties成为AutoSize时间条、工具栏、Fill列表/详情三行布局。没有新增时间控件或改播放头/起止事件，rightTime只有一份且直接挂在DockPage:keys内。
- DarkScrollbars在Windows10 build18362及以上通过RtlGetVersion及动态uxtheme入口查询设置本进程ForceDark，AllowDarkModeForWindow+SetWindowTheme(DarkMode_Explorer)应用于实际原生滚动窗口。缺少入口/旧系统保留原生回退。只对AutoScroll=true的滚动面板及ScrollBar/ListBox/ListView/TreeView/TextBoxBase/ComboBox设置窗口主题，不给普通布局容器套原生主题。
- Program在VisualStyles前初始化；MainForm构建后附加，Language.Attach覆盖所有应用弹窗；ConditionalWeakTable避免重复事件、句柄标识避免每次Layout重复应用，HandleCreated/Destroyed处理重建，ControlAdded覆盖动态属性。ComboBox原生列表/编辑句柄在建立和DropDown时分别应用。未改Windows全局主题设置，没有自绘或接管滚动交互。
- 临时隔离ThemeProbe先确认原生暗色样式能覆盖控件，随后Test-ScrollbarTheme在自身不激活/屏幕外窗口核对横纵、Panel、ListBox、TreeView、资源ListView、GML横纵、动态TextBox与句柄重建，样图在build/scrollbar-theme-check。保留原生ValueChanged、AutoScrollPosition、TopIndex。
- InspectorDocking新增rightTime祖先为DockPage:keys断言。DrawToBitmap对合并后的隐藏页面产生部分托管标签漏绘，改为在自己的离屏ImageForm用PrintWindow获取实际布局，证实Start/End标签、工具栏与关键帧详情完整；没有为截图问题改产品绘制。该测试不启动MainForm.Shown，也不读取/控制用户桌面或启动GM。
- 日志build/scrollbar-theme-check下theme-final、docking-print、workspace、events、performance、localization、shell、build-final.log均通过，初始日志与native-theme.png保留；all-scrollbars及build/inspector-docking-check下更新的columns/tabs中英图已检查。性能回归仍保留常规控件，绝对耗时受并行影响，不用作游戏FPS结论。
- 文件与用户数据边界如上，旧exe保留，版本仍2.3.0本地含未发布修改。未提交或推送；指南docs/inspector-layout.md描述时间条位置，开发入口记录系统版本边界和验证方式。无必做待办，等待用户保存重启后的实操反馈。

## 2026-10-27：右栏可拖动停靠页面（本地完成，未推送）

- 新增InspectorDockWorkspace，两个常驻Page控件在原生SplitContainer与页签容器间重新挂载，不Dispose或重建输入控件。鼠标从标题或页签捕获并检测拖动阈值，四边/中心有停靠区域预览；出界/Esc/捕获丢失取消。标题⋮与右键提供对应菜单和默认布局。停靠范围为右栏内；外层rightSplit仍决定整个右栏大小。
- WorkspaceView新增可选inspectorDock={mode,first,active,rows,columns,details}，不进EditorState。上下/左右比例独立；旧keyframesHeight保留像素高度直到用户调整/保存，旧keyframesOpen=false迁移为页签。缩小窗口只约束实际Divider，不覆盖偏好；0高度隐藏时不设置无效距离。页面前后顺序与当前页签可恢复。
- 常规inspectorTabs仍是属性0/行为1/统计2，以保留既有入口。选中帧的专用字段从properties移到独立keyProperties，通过NativeProperties.TryRebind/Reconcile复用。keySplit现在分关键帧列表/详情；无单选可动画物件时显示提示。关键帧属性名只翻译显示值。统计状态链接和生成规则聚焦会激活常规页，下栏关键帧入口激活关键帧页。
- CommitInspectorEdits同时提交properties/keyProperties；各工程/预设/语言/引擎/模板/更新入口使用同一方法，Ctrl+S、F5和导出也提交当前输入。Ctrl+Z取消输入与焦点判断包含keyProperties，选帧刷新计时器同步两端。keyProperties数值上下拖动沿用原BeginScrub/EndScrub与参数步长。菜单上下文按来源选择正确关键帧属性。
- 定向Test-InspectorDocking验证四边真实控件鼠标事件、从页签拖出、独立宽高比例、菜单共用DockPage路径、窗口缩放/0尺寸、出界/Esc、旧高度270/旧折叠迁移、工作区JSON恢复、布局不写工程/Undo、详情值修改和Undo后选中、切键重绑以及中英文900/1100/360px视图。新源文件已纳入Build-Native显式列表。
- 既有KeyframeWorkspace/RandomTimeline/EditorPerformance测试的“数值”引用改到新keyProperties，保留原撤销、控件复用与步长验证含义。性能合成40物件6400键标量编辑中位2.66ms、撤销8.57ms，保留全部50个常规控件；并行验证测量不当作游戏FPS或与前轮的严格性能比较。
- 初次Docking测试用JSON字符串比较因字段输出顺序不同失败；SceneComparison及完整解析后逐值对比确认无工程值变化，修正断言后通过。原失败docking-initial.log与layout-before/after.json保留。首次编译提示Capture方法遮蔽Control.Capture，改为CaptureView；最终构建无该警告。没有无人处理错误弹窗或用户进程被关闭，未用电脑控制技能。
- 最终日志docking/localization/shell/lanes/copy-final.log、workspace/performance/event/random-initial.log（这些回归均通过）、build-final.log与inspector-columns-zh/en、rows-zh、tabs-zh、narrow-en等图在build/inspector-docking-check。完整旧状态与同轨互斥继续保留，教程与中英README说明本地功能，不冒称远端已发布。

## 2026-10-27：同轨时间互斥（本地完成，未推送）

- 时间边界包含首尾：49结束与50开始可同轨；重叠检测包括禁用的片段、独立模板和父子组成员，不处理原生轨道与已绑定生成组模板。打开工程先Motion.Normalize保留原先执行排序，再EnsureExclusive分轨、设置preserveTrackOrder并同步保存；重复打开不再移动。原有项目中的物件不会被删除、裁短或改动画。
- MainForm.MarkChanged按主选择、其他选中、其余原顺序优先放置；先查是否冲突，仅冲突时分配空闲lane并保留当前执行顺序，覆盖新增、复制/粘贴、数值修改、右键改轨、多选、预设、分组与拆片段。LaneCount递增；最多200条同时物件可分别落在0..199轨。正常状态一次检测后不改lane或执行顺序。
- TimelineCanvas去掉同一lane再分叠行的显示；拖放/拉伸仍走原CutOverlaps并在松手时裁掉被覆盖片段，自动分轨只补齐未裁剪的冲突。被引用模板保留生成组内行；这种行不是独立歌曲时段。
- 旧RandomTimeline回归假设重叠叠行，改为先调用旧工程分轨并验证；原用例明确选keep关键帧模式以验证边界拖动，保留其不移动键的测试含义。两次针对旧期望的失败日志保留在build/lane-exclusivity-check/random-old-overlap-expectation.log与random-stretch-expectation.log，修正后通过。
- 项目同步测试先设置RUFMAKER_ROOT/PROJECT再导入studio/project.mjs：build/lane-exclusivity-check/Lane Sync 1793041880298/GameMaker.gm82/rufmaker-demo.gm82；迁移前后同项目生成脚本与控制器完整一致。仅此隔离工程写入，未使用桌面控制或关闭用户进程。

## 2026-10-27：2.3.0发布验证（完成）

- 用户在实现中追加明确授权“做完推送git并且release”。54个精确产品文件提交cea040920c9ac8c42c472989eea53114a9b17f59，main与annotated tag v2.3.0原子推送成功，未包含任何projects/歌曲/底片。随后仅补交接文档，不改发布标签或重新发布二进制。
- Actions 40382567777：completed/success，https://github.com/wymcaT/RufMaker/actions/runs/40382567777。Release https://github.com/wymcaT/RufMaker/releases/tag/v2.3.0 为latest、draft=false、prerelease=false，四个附件均uploaded。
- 线上完整包60,002,231 bytes，SHA256 2fcafbbf9313621169ef9848b9872b9a6abce53b38a612124791eed11d0e7e1e；更新包58,047,468 bytes，SHA256 739f6743efd5bbdae5d8fae784abd0e527ac104f515e68d7ce0832ffb34a389c；两份旁置校验文本102/85 bytes。线上CI EXE为2.3.0/1,256,960 bytes，本机编译2.3.0/1,258,496 bytes；编译器/文档打包差异不误当成字节一致，以各自清单校验为准。
- 本地两个包检查通过，原v2.0.0更新器接受2.3.0更新包并校验解包2620文件。Test-Updater.ps1本地确认版本提示、保留用户文件、隐藏替换/备份/重启通过；-Online -Version 2.3.0实际调用产品latest、下载公开ZIP及SHA、逐清单解压通过。
- 线上完整包已下载到build/release-check-2.3.0/published-portable.zip，旁置SHA和GitHub资产digest均吻合；解压内容有EXE/运行依赖/资源/模板/文档、无用户工程，verify-portable-release.mjs首次新建空工程和重开通过。未启动或控制用户的游戏/编辑器。
- 日志在build/copy-editing-check：release-published.json、release-runs.json、updater-local/online.log、package-release/check.log、old-updater-release.log、portable-release/online.log；本地与线上包在build/distribution/v2.3.0和build/release-check-2.3.0。用户demo仍12文件817增236删及个人未跟踪工程保持。无待发布必做项，尚无本版用户实操验收；后续推送仍需用户新授权。

## 2026-10-27：复制obj与时间轴整理、关键帧拉伸（完成，2.3.0已发布）

- 新增CopyGeneration.cs/copy-generation.mjs，生成模板由五种扩为六种。复制源及parentId子树成共享templateOnly定义，保留旧行为，在克隆后才附加新规则避免无意自递归；默认spawnActions整对象/发射器，间隔50帧、0为一次，可切到飞行弹幕。副本从模板起点播放，来源后改不自动重制模板；已有规则可多代生成，沿用四代限制。
- BulletAction新增copyMode/Facing/ColorMode/Color、Up/Down/Left/Right/Center、Count、SpacingX/Y、Width/Height、Radius/Angle/FacingAngle/Hue/ColorDensity。方向支持多层；网格宽高为像素、中心对称、旋转、可排除真实中心格点；圆周等距；每轮最多300，UI编辑先校验快照。朝向保留/固定/内向/外向/切线并减模板初始角，颜色继承/固定/彩虹。普通图混色需白底，彩虹Cherry按副本冻结贴图帧，继承模式保持成员彩虹分布。分组保留子图。C# GenerateOne/GM rf_template_spawn共用既有后代时钟、外观/大小继承和生命周期；rf_copy_anchor_angle仅有复制规则时生成。复制模板是动画初始快照，未宣称实时深拷贝任意手写GML实例。
- TimelineCanvas含末帧的长条画到end+1，命中/边界/拖动预览同步；相邻同轨同物理行画1px白缝，不改实际帧。LaneAt优先实际落点物件，选中用于共边界判别，避免左片段宽容区抢走右片段。
- 新增TimelineEditing.cs。整理按类型/形状/功能分lane，同类非重叠共享轨道，模板后代跟源收纳。EditorState.preserveTrackOrder（默认false，整理时true）令C#/JS归一化、展开片段及模板定义保留原执行数组，避免改变随机、同帧变量和绘制次序。支持Undo/保存；不动时段和参数。
- WorkspaceView.stretchKeys默认sync，菜单四模式keep/sync/scale/repeat，所有拉边/右栏/多选入口统一。普通键按比例，密集camera/effect某属性>=8键时自动重复；Track.keyframeCycle深复制保存length、相对keys及FNV编辑戳，原周期包含末帧，0..20下一轮从21开始。缩短再增长保留原样式，手改后再捕获，尾周期补到末帧；跨FPS换算周期和速度/寿命单位。动作预设XY先排除再按参数重铺，其余键照选项处理，自动XY不会误判密集重复。重复已烘焙到真实关键帧，预览索引与GML>32键二分树一致，不新增仅编辑器效果。
- 文件：desktop/CopyGeneration.cs、TimelineEditing.cs、GenerationPresets.cs、TemplateRuntime.cs、EditorCore.cs、BulletSystem.cs、PresetLibrary.cs、Motion.cs、TimelineLayout.cs、TimelineCanvas.cs、EditorShell.cs、MainForm.cs、BehaviorWorkspace.cs、WorkspaceView.cs、KeyframeWorkspace.cs、Build-Native.ps1、Updater.cs；studio/copy-generation.mjs、template-system.mjs、bullet-schema.mjs、motion.mjs、project.mjs；定向测试/GM探针、中英词条及README/生成教程/复制教程/剪辑教程/开发入口/交接/发布说明与EXE。此前两轮产品修改一并保留。新CS已列Build-Native显式源表。
- 定向覆盖方向/旋转网格/圆周、初始朝向校正、颜色/彩虹帧、分组随机子发射器、倒拖、循环、保存预设/FPS、比例/保持/重复/缩短再增长/手改样式、动作路径+其他属性、无缝白线像素、右栏resize/Undo和整理执行不变。查看copy-grid-zh/en、adjacent-seam与宽窄shell图。性能合成40物件6400键，普通编辑中位1.72ms、撤销5.26ms且控件保留；不当作游戏FPS。
- 最终GM夹具build/copy-editing-check/GM Copy 1793040744857/GameMaker.gm82：1487条含出生/坐标/运动/朝向/混色/彩虹帧，17个重复特效值及F5第85帧一致。只用隔离工程，未控制用户桌面。日志copy-inclusive-delivery、gm-inclusive-delivery、node-final-delivery、build-final-delivery、templates/performance/shell/localization、events-ui-delivery等在build/copy-editing-check。
- 发现并修正两处：恢复行为页签时隐藏SplitContainer高度0，SplitterDistance越界；高度有效才设置，保留hidden-sidebar-failure.txt并同布局及事件分隔条回归通过。早期循环长度漏包含末帧，稀疏非闭合键边界覆盖变平；改为包含首尾，sparse-cycle-failure.log保留，速度/FPS与路径混合周期用例通过，重新GM核对通过。无无人处理错误弹窗，无用户进程被关闭。

## 2026-10-27：可拉伸关键帧区与全局事件（完成，本地未提交/推送）

- 用户反馈上轮关键帧区被压成滚动条、起终点按钮裁切，并要求事件预设。修正固定200px上限：KeyframeWorkspace使用SplitContainer，工具栏在外，关键帧/参数分别挂两面板；分隔条7px可拖，宽度/字号改变按最小可见行约束而不覆盖手动高度。WorkspaceView新增keyframesHeight=220，保留keyframesOpen；收起后保留标题，多选/无动画字段隐藏键区。刻度/名字宽度/行高随字号调整。SelectionTimeControl原生TrackBar关闭AutoSize，表行按实际字体/数字框高度计算，主检查器时间区改AutoSize，标签保持亮色。
- 左栏新增event-presets/“事件”，支持拖到歌曲时间落点或双击播放头插入control事件，默认start=end，可拉长每帧执行。四种：clear-bullets清飞行子弹（保留未释放队形）；clear-danmaku同时隐藏当前rod/object和模板可视成员，后帧不再复活；stop-bullets该帧速度0并跳过本帧加速积分，未来行为可恢复；reset-motion恢复出生速度/方向或用自定义速度方向加速度转向，可禁用后续年龄运动/追踪/表达式，外观/寿命/生成行为和未来时间轴控制保留。
- 模型复用control轨道，新增eventPreset、eventNativeObjects、eventRestoreBirth=true、eventDisableMotion=true、eventAcceleration、eventTurn；controlAction固定event，controlValue/from存自定义速度/方向。新字段为primitive，Snapshot/Undo/复制直接保留；Normalize同步默认和校验，跨FPS预设/片段换算速度/加速度/转向单位。SimBullet.motionReset纳入现有MemberwiseClone检查点。
- C#按帧缓存清除条件，清子弹在年龄行为前立即移除（含寿命过渡中的弹幕、触发帧新弹，未真正出生的延迟弹保留）；清所有弹幕通过TemplateRuntime.hidden快照维持显隐，包括同帧新生成模板。GML对应eventEarly/eventTick/rf_event_prepare/rf_event_template，纳入生成脚本索引；rf_motion_reset在objRufBullet初始化，出生后行为及追踪守卫一致。没有改GM默认FPS或游戏重启方式。
- 可额外填写逗号分隔的原生对象名，校验真实工程资源和标识符，不自动猜测玩家/地形/控制器；GML with(parent)覆盖子类且跳过objRufBullet以免重复处理。原生reset使用自定义数值操作GM内建speed/direction/gravity/friction，自定义Step仍可覆盖；出生状态无法推算。原生事件只在真实当前rf_frame执行，避免在overlay预热循环反复作用于当前原生实例。缓存原生底片不会随这些事件改变，F5/导出核对原生效果。
- 实际文件：desktop/KeyframeWorkspace.cs、SelectionTimeControl.cs、WorkspaceView.cs、TimelineCanvas.cs、TimelineAudio.cs、EditorShell.cs、MainForm.cs、EventPresets.cs、EditorCore.cs、BulletSystem.cs、TemplateRuntime.cs、BulletTools.cs、Motion.cs、PresetBrowser.cs、PresetLibrary.cs、Build-Native.ps1；studio/event-presets.mjs、event-presets.test.mjs、bullet-schema.mjs、bullet-system.mjs、template-system.mjs、project.mjs；desktop/Test-EventPresets.ps1、tests/EventPresetTests.cs、tests/verify-event-gml.mjs；assets/locales/en.json、双语README、docs/event-presets.md、动作教程/开发入口/交接及EXE。上一轮未提交UI文件仍保留。
- 定向EventPresets通过：清除含死亡过渡/同帧出生、保留未来发射、区分held、无复活、模板后代、连续停速、运动行为禁用/恢复出生、205帧检查点倒拖、Snapshot和跨FPS、分类/落点/Undo/双语、真实分隔条移动不被刷新覆盖且保存、高字号完整行/起终点。KeyframeWorkspace原回归通过；EditorPerformance/ShellLayout/Localization通过；Node event/bullet/template/presets/random共17/17。属性控件保留，编辑中位3.10ms、撤销8.92ms（合成40物件6400键，不代表游戏FPS）。
- GM：通用生成探针537条状态/17帧变量/F5第85帧一致（GM Generators 1793032923224）；专用事件探针543条状态/21个显隐与静态整体清除取样、原生父子对象清除/停速/自定义reset一致（GM Events 1793033270863）。最终新增出生恢复事件也包含在专用探针中。编译和实际运行日志均检查，不是只验证编译。
- 失败记录必须保留：沙箱GM启动3221225794后获审核运行；首个新rf_event_template缺闭括号，修正并增加Node全脚本括号验证后通用探针通过。新原生测试对象曾误用id/kind元数据，触发用户截图的unknown key和resource data弹窗；改为工程标准action_id=603/applies_to=self，测试进程已退出，Get-Process无残留。随后探针读取rf_dir/rf_speed被控制器局部变量遮蔽，改self字段后专用检查通过。以上均build隔离夹具，用户工程未写入。最初Splitter测试反射只搜NonPublic未找到公开OnSplitterMoving，改直接调用；产品控件无该问题。
- 产物和日志在build/event-presets-check：events-delivery/performance/shell/localization/node-delivery/build.log、gm-native-final/gm-events-final.log与保留失败日志；resizable-keys-large/time-large等截图已查看。Build-Native显式新增EventPresets，根EXE版本2.2.0/1,232,896 bytes安全替换，备份RufMaker.before-20261027-005325.exe。没有结束用户编辑器，也没有使用电脑控制技能。
- 用户demo仍12文件817增236删，个人工程/素材/歌曲未提交。无本地提交、无远程推送或Release；应用版本未提升，本机含未发布功能。无必做待办，用户需保存重启后实际验收；原生录像和任意手写运动代码边界如上。

## 2026-10-27：右栏关键帧与精简下栏（完成，本地未提交/推送）

- 用户要求关键帧编辑留在右栏，下栏只显示当前物件的键、其他全部折叠且隐藏属性行。从2de5a96/main接续；本轮无推送授权。用户projects、歌曲、个人预设未写入；demo仍12文件817增236删。
- 新增KeyframeWorkspace.cs并纳入Build-Native显式源码。右栏属性页顶端嵌入TimelineCanvas关键帧模式，按已动画属性分行；选择、边界内拖动、框选、增删、前后键跳转、适应和Ctrl滚轮缩放沿用原编辑路径。与主时间轴共享播放头和选中；选中键的帧/值/缓动放在参数表前方。高度按可用空间适配，可收起；多选/未选/原生代码轨道不显示这块。
- WorkspaceView仅新增keyframesOpen=true布局字段，旧布局可读。产品下栏SelectionKeyframesOnly、右栏InspectorKeyframes；底部不建立属性子行，只绘制当前物件摘要，点击标记定位并打开右栏，双击普通物件也可打开。其他物件及未选原生事件标记隐藏；生成组聚焦当前来源/模板引用，保留规则和卡片，选中模板显示局部键摘要。
- 旧expanded偏好不重新铺开属性行；无效的展开关键帧/原生来源菜单移除，改用右栏编辑入口。“只展开当前生成组”固定成为产品行为。TimelineCanvas原通用详细模式保留供右栏和既有独立控件测试复用；新增用例专门核对产品紧凑模式。
- 自动采样动作预设XY不进入手工编辑区，仍通过动作参数修改；没有删键或改变工程/JS/GML/随机/运动语义。右区宽度变化重适应物件时段，窄栏刻度标签不再侵入属性标题。
- 文件：desktop/TimelineCanvas.cs、TimelineAudio.cs、SpawnTimeline.cs、MainForm.cs、EditorShell.cs、StatisticsTools.cs、WorkspaceView.cs、KeyframeWorkspace.cs、Build-Native.ps1；Test-KeyframeWorkspace.ps1、tests/KeyframeWorkspaceTests.cs；assets/locales/en.json；README中英文、docs/motion-timeline-editing.md、开发入口、协作状态及根RufMaker.exe。
- 定向验证通过：旧展开布局、选择切换、下栏无属性行/未选键不命中、右栏拖动/框选/边界/单次Undo/恢复选中/空白不丢物件/加删键/数值重绑/共享时间、多选/共用模板/密集动作、中英文250px窄栏。最初JSON字符串等值断言受字段输出顺序影响，改持久字段SceneComparison后确认内容不变；不是工程数据变化。
- 回归通过：EditorPerformance、ShellLayout、Localization、MotionEditing、RandomTimeline，含控件复用、撤销重绑、折叠滚动锚点和物件拖动/覆盖/吸附。合成40物件6400键的标量编辑中位2.74ms、撤销6.82ms；不当作游戏FPS。未改运行公式，不重跑GM探针。均离屏原生控件，未控制用户桌面。
- 日志/截图：build/keyframe-workspace-check下workspace-final/build/performance/shell/localization/motion/random.log，workspace-zh、keys-zh/keys-en/keys-narrow-en、bottom-compact.png。Build-Native通过，根EXE版本2.2.0/1,220,096 bytes，File.Replace安全替换，旧入口备份RufMaker.before-20261027-001448.exe。
- 无本地提交、无推送、无Release。应用版本暂未提高，本机EXE有未发布UI改动，不能称远程2.2.0包包含本轮修改。无必做待办，待用户保存并重启后的使用反馈。

## 2026-10-26：2.2.0正式推送与发布（完成）

- 授权：用户先要求不再用SHA256，得知会破坏旧更新器后明确选择“保留原更新方式，继续推送发布”。取消尝试已精确撤回，AGENTS/打包脚本/工作流保持原内容，Updater实现只含既有2.2.0版本变更。没有私自改变后续默认不推送规则。
- 从b88164b/main接续，远端main为71a0982且是本地祖先；先验证发布包，再精确暂存57个产品源码/测试/文档/EXE文件，提交b53abb9242244cd5152e681bd2defe86f419e4a2。用户projects、歌曲、个人耐久、安装与缓存均未暂存。main与annotated tag v2.2.0原子推送成功。
- GitHub Actions：40302243614，https://github.com/wymcaT/RufMaker/actions/runs/40302243614，head为b53abb9，completed/success。https://github.com/wymcaT/RufMaker/releases/tag/v2.2.0 为latest、draft=false、prerelease=false；四个附件均uploaded。
- 线上附件：RufMaker-2.2.0-Windows-Portable.zip 59,955,673 bytes（SHA 18c44109b955f15a799ad01754e9e68405bbf31b2becfb798289d6145d94ed33）；RufMaker-2.2.0.zip 58,020,935 bytes；两份旁置SHA文本102/85 bytes。CI完整包解压EXE版本2.2.0、1,211,904 bytes。本地编译EXE1,212,928 bytes，本地ZIP58,905,941/56,983,902 bytes；编译器/压缩输出差异，以线上自身清单校验为准，不误认字节一致。
- 本地验证：Package-Release构建成功；两个ZIP检查版本、必需文件、重复条目、个人数据排除与逐项散列通过。原v2.0.0的Updater.cs单独编译测试探针，直接接受新更新包并校验解压2616个文件。Test-Updater本地确认版本提示、保留工程/音乐/预设/安装、备份及隐藏重启通过。没有操作用户桌面。
- 线上验证：Test-Updater.ps1 -Online -Version 2.2.0通过，实际使用产品更新逻辑请求latest、下载ZIP与SHA、逐清单校验/解压。实际下载线上完整包并核对SHA及GitHub资产digest，运行build/verify-portable-release.mjs，确认EXE/依赖/素材/文档/模板齐全、无个人工程，在隔离无GM环境首次创建空工程及重开通过。
- 日志/产物：build/distribution/v2.2.0、build/release-check-2.2.0、build/motion-editing-check下package-release/package-check/old-updater-check/updater-release/updater-online日志、release-published.json与release-runs.json。早期只读API曾共享IP限速，带新鲜查询后恢复；便携包首次检测把HTTP Byte[]内容当成文本误判，ASCII解码后匹配旁置SHA及GitHub digest，产物无损坏。测试探针首次csc相对路径解析有误，改绝对路径后通过。
- 发布说明汇总2.1.x及2.2.0功能，README中英文已指向2.2.0。当前仅补交接文档提交，不重打标签或重新发布二进制。无待办；用户原demo12文件817增236删仍保持，其他个人项目/例子未提交。后续工作仍需用户新授权才push。

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
