# Spherewright


## Fork 授权与来源

本 ka0n fork 只集成固定的正式发行版。上游 Lab 规则、安装候选证据及作者个人批准不授予本 fork 权限。当前 fork 来源保留于 docs/current-status.md，执行及部署由 Steering 决定。审计窗口默认为十次 accepted，扩大窗口需 Steering 单独授权。

公共丰富读取与普通写入有独立且默认关闭的门。精确当前非受管会话的公共丰富读取要求 `Safety.AllowUnownedRichReads=true`。普通非战斗动作要求同时启用 `Safety.AllowWrites=true` 与 `Safety.AllowUnownedNormalWrites=true`，不依赖读取门；`OwnedBySpherewright=false` 不变。Peaceful、Passive 及黑雾侵略性（包括未知遥测）都不是该类写入授权门。禁止战斗自动化。私有动作 reader 不扩大公共读取。同星系远端读取要求公共读取权限、已驻留工厂及精确身份；普通动作仍限当前星球。

授权的非受管 Save 只用当前原生存档身份，不接管、不改名、不创建受管副本或恢复票据。非受管飞行不创建受管 FlightCheckpoint；有限蓝图进度只属于当前会话和进程。公共蓝图检查与导出、Journal、Governor、Import、受管 Save/Resume 及 checkpoint reload 保留独立的所有权和来源门。导入仍须 prepare 披露之后的明确确认。新世界保持单人、和平、非沙盒、1 倍。

Passive-Drift 证据跨两个独立 reader 共享，绑定精确世界、会话、星球、原 hash 与原到期时间，内部读取不续期。蓝图 v4 只接受 EOF 或精确尾序列 `04 00 00 00 00`，其他后缀拒绝。原生校验、健康与隔离、幂等、单飞、终态核销及恢复门均不放宽。

[English](README.md) · [简体中文](README.zh-CN.md)

Spherewright 通过配套 MCP 服务和 BepInEx Plugin，把外部 Agent 接入《戴森球计划》。结构化观察、规划和有界原生动作保留正常材料、科研、供电、施工无人机及游戏时间约束。产品不内置 LLM 或自主扩产循环。

Plugin、MCP、Contracts 和 Bridge.Core 保持为强版本耦合的 monorepo。[Spherewright-Lab](https://github.com/Arcueid77/Spherewright-Lab) 维护场景、运行交接和完整证据；产品独立构建、测试和打包。

## 版本与范围

本 fork 集成正式 [v0.4.0 发行标签](https://github.com/Arcueid77/Spherewright/releases/tag/v0.4.0)，供 Steering 独立审查。见 [fork 集成报告](docs/validation/fork-v040-integration.zh-CN.md)。上游包调用候选为 `e018b7f45c70f5fe029418cad987af794b5cb9bf`，冻结生产验收候选为 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`。下文上游实机结果保留原源码与会话绑定，不代表 fork 验收或部署权限。

- **Overseer**：有界生产、能源、物流及科研观察，上游追踪与诊断。建议不授予写入权限。
- **Foundry**：多级配方、材料、设备、基础功率及有界场地和依赖计划，明确外部供给。预览与执行保持独立。
- **有限动作**：原生施工、配置、受支持升级、取材与有限蓝图，保留逐对象进度、取消及 fresh 续建。
- **Governor**：执行前生产声明与实际游戏 tick 观察。库存、额定容量和短样本不能证明持续生产。
- **存档连续性**：owned、票据绑定的精确主档恢复，保持 durable Journal 连续性；已披露迁移另需后续确认。不提供任意选档或编辑器。

冻结的 29145 候选已通过规定的三级链、连续联合供给、准备套件、保存恢复及两种本机包布局检查。[验收](docs/validation/v0.4.0.zh-CN.md)记录其准确 hash、范围与限制。这些生产结果保留原源码及 session 范围。实际跨恒星航行和物流仍属后续工作。

## 安装与使用

冻结生产验收使用 Windows x64、DSP `0.10.35.29145`、BepInEx `5.4.17.0`，一个 owned、和平、非沙盒、1× 的本机世界。当前候选在同一本机环境及 DSP `0.10.35.29151` 完成测试。旧发行版保留各自支持范围。Plugin、MCP 和共享程序集必须配套。

当前两种包布局各自完整通过已安装 MCP 的 64 工具调用，分别记录数据/预检成功与预期原生安全拒绝，矩阵内无 accepted 或 unknown 动作。所有者明确确认的原世界迁移及之后的同版本恢复保留持久 Journal 与已完成历史。详见[固定实机报告](https://github.com/Arcueid77/Spherewright-Lab/blob/00f4359eef704bc7cd5c148438c05e0d3121bda6/operations/package-callability-2026-10-17.md)及[原生验证](docs/validation/native-29151-preflight.zh-CN.md)。本阶段不增加新的持续生产验收。

- [安装](docs/release-installation.zh-CN.md)与[中断安装恢复](docs/installation-recovery.zh-CN.md)
- [Agent 指南](docs/agent-playbook.zh-CN.md)，英文版也嵌入 MCP 并随包提供
- [协议](docs/protocol.zh-CN.md)、[安全模型](docs/safety-model.zh-CN.md)和[架构](docs/architecture.zh-CN.md)
- [发行说明](docs/release-notes/v0.4.0.zh-CN.md)、[变更记录](CHANGELOG.zh-CN.md)和[全部文档](docs/README.zh-CN.md)

写入要求 fresh 原生 prepare、计划绑定的 commit、原 action 核销及相关读回。结果未知时进入隔离。物品或科技注入、瞬间施工、传送及直接修改游戏缓冲不属于公开动作面。黑雾战斗、多人/Nebula、广泛第三方兼容及另一台计算机使用尚未验证。

## 开发

```powershell
dotnet restore Spherewright.Core.slnf --locked-mode
dotnet build Spherewright.Core.slnf -c Release --no-restore
dotnet test Spherewright.Core.slnf -c Release --no-build
```

完整 Plugin 构建需要合法安装的 DSP/BepInEx 引用，见[环境说明](docs/research/environment.zh-CN.md)。打包脚本记录本仓自身源码 SHA 与 clean 状态，不拉取或执行 Lab。

场景工作从 Lab 开始；固定产品子模块默认只读。通用修复在独立 clone/worktree 实现、测试、提交并取得准确 SHA 的成功 CI 后，Lab 才更新 gitlink/锁。另见[贡献政策](CONTRIBUTING.zh-CN.md)、[协作规则](AGENTS.zh-CN.md)、[产品状态](docs/project-status.zh-CN.md)和[Lab 设置](docs/sample-validation.zh-CN.md)。

使用 [MIT 许可](LICENSE)，另有[非权威中文译文](LICENSE.zh-CN.md)。
