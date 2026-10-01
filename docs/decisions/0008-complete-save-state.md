# ADR-0008：存档保存完整因果状态及合法零值

- 状态：采用
- 记录日期：2026-10-01
- 来源：当前格式可靠恢复的产品要求；完整状态、源生成 JSON 与零值处理是工程选择及实际缺陷修复经验。

## 背景

世界可在移动、运输、传讯、建设或研究途中保存。只保存最终库存和实体位置会丢失后续演化所需状态。曾经省略 JSON 默认值后，合法的 `0`／`false` 在恢复时被 C# 属性初始化值替代，例如魔力零值恢复成初始魔力。

## 决定与约束

保存版本、种子、随机状态、世界时间、实体 ID 与关系，以及所有会影响后续演化的中间状态。采用源生成序列化以适配 WASM 裁剪，保留完整属性值；不得无条件启用 `WhenWritingDefault` 压缩存档。

当前格式须满足完整往返和中途保存续演：相同程序版本、状态和操作序列产生相同结果。新增字段同步处理初始化、序列化、校验和恢复；非法输入拒绝时保持原世界。确定性不承诺跨硬件、框架或程序版本的浮点结果完全一致。

## 原因与代价

显式保存较大，但可避免“能够导入”却已悄悄改变世界。省略字段或只存事件的方案需要额外定义缺省、重放和版本语义；不能只为缩小文件而引入不明确的恢复行为。

## 重新评估条件

实际存档体积或写入耗时成为瓶颈时可设计压缩或新格式，先定义缺失字段语义，再通过零值、完整往返和途中续演验证。alpha 可以改变格式，不能免除当前格式正确性。

## 实现与验证

- [序列化上下文](../../src/SeWZC.WorldBox.Core/WorldJsonContext.cs)、[持久化](../../src/SeWZC.WorldBox.Core/WorldEngine.Persistence.cs)、[当前模型校验](../../src/SeWZC.WorldBox.Core/WorldEngine.ValidationV2.cs)。
- [基础保存续演](../../tests/SeWZC.WorldBox.Core.Tests/Program.cs)、[编辑零值回归](../../tests/SeWZC.WorldBox.Core.Tests/EditorAndMigrationTests.cs)、[发展途中续演](../../tests/SeWZC.WorldBox.Core.Tests/SocietyBehaviorTests.cs)。
- [浏览器完整导入导出](../../tests/browser/smoke.cjs) 核对真实文件和存储，而非只比较测试对象。

相关：[产品约定](../product.md) · [alpha 与容量边界](0009-alpha-and-budgets.md) · [决策索引](README.md)
