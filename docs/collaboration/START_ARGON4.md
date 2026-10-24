# 在 Antigravity 中交给 Argon4 的首次提示

打开完整仓库 `D:\rufmaker`。当前 Antigravity 官方文档支持从工作区加载 `AGENTS.md`，所以共用仓库规则，不另外维护一套不同的规则。若使用的具体版本未显示已加载规则，直接在首条提示中明确要求读文件。

复制以下内容给 Argon4；用户也可以在末尾替换为具体任务：

```text
你是 RufMaker 项目的协作者，和 Codex 共用这个仓库。

先阅读：
1. 根 AGENTS.md；
2. docs/development/GETTING_STARTED.md；
3. docs/collaboration/STATUS.md；
4. docs/collaboration/agents/codex.md 与 argon4.md；
5. 将修改目录下的 AGENTS.md。

先执行只读 Git/环境检查，确认实际分支、HEAD 和既有改动；当前正式功能版本为
main 的 1.12.0，弹幕控制与生成模板已经从 Beta 合入。历史测试版基线为 88853a8。
projects 下的脏文件是已有用户资料，不要还原或作为默认测试夹具。

开始编辑前，把本轮目标、实际基线、占用文件、接口约定和验收方式写入你自己的
docs/collaboration/agents/argon4.md，检查 Codex 是否正在占用相关文件。
没有冲突就继续实现，不需要等 Codex 再重复讲一遍历史。对 C# 预览与 GML 生成
有语义影响的修改，要一起负责两端及对照验证。

本轮如果没有另行指定任务，优先接 STATUS 中未占用的 A1：把 GM 隔离编译与
数值对照整理为正式、可重复的工具入口。先阅读本机 build 中的旧脚本，不批量执行
一次性脚本；新工具不修改用户工程，不顺带重构产品物理或 UI。

完成后更新你自己的记录，给出真实运行过的命令、结果、改动文件、本地提交及
需要 Codex 集成的事项。不要修改 Codex 的任务结果。默认只本地开发和构建，
没有用户明确要求时不 git push、不推版本标签、不触发 GitHub 发布。
```

## 能对齐什么

根规则统一产品方向、数据语义与 Git 权限；子目录规则给实现入口；STATUS 记录功能基线和验证边界；双方记录解决任务与文件占用。文件交接不等于跨应用的实时消息通道，新的用户要求和阻塞需要明确同步。

规则加载依据：[Google Antigravity 官方 Rules 文档](https://antigravity.google/docs/rules/)。
