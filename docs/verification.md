# 构建与验证记录

## 第三轮实现验证（2026-10-01）

本条对应实现提交 [`16dfc7a`](https://github.com/SeWZC-AI/SeWZC.SandboxGame/commit/16dfc7aa126741bad38e1064f61466b209d601cf)（基线 `959761c`），不是下方旧构建的结果。环境为 Linux x64、.NET SDK 10.0.401、Avalonia 12.1.3、Node.js 24.19.0 和系统 Chromium 151.0.7922.173；手机为 390×844 触屏仿真，并检查 844×390 横屏。未使用真实 Android / iOS 设备，也未重跑 2,000 人压力基准或强制软件渲染。

- Release 完整构建通过，0 警告、0 错误；核心 **34 / 34** 通过。新增验证覆盖规则关闭值持久化、非法规则原子拒绝、放置预览只读、赐予与施工差异、发展受阻及恢复、外交接触和结盟递送、外国军令隔离、实地迁徙和性格预设的实际影响。
- 发布裁剪后的 WASM 通过静态资源及仓库子路径检查。
- 桌面、触屏、运动三套真实浏览器脚本均通过；正常与 `?e2e=1` 入口无渲染回退、页面或控制台错误。核对了姓名编辑保持其他字段精度、枚举与数值表单、导入导出及刷新恢复、无效放置不修改世界、免费完工建筑与材料施工、独立规则预设、四类工具不打开面板、手机默认地图区域超过屏幕高度的 75%、触屏预览确认、拖动／捏合、编辑横竖屏切换、居民点选及跟随、暂停冻结和实际帧内运动。截图与 JSON 在本次运行的 `artifacts/browser-tests`，长程结果在 `artifacts/evolution-probe.json`。

五个 256×256 示例世界各运行 6,000 tick，相当于默认 1 倍速 20 分钟，再分别比较保存恢复后 60 tick 与不间断运行，全部一致。计数只包括建国、增长、建设、研究、外交、战争与灾害；排除贸易、通信、施法及个人文化变化，避免把高频记录数量当作玩法进展。

| 种子 | 上述事件数 | 最长空档（1 倍速秒） | 末期人口 | 末期国家／聚落 | 末期已掌握研究总数 |
| --- | ---: | ---: | ---: | --- | ---: |
| 73921 | 87 | 150.8 | 192 | 4／4 | 9 |
| 42 | 136 | 68.4 | 174 | 4／4 | 12 |
| 223 | 141 | 93 | 211 | 3／6 | 17 |
| 17 | 157 | 71 | 247 | 4／6 | 18 |
| 9876 | 107 | 120 | 204 | 2／3 | 12 |

这验证了这些种子的活动节奏、研究进展及确定续演；不保证所有种子、地形或干预都有相同结果，也不能直接证明主观乐趣。默认强度下样本以发展和合作为主，战争记录包含国家消亡，不能都解释为自主宣战。完整战争目标、家族故事、更长时期内容与平衡仍需扩展。

复现长程检查（独立运行，不加入每次 CI 的核心测试）：

```bash
dotnet run --project tests/SeWZC.WorldBox.Core.Tests -c Release --no-build -- \
  --evolution artifacts/evolution-probe.json
```

桌面、触屏和运动脚本见下方复现命令。当前存档格式与模拟版本为 **3**，导入格式 1、2、999 均明确拒绝且保留当前世界；旧格式 2 需新建。

## 历史构建 ba395b9

记录日期：2026-10-01。以下结果对应实现提交 [`ba395b9`](https://github.com/SeWZC-AI/SeWZC.SandboxGame/commit/ba395b9ffe400af69dfd90b89fa97d3873332277)，不自动适用于后续源码，也不代表所有浏览器或设备都已通过验收。本次文档整理引用已有证据，没有重新执行游戏验收。

该提交的 [Actions 运行 36868079900](https://github.com/SeWZC-AI/SeWZC.SandboxGame/actions/runs/36868079900) 中，构建与验证、alpha 部署、部署后的桌面／触屏／运动检查均成功。公网结果来自实际 [Pages 站点](https://sewzc-ai.github.io/SeWZC.SandboxGame/)，[浏览器证据产物](https://github.com/SeWZC-AI/SeWZC.SandboxGame/actions/runs/36868079900/artifacts/11165088174) 随 Actions 的保留策略可能过期；普通／强制软件的本地复验及独立压力测量另见下文，不能推断为每次 CI 都执行了所有压力检查。

以后新增记录应注明对应提交、环境、命令、结果与限制；保留旧证据的原始归属。范围选择见 [开发约定](development.md#按改动选择验证)。

## 该提交的核心验证

核心模拟 **27 / 27** 场景通过，覆盖：

- 种子复现、世界推进、保存续演、明确拒绝旧版及未知格式、异常值与引用校验。
- 地形与混合编辑、居民原子编辑及零值保存、领土转移、国家分裂、混合种族归属。
- 食物匮乏与灾害的实际影响、海域对陆军和占领的阻挡、战争伤亡及占领。
- 远方未观察事件不可知、单步不能无限转述、消息来源／时间／可信度改变选择。
- 采集所得留在随身库存，商旅与迁居者携带货物实地移动，意见送达后才影响政策。
- 居民职业诉求、制度权重与去重；文化通过接触改变且独立于种族及国籍。
- 建设施工与本地研究的材料和到场劳动要求；训练、天赋与魔力约束局部法术。
- 道路、运作中的信号塔、覆盖及连接影响通信；自治社会能自行建设与研究，途中反复保存仍确定续演。

原生基准使用 256×256 地图、2,000 名初始居民、16 国及 8 场战争。完成 120 步测量，并在 tick 180 验证 2,198 名居民、13,960 条居民记忆的当前格式存档可恢复；JSON 为 **19,879,876 bytes**，低于 32 MiB 导入上限。原生耗时仅说明该机器上的模拟开销，不包含浏览器绘制、输入与存储，不能作为手机帧率。

## 该提交静态产物的浏览器验证

全解决方案 Release 构建无警告和错误，最终 WebAssembly 发布及相对资源检查通过。以下测试直接打开发布产物的 `/SeWZC.SandboxGame/` 子路径，使用真实输入和实际存档核对行为。

| 检查 | 结果与范围 |
| --- | --- |
| 普通 URL 与测试入口 | 普通入口无测试快照；显式 `?e2e=1` 的只读控件快照正常，页面和中文界面正常显示 |
| 桌面 1440×960 | 通过居民投放、实际地形绘制、暂停时间、整世界撤销等值、真实下载与文件选择器导入、当前格式完整往返、旧版／未知版本拒绝、刷新恢复 |
| 居民与社会编辑 | 通过姓名、目标及理由、人格、带来源记忆、结构化经历编辑；经历影响未来性格，地格与库存不被回算。国家文化、制度与政策可改，居民种族和文化保持独立 |
| 建设与日志 | 学院未建成时研究被拒绝且库存不变；新建学院和道路扣除精确材料；关闭魔法发展保留现有设施。日志重要程度／国家／文字筛选对应真实事件，并保持世界只读 |
| 触屏与旋转 | 390×844 触屏仿真通过投放、自动暂停、单指平移／双指捏合及全世界存档不变；五个档案页签的提交位置固定，打开弹窗后旋转为 844×390，提交与取消仍可操作 |
| 固定布局 | 六种分类保持页首、暂停／速度及八个工具槽的实际矩形；等待主题按压动画回到基线后精确比较 |
| 连续运动 | 独立运动脚本在普通及强制软件渲染下通过：同一模拟 tick 内存在连续显示位移、暂停冻结、镜头操作保持存档与随机状态、2× 继续移动、地图点选与跟随实际居民 |

上述桌面、触屏及连续运动脚本均已在普通和强制软件渲染下通过，除精确匹配的后端降级诊断外未记录页面或控制台错误。最新静态产物还完整复验了玩家新增记忆的来源职业，与居民实际职业一致。截图和 JSON 证据位于 `artifacts/browser-tests`；强制软件的桌面／触屏证据放在其 `software/` 子目录，运动软件证据在 `motion-software/`。

独立运行的浏览器压力检查通过：256×256、2,000 名初始居民、16 国和 8 场战争，在 15 秒观察及输入控制共 19,803 ms 内由 tick 0 推进至 74，居民增至 2,123，保留 16 国／16 支军队和 8,477 条记忆。实际 IndexedDB JSON 为 **16,607,833 bytes**，低于 32 MiB 上限；页面、控制台及 HTTP 均无错误，普通渲染路径未降级。

该短时无界面 Chromium 场景记录到 150 次主线程长任务，最长 377 ms。它验证了这组负载能推进并保存，不构成 FPS、真实手机、复杂地形或长期稳定性的保证；并发浏览器检查期间的竞争数据未用作本次性能记录。

## 复现命令

在安装固定 SDK 与 `wasm-tools` 后，从仓库根目录运行：

```bash
dotnet build -c Release
dotnet run --project tests/SeWZC.WorldBox.Core.Tests -c Release --no-build
dotnet run --project tests/SeWZC.WorldBox.Core.Tests -c Release --no-build -- --benchmark
bash scripts/publish-browser.sh
```

测试程序任何场景失败都会返回非零退出码。`--benchmark` 在行为测试成功后执行原生基准，输出本机测量结果；比较不同修改时应使用相同机器、构建配置及负载，不应直接将其作为 WebAssembly 或手机帧率。

可生成同样规模的当前格式存档，再通过真实浏览器导入进行压力检查：

```bash
dotnet run --project tests/SeWZC.WorldBox.Core.Tests -c Release --no-build -- \
  --export-browser-fixture artifacts/stress-world-v3.json
node tests/browser/stress.cjs artifacts/stress-world-v3.json
```

核心导出选项只生成并校验场景，不运行完整测试套件。浏览器压力脚本需要已运行的静态站点及 Playwright；默认输出 `artifacts/browser-tests/stress-report.json`，不加入普通 CI 冒烟。场景以平坦草地、充足资源及固定八场战争隔离人口与军队负载，不覆盖复杂山海地形或长期资源匮乏情况。

## 浏览器检查

浏览器脚本位于 `tests/browser/smoke.cjs`。先发布站点，再安装固定版本依赖：

```bash
npm ci --prefix tests/browser
cd tests/browser
npx playwright install --with-deps chromium
cd ../..
mkdir -p artifacts/browser-preview/SeWZC.SandboxGame
cp -a artifacts/site/. artifacts/browser-preview/SeWZC.SandboxGame/
python3 -m http.server 8080 --bind 127.0.0.1 --directory artifacts/browser-preview
```

保持 HTTP 服务运行，在另一终端的仓库根目录执行：

```bash
npm test --prefix tests/browser
npm run test:mobile --prefix tests/browser
npm run test:motion --prefix tests/browser
```

默认地址为 `http://127.0.0.1:8080/SeWZC.SandboxGame/`。可通过以下环境变量覆盖：

| 变量 | 用途 |
| --- | --- |
| `WORLDBOX_BASE_URL` | 已运行站点的完整地址，包含项目子路径及结尾 `/` |
| `CHROMIUM_EXECUTABLE` | 使用已有 Chromium 的可执行文件路径；未设置时使用 Playwright 安装的版本 |
| `WORLDBOX_ARTIFACT_DIR` | 桌面／触屏／运动脚本的证据输出目录；同时验证不同渲染方式时用于分开文件 |
| `WORLDBOX_TEST_DISABLE_WEBGL` | 仅在值为 `1` 时为测试浏览器禁用 WebGL，用于复现软件渲染回退；默认不禁用，不改变应用渲染优先顺序 |

例如使用本机已有的 Chromium：

```bash
WORLDBOX_BASE_URL=http://127.0.0.1:8080/SeWZC.SandboxGame/ \
CHROMIUM_EXECUTABLE=/usr/bin/chromium npm test --prefix tests/browser
```

检查在独立浏览器上下文中运行，使用示例世界，不操作日常浏览器中的存档。截图、导出及导入样本写入 `artifacts/browser-tests`；CI 额外保存 HTTP 与测试日志，并在成功或失败时上传 `worldbox-browser-tests`。

Avalonia 使用画布呈现界面。测试以 `?e2e=1` 显式启用只读 UI 快照，读取语义控件 ID、实际位置、可见性和有限界面状态，再派发真实鼠标／键盘／触摸事件；默认 URL 不公开该快照，桥不提供修改或推进世界的命令。桌面为 1440×960，触屏模拟为 390×844，并检查 844×390 旋转；按钮按下与松开间隔 80 ms。世界断言来自真实 IndexedDB 存档或下载文件，不以截图替代数据检查。固定槽位测试等待按压动画结束后，精确比较所有分类下的实际按钮矩形，不使用像素容差掩盖持续位移。

CI 机器可能无法创建 WebGL2 / WebGL 上下文，Avalonia 会记录后端诊断并转用软件渲染。共享检查器只接受以下两条完整匹配的 `console.error` 文本；所有 `pageerror` 和其他 `console.error` 仍导致失败：

```text
Failed to create render target for mode 3 : HTMLCanvasElement.getContext returned null.
Failed to create render target for mode 2 : HTMLCanvasElement.getContext returned null.
```

观察到这些诊断时，检查器必须从当前页面的 `#out canvas.avalonia-canvas` 获取实际 2D 像素，至少采到 32 个非透明点和 8 种 RGB 颜色，才允许测试通过。刷新会重置当前页面的诊断状态，刷新后的画布需重新检查，避免使用旧页面的绘制证据。桌面与触屏测试在启动和最后交互后执行检查，运动测试在动作完成后检查，记录渲染器及像素采样信息；原有模拟、编辑、存档和触屏断言全部保留。

部署任务成功后，独立的在线验证任务会以实际 GitHub Pages URL 重新执行桌面、触屏与运动三套检查，并上传 `worldbox-live-browser-tests`，用于验证真实公网部署。

## 限制与后续验证

- 尚未在真实 Android / iOS 手机上测量运行性能、内存与存储行为。
- Chromium 的手机尺寸模拟不能替代移动 Safari / Chrome 真机验证；Firefox 与 Safari 仍需检查。
- 原生人口基准不包含渲染开销；浏览器短时压力运行不等于帧率、首次下载时间、复杂地形或长期稳定性验收。
- 存档仍依赖本设备与当前站点的存储；需持续检查配额耗尽、多标签页及浏览器关闭等情形。
- 远端 GitHub Pages 由 `alpha` 分支的工作流发布；本记录中的本地子路径验证不替代远端验收，实际部署结果以对应 Actions 运行和 `github-pages` 环境为准。首次部署需要 Pages 使用 GitHub Actions，并允许 `alpha` 分支进入该环境。
- 已实现的是文化、制度、研究、通信、建设与魔法的基础闭环；完整历史演化、宗教、未来科技树、复杂战术、海战与多人联机仍不在当前范围。自主外交宣战尚未实现。
