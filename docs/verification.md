# 构建与验证

本页维护当前测试入口和最近的实际结果。历史记录通过 Git 查阅，日志、TRX 和源码摘要放入已忽略的 `artifacts/`；旧套件结果不作为新测试的证据。

## 代码验证记录

2026-10-07 重建单元测试时的记录，源码为 **`133b01f` 基线加当时工作区修改**，实现已提交于 `131a0d1`。环境为 Linux 云环境，.NET SDK 10.0.401／Runtime 10.0.12，Avalonia 12.1.3。以下为该轮实际结果，本次文档整理没有重新运行或重新计时。

- 全部旧核心可执行测试、Headless UI、浏览器测试、混入测试程序的诊断工具及配套入口已删除；当前使用 xUnit 2.9.3、.NET Test SDK 18.0.1 与 Visual Studio 适配器 3.1.5。
- 新研究目录初始化用例在修复前失败，报 `TypeInitializationException`，内因是尚未初始化的 `All` 被用于建立索引。索引改在静态构造函数中建立，修复后通过。
- `dotnet build -c Release --no-restore` 全解决方案通过，**0 警告／错误**；清除旧测试 `bin/obj` 后再次还原、构建 `scripts/ci-build.slnf`，同样 **0 警告／错误**。
- `dotnet test scripts/ci-build.slnf -c Release --no-build --no-restore`：**286/286 核心、26/26 界面逻辑**通过，无失败、无跳过。测试运行器分别报告 **885 ms、131 ms**；整个命令墙钟 **4.33 秒**，包含测试发现、运行器与进程启动，不含编译／还原。
- `bash scripts/publish-browser.sh --no-restore` 通过，包含裁剪后的 WebAssembly 发布及相对路径、静态资源与运行时载荷检查。旧浏览器测试桥已从发布源码中移除。
- 工作流已改为标准单元测试并上传 TRX，部署依赖原生验证和静态发布。旧浏览器矩阵、全量回归选项和部署后交互检查已移除。

当时的原始 TRX、测试日志、源码 SHA-256 清单和初始化修复前的失败日志位于已忽略的 `artifacts/unit-tests/final/`，不会随仓库交付。该轮还检查了工作流依赖、文档文件链接和 `git diff --check`，没有运行长程模拟、真实浏览器交互或线上部署，不沿用旧测试的成功记录。

## 文档整理验证

2026-10-07，rebase 到 `131a0d1` 后审查文档差异；相对该提交只修改 Markdown，代码、测试实现、配置及 CI 保持一致，根 README 保留远端最简版本。文档由 45 份收拢到 12 份，33 项决策在同一索引保留来源、代价和替代关系。

相对链接、标题锚点、决策编号、Bash 语法、现行命令／源码／测试类型引用及 `git diff --check` 通过；核对了 SDK、存档版本、xUnit 依赖与 CI 条件。未重新执行游戏测试、浏览器发布或性能测量；检查日志在已忽略的 `artifacts/docs-review-20261007/`。

## 复现命令

```bash
dotnet restore scripts/ci-build.slnf
dotnet build scripts/ci-build.slnf -c Release --no-restore
dotnet test scripts/ci-build.slnf -c Release --no-build --no-restore --logger trx --results-directory artifacts/unit-tests
```

用 `--list-tests` 查看标准发现结果，用 `--filter FullyQualifiedName~ResidentEditingTests` 运行指定类型。用例组织与夹具边界见 [测试说明](../tests/README.md)。

## 浏览器检查

旧浏览器脚本与 `?e2e=1` 快照桥已移除。静态发布和本地预览见 [部署指南](deployment.md#本地发布与预览)，仍可使用：

```bash
bash scripts/publish-browser.sh
python3 -m http.server 8080 --bind 127.0.0.1 --directory artifacts/site
```

打开 `http://localhost:8080/` 进行实际操作。仓库子路径检查可把产物复制到临时 `SeWZC.SandboxGame/` 子目录，再从父目录提供 HTTP 服务。静态检查能发现缺失资源和错误根路径，不能证明游戏启动、触控、中文输入和存储恢复正常。

## CI 与部署验收

工作流由 `main` 推送、PR 或手动触发。原生 `build` 与浏览器 `publish` 在独立 runner 执行：前者还原和构建 `scripts/ci-build.slnf` 并运行 xUnit，后者安装 WASM 工作负载、还原浏览器项目、裁剪发布并检查静态资源。新增原生项目时同步维护筛选文件。

测试报告上传为 `worldbox-unit-tests`，静态文件上传为 `worldbox-static-site`；部署依赖两项任务成功，只有 `main` 的非 PR 运行部署。当前没有自动浏览器交互或部署后游戏验收。

## 验证边界

- 单元覆盖资源隔离、通行与植物、研究目录／命令／操作、现场生产、防护、居民与国家编辑、保存格式与取消，以及文字、运动插值和研究布局。
- 未覆盖完整战争、灾害、通信链、自主演化、长期保存续演和大规模人口平衡；删除旧套件后尚无这些场景的替代自动验收。
- 未覆盖实际 UI 控件事件、浏览器输入／触屏／渲染、IndexedDB 或桌面压缩落盘。单元保存检查只验证核心捕获和 JSON。
- 真机、Firefox／Safari、长期性能和线上部署均未验证。历史性能结果的源码归属见 [性能说明](performance.md)。
