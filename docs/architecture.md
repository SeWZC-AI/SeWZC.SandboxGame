# SeWZC.WorldBox 架构

本页描述当前状态、模块与工程边界。玩家行为见 [产品约定](product.md)，修改规则见 [开发约定](development.md)，取舍见 [决策记录](decisions/README.md)。版本和数值须与链接中的实现一起更新，不保留旧格式分节。

## 依赖与状态流

```mermaid
flowchart TD
    Browser[Browser: 浏览器启动和存储] --> UI[Avalonia 共享界面]
    Desktop[Desktop: 桌面启动和存储] --> UI
    UI --> Core[Core: 状态、模拟、命令、序列化]
    UI --> Storage[IWorldStorage]
    Browser -. 实现 .-> Storage
    Desktop -. 实现 .-> Storage
    CoreTests[Core.Tests xUnit 单元测试] --> Core
    UITests[UI.Tests xUnit 逻辑测试] --> UI
```

Core 不引用 Avalonia、平台 API 或文件系统。主界面采用 MVVM：`MainViewModel` 持有引擎引用、时间偏好、选择、面板与通知状态及 HUD 命令；`InspectorViewModel` 管理详情列表、关注、研究和折叠状态；`WorldEditorViewModel` 管理编辑草稿与提交命令；`ResearchGraphViewModel` 管理研究节点布局、缩放与滚动状态。`ViewModels/Details` 提供可观察字段，`MainView.axaml`、`Views/GameHud.axaml`、`Views/DetailView.axaml`、`Views/ModalDialog.axaml` 和 `Controls/ResearchGraphControl.axaml` 定义布局及编译模板。`MainView` 接入地图、窗口、导航、输入和界面计时器；`PresentationRequests` 与 `IMapPresentation` 传递展示请求，不将控件交给展示模型。`Services/SimulationScheduler`、`WorldEditCoordinator`、`WorldSaveCoordinator` 分别承担模拟预算、编辑事务和存档协调，平台入口实现 `IWorldStorage`。设计器配置见 [开发指南](development.md#axaml-设计时数据)。浏览器模拟及界面使用单线程 WebAssembly，普通 JavaScript Worker 仅用于保存。原生大群体先按固定顺序结算随机事件、公共补给与取水，再顺序转换整批个人身体的不可变输入，避免每 tick 的线程调度，按居民顺序提交结果。

模拟按完整离散日推进，呈现时钟在真实移动起终点间插值。慢设备限制实际推进速度，不跳过劳动、运输、通信或生存步骤。暂停冻结显示，变速重锚时间，载入和编辑传送重置轨迹；命中、标记、选中圈和跟随共用显示位置。短暂动作通知只说明已执行结果，不保存为世界事实或分配实体 ID。

## 状态归属

| 状态 | 权威来源与边界 |
| --- | --- |
| 世界事实 | [WorldState](../src/SeWZC.WorldBox.Core/WorldState.cs) 的固定地格、稳定 ID 实体、时间和随机状态 |
| 居民认知 | [AgentState](../src/SeWZC.WorldBox.Core/AgentState.cs) 的目标、记忆、人格、已接受的工作地点及经历；信息快照不可变，传播时保留来源及时效 |
| 制度材料 | [SocietyState](../src/SeWZC.WorldBox.Core/SocietyState.cs) 的 `Reports`；不读取未传达的全国居民思想 |
| 资源 | 聚落 `Resources`、个人 `Inventory` 及军队补给分别记账；国家总量是聚合展示 |
| 身份与归属 | 种族、文化、国家及聚落各自稳定 ID；转移通过统一命令维护首都和关联实体 |
| 领地与等级 | `Tile.ClaimedSettlementId` 和 `NationId` 登记，中心是连通根；`Settlement.Tier` 保存村镇城，建筑 `Level` 独立 |
| 研究与规则 | [Advancement](../src/SeWZC.WorldBox.Core/Advancement.cs) 及其 [目录](../src/SeWZC.WorldBox.Core/Advancement.Catalog.cs) 统一知识图、费用和解锁，前置直接引用研究对象；[ResearchRules](../src/SeWZC.WorldBox.Core/ResearchRules.cs) 负责路线与解锁索引，[ProductionRules](../src/SeWZC.WorldBox.Core/ProductionRules.cs) 定义生产配方 |
| 文明结果 | `GetCivilizationProgress` 从知识、可运作设施和首批生产派生，不作为研究或奖励保存 |
| 战争与故事 | 军队现场状态、机构已收军事报告、独立事件和真实经历分开；历史引用允许记录淘汰但不指向未来或形成循环 |
| UI 会话 | 镜头、选择、跟随、关注、折叠、导航、研究视野和短暂效果不进入模拟存档 |

实体、枚举和跨文件结果类型放同名文件，机制使用分部文件。状态转换从不可变输入产生新值，由调用方显式提交；共享规则、费用和研究前置保持只读，显示标识不参与业务判断。

- **世界快照**：引擎持有当前时间、随机序列和编号，捕获时冻结实体集合并组合世界根；`WorldEngine.State` 返回可独立保留的快照。撤销保留旧快照，恢复时校验并重建派生索引。
- **集合更新**：`EntityStore` 维护稳定定位引用及局部持久化构建器。批次内触及的分支只复制一次，成员增删立即生效；捕获、整批转换或退出 tick 时冻结发布，后续写入重新复制。定位引用和派生索引不进入存档。
- **居民转换**：身体、移动、身份和认知使用不可变记录，无变化时共享旧值。动作、补给和日结算合并相关字段后一次提交；居民编辑从输入快照计算候选居民及下一编号，校验后提交。
- **库存转换**：`ResourceAmounts` 表示配方，`ResourceStock` 表示库存。粮水直接保存，其他材料共享记录；卸货、补给和运输返回新的个人与仓库库存。连续补给在结算函数内按居民顺序暂存，阶段结束提交。
- **行为入口**：信息议题通过多态对象处理时效、记忆替换和递送；地图工具使用 `MapTool`／`ToolCategory`，研究操作通过 `IResearchActionHandler` 调用界面命令。

## 模块定位

下表文件名相对于 [Core](../src/SeWZC.WorldBox.Core/)、[UI](../src/SeWZC.WorldBox.UI/) 或 UI 的 [Controls](../src/SeWZC.WorldBox.UI/Controls/)。测试组织见 [单元测试说明](../tests/README.md)。

| 机制 | 主要文件 |
| --- | --- |
| 资源与费用 | Core `ResourceStock.cs`、`ResourceAmounts.cs` |
| 生成与种族 | Core `WorldEngine.Geography.cs`、`WorldEngine.WaterTerrain.cs`、`RaceTerrainRules.cs`、`WorldEngine.RacialBuildings.cs` |
| 行动与通信 | Core `WorldEngine.Agents.cs`、`WorldEngine.Communication.cs`、`WorldEngine.WorkQueries.cs` |
| 制度与文化 | Core `WorldEngine.Society.cs`、`SocietyState.cs`、`CultureDefinition.cs` |
| 发展与外交 | Core `WorldEngine.Development.cs`、`WorldEngine.DevelopmentDemand.cs`、`WorldEngine.Evolution.cs` |
| 城镇与施工 | Core `WorldEngine.Towns.cs`、`WorldEngine.Claims.cs`、`WorldEngine.BuildingSites.cs`、`WorldEngine.Upgrades.cs`、`WorldEngine.Workforce.cs` |
| 地块与运输 | Core `WorldEngine.Land.cs`、`WorldEngine.Transport.cs`、`WorldEngine.Provisioning.cs`，`Tile.Land.cs`、`Tile.Provisioning.cs` |
| 生态与养殖 | Core `AnimalRules.cs`、`PlantCoverage.cs`、`WorldEngine.Ecology.cs`、`WorldEngine.Husbandry.cs`、`WorldEngine.SustainableHarvest.cs` |
| 生产与研究玩法 | Core `Advancement.Catalog.cs`、`ProductionRules.cs`、`WorldEngine.Advancement.cs`、`WorldEngine.ResearchGameplay.cs`、`WorldEngine.KnowledgeQueries.cs` |
| 战争与灾害 | Core `WorldEngine.Warfare.cs`、`WorldEngine.Campaigns.cs`、`WorldEngine.Conflicts.cs`、`WorldEngine.Fire.cs`、`WorldEngine.Mortality.cs` |
| 观察与故事 | Core `WorldEngine.Inspection.cs`、`WorldEngine.EffectsInfo.cs`、`WorldEngine.TaskIcons.cs`、`WorldEngine.Presentation.cs`、`WorldEngine.Stories.cs`；UI `ViewModels/InspectorViewModel.Stories.cs` |
| 编辑与存档 | Core `WorldEngine.Commands.cs`、`WorldEngine.ResidentEditing.cs`、`WorldEngine.NationEditing.cs`、`WorldEngine.Persistence.cs`、`WorldEngine.ValidationV2.cs`、`WorldJsonContext.cs` |
| 详情与异步提交 | UI `ViewModels/InspectorViewModel.*.cs`、`ViewModels/WorldEditorViewModel.*.cs`、`Services/WorldEditCoordinator.cs`；视图 `MainView.Navigation.cs`、`MainView.AsyncEdits.cs`、`MainView.Inputs.cs` |
| 科技树 | UI `ViewModels/InspectorViewModel.Research.cs`、`ViewModels/ResearchGraphViewModel.cs`、`ViewModels/WorldEditorViewModel.ResearchActions.cs`；Controls `ResearchTreeLayout.cs`、`ResearchGraphControl.axaml`、`ResearchGraphControl.axaml.cs` |
| 地图与输入 | Controls `WorldMapControl.*.cs`、`EntityMotionTrack.cs`；Browser `wwwroot/text-input.js`、`touch-gestures.js` |
| 平台保存 | [BrowserWorldStorage](../src/SeWZC.WorldBox.Browser/BrowserWorldStorage.cs)、`storage.js`、`storage-worker.js`；[DesktopWorldStorage](../src/SeWZC.WorldBox.Desktop/DesktopWorldStorage.cs) |
| 发布与 CI | [发布脚本](../scripts/publish-browser.sh)、[静态检查](../scripts/check-static-site.py)、[工作流](../.github/workflows/build-and-deploy.yml) |

## 派生缓存与预算

缓存不进入存档，不授予知识，也不消费随机数；载入、更换网格或相关输入变更后重新推导。新增状态修改路径须核对下列失效责任。

- **居民阶段**：工作分组、知识位索引、报告去重、文化接触位置、工位预约及生产预算只服务当前逻辑阶段，退出时清空引用；发展预留在本阶段首次实际查询时评估，后续取料仍核对实时库存；阶段内死亡、迁居、目标或知识变化同步维护。外部命令读取权威集合，不依赖上一步缓存。
- **通信与搜索**：复用地格人数、连续居民索引、候选和投递容器，按邻格人数直接定位接收者，保持消息顺序及独立事实副本；附近居民分区在位置不变的社会阶段复用于施法，阶段外、成员变化或通道倒塌后读取实时位置，退出模拟步清除引用。大群体按已保存的居民顺序错峰普通交谈，每 tick 约启动 32 次；普通观察以至少三十二 tick的周期错峰安排，大群体每 tick 约安排 32 人；非军队居民所在格起火时立即观察；持续缺粮或严重缺水的目标复评对齐个人四 tick 错峰，首次对齐间隔三至六 tick，初次紧急判断仍即时执行，正在有效采食或到充足水源补水时继续当前劳动，现场枯竭与新危险仍提前复评；消息中的事实合并记忆后提交居民状态。采集选择附近可达来源，取水比较实际供水、距离和自身记忆；供水和采集仍核对实际存量。居民局部选路复用有界优先队列，比较真实通行耗时；目标保存来自六格视野的至多六步短路线，后续抵达时逐步核对通行与火情，受阻、位置或交通方式改变后重新规划；单次搜索复用进入成本，以可行路线的成本上界剪枝，视野内目标按距离下界引导搜索；`AgentState.FamiliarTiles` 保存最近实际到访的有限地格，只影响本人偏好，不进入共享可达性缓存。
- **通行与领地**：`TerritoryCounts` 增量维护绑定地格的归属和通行修订及已登记地格；单个对象替换不能绕过通知。连通修复在归属未变时跳过，变化后仅检查已登记地格，导入独立校验。局部可达性按起点、种族、交通方式和通行修订缓存，日内道路完工、火情边界等立即失效，缓存有固定槽位而非随居民 ID 无界增长。
- **生态**：每 tick 最多复评 128 格，完整周期为 `max(6, ceil(地块数 / 128))` tick，植物每格每年错峰复评。区域从保存 tick 序推导，无待提交游标。当前区和相邻边界使用共同初态计算，捕食与迁移消耗真实存量。
- **容量与物种**：静态栖息容量按地形、植被、饱和资源、肥力、水、耕作、占地和灾害输入失效；实际猎物限制用当天快照，不能当作静态容量缓存。可食动物取实际数量至少 0.05 的候选中生物量最大者，种群变化立即失效，种群值位于 [Tile.Ecology.cs](../src/SeWZC.WorldBox.Core/Tile.Ecology.cs)，引擎按一次地格转换统一分类失效输入，独立的生态查询缓存只接收不可变地格，集合通知维护领地与通行索引。
- **绘图**：居民按快照建立空间分区，只查询可见区域和移动区段；过密人物按屏幕小格合并，命中仍保留全部居民。地格屏幕边长达到 32／48／64 像素时分别启用人物外观、生态图标和任务及建筑名称。地形块保留像素及图像输入，变化时重画脏格与邻格；聚落、水源、肥力和灾害高亮在全图复用分块位图，放大后填充可见地格。缓存按不可变输入失效，在实际绘制时合并刷新；分块只保留修订号，避免持有多日世界快照。动物容量和六类代表物种一次批算，生态指令按精确输入复用。高建筑、居民和舟船用复用列表按插值后底部 Y 排序，`BuildingBounds` 共用图形和命中边界。
- **调度**：1／2／5 倍分别使用 12／48／64 ms 软预算，每回调最多四个完整 tick，五倍最多八 tick 欠账；“不限制”不等待 tick 截止时间、不限制批次步数，使用 64 ms 软预算后让出界面线程并尽快继续。动画总览 15 Hz、普通近景 30 Hz、五倍及不限速近景 20 Hz。软预算在完整模拟步结束后检查。

记录容量由模型与校验器共同约束。当前世界事件最多 400 条，居民记忆 16 条、决策 6 条、经历 24 条，亡者档案 256 位；按意义淘汰并保留历史引用。调整需同时核对保存大小和浏览器负载。

## 编辑与存档

### 当前格式

存档格式 **24**、模拟版本 **29**，权威定义见 [WorldState](../src/SeWZC.WorldBox.Core/WorldState.cs)，接受条件见 [持久化校验](../src/SeWZC.WorldBox.Core/WorldEngine.Persistence.cs)。alpha 拒绝旧版及未知格式；存档格式或模拟版本不匹配时需新建世界。

JSON 使用源生成上下文，中文直接写入 UTF-8，仍转义 HTML 敏感字符；保存种子、随机状态、时间、稳定 ID、关系及所有影响未来的途中状态。研究保存为 `Advancement.Id`，读取时恢复目录中的共享对象，不保存整份规则图。字段省略须保持缺失时的语义；非零默认值和必需字段显式保存。存档中的居民信息按固定 18 字段数组保存，完整保留内容、来源、时刻、可信度及军事关联，编辑 JSON 仍使用字段名。附加动物按物种编号／精确数量交替保存，拒绝重复、未知、缺配对或非法数量。

导入先构造并完整校验候选状态，再替换世界，检查尺寸、容量、枚举、有限数值及实体关系。未压缩 UTF-8 上限 **64 MiB**，压缩读取也核对实际解压字节与申报大小。保存续演比较使用同一程序版本、初态、操作序列及运行环境。

### 编辑与保存协调

`MainView.Navigation.cs` 保存最近 32 处 UI 历史，`WorldEditCoordinator` 将提交绑定原世界、窗口世代和导航会话，`MainView.AsyncEdits.cs` 仅适配控件锁定与地图工具有效性。对象跳转先记录当前位置；窗口和地图取点仅临时停表。恢复点准备期间锁定原输入并可取消，失效任务不得修改新世界或关闭新窗口。

有效编辑先取得整轮世界快照，再提交并保持暂停；命令部分成功后报错仍保留撤销点，完全未变的失败恢复原运行偏好。继续模拟清除这一轮恢复点，历史记录编辑不回算客观世界。

`WorldSaveCoordinator` 串行协调保存、读取和导出，记录世界引用、tick 与编辑修订，判断世界是否需要重新保存。`ExportJsonChunksAsync` 用 16 KiB 序列化缓冲与完整 UTF-8 码点产生有界文本块，前台约每 4 ms 让出执行权。捕获时固定世界 tick 序，镜头可用；编辑、撤销或换世界在修改前取消捕获并保留旧存档。自动保存不拼接整块字符串，文件导出仍有最终拼接开销。

UI 每 30 秒检查自动保存，世界实例、tick 序及编辑修订都未变时跳过；手动保存仍执行，失败保留待保存状态。编辑结束至少留 5 秒空闲后再捕获。捕获完毕恢复模拟，压缩与落盘期间继续，不追赶捕获停留或后台离线时间；完整写入成功才更新保存标记。

### 平台保存

浏览器逐块传输，用 `scheduler.yield`／MessageChannel 让出事件循环。`storage-worker.js` 压缩 Blob 并事务写入 IndexedDB，入口提供已解析的模块 URL。无 Worker 时在主线程写入，无压缩 API 时保存未压缩 Blob。读取校验 Blob 记录、流式解压并严格验证 UTF-8，失败不提交不完整捕获。

桌面后台检查大小、gzip 压缩、写同目录临时文件并原子替换。实际路径和玩家备份操作见 [操作与存档](gameplay.md#存档与兼容性)，核心取消测试与平台验收边界见 [单元测试说明](../tests/README.md)。

## 静态交付

[Browser 项目](../src/SeWZC.WorldBox.Browser/SeWZC.WorldBox.Browser.csproj) 的发布 `wwwroot` 是完整静态站点；脚本复制到 `artifacts/site`，生成提交标记、添加 `.nojekyll` 并检查资源和 WASM。自有资源用相对 URL，支持根目录和仓库子路径。

CI 范围、Pages 设置和浏览器检查见 [部署指南](deployment.md#发布到-github-pages)。
