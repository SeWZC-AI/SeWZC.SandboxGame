# 构建与验证

本页维护复现入口与最近的有效结果。历史记录通过 Git 查阅，逐次日志、JSON、截图和存档写入已忽略的 `artifacts/` 或 Actions 产物。各轮结果注明实际源码状态，未覆盖的范围不沿用此前的成功结论。

## 最近验证

2026-10-06 强类型重构的推送复验对应源码提交 **`bcb78d754090a0a36232a269320b871df7ec0a83`**，已整合远端 **`c83b44d326eb812803d74918d06b28ac0da9ce07`** 的类型拆分、动物优化和废弃状态清理。环境为 Debian 13 / x64、.NET SDK 10.0.401、Runtime 10.0.12；193 个生产 C# 文件的 SHA-256 为 `df82ee1c5b0cb4f9f64fad38710c39460e81ee167cbc4c26a762915e9190f0ff`。摘要按排序后的 `src/**/*.cs` 相对路径、NUL、文件内容、NUL 依次计算，排除 `bin`／`obj`。后续验证记录提交只修改本页。

- `dotnet build scripts/ci-build.slnf -c Release --no-restore` 通过，**0 警告／错误**；`python3 scripts/run-fast-tests.py` 通过 **94/94 核心单元、57/57 Headless UI**，合计 **7.81 秒**，包含进程启动。
- `--suite integration` 通过 **122/122**，耗时 **30.24 秒**；`--suite long --filter 'technology civilizations complete real industrial production within a century'` 通过 **1/1**，耗时 **47.88 秒**。种子 73921／42 分别在第 29／4 年发生实际工业生产，并完成百年推进及保存续演。
- 回归覆盖共享规则集合和费用的隔离、不可变资源读取零分配、多态研究操作、地图连续笔画与单次投放、切换工具、只读导航及当前格式保存恢复。保留远端的同名类型文件与保存／模拟版本。
- 同机串行比较上述远端基线和源码提交，使用现有 `--profile-simulation --population 2000 --warmup 60 --ticks 120 --repetitions 3`，两侧均设置 `DOTNET_TieredCompilation=0`。种子 451、256×256、16 国、8 场战争，无自然灾害，预热及序列化在计时外。平均墙钟 **5.400 → 5.438 ms/tick**（+0.71%），进程 CPU **5.429 → 5.435 ms/tick**（+0.11%），平均分配 **1945724.4 → 1933167.4 bytes/tick**（−0.65%）。墙钟各轮范围为基线 5.254–5.487、重构 5.042–5.663 ms/tick；本次小幅均值差异处于组内波动范围，不能据此声称稳定加速或硬实时保证。
- 六轮终态均为人口 2,035、日序 180、实际存档 **36,870,662 bytes**；完整存档 SHA-256 均为 `65FD3FCA2FB5B4D53913A2B03E399FA36766667ADFF98CE1639C63EDB8079F50`，并通过导入。原始日志、逐轮 JSON 与环境摘要保留在 `artifacts/strong-types-push-verification-20261006/`；整合前证据保留在 `artifacts/strong-types-verification-20261006/`，不归给当前源码。
- 本轮未重新执行裁剪后的 WASM 发布、真实浏览器／触屏、完整长程套件、部署后公网检查或手机真机验证。原生性能不能换算为浏览器帧率；此前浏览器结论的源码归属可通过 Git 历史查阅。

## 复现命令

安装固定 SDK 与 `wasm-tools` 后，在仓库根目录运行：

```bash
dotnet build -c Release
python3 scripts/run-fast-tests.py
dotnet run --project tests/SeWZC.WorldBox.Core.Tests -c Release --no-build -- --suite integration
dotnet run --project tests/SeWZC.WorldBox.Core.Tests -c Release --no-build -- --suite long
bash scripts/publish-browser.sh
```

按 [开发约定](development.md#按改动选择验证) 选择相关范围。核心测试是可执行程序，不能用 `dotnet test` 代替；`--suite all` 运行全部核心检查，`--filter <名称片段>` 可聚焦场景。失败返回非零退出码。性能测量入口见 [性能说明](performance.md#复现测量)。

## 浏览器检查

先发布站点，再安装锁定依赖并准备仓库子路径：

```bash
npm ci --prefix tests/browser
cd tests/browser
npx playwright install --with-deps chromium
cd ../..
mkdir -p artifacts/browser-preview/SeWZC.SandboxGame
cp -a artifacts/site/. artifacts/browser-preview/SeWZC.SandboxGame/
python3 -m http.server 8080 --bind 127.0.0.1 --directory artifacts/browser-preview
```

保持 HTTP 服务运行，在另一终端的仓库根目录执行相关套件：

```bash
npm test --prefix tests/browser
npm run test:mobile --prefix tests/browser
npm run test:motion --prefix tests/browser
npm run test:entries --prefix tests/browser
npm run test:deploy --prefix tests/browser
```

其余脚本与参数见 [浏览器测试目录](../tests/browser/) 和 [CI 工作流](../.github/workflows/build-and-deploy.yml)。保存及研究回归需要当前格式夹具；CI 在完整回归时生成并上传，研究夹具通过 `python3 scripts/prepare-research-fixtures.py` 生成。

| 变量 | 用途 |
| --- | --- |
| `WORLDBOX_BASE_URL` | 站点完整地址，包含项目子路径及结尾 `/`；默认 `http://127.0.0.1:8080/SeWZC.SandboxGame/` |
| `CHROMIUM_EXECUTABLE` | 已有 Chromium 的可执行文件路径；未设置时使用 Playwright 安装版本 |
| `WORLDBOX_ARTIFACT_DIR` | 浏览器证据输出目录，默认写入 `artifacts/browser-tests` |
| `WORLDBOX_TEST_DISABLE_WEBGL` | 值为 `1` 时复验软件渲染回退 |
| `WORLDBOX_EXPECTED_REVISION` | 冒烟检查要求的完整 Git SHA；CI 必填，本地可选 |

Avalonia 在 canvas 上绘制。`?e2e=1` 提供只读控件快照，交互仍通过实际鼠标、键盘与触屏。软件渲染回退只允许共享检查器明确列出的诊断，且必须验证当前页面的实际 2D 像素；页面异常或其他控制台错误仍失败。细节见 [共享浏览器检查器](../tests/browser/browser-support.cjs)。

## CI 与部署验收

日常 CI 执行原生构建、核心单元、Headless UI、浏览器发布、静态资源检查与 `deploy-smoke`。手动设置 `full_regression=true` 才执行集成、长程及完整浏览器矩阵。仅 `main` 的非 PR 运行部署，部署后对实际 Pages URL 再运行 `deploy-smoke`。

CI 浏览器证据上传为 `worldbox-browser-tests-<suite>`，公网检查为 `worldbox-live-browser-tests`。本机子路径结果不替代部署后检查；实际上线提交以对应 Actions 和 `github-pages` 环境为准。

## 验证边界

- Chromium 手机尺寸模拟不能替代真实 Android／iOS；Firefox、Safari、真机性能及存储仍需验证。
- 原生基准不含渲染；短时浏览器压力结果不能证明复杂地形、大人口长期稳定性或手机帧率。
- 存档依赖本设备与站点存储，配额、多标签页与关闭浏览器等边界需按平台继续检查。
- 当前能力与后续范围见 [产品约定](product.md)，避免用旧验收记录判断功能是否实现。
