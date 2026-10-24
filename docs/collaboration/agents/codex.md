# Codex 任务记录

- 状态：`completed`
- 任务：补齐共同开发规则与 Argon4 接手材料。
- 开始时功能基线：`88853a8`，分支 `codex/bullet-control-beta`。
- 当前占用文件：无；本轮交付后释放文档占用。
- 集成角色：当前负责阶段收口和整体一致性；可按用户安排调整。

## 本轮改动

- 根 `AGENTS.md` 与 `desktop/AGENTS.md`、`studio/AGENTS.md`。
- `docs/development/GETTING_STARTED.md`。
- `docs/collaboration/STATUS.md`、`START_ARGON4.md` 及本目录接手模板。
- 旧目录说明补充新的开发入口。

没有进行中的产品代码任务。Argon4 可在阅读双方记录后登记未占用的任务；建议从 STATUS 的 A1 开始。不要把这里的建议视为其已经接单。

## 对齐依据

所有代理以用户最新明确指令、根 AGENTS 和实际 Git 状态为准。代码基线的既有检查见 STATUS；本轮只核对文档路径、命令入口与规则加载方式，不重新宣称一轮游戏验收。

本轮文档检查通过：9 份 Markdown 的相对链接、69 个源码/构建/测试入口均存在，原 Git 推送约定完整保留。已核对本机两个 EXE 的 ProductVersion 与交接记录一致，并检查文档 diff 无空白错误。

本轮本地文档提交可用 `git log -1 -- docs/collaboration/agents/codex.md` 查询；未推送。

## 下次开始任务时更新

将状态改为 `in_progress`，填写具体任务、基线 SHA、占用文件、跨端字段/接口与验收方式。结束时记录命令结果、提交 SHA、限制，并释放占用。
