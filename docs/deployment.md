# 浏览器发布与部署

浏览器版可作为纯静态文件部署到 GitHub Pages 或其他支持 WebAssembly MIME 类型的静态服务器，无需游戏服务器、API 密钥或自定义 COOP / COEP 响应头。浏览器入口见 [项目 README](../README.md)，本地启动见 [开发约定](development.md#本地运行)，检查结果与 CI 验收范围见 [构建与验证](verification.md)。

## 本地发布与预览

需要 [global.json](../global.json) 指定的 .NET SDK、`wasm-tools` 工作负载和 Python 3.10 或更新版本。发布脚本可在 Linux、macOS 或 Git Bash 中运行。在仓库根目录执行：

```bash
dotnet workload install wasm-tools
bash scripts/publish-browser.sh
python3 -m http.server 8080 --directory artifacts/site
```

访问 `http://localhost:8080/`，不要直接用 `file://` 打开 `index.html`。

[发布脚本](../scripts/publish-browser.sh) 会清空并重建 `artifacts/browser` 和 `artifacts/site`，自动还原浏览器项目、发布 Release 产物并检查静态资源；最终站点位于 `artifacts/site`。可通过 `WORLDBOX_DOTNET=/path/to/dotnet` 指定 SDK 可执行文件。

入口和自有资源使用相对路径，支持项目子路径。发布输出包含 `.nojekyll`，避免 `_framework` 被静态处理忽略。浏览器项目关闭 WebAssembly 多线程与 AOT；保存使用普通 JavaScript Worker，不支持时使用分块回退。

## 发布到 GitHub Pages

仓库已配置 [GitHub Actions 工作流](../.github/workflows/build-and-deploy.yml)。`main` 是 Pages 发布分支；主分支与旧环境的迁移见 [开发约定](development.md#主分支与环境迁移)。

1. 在仓库 **Settings → Pages → Build and deployment** 中，将 **Source** 设置为 **GitHub Actions**。
2. 如果 **Settings → Environments → github-pages** 设置了部署分支限制，确保允许 `main`；从 `alpha` 迁移时，删除旧的 `alpha` 部署分支规则并添加 `main`。
3. 将代码推送到 `main`，构建及测试通过后自动部署。工作流已存在于默认分支时，也可在 **Actions → Build, test and deploy WorldBox** 中选择 `main` 手动运行。
4. 部署完成后，从工作流的 `github-pages` 环境打开页面。该仓库的标准地址为 [SeWZC.WorldBox 浏览器版](https://SeWZC-AI.github.io/SeWZC.SandboxGame/)；仓库设置和实际部署结果决定最终地址。请访问以 `/` 结尾的项目地址。

首次部署需先完成仓库 Pages 设置。工作流使用 `configure-pages` 检查配置，部署任务仅申请 `pages: write` 与 `id-token: write` 权限；不需要另建 `gh-pages` 分支或在仓库中保存个人访问令牌。

PR 和 `main` 推送执行原生 Release 构建、xUnit 单元测试、浏览器裁剪发布及静态资源检查。只有 `main` 的非 PR 运行在上述任务全部通过后部署。旧浏览器矩阵、完整回归选项与部署后交互检查已删除；当前验证范围和 TRX 报告位置见 [CI 与部署验收](verification.md#ci-与部署验收)。

## 检查仓库子路径

发布后，在仓库根目录准备与 GitHub Pages 一致的子路径：

```bash
mkdir -p artifacts/browser-preview/SeWZC.SandboxGame
cp -a artifacts/site/. artifacts/browser-preview/SeWZC.SandboxGame/
python3 -m http.server 8080 --bind 127.0.0.1 --directory artifacts/browser-preview
```

打开 `http://127.0.0.1:8080/SeWZC.SandboxGame/`。静态资源检查能够发现根路径引用与缺失文件，实际交互、存储、触控和浏览器兼容性仍需浏览器验证；本地预览和当前验证限制见 [浏览器检查](verification.md#浏览器检查)。
