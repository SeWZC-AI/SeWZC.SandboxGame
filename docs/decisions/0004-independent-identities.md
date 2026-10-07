# ADR-0004：种族、文化与国家独立建模

- 状态：采用
- 记录日期：2026-10-01
- 来源：用户明确要求文化、种族与国家是独立概念且可以相互影响；文化参数、接触累计方式和当前编辑接口属于工程选择。

## 背景

幻想种族可以有不同生理与能力，但不能因此自动等同于固定文化或永久敌对国家。迁居、多种族国家和思想传播都需要分别表达身份与归属。

## 决定与约束

- 种族描述生理、寿命、栖息或能力差异；文化描述认同和价值；国家描述政治归属与组织。
- 居民分别保存种族、文化和国家关系，聚落与国家也有自己的文化认同，不能用其中一个字段代替其余概念。
- 国家可以包含多个种族；在已有国家领土投放不同种族居民，不会仅因种族不同自动创建敌对国家。
- 文化通过实际接触与社会机制影响角色；改变国家文化不瞬间改写所有居民的认同。
- 当前文化价值与接触传播是基础模型，不把它宣称为已完成的传统、宗教或完整文明史系统。

## 原因与代价

独立建模为迁居、同化、交流和制度变化留下真实空间，也使种族能力差异与政治阵营不再被错误绑定。

代价是更多实体引用、编辑校验和界面概念。观察面板必须清楚区分一个角色的种族、文化与国籍，不能用同一个名称或颜色含混展示三者。

“一个种族对应一个国家和文化”的备选更省字段，但直接限制已确认的混合社会行为。当前少量文化价值是有边界的实现选择，未来可以扩展，不代表所有文化差异都能用这些数值解释。

## 重新评估条件

新增宗教、混合文化、多重身份或更复杂迁居规则时，重新审视文化数据结构与传播方式；扩展种族能力时继续区分固有能力与后天文化影响。

## 实现与验证

- [基础世界模型](../../src/SeWZC.WorldBox.Core/WorldState.cs)、[居民身份](../../src/SeWZC.WorldBox.Core/Resident.cs)、[居民认知](../../src/SeWZC.WorldBox.Core/AgentState.cs)、[社会状态](../../src/SeWZC.WorldBox.Core/SocietyState.cs)。
- [社会行为测试](https://github.com/SeWZC-AI/SeWZC.SandboxGame/blob/133b01f06f48a5da368976a2642aef75e20d12eb/tests/SeWZC.WorldBox.Core.Tests/SocietyBehaviorTests.cs)中的 `CulturalExchange` 验证接触改变文化时种族与国籍保持独立。
- [核心场景](https://github.com/SeWZC-AI/SeWZC.SandboxGame/blob/133b01f06f48a5da368976a2642aef75e20d12eb/tests/SeWZC.WorldBox.Core.Tests/Program.cs)中的 `SpawnOnOwnedLand` 与[浏览器编辑检查](https://github.com/SeWZC-AI/SeWZC.SandboxGame/blob/133b01f06f48a5da368976a2642aef75e20d12eb/tests/browser/smoke.cjs)验证混合种族归属和国家文化编辑边界。

相关文档：[产品约定](../product.md) · [决策索引](README.md)。
