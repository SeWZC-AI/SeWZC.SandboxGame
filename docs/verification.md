# 构建与验证

本页维护复现入口与最近的有效结果。历史记录通过 Git 查阅，逐次日志、JSON、截图和存档写入已忽略的 `artifacts/` 或 Actions 产物。各轮结果注明实际源码状态，未覆盖的范围不沿用此前的成功结论。

## 最近验证

2026-10-06 类型与文件归属整理的验证基于 `988df9cb026178b8a803bc8fe8f23e0d0164c905` 加未提交修改，在 Linux 云环境使用 .NET SDK 10.0.401、Playwright 1.57.0 和系统 Chromium。224 个生产及测试 C# 文件的 SHA-256 为 `f40078091259e8c15ca0ed22a3aebbd603f5e69a8cd14dc9d56a6230dda8e326`；摘要按相对路径排序，逐项包含路径、NUL 和原始文件字节。

- Release 原生构建无警告／错误，快速入口的 91/91 核心单元与 56/56 Headless UI 通过；122/122 核心集成检查通过，集成入口报告 30.27 秒。
- 裁剪后的浏览器发布与静态资源检查通过；本机仓库子路径验证实际渲染、模拟推进、IndexedDB 保存和刷新恢复，耗时 21.82 秒。产物 revision 标签为上述基线提交，实际被验证的未提交源码由摘要标识；浏览器证据在 `artifacts/type-refactor-browser/`。
- 使用 Roslyn 对比整理前后的 178 个类型及顶级语句入口、2,628 组声明和成员 token，忽略类型声明的 `partial` 修饰符后无差异。类型成员与序列化特性完整保留；所有手写多文件分部类型都有同名且带类型文档注释的主文件，文档源码链接及 `git diff --check` 通过。
- 本轮未执行长程检查、完整浏览器交互矩阵、性能测量、公网部署或手机真机验证。

此前动物优化的验证基于 `5906ff7af5ceaf4e49b6a6342d642f93fbb75942` 加未提交修改，生产源码摘要为 `c22f06a02d4a4ce8ad459135a8115b74bd5b05c670663a15314188e9a17a8f86`。该版本通过 122/122 核心集成和 3/3 相关长程生态检查，覆盖稀疏周期保存续演、混合食物网及居民实地采收下的 6,000 日多样性；浏览器运动、桌面／触屏画面与只读观察通过。性能结果见 [性能说明](performance.md)。这两轮均未验证公网部署或手机真机。

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
