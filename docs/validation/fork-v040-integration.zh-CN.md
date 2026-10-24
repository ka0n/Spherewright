# Fork v0.4.0 集成审查

[English](fork-v040-integration.md) · [简体中文](fork-v040-integration.zh-CN.md)

2026-10-24，Europe/Berlin。仅源码集成与独立 Steering 审查。无游戏或 Bridge 调用、部署、运行文件替换、存档访问、DSP 生命周期操作、main 更新、发行发布或 v0.5 源码集成。原 fork 实机验收保留旧组件绑定，上游安装及生产证据不授予 fork 权限。

## 固定来源与隔离

| 引用 | 已核值 |
| --- | --- |
| Fork 本地 HEAD 与远端 main | `e3a83ddb9b17764b1e27f1947ffd0bafbd93e586` |
| 正式 v0.4.0 附注标签对象 | `3e6f3b315e3234958be625520cb8fb2a00f5e1e1` |
| 标签解析的发行 commit | `b7c0002dbe2e8f5608c5ca140b0c6326a1818021` |
| 共同祖先 | `8df3b7764c57cae634bab1e9762b4f116f7838ae` |
| 集成分支 | `integration/upstream-v0.4.0-20261024` |

独立 clone 位于忽略的本地目录，拥有独立 Git、index 和构建输出。使用原生 Windows Git 核对 fork 远端及 Arcueid77/Spherewright 正式标签；主工作区的旧 AvaloNero remote 不变。只 fetch 精确标签，未用实验 main。双父 merge 保留祖先，没有 cherry-pick、squash、rebase、改 tag 或强制推送。最终 commit、准确远端 head、Draft PR 和 CI 在提交推送后的 PR 及 Steering 交接中记录。

## 十三个真实冲突

| 文件 | 解决 |
| --- | --- |
| AGENTS.md | 保留 fork Steering、安全、默认十写及部署边界；中文伴页对应现行规则。 |
| README.md | 使用新版产品结构，补 fork 独立权限和来源，纠正过期的未发布声明。 |
| docs/agent-playbook.md | 新恢复与施工说明结合 fork 非受管入口、双 reader、Save 读回及十写窗口；更新伴页。 |
| docs/current-status.md | 保留 fork 来源及旧有有界实机验收，不换成上游 Lab 状态。 |
| docs/evidence/2026-10-04/material-inventory-cuts.md | 保留历史 fork/上游来源，不新增实机授权。 |
| docs/experience-ledger.md | 保留现有 fork 账本。 |
| docs/gameplay-timeline.md | 保留现有 fork 时间线。 |
| docs/incident-fix-log.md | 保留现有 fork 事故记录。 |
| docs/protocol.md | 新协议恢复说明结合 fork 读写及远端会话规则；更新伴页。 |
| docs/release-installation.md | 保留新版双语安装说明，补 fork 范围与独立门。 |
| docs/safety-model.md | 新有界恢复策略结合精确 fork 权限；去除普通动作必须 owned 或由战斗姿态阻止的含义。 |
| scripts/SpherewrightStageTools.ps1 | CI 查询仍为 ka0n/Spherewright，默认审计十写不变。 |
| src/Spherewright.Plugin/Hosting/SpherewrightBridgeHost.cs | 给 BlueprintBuildStore 接入上游主线程 Journal provider，同时保留两个 reader 与 fork import coordinator。 |

上游改为 Lab 跳转的旧证据仍保留 fork 原文。文档检查通过明确清单绑定 UTF-8/LF 内容 SHA-256，修改或缺失失败关闭；保留原语言及迁移前链接语境，隐私、凭据和 fence 检查仍执行。产品文档继续完整双语、值与链接检查。五项离线回归覆盖保留、换行、漂移、私有路径与缺失记录。包内本 fork 精确源码链接指向 ka0n/Spherewright。

自动合并的测试依赖另需两项适配：Plugin.ReadAccess.Tests 链接真实 GameplayJournalIdentity；上游恢复 test double 暴露 fork 会话谓词。未为编译而放宽生产权限。

## Fork 安全证明

这些关键生产文件相对 fork 基线没有变化：SpherewrightConfiguration；GameSessionTracker 及 ReadAccess/NormalActions/MoveInspection；GameStateReader ReadAccess/RemoteFactoryRead/MoveInspection；NormalGameActionCoordinator 及 PrepareAuthorization/MoveInspection/Save；UserSaveImportCoordinator；TestWorldCoordinator；BoundedBlueprintReader。解析器回归文件不变。所有原 ReadAccess 测试正文保留，仅补生产依赖链接。

| 不变量 | 本次证据 |
| --- | --- |
| 独立读写门 | NormalActionAuthorizationTests、IntegrationAuthorityMatrixTests、ReadAccessTests 通过；含四种丰富读取/普通写入组合及 AllowWrites 总门。 |
| 精确非受管普通动作 | 原生产谓词不变；矩阵覆盖战斗世界和可空侵略性遥测，无 Peaceful/Passive 前提或所有权授予。 |
| 公私 reader 分离 | Host 仍创建两个实例，仅 action reader opt-in；write-only 不开放公共玩家/工厂/远端读取。 |
| 同星系驻留远端读取 | RemoteFactoryReadAccessTests 覆盖公共权限、身份、驻留、星系与池边界、本地动作限制；不创建或加载工厂。 |
| Save/Import/Resume | Fork Save/import 不变；精确主档迁移保留披露、digest、确认、租约与消耗；默认恢复同版本，autosave 不迁移。 |
| 无战斗自动化 | 原工具面和普通动作策略保留，未增加攻势动作；遥测不是权限。 |
| 蓝图 v4 | EOF 或精确 `04 00 00 00 00`；截短、修改或加长尾部均拒绝。 |
| Passive Drift | CrossReaderPassiveDriftTests 通过，精确世界/会话/星球/hash/原到期绑定不变，内部不续期。 |
| 新世界默认值 | TestWorldCoordinator 与测试保留单人、和平、非沙盒、1 倍原生创建。 |

新版恢复仅允许已研究的定向链。至 29151 的第五个 durable 转换只经 expired-primary；active-primary evidence 5/6 保留之前两个版本对。已完成蓝图仅沿已证明后缀保留；partial/unknown 拒绝，读取不改原字节。这些能力需要未来单独授权，本次未迁移。

## 构建与自动验证

Windows SDK 8.0.426 / .NET 8.0.32。完整 solution 锁定 restore 与 Release 构建包含真实 net472 Plugin，零警告、零错误。初次构建发现上述三个测试链接编译错误，已修复后通过。

五份编译引用只在 clone 忽略目录。只读复制当前 Assembly-CSharp 作构建输入，SHA-256 为 `EF314425DADE51F529F184F07BE136CD3C2660A6A81515603C7226FBE7AB1D8C`，精确匹配上游 0.10.35.29151。BepInEx 引用 hash 为 `DC1CB6B58B962BDA5AAA1D6B5F9AE14EC174F61836A1A1F96C1A040C7E8381F7`。Unity 引用匹配安装文件。BepInEx 来自现有受保护本地编译集，未重新认证其实际游戏安装状态。DLL 和私有 manifest 不提交。

| 测试套件 | 通过 | 失败 / 跳过 |
| --- | ---: | ---: |
| Contracts | 73 | 0 / 0 |
| Bridge.Core | 2844 | 0 / 0 |
| Plugin.ReadAccess | 145 | 0 / 0 |
| MCP | 259 | 0 / 0 |
| 总计 | **3321** | **0 / 0** |

命令为 `dotnet restore Spherewright.sln --locked-mode`、`dotnet build Spherewright.sln -c Release --no-restore`、`dotnet test Spherewright.Core.slnf -c Release --no-build`，本地保留 TRX。

十四项真实 `pwsh -NoProfile -File` 通过：bridge-client、action-client、stage-tools、belt-site-qualification、evidence-index、evidence-reader、factory-evidence、production-sampling、package-surface-policy、powershell-entrypoint、install-release-preflight、install-transaction、install-recovery、source-mcp。Fixture 无真实游戏调用。文档及五项保留回归通过。MCP 0.4.0.0，64 tools / 一 resource，精确嵌入 playbook，正常退出，无多余 stdout。

手动及 Thunderstore 布局已构建为本地 preview 并检查，preview manifest 明示 sourceDirty=true。提交后的干净源码重建、包元数据、双语 smoke 与远端 CI 以最终 head 记录于 PR。安装 fixture 只证明合成行为，不代表真实安装、中断恢复或异机。所有构建输出被忽略，不是发行产物。

## Steering 与部署边界

准确 head 的 CI 成功后供独立源码审查。原生编译及离线测试不等于实机验收。另行授权冷部署前，必须核真正匹配的 BepInEx/native/runtime 组件、当前会话权限、durable Journal 与未决动作，不能继承上游批准。保留备份并使用既有事务部署和恢复流程。

未来有界实机验收应重查 29151 精确非受管普通动作、同星系远端读取、配置的独立门、Save 身份与读回，以及明确获准的受管恢复/历史场景。正向 Passive Drift 仍是单独实机范围，旧组件的 Steering 验收不重复也不失效。本次不声明新持续生产、恢复、管理器 profile 或异机证据。Draft PR/CI 交接后停止；本文不授权 merge 或 deploy。
