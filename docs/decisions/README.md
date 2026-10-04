# 设计决策记录

设计决策记录（ADR）解释长期行为与跨模块取舍的原因。功能清单见 [产品约定](../product.md)，实施流程见 [开发约定](../development.md)，模块关系见 [架构说明](../architecture.md)。

这些记录于 2026-10-01 从已确认需求和 `ba395b9` 实现整理而成，日期是记录日期。每篇区分用户要求与工程选择，不代表每个实现细节都经过独立问答确认。

| 编号 | 决策 | 状态 |
| --- | --- | --- |
| [0001](0001-static-avalonia.md) | Avalonia 共用界面与 Pages 静态交付 | 采用 |
| [0002](0002-local-knowledge.md) | 居民使用本地认知，信息与货物实际传递 | 采用 |
| [0003](0003-delivered-governance.md) | 制度根据收到的报告和议题权重决策 | 采用 |
| [0004](0004-independent-identities.md) | 种族、文化、国家分别建模 | 采用 |
| [0005](0005-forward-only-editing.md) | 编辑个人历史不重演世界过去 | 采用 |
| [0006](0006-stable-controls.md) | 核心操作位置稳定，内容在固定区域切换 | 被 0011 替代 |
| [0007](0007-presentation-clock.md) | 离散模拟与连续呈现分开 | 采用 |
| [0008](0008-complete-save-state.md) | 存档保存完整因果状态及合法零值 | 采用 |
| [0009](0009-alpha-and-budgets.md) | alpha 不迁移旧格式，当前记录与载荷有界 | 采用 |
| [0010](0010-real-browser-verification.md) | 真实浏览器输入、只读测试快照与部署后验证 | 采用 |
| [0011](0011-evolution-and-map-tools.md) | 自主文明与可收起的地图工具 | 采用 |
| [0012](0012-campaigns-and-stories.md) | 有限战争、实际战报与只读故事观察 | 采用 |
| [0013](0013-independent-advancement.md) | 科技与魔法并行发展，保持各自的生产基础 | 采用 |
| [0014](0014-land-and-transport.md) | 地块改造、阶段资源与基础载具运输 | 采用 |
| [0015](0015-ecology-and-escalation.md) | 有界动物生态、逐步资源冲突与可操作观察 | 采用 |
| [0016](0016-development-and-refresh.md) | 发展路线、物资预算与按变化刷新 | 采用 |
| [0017](0017-readable-map-and-communities.md) | 可辨识地图、紧凑详情与多物种共存 | 采用 |
| [0018](0018-five-speed-work-budget.md) | 分区生态、增量统计与可响应的保存 | 采用 |
| [0019](0019-local-claims-water-and-upgrades.md) | 实地占地、独立供水与可施工升级 | 采用 |
| [0020](0020-survival-and-gradual-disasters.md) | 可达的生存补给与逐步发展的灾害 | 采用 |
| [0021](0021-town-expansion-and-infrastructure.md) | 付费城镇扩充、地理设施与有界导航 | 采用 |
| [0022](0022-geography-races-and-food-web.md) | 地理、种族适应与食物链 | 采用 |
| [0023](0023-connected-town-work-and-fishing.md) | 连续城镇领地、稳定任务与真实捕鱼 | 采用 |
| [0024](0024-territory-activation-and-simulation-decisions.md) | 城镇生效面积、资源来源与可执行任务 | 采用 |
| [0025](0025-research-trees-and-empire-simulation.md) | 科技树、帝国研究与实物发展预算 | 部分采用；帝国项目被 0028 替代 |
| [0026](0026-connected-research-graph.md) | 有真实依赖连线的科技树 | 采用；混排与外围连线被 0027 替代 |
| [0027](0027-readable-research-branches.md) | 两条帝国路线分别成树，支线独占位置 | 部分采用；帝国汇合被 0028 替代 |
| [0028](0028-civilization-outcomes-and-research-gameplay.md) | 文明结果与研究解锁玩法 | 采用 |

## 何时新增或替代

当一个决定影响产品语义、多个模块、存档、部署约束，或后来的人很可能问“为什么不采用另一个方案”时记录 ADR。普通 bug 修复、文案或局部参数调整用代码、测试及对应现行文档说明即可。

状态使用“提议”“采用”“被替代”；只有实际决定采用的方案才写“采用”。替代时新增连续编号，旧文档标为“被替代”并链接新文档，新文档说明变化原因；不重写历史来伪装始终如此。重新评估条件用于提示何时值得讨论，不是额外审批要求。

## 简短模板

```markdown
# ADR-编号：决定名称

- 状态：提议 / 采用 / 被替代（链接）
- 记录日期：YYYY-MM-DD
- 来源：用户明确的要求；本次工程选择；实现或验证基线

## 背景
要解决什么问题，哪些条件影响选择。

## 决定与约束
选择什么，修改时必须保持什么可观察行为。

## 原因与代价
为什么适合当前阶段；其他方案的适用条件及未选择原因。
区分事后分析与真实讨论，不虚构已评审的备选。

## 重新评估条件
什么新需求或测量结果可能使选择需要改变。

## 实现与验证
链接主要代码和有区分力的测试；结果链接到有提交归属的验证记录。
```

只记录对未来修改有用的理由，避免复制全部代码、产品清单或聊天过程。
