# ADR-0001：Avalonia 共用界面与静态浏览器交付

- 状态：采用
- 记录日期：2026-10-01
- 来源：用户明确要求 Avalonia、2D 沙盒和 GitHub Pages；项目分层、单线程 WebAssembly、关闭 AOT 及平台存储接口属于当前工程选择。

## 背景

游戏需要在浏览器中直接运行，同时保留桌面入口。GitHub Pages 提供静态文件托管，不能承载游戏服务端，也不能假定可以配置跨源隔离响应头。

## 决定与约束

- Core 保存世界状态、模拟规则、编辑与序列化，不引用 Avalonia、浏览器 API 或桌面文件系统。
- UI 共用 Avalonia 界面；Browser 与 Desktop 负责启动和各自的存储适配，通过 `IWorldStorage` 隔离平台差异。
- 浏览器产物是完整静态站点，使用相对资源路径，支持仓库子路径；发布包含 `.nojekyll`。
- 当前运行单线程 WebAssembly，不依赖 `SharedArrayBuffer`、游戏服务器、API 密钥或专有响应头；AOT 当前关闭。
- 浏览器世界保存在当前站点的本地 IndexedDB，导出文件供玩家备份，不把 Pages 当作存档服务器。

## 原因与代价

这一结构满足指定托管方式，让桌面与浏览器使用同一套规则和界面，核心也可以脱离图形环境验证。

代价是 WebAssembly 与字体带来首次下载成本，模拟和呈现共享浏览器主线程预算。本地存档不提供跨设备同步，浏览器配额和数据清理仍会影响保存。

可替代方案包括独立 Web 前端、服务端模拟或依赖跨源隔离的线程方案。当前没有相应产品需求或性能证据支持增加这些边界；关闭 AOT 是现阶段构建选择，不是永久禁止优化。

## 重新评估条件

真机测量证明下载体积或主线程开销不能满足目标，或多人、云同步进入明确范围时，重新比较部署成本和运行收益。优化方案仍需说明如何支持用户要求的 Pages 交付。

## 实现与验证

- [浏览器项目设置](../../src/SeWZC.WorldBox.Browser/SeWZC.WorldBox.Browser.csproj)、[平台存储接口](../../src/SeWZC.WorldBox.UI/Platform/IWorldStorage.cs)。
- [静态发布脚本](../../scripts/publish-browser.sh)、[构建与 Pages 工作流](../../.github/workflows/build-and-deploy.yml)检查实际发布目录及仓库子路径。
- 已测环境与性能限制见[验证记录](../verification.md)；构建通过不等于真实手机性能通过。

相关文档：[产品约定](../product.md) · [决策索引](README.md)。
