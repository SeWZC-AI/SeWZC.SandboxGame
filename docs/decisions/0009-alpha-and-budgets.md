# ADR-0009：alpha 不迁移旧格式，当前记录与载荷有界

- 状态：采用
- 记录日期：2026-10-01
- 来源：用户明确“alpha 不考虑兼容性”；版本号、记录容量和载荷上限是当前工程选择。

## 背景

早期模拟结构还在快速变化，维持旧接口及旧世界迁移会拖累机制重构。浏览器内存与存储有限，无界事件和个体历史又会使长时间运行逐渐失控。

## 决定与约束

alpha 不以旧内部接口或布局兼容为约束，不提供旧存档迁移；无需仅因接口或布局已经存在就主动删除或重写。当前接受的格式与模拟版本统一见[架构文档](../architecture.md#编辑与存档)；旧版、未知版明确拒绝，玩家可新建世界。当前格式仍需校验、保存恢复和失败保护。

记录有明确预算，世界日志优先保留重要事件，个体记忆、决策、经历和亡者档案也会淘汰。当前上限集中说明于 [架构文档](../architecture.md)，以模型与校验器为实现依据，不在每份文档重复数值；导入导出载荷同样受预算限制。

## 原因与代价

迁移兼容和无限历史各有用途，但现阶段会增加测试范围、内存、载荷和维护成本。代价是旧世界可能必须重建，早期经历可能被淘汰；界面和功能介绍不能承诺永久完整的人生档案。

## 重新评估条件

进入稳定阶段、明确要求保留长期世界，或实测负载改变时重新评估。扩大容量时同步模型、校验和存储限制，并测量实际 UTF-8 存档大小与浏览器负载。

## 实现与验证

- [世界模型](../../src/SeWZC.WorldBox.Core/WorldState.cs)、[认知模型](../../src/SeWZC.WorldBox.Core/AgentState.cs)、[社会模型](../../src/SeWZC.WorldBox.Core/SocietyState.cs)、[存档校验](../../src/SeWZC.WorldBox.Core/WorldEngine.Persistence.cs)。
- [异常存档检查](https://github.com/SeWZC-AI/SeWZC.SandboxGame/blob/133b01f06f48a5da368976a2642aef75e20d12eb/tests/SeWZC.WorldBox.Core.Tests/Program.cs)、[浏览器旧版与未知版拒绝](https://github.com/SeWZC-AI/SeWZC.SandboxGame/blob/133b01f06f48a5da368976a2642aef75e20d12eb/tests/browser/smoke.cjs)、[压力检查](https://github.com/SeWZC-AI/SeWZC.SandboxGame/blob/133b01f06f48a5da368976a2642aef75e20d12eb/tests/browser/stress.cjs)。
- 容量与性能的已测结果、适用环境和限制见 [验证记录](../verification.md)，不能由短时通过推断无限运行。

相关：[产品约定](../product.md) · [完整存档状态](0008-complete-save-state.md) · [决策索引](README.md)
