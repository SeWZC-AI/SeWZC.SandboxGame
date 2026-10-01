# SeWZC.WorldBox

用 **C# / Avalonia** 构建的 2D 上帝沙盒，属于 `SeWZC.SandboxGame` 项目。玩家创造地形、放置人类 / 精灵 / 矮人 / 兽人，观察聚落、资源、国家和冲突，并通过编辑器与灾害干预世界。

当前版本是古代文明的早期可运行原型。浏览器入口使用 .NET WebAssembly，可作为纯静态文件部署到 **GitHub Pages**；桌面入口与浏览器共用模拟核心和 Avalonia 界面。无需游戏服务器。

## 当前范围

- 根据种子生成海陆、森林、山地等地形，提供地形笔刷与居民投放。
- 居民具有身份、种族、职业、年龄、健康和所属聚落 / 国家。
- 简化的聚落生产、粮食消费、人口变化、领土扩张、陆路贸易及自动战争。
- 火灾、干旱、疫病，以及世界事件与国家决策说明。
- 暂停、1× / 2× / 5× 时间速度、地图观察，以及暂停编辑期间的世界快照撤销。
- 国家名称、旗色、资源、古代发展水平（1–5 级）、外交与领土编辑；拥有至少两处聚落的国家可让一处聚落独立建国。
- 在既有国家领土投放的其他种族加入该国聚落，可以形成多种族国家。
- 浏览器存储与导入导出、桌面文件存储、GitHub Actions 构建及 Pages 发布流程。

这些系统仍采用原型规则：经济以聚落库存结算，居民行为和陆战经过简化。古代发展水平影响生产效率，不是完整科技树；独立文化 / 文明实体、文化传播、制度与宗教、未来科技、魔法体系、海战、多人联机尚未实现。

已通过核心行为测试、静态发布检查，以及 Chromium 仓库子路径下的编辑、撤销、存档与导入导出检查。手机尺寸触屏模拟已检查居民投放、保存、平移与捏合，并完成 256×256 地图、约 2,000 居民的原生模拟基准。原生模拟结果不能换算为浏览器帧率；真实手机性能尚未测量。详见 [验证记录](docs/verification.md)。

## 操作

首次打开会生成带四个种族的示例世界。通过“新世界”可选择整数种子、128×128 或 256×256 地图，以及是否预先投放居民。

| 操作 | 方法 |
| --- | --- |
| 查看地格 / 居民 | 选择“观察”，点击地图；“概览”打开国家列表 |
| 鼠标移动地图 | 观察模式下左键拖动，或任意工具下右键 / 中键拖动 |
| 鼠标缩放 | 滚轮，或地图上的 `+` / `-`；“全图”恢复总览 |
| 地形与领土绘制 | 选择工具后左键按住拖动，可选择小 / 中 / 大笔刷 |
| 投放居民 / 灾害 | 选择种族或灾害，点击地图；每次居民投放为 12 人 |
| 触屏操作 | 单指轻点执行工具，单指拖动平移，双指捏合缩放；触屏绘制采用轻点笔刷 |
| 时间控制 | 点击“暂停 / 继续”，或在地图获得焦点后按空格；倍率按钮选择 1× / 2× / 5× |
| 关闭面板 | `Esc` 关闭对话框或窄屏观察面板 |

地形、居民、灾害与国家编辑会暂停世界，并保存本轮编辑前的快照。侧栏“撤销”或“存档 → 撤销本轮编辑”恢复这整轮编辑之前的世界，窄屏可使用后者；继续模拟后该恢复点清除。它不是逐笔多级撤销，也不保留跨会话的撤销历史。

在国家详情打开编辑器，可修改名称、旗色、库存与外交，选择古代发展水平，向首都添加指定种族，或者启用该国领土笔刷。领土笔刷覆盖聚落时，会一并转移聚落、居民、库存及相关归属；聚落独立入口在该国拥有至少两处聚落时出现。

## 开发环境

- .NET SDK **10.0.401**，版本由 `global.json` 固定。
- Avalonia **12.1.3**，版本由 `Directory.Build.props` 固定。
- WebAssembly 工作负载 `wasm-tools`。
- 发布检查和本地静态预览使用 Python 3.10 或更新版本；Linux / macOS / Git Bash 可运行发布脚本。
- 浏览器自动检查使用 Node.js 22 与 Playwright 1.57.0；普通运行游戏不需要 Node.js。

在仓库根目录执行：

```bash
dotnet workload install wasm-tools
dotnet restore
dotnet build -c Release
dotnet run --project tests/SeWZC.WorldBox.Core.Tests -c Release
```

核心测试采用可执行测试程序，失败时返回非零退出码；请运行上面的 `dotnet run`，而非以 `dotnet test` 代替。

启动浏览器开发版：

```bash
dotnet run --project src/SeWZC.WorldBox.Browser
```

打开终端显示的本地 HTTP 地址。启动桌面版：

```bash
dotnet run --project src/SeWZC.WorldBox.Desktop
```

Linux 桌面版需要图形会话以及 Avalonia 所需的系统图形库。无图形会话的 CI 仍可构建桌面项目并运行模拟测试。

## 发布到 GitHub Pages

仓库中的 [GitHub Actions 工作流](.github/workflows/build-and-deploy.yml) 会构建、运行模拟测试、发布 WebAssembly 文件，并在 Chromium 中实际打开仓库子路径检查核心交互。所有分支与 PR 都执行验证；只有 `alpha` 分支的推送或该分支的手动运行会进入部署任务。部署目标独立于仓库默认分支。浏览器检查的截图、存档样本与日志上传为 `worldbox-browser-tests` 构建产物。

1. 在 GitHub 仓库的 **Settings → Pages → Build and deployment** 中，将 **Source** 设置为 **GitHub Actions**。
2. 如果 **Settings → Environments → github-pages** 设置了部署分支限制，确保允许 `alpha`，而不是只允许默认分支。
3. 将代码推送到 `alpha`，构建及测试通过后自动部署。工作流已存在于默认分支时，也可在 **Actions → Build, test and deploy WorldBox** 中选择 `alpha` 手动运行。
4. 部署完成后，从工作流的 `github-pages` 环境打开页面。该仓库的标准地址形式为 `https://SeWZC-AI.github.io/SeWZC.SandboxGame/`；仓库设置和实际部署结果决定最终地址。

首次部署需先完成仓库 Pages 设置。工作流使用 `configure-pages` 检查配置，部署任务仅申请 `pages: write` 与 `id-token: write` 权限；不需要另建 `gh-pages` 分支或在仓库中保存个人访问令牌。

无需额外服务器、API 密钥或自定义 COOP / COEP 响应头。浏览器项目关闭 WebAssembly 多线程与 AOT；入口和自有资源使用相对路径，支持项目子路径。发布输出包含 `.nojekyll`，避免 `_framework` 被静态处理忽略。请访问以 `/` 结尾的项目地址。

本地生成与检查相同的静态产物：

```bash
bash scripts/publish-browser.sh
python3 -m http.server 8080 --directory artifacts/site
```

访问 `http://localhost:8080/`。不要直接用 `file://` 打开 `index.html`。发布目录为 `artifacts/site`，可以部署到其他支持 WebAssembly MIME 类型的静态服务器。脚本可通过 `WORLDBOX_DOTNET=/path/to/dotnet` 指定 SDK 可执行文件。

验证 GitHub Pages 子路径时，可把 `artifacts/site` 的内容复制到临时目录的 `SeWZC.SandboxGame/` 子目录，再从其父目录启动 HTTP 服务，并打开 `/SeWZC.SandboxGame/`。CI 的资源检查能够发现根路径引用与缺失文件，实际交互、存储、触控和浏览器兼容性仍需浏览器验证。

本地复现浏览器检查的命令及 `WORLDBOX_BASE_URL`、`CHROMIUM_EXECUTABLE` 参数见 [验证记录](docs/verification.md#浏览器检查)。

## 存档与兼容性

浏览器存档保存在本设备当前站点的 IndexedDB 中，不会上传到 GitHub。清理网站数据、隐私模式退出、存储配额限制或更换站点域名都可能导致存档不可用；重要世界请使用导出功能保存 JSON 文件。导入会验证格式和世界数据，通过后替换当前世界。单个存档上限为 32 MiB。

桌面自动存档位于系统本地应用数据目录下的 `SeWZC/WorldBox/autosave.json`，导入和导出通过系统文件选择器完成。

切换后台时暂停世界，返回后继续；不会按离线时间补算发展。保存采用带格式版本的数据，并记录模拟时间及随机数状态。跨版本迁移仍需随着存档格式演进持续维护。

浏览器当前验证记录见 [验证记录](docs/verification.md)。已跑通约 2,000 居民的短时浏览器压力场景；真实 Android / iOS 设备、Firefox / Safari、复杂地形和大规模人口长时间运行仍需继续验证。

## 结构与后续计划

```text
src/
  SeWZC.WorldBox.Core/       # 世界数据、生成、模拟、编辑、序列化
  SeWZC.WorldBox.UI/         # Avalonia 界面、自定义地图绘制、存储接口
  SeWZC.WorldBox.Browser/    # WebAssembly 入口与浏览器适配
  SeWZC.WorldBox.Desktop/    # 桌面入口与文件适配
tests/
  SeWZC.WorldBox.Core.Tests/ # 无界面的模拟与存档验证
scripts/                    # 静态发布与资源检查
```

设计取舍与模块扩展见 [架构说明](docs/architecture.md)。开发顺序是完善古代文明闭环，验证移动端与规模，再扩展文明文化、科技帝国、奇幻帝国及融合路线。种族、文化和国家将逐步独立建模，未来关闭发展路线时保留已有成果；当前界面不承诺尚未实现的系统。

中文字体随应用打包，使用 Noto CJK 字体；其版权及许可见 [字体许可文件](src/SeWZC.WorldBox.UI/Assets/Fonts/LICENSE.txt)。
