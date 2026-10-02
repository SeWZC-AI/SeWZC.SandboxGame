# 开发与 AI 协作约定

本指南说明如何接续实现，适用于人工和 AI。产品目标见 [产品约定](product.md)，模块关系见 [架构说明](architecture.md)，重要取舍见 [设计决策](decisions/README.md)。这些约定用于减少重复排查与行为倒退，可在有新需求或证据时更新。

## 从一个可验证的改动开始

1. 查看分支、未提交修改和相关文档，使用 `rg` 定位实现及测试；保留他人工作。
2. 写清这次要改变的玩家可观察行为，区分已确认要求、实现假设与未来方向。会影响产品方向的缺失信息用交互式询问工具询问，同时继续不依赖答案的工作；已有明确授权不重复询问。
3. 找到权威状态、命令入口、呈现与存储路径。新增机制需要同时说明它如何影响其他系统、玩家在哪里观察、如何保存恢复。
4. 修改实际机制，再核对解释文字和界面；不能仅让理由文本或统计数字看起来符合要求。
5. 按下表验证并同步文档。报告实际完成的检查、未覆盖范围和阻塞，不把“编译成功”写成“功能已验证”。

小型、可逆的文案或布局修改不需要机械增加测试。修复跨状态边界的缺陷时，优先增加能复现真实失败的回归场景，避免逐行复制实现算法的测试。

## 主分支与环境迁移

`main` 是唯一长期主分支及 Pages 发布分支；alpha 是当前产品阶段，应用版本由 [Directory.Build.props](../Directory.Build.props) 统一设置，仍不提供旧存档或内部接口兼容。切换分支不代表进入稳定阶段。

从 `alpha` 迁移时，先将其全部提交保留在 `main`，再完成以下仓库与环境设置：

1. 在仓库 **Settings → General → Default branch** 将默认分支设为 `main`。
2. 在 **Settings → Environments → github-pages** 核对部署分支策略：若使用选定分支规则，将 `alpha` 改为 `main`；若仅允许受保护分支，确保 `main` 受保护并允许部署。Pages 的 Source 保持 GitHub Actions。
3. 将开发／云环境的仓库检出分支改为 `main`，不再固定 `alpha`；已有工作区需同步上游分支与远端 HEAD。
4. 确认 `main` 包含全部原 `alpha` 提交、默认分支和环境设置已迁移后，删除远端 `alpha`。本地保存未提交工作后执行 `git fetch origin --prune`、`git switch main`、`git branch --set-upstream-to=origin/main main` 和 `git remote set-head origin -a`；新工作区可用 `git switch --track origin/main` 创建本地分支。确认本地 `alpha` 已完全合入后，用 `git branch -d alpha` 清理。

工作流指定的部署分支与仓库默认分支、GitHub 部署环境策略、开发环境检出分支是独立设置。修改工作流不会自动修改后三者；报告迁移结果时需分别核对。

## 代码定位与责任

下表中的文件名相对于对应目录；同一 `partial` 类按机制分文件，新代码尽量进入已有责任范围。

| 修改内容 | 主要入口 | 相关验证 |
| --- | --- | --- |
| 世界数据、版本和基本编辑 | [Core](../src/SeWZC.WorldBox.Core/) 中 `WorldState*.cs`、`WorldEngine.Commands.cs`、`WorldEngine.NationEditing.cs` | [Program.cs](../tests/SeWZC.WorldBox.Core.Tests/Program.cs) |
| 世界规则、发展与自主外交 | Core 中 `WorldEngine.Evolution.cs` | `EvolutionTests.cs`、`--evolution` 长程探查 |
| 目标、知识与通信 | Core 中 `WorldEngine.Agents.cs`、`WorldEngine.Communication.cs` | [AgentBehaviorTests.cs](../tests/SeWZC.WorldBox.Core.Tests/AgentBehaviorTests.cs) |
| 制度、文化、研究、建设与魔法 | Core 中 `WorldEngine.Society.cs`、`SocietyRules.cs` | [SocietyBehaviorTests.cs](../tests/SeWZC.WorldBox.Core.Tests/SocietyBehaviorTests.cs) |
| 角色编辑、存档校验 | Core 中 `WorldEngine.ResidentEditing.cs`、`WorldEngine.ValidationV2.cs`、`WorldEngine.Persistence.cs`、`WorldJsonContext.cs` | [EditorAndMigrationTests.cs](../tests/SeWZC.WorldBox.Core.Tests/EditorAndMigrationTests.cs)、`Program.cs`；文件名不表示支持旧存档迁移 |
| 界面、工具与表单 | [UI](../src/SeWZC.WorldBox.UI/) 中 `MainView*.cs` | [smoke.cjs](../tests/browser/smoke.cjs)、[mobile-smoke.cjs](../tests/browser/mobile-smoke.cjs) |
| 地图、命中与运动 | [Controls](../src/SeWZC.WorldBox.UI/Controls/) 中 `WorldMapControl*.cs`、`EntityMotionTrack.cs` | [motion.cjs](../tests/browser/motion.cjs)、触屏检查 |
| 平台存储和浏览器生命周期 | [IWorldStorage](../src/SeWZC.WorldBox.UI/Platform/IWorldStorage.cs)、[Browser](../src/SeWZC.WorldBox.Browser/)、[Desktop](../src/SeWZC.WorldBox.Desktop/) | 核心保存恢复、浏览器真实导入导出与刷新恢复 |
| 静态发布和自动化 | [发布脚本](../scripts/publish-browser.sh)、[静态检查](../scripts/check-static-site.py)、[工作流](../.github/workflows/build-and-deploy.yml)、[浏览器驱动](../tests/browser/ui-driver.cjs) | 发布产物子路径检查，以及部署后的实际站点检查 |

`Core` 不依赖 Avalonia、浏览器或桌面文件系统。共享界面通过平台接口访问存储；不要为了一个功能把平台 API 引入模拟。C# 延续现有命名与文件风格，保持 nullable 检查；SDK、语言与依赖版本查看 [global.json](../global.json)、[Directory.Build.props](../Directory.Build.props) 和对应项目配置。

## 修改时要守住的因果关系

### 模拟、知识与资源

- 随机事件使用世界保存的随机状态；不要在核心规则中引入墙钟、渲染帧率或独立的未保存随机源。确定续演的适用边界见 [ADR-0008](decisions/0008-complete-save-state.md)。
- 判断角色选择时使用其实际观察或收到的知识；引擎知道某事实不等于角色知道。消息保留原始来源、当时职业、观察／获知时间及可信度，转述不能刷新成亲眼所见。
- 当前制度报告的权威集合是 `State.Society.Reports`，不要因为旧字段名看起来相关就把它当成正在使用的路径。新增制度逻辑要验证递送、去重和议题权重，而非直接汇总全体居民内心。
- 聚落库存与随身货物分别记账，国家库存是聚合展示。收获、携带、交付、消费和损失应能追溯实际位置与资源变化；玩家直接编辑属于显式干预。
- 使用稳定 ID 维护居民、聚落、国家、文化和消息关系；转移归属应沿用统一命令，避免只修改其中一个对象。

### 编辑、持久化与日志

- 编辑和导入先在候选状态完成校验，通过后整体提交；失败不能污染当前世界。修改历史只影响当前认知及未来，见 [ADR-0005](decisions/0005-forward-only-editing.md)。
- 新增可影响后续演化的状态时，同时处理初始化、序列化、输入校验和中途保存续演；也要检查属性初始化值与合法零值的关系。
- 不将 `WhenWritingDefault` 当作无条件的存档压缩手段。曾出现 `0`／`false` 被省略后恢复为属性初始化值的错误，详细约束见 [ADR-0008](decisions/0008-complete-save-state.md)。
- 日志等级依据事件对玩家的意义，附上准确时间、国家与位置。事件集合按重要性淘汰，新插入项可能不被保留；需要补充元数据时使用 `AddEvent` 返回的事件对象，不通过 `State.Events[^1]` 猜测新事件。
- 记录容量与载荷预算是当前工程选择，调整需核对验证器、保存大小和浏览器负载；alpha 无迁移并不意味着当前格式可以损坏，见 [ADR-0009](decisions/0009-alpha-and-budgets.md)。

### 界面与呈现

- 核心工具区域保持稳定；不可用项用状态表达，避免按动作数量持续重排。周期刷新不能覆盖正在编辑的文本或破坏选择、滚动与提交按钮的可达性。
- 镜头、选择、过滤、跟随及运动插值只操作呈现状态，不推进模拟或消耗世界随机数。暂停、变速、载入与编辑传送的边界见 [ADR-0007](decisions/0007-presentation-clock.md)。
- 界面解释来自真正使用的评分、来源与状态，自由文本不是在线模型指令。新增机制应提供观察入口，不能只增加不可验证的“智能”标签。

## 按改动选择验证

安装与启动命令统一维护在 [项目 README](../README.md#开发环境)，完整发布和浏览器复现步骤见 [验证记录](verification.md#复现命令)。以下是范围选择，不要求每次无关改动都重复全部压力测试。

| 改动范围 | 应执行的检查 |
| --- | --- |
| 仅文档 | 核对描述与实现、相对链接及标题锚点，运行 `git diff --check`；不声称重跑游戏验收 |
| 核心行为、数据或编辑 | Release 构建、可执行核心测试；针对因果关系或失效边界加回归场景 |
| 存档格式或序列化 | 上述检查，加完整往返、合法零值、非法输入不改世界、中途保存续演；涉及平台时验证真实存储 |
| 界面、输入或呈现 | 发布裁剪后的浏览器产物，在仓库子路径运行相关桌面／触屏／运动脚本；涉及渲染回退时复验软件路径 |
| 资源、平台入口或部署 | 静态发布检查、真实浏览器加载和存储；发布后检查实际 Pages URL |
| 性能、记录容量或大世界 | 先通过功能验证，再在独立负载下测量，记录种子、规模、配置、耗时、实际存档字节数与环境 |

核心测试是可执行程序：

```bash
dotnet build -c Release
dotnet run --project tests/SeWZC.WorldBox.Core.Tests -c Release --no-build
dotnet run --project tests/SeWZC.WorldBox.UI.Tests -c Release --no-build
```

`dotnet test` 不能替代它；桌面构建也不能替代经过裁剪的 WASM 发布。测试失败返回非零退出码，先定位失败行为，不为过关删除约束或扩大误差。

`SeWZC.WorldBox.UI.Tests` 使用 Avalonia Headless 运行共享界面的状态与控件事件回归，纳入解决方案和 CI。它适合精确复现对象消亡、编辑未变字段、世界切换等边界；真实浏览器仍负责验证裁剪发布、渲染、触屏与存储。

浏览器验证尤其注意：

- Avalonia 绘制在 canvas 上。`?e2e=1` 的只读快照用于定位实际控件，行为通过鼠标、键盘和触屏完成，再读取实际存档核对；不增加修改世界或推进时间的测试后门。
- 平滑运动要观测**最后实际绘制的位置**在同一模拟 tick 内变化，并验证暂停冻结；只检查公式算出的目标位置无法证明用户看到了动画。
- 触屏平移与捏合既要验证世界不变，也要确认镜头确实移动或缩放。固定布局等待按压动画结束后精确比较，不用宽松像素阈值掩盖持续跳动。
- 软件渲染回退只允许已知、精确匹配的诊断，并检查实际游戏画布有有效像素。不要泛化忽略 `console.error` 或把空白页面当成成功。
- 手机尺寸仿真不是实机，原生基准不是浏览器 FPS；未验证的输入法、浏览器和设备要如实保留边界。

## 并行协作与交付

可并行进行有清晰文件边界的阅读、实现与审查；开始前分配责任，接口变动及时通知其他参与者。共享工作区中，构建、发布、浏览器验收和性能测量由一个协调者安排，避免混合不同版本的文件与产物。

[发布脚本](../scripts/publish-browser.sh) 会清空并重建 `artifacts/browser` 和 `artifacts/site`。发布前结束相关源码修改；同一输出目录不要同时发布与测试。需要稳定预览时，在发布完成后复制到独立预览目录。共用 `obj` 的构建也不要相互竞争。

性能测量不与其他浏览器压力任务争抢资源，功能验证与测量结论分别记录。存档大小测量实际写入的 UTF-8 内容，不能把解析后重新序列化的体积冒充真实载荷。

当前工作流对所有分支与 PR 验证，只有 `main` 非 PR 运行会部署，并在部署后执行公网检查。推送授权沿用当前任务与会话约定；本指南不增加新的授权，也不要求重复确认已有授权。已部署与已通过公网验收是两个状态，报告时分别说明。

## 文档随行为一起维护

功能变化更新 [product.md](product.md) 中的能力与验收；模块变化更新 [architecture.md](architecture.md)；重要取舍新增或替代 [ADR](decisions/README.md)。纯重命名、局部参数或一般修复通常不需要单独写 ADR。

验证记录写明对应提交、命令、环境、结果、证据和未覆盖范围。历史证据保留其归属，不能只更新日期就把旧结果归给新代码。临时计划与排查过程完成后提炼成简短原因，不把整段聊天或短期状态堆进长期文档。
