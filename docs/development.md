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
| 有限战争、战报与故事观察 | Core 中 `WorldEngine.Campaigns.cs`、`WorldEngine.Stories.cs`、`WorldState.Stories.cs`；UI 中 `MainView.Stories.cs` | `StoryTests.cs`、`tests/browser/stories.cjs` |
| 世界规则、发展与自主外交 | Core 中 `WorldEngine.Evolution.cs` | `EvolutionTests.cs`、`--evolution` 长程探查 |
| 动物、局部冲突与死亡 | Core 中 `WorldEngine.Ecology.cs`、`WorldEngine.Conflicts.cs`、`WorldEngine.Mortality.cs`；UI 中 `MainView.Buildings.cs`、`WorldMapControl.Ecology.cs` | `EcologyAndConflictTests.cs`、Headless、桌面／触屏浏览器与五种子演化 |
| 目标、知识与通信 | Core 中 `WorldEngine.Agents.cs`、`WorldEngine.Communication.cs` | [AgentBehaviorTests.cs](../tests/SeWZC.WorldBox.Core.Tests/AgentBehaviorTests.cs) |
| 时代路线、配方与加工运输 | Core 中 `AdvancementRules.cs`、`WorldEngine.Advancement.cs`；UI 中 `MainView.Society.cs` | `AdvancementTests.cs`、`tests/browser/advancement.cjs` |
| 地块改造、矿藏与载具 | Core 中 `WorldEngine.Land.cs`、`WorldEngine.Transport.cs`、`WorldState.Land.cs`；UI 中 `MainView.Selection.cs`、`WorldMapControl.Transport.cs` | `LandTransportTests.cs`、`tests/browser/land.cjs` |
| 制度、文化、研究、建设与魔法 | Core 中 `WorldEngine.Society.cs`、`SocietyRules.cs` | [SocietyBehaviorTests.cs](../tests/SeWZC.WorldBox.Core.Tests/SocietyBehaviorTests.cs) |
| 科技树的节点、连线与视野 | UI 中 `MainView.Research.cs`、`Controls/ResearchTreeLayout.cs`、`Controls/ResearchGraphControl.cs` | `AdvancedResearchUi`、`tests/browser/research-trees.cjs` |
| 城镇扩充、设施选址与建设地图 | Core 中 `WorldEngine.Towns.cs`、`BuildingSites.cs`、`Claims.cs`；UI 中 `MainView.Buildings.cs`、`WorldMapControl.Infrastructure.cs` | [TownInfrastructureTests.cs](../tests/SeWZC.WorldBox.Core.Tests/TownInfrastructureTests.cs)、[infrastructure.cjs](../tests/browser/infrastructure.cjs) |
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

地图图形优先复用 `WorldMapControl.Sprites.cs` 的透明像素位图；种族、职业、动作和建筑用途分别决定图形，图例使用同一素材。静态相邻地貌进入地形分块缓存，海拔只参与生成，不进入纹理失效摘要或运行时移动、劳动、建筑与通信计算；动态水流仅遍历缓存的可见河格。不要在每帧扫描全世界来判断建筑风格、生态容量或资源种类。动物环境容量按精确栖息地输入失效，资源食物输入先归一化并截在 0–1；同为饱和供给时不重算，捕食者的猎物限制则按当前快照计算。每日最多复评 256 格动物，完整周期为 `max(6, ceil(地块数 / 256))` 日，快照只保留当前区域和相邻边界；植物每格每 120 日复评。分区由已保存日序推导，不留未提交状态。地形资源摘要仅依赖森林资源不足 25 的图像阈值，动物及植物数量由独立图层刷新。

超过一格高的建筑以底部落点排序；近景将可见建筑、居民和舟船放入复用的绘制列表，按实际插值后的底部 Y 排序，不能先画所有建筑再画所有人物。可见建筑集只在世界或视野范围变化时重建；飞机、选中圈和文字保留上层。绘制、屋顶点选和名称位置共用 `BuildingBounds`，避免图形高度与命中地块不一致；屋顶选择高亮建筑真实占地；屋顶覆盖后方建筑时优先选择指针所处地格上的建筑，空地屋顶仍可点选。

布局优先调整 Fluent 主题的 `ExpanderMinHeight`、`ExpanderHeaderPadding`、`ExpanderContentPadding` 和小箭头尺寸；内容与标题都核对折叠、展开后的真实高度。建筑生命与停工阈值必须在默认详情可见，不能依赖展开才能解释“受损”。按钮以内容居中、自然宽度和横向间距控制密度。

可用 Core.Tests 的 `--export-visual-fixture <路径>` 导出四种族、职业、同类建筑、山河与多物种的受控画面夹具；发布后运行 `CHROMIUM_EXECUTABLE=/usr/bin/chromium node tests/browser/visuals.cjs <路径>` 验证桌面／触屏生命值、折叠高度、按钮间距、近景标记和观察不变性。定位建筑只移动镜头，检查近景须另行放大。夹具不是自主发展证据；百年晋升仍由核心长程场景验证。

### 浏览器速度调查

`scripts/profile-browser-stages.py <新目录>` 将 Core、UI、Browser 复制到独立目录，插入有界的阶段计时和只读计时导出；不修改生产源码。发布打印出的 Browser 项目，使用该静态产物单独服务，再运行以下命令：

```bash
python3 scripts/profile-browser-stages.py /tmp/worldbox-speed-probe
dotnet publish /tmp/worldbox-speed-probe/src/SeWZC.WorldBox.Browser/SeWZC.WorldBox.Browser.csproj -c Release -o /tmp/worldbox-speed-probe/publish
WORLDBOX_BASE_URL=http://127.0.0.1:8080/probe/ CHROMIUM_EXECUTABLE=/usr/bin/chromium node tests/browser/five-speed-profile.cjs artifacts/separator-verification/baseline.worldbox.json artifacts/speed-investigation/probe
```

需先将 `publish/wwwroot` 放在上述 `/probe/` 服务路径；夹具可以由现有 `--export-browser-fixture` 生成。`WORLDBOX_PROFILE_CASES` 可筛选 `default-far-1,default-far-5,default-near-5,large-far-1,large-far-5,large-near-5,large-auto-5`，默认普通场景观察 15 秒，自动保存场景 38 秒。`WORLDBOX_PROFILE_SECONDS` 设置普通场景时长；`WORLDBOX_PROFILE_DEFAULT_FIXTURE` 可指定此前保存的默认世界，避免两个版本初次启动后日序或群落数量不同。对正式产物运行同一脚本会只记录推进、长任务和浏览器动画帧机会，不要求它暴露探针。

隔离副本额外允许 `WORLDBOX_PROFILE_EXACT_TERRAIN=1` 做地形资源失效条件的实验对照；这个开关只存在于副本中，不是正式产品选项。它限定资源量只影响森林树桩阈值，不改变世界；当前正式实现已采用此条件，隔离副本可用 `WORLDBOX_PROFILE_LEGACY_TERRAIN=1` 恢复旧失效条件作对照；需用相同夹具、相同视角与无并发负载分别测原条件与实验条件。嵌套计时不能直接全部相加，`Save.Serialize` 在缓冲保存实现中包含让出执行权的等待，异步存储等待也不能全部归为主线程阻塞；动画帧机会不等于实际绘制 FPS。原始逐次计时与汇总一起保留，并补一轮未插桩对照以检查探针对结论的影响。

`tests/browser/saving.cjs <当前格式大世界存档>` 通过实际按钮与拖动验证保存时镜头可用、日序一致、编辑取消与 Worker 不可用时的回退；CI 使用构建任务生成并上传的同一大世界夹具。存储 Worker 不继承文档 import map，必须使用发布后实际解析的模块 URL。

### 检查范围

安装与启动命令统一维护在 [项目 README](../README.md#开发环境)，完整发布和浏览器复现步骤见 [验证记录](verification.md#复现命令)。以下是范围选择，不要求每次无关改动都重复全部压力测试。

| 改动范围 | 应执行的检查 |
| --- | --- |
| 仅文档 | 核对描述与实现、相对链接及标题锚点，运行 `git diff --check`；不声称重跑游戏验收 |
| 核心行为、数据或编辑 | Release 构建、快速检查、核心 `--suite integration`；针对因果关系或失效边界加回归场景 |
| 存档格式或序列化 | 上述检查，加完整往返、合法零值、非法输入不改世界、中途保存续演；涉及平台时验证真实存储 |
| 界面、输入或呈现 | 发布裁剪后的浏览器产物，在仓库子路径运行相关桌面／触屏／运动脚本；涉及渲染回退时复验软件路径 |
| 资源、平台入口或部署 | 静态发布检查、真实浏览器加载和存储；发布后检查实际 Pages URL |
| 性能、记录容量或大世界 | 先通过功能验证及相关 `--suite long`，再在独立负载下测量，记录种子、规模、配置、耗时、实际存档字节数与环境 |

核心测试是可执行程序：

```bash
dotnet build -c Release
python3 scripts/run-fast-tests.py
dotnet run --project tests/SeWZC.WorldBox.Core.Tests -c Release --no-build -- --suite integration
```

`dotnet test` 不能替代它；桌面构建也不能替代经过裁剪的 WASM 发布。测试失败返回非零退出码，先定位失败行为，不为过关删除约束或扩大误差。

按本次用户决定，单元测试应聚焦单个行为；测试作者负责控制测试规模，使核心单元和 Headless UI 快速检查合计明显低于 **10 秒**，为运行波动留出余量。接近或超过上界应定位慢用例并缩小夹具，不能视作正常目标。这是编写与维护约定，不在 CI 或测试运行代码中设置硬超时、按耗时判失败。`scripts/run-fast-tests.py` 只报告实际墙钟耗时，包含两个进程启动；UI 控件回归仍属于组件检查，不因短小而改称纯单元。编译、还原、跨系统集成、浏览器验收与性能基准另计。

核心测试方法用 `[UnitTest]` 标记聚焦且有界的命令／查询／规则检查；未标记的方法归入 `integration`，避免新增长程场景无意挤入快速套件。`[LongRunningTest]` 标记混合编辑随机回归、五种子 6,000 tick 战争、大世界发展。CLI 默认 `unit`，另支持 `--suite integration|long|all`、`--list` 和 `--filter <名称片段>`；筛选零项视为错误。不得仅为达到耗时目标把普通单元标成集成，应先缩小夹具或直接构造前置状态。

按 2026-10-03 用户关于避免浪费测试时间的决定，日常 CI 执行核心单元、Headless UI、静态资源与快速浏览器冒烟检查。普通集成、长程模拟和原有九套完整浏览器回归通过 Actions 手动输入 `full_regression=true` 运行，保留原种子、步数与断言；本地仍可运行对应套件。涉及战争长程恢复、自主发展或随机编辑存档的修改按范围补跑对应长程回归。全部核心检查使用 `--suite all`；仅调整 CI 编排时检查工作流语法、任务依赖、条件及脚本入口，不必重新构建游戏或执行完整回归。

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

原生阶段调查用 `scripts/profile-simulation-stages.py <新目录>` 创建隔离副本，生产 Core 不插桩。副本支持 `--profile-simulation --ecology-only --dense-ecology --warmup 0 --ticks 512 --repetitions 3 --output <文件>`：仅推进动物和植物，使用高肥力高供水、可适应物种齐备的森林，覆盖大图两个完整动物周期，每轮包含缓冲首分配。进程 JIT 仍先预热；不能把此场景的均值或峰值当成含居民行动、GC、浏览器绘图的全游戏硬实时保证。另跑普通完整模拟和未插桩原生测量，报告生态合计的均值、P95、最大值及完整步进。探针记录包含调度和运行时停顿，阶段采样数组在测量前预分配。当前功能和实际参数见 [生态性能记录](performance.md)。

当前工作流自动验证 `main` 推送与 PR，其他分支可手动运行，避免功能分支 push／PR 双跑。原生 `build` 与浏览器 `publish` 任务在独立 runner 并行执行：前者只还原和构建 `scripts/ci-build.slnf` 中的桌面与测试依赖，不安装 WASM 工作负载；后者只还原浏览器项目并发布一次。新增项目时同步维护筛选文件。CI 发布传 `--no-restore`，本地独立发布仍自动还原。

日常浏览器矩阵只执行 `deploy-smoke.cjs`；`full_regression=true` 时加上十套完整回归（包含真实帝国存档与科技树），下载同一静态产物，在独立 runner 并行执行，避免运动／特效测量争抢 CPU。大世界保存夹具只在完整回归时生成和上传。部署依赖原生构建、发布及本次选择的全部浏览器检查成功。共享浏览器安装 action 按操作系统、架构及 lockfile 缓存 Chromium 下载，缓存命中仍检查系统依赖。

只有 `main` 非 PR 运行会部署；部署前的本地产物和部署后的公网均用快速冒烟校验 HTML 提交标记、渲染、模拟推进、存档和刷新恢复。静态产物的提交标记由发布脚本生成，CI 使用 `GITHUB_SHA`。日常成功表示快速检查通过；触屏、编辑与其他完整行为只由对应回归结果证明。推送授权沿用当前任务与会话约定；本指南不增加新的授权，也不要求重复确认已有授权。已部署与已通过公网验收是两个状态，报告时分别说明。

## 文档随行为一起维护

界面字段不能用间隔点或圆点分隔。修改时同时检查 UI 文案、Core 返回的可见摘要及浏览器标题；长内容分行并标注字段，短状态使用完整句子。日期保持完整，工具预览中的换行直接来自提示内容，鼠标与触屏使用同一格式。

功能变化更新 [product.md](product.md) 中的能力与验收；模块变化更新 [architecture.md](architecture.md)；重要取舍新增或替代 [ADR](decisions/README.md)。纯重命名、局部参数或一般修复通常不需要单独写 ADR。

验证记录写明对应提交、命令、环境、结果、证据和未覆盖范围。历史证据保留其归属，不能只更新日期就把旧结果归给新代码。临时计划与排查过程完成后提炼成简短原因，不把整段聊天或短期状态堆进长期文档。

## 本轮增量刷新与软键盘处理（2026-10-03）

地图只散列可见分块及一格边缘，镜头进入新分块时重新核对；离屏状态不依赖缓存成为模拟事实。居民视野集合按逻辑日序或视野范围变化更新，逐帧只插值可见角色；近景不构造不会绘制的远景轮廓。动物形状复用小位图，位置／容量／矿藏可见性缓存按逻辑变化和镜头范围失效。插值动画按全景 15 Hz／近景 30 Hz 调度；逻辑变化和交互可立即刷新，世界步进独立；暂停查看不改变世界或随机数。

详情文本比较内容后再赋值，折叠区不做持续计算。道路／建筑名单按日序、搜索和最新事件标识缓存，事件焦点按保留事件与关注集合变化分组。搜索输入用 220 ms 合并刷新，焦点控件不拆换；选择变化才重建详情。

`wwwroot/text-input.js` 是针对锁定 Avalonia 12.1.3 的输入适配：该版本只从键盘和 `compositionend` 接收字符，忽略 `beforeinput` 的普通插入。适配仅补齐未被既有输入路径消费的插入／删除，排除合成中事件、实体按键和输入法尾随提交，防止重复。升级框架时重跑 `details.cjs`，确认是否可以去掉此适配。桌面不经过该浏览器适配。

百年研究／生产与法术传承回归位于 `DevelopmentPlanningTests`；诊断入口 `--simulate-development <目录> [种子] [尺寸] [日序] --technology` 或 `--magic-practice` 仅指定国家发展偏好，不赠送物资或知识。普通模式不加方向参数。


细分性能调查使用 `python3 scripts/profile-simulation-details.py <新目录>`，在隔离副本中生成整数索引的无分配嵌套计时器，记录方法 inclusive / self 耗时与调用次数，并单独测量通信邻居收集、收件人排名选择，以及共享可达性缓存查询／标记与实际 BFS；缓存查询次数减 BFS 次数得到共享缓存命中数。动物缓存仅计数请求与种群扫描，避免每次地格查询的计时干扰。按阶段区分环境观察和通信的共享记忆方法。默认跳过高频叶查询计时，`--deep` 用于调用次数诊断，必须量化额外开销。`self` 扣除已计时子方法；inclusive 不能相加。另跑未插桩的相同负载，比较终态存档摘要与探针开销。具体方法内仍未测量的部分保留为残余。报告只保留能支撑结论的热点、优化依据与验证，不罗列无关细碎操作或未量化的改造设想。

生态结构默认值比较和空世界保存可用 `python3 scripts/profile-ecology-values.py <已构建的 Core.dll> <输出.json>` 调查，SDK 路径通过 `WORLDBOX_DOTNET` 指定。脚本在临时目录编译独立探针，分别记录泛型比较器的调用耗时／分配与 32×32 空世界实际序列化耗时、载荷和摘要。优化前后 DLL 先后串行运行；微基准不能冒充真实生态或整场游戏的 CPU 百分比。

原生 `--profile-simulation` 同时记录整个测量窗口的进程 CPU 时间和每 tick 墙钟耗时。CPU 包含该进程的 GC、JIT 和工作线程，可能大于墙钟；不包含生成、预热或保存，也不能当成模拟主线程或单个方法的 CPU 百分比。分批与交替复测结论不一致时保留所有组，不将负“探针开销”当成优化、不将异常直接归因于 GC／调度，记录独立的配对 CPU 与墙钟证据。


### 帝国研究与存档模拟

研究图、说明、成本和前置统一维护在 `ResearchRules.cs`，每批生产配方仍由 `AdvancementRules.cs` 负责。新增知识的效果需进入实际采收、生产、训练等规则及对应效果展示，不能只新增界面节点。矿工材料目标是保存状态；生产预算缓存只在居民阶段存在，退出阶段必须清空。

科技树布局只属于 UI。节点位置与每条实际依赖的线路由 `ResearchTreeLayout` 生成；`ResearchGraphControl` 保留拖动、缩放与定位，周期刷新不替换节点或复位视野。Headless 检查两条研究树与共同基础的连接覆盖、节点重叠、线路穿越和终点不绕外围；浏览器 `research-trees.cjs` 使用交付 ZIP 中的真实存档，检查节点上的鼠标／真实触摸横向与纵向拖动、缩放、完整概览与存档不变性。触屏回归须固定同一手势的触点编号，可用 `WORLDBOX_RESEARCH_CASES=technology-mobile,magic-mobile` 聚焦失败场景；默认执行桌面／触屏共四个场景。核心规则未变的图形修正无需重跑帝国模拟。

复现完整自主科技路线：

```bash
dotnet run --project tests/SeWZC.WorldBox.Core.Tests -c Release --no-build -- --simulate-development artifacts/empires/technology 73921 128 24000 --peaceful --technology --require-empire --until-empire
```

将 `--technology` 替换为 `--arcane-industry` 可运行魔法帝国路线。`--require-empire` 失败时返回非零，要求同一聚落完整掌握路线并有各配套设施真实生产记录；`--until-empire` 达标后继续 1,200 日再保存。报告记录实际推进日数、首次达标、各采样、死亡原因和 24 日保存续演校验。繁荣规则场景与默认战争／灾害世界的结果须分别说明。


### 格式 14 研究玩法扩展

`WorldEngine.ResearchGameplay.cs` 负责文明条件、公共服务、新岗位、铁路、折跃与射击，`WorldEngine.KnowledgeQueries.cs` 负责逐日派生知识索引。研究声明的建筑／职业／法术与操作入口由 `MainView.ResearchActions.cs` 执行正常编辑命令，不能在测试桥中添加修改入口。可变保存字段必须同步验证；枚举槽 18、23 为已删除的错误帝国项目，不得复用。

完整模拟以 `GetCivilizationProgress` 为同一判据，必须检查配套设施的健康、停用、领地、完成状态及真实首批记录。`ResearchGameplayTests` 验证新机制的成本、范围、交战知识、拒绝时不变和保存续演；浏览器 `research-gameplay.cjs` 在桌面／触屏走实际岗位、建造、铁路、折跃与法术入口，`research-trees.cjs` 导入当前交付 ZIP 检查图形与只读行为。新增内容不能仅依赖 enum 数量测试。
