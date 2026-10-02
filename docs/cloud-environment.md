# 云开发环境配置

本页用于配置这个仓库的 Linux 云开发环境。仓库初始化脚本与 GitHub Actions 的构建任务共用；平台配置仍需在云环境配置页面应用，提交本页不会自动修改平台设置。

## 运行时与检出

- 仓库：`SeWZC-AI/SeWZC.SandboxGame`，检出分支：`main`。不要固定旧的 `alpha` 分支；产品阶段仍为 alpha。
- 使用 glibc Linux x64 或 ARM64 运行时；建议 Ubuntu 24.04。运行时需包含 .NET 所需的 ICU、OpenSSL、zlib、libstdc++ 等系统库，并预装 Bash、Git、curl、tar、sha512sum、getconf、Python 3.10 或更新版本、Node.js 22 或更新版本及 npm。
- 浏览器检查需要 Chromium 系统依赖。运行时需已包含依赖，或允许 Playwright 的 `--with-deps` 使用 root／免密 sudo 安装。
- .NET SDK 由 [global.json](../global.json) 固定，Avalonia 与应用版本由 [Directory.Build.props](../Directory.Build.props) 固定；不另外维护不同版本的环境配置。

初始化命令在仓库根目录运行：

```bash
bash scripts/setup-cloud-environment.sh
```

脚本从 Microsoft 发布清单下载固定 SDK，校验 SHA-512，并安装到仓库的 `.dotnet/`；随后安装 `wasm-tools`、恢复 NuGet 与 npm 依赖、安装 Chromium 及系统依赖。再次执行复用已有 SDK。SDK、工具缓存和浏览器都保留在仓库可写目录，已被 Git 忽略，不写入系统 SDK 目录。

初始化脚本的导出变量不会自动传入之后的新终端。每个新 shell 在构建、发布或浏览器检查前执行：

```bash
source artifacts/cloud/environment.sh
```

该文件只包含工具路径和非敏感设置；不存放凭据，也不替换继承的代理与 CA 配置。CI 通过 GitHub Actions 的环境文件传递相同设置。

## 网络与身份

保留受限网络与包管理器预设，再按实际下载地址配置允许的目的主机：

| 用途 | 主机 |
| --- | --- |
| 固定 SDK 发布清单与压缩包 | `builds.dotnet.microsoft.com` |
| NuGet 与 WASM 工作负载 | `api.nuget.org`、`globalcdn.nuget.org` |
| npm | `registry.npmjs.org` |
| Playwright Chromium | `cdn.playwright.dev`、`playwright.download.prss.microsoft.com` |
| Git 仓库及 GitHub API 管理 | `github.com`、`api.github.com` |
| 实际 Pages 验证 | `sewzc-ai.github.io` |

系统依赖还需要运行时配置的 Linux 包镜像；优先沿用包管理器预设。若下载发生重定向或使用其他镜像，按代理返回的拒绝地址补充所需目的主机；不关闭 TLS 验证，不取消代理绕过策略。

通过平台支持的身份／凭据绑定配置仓库 Git 访问；管理默认分支、删除分支及 GitHub 环境策略还需要相应的仓库管理权限。GitHub 连接器的代码读写能力不自动赋予云执行器身份，也不代表其接口支持管理操作。令牌不写入源码、初始化脚本或生成的环境文件。

## 验证与故障定位

恢复后的实际云执行器应执行：

```bash
source artifacts/cloud/environment.sh
dotnet --version
dotnet build -c Release --no-restore
dotnet run --project tests/SeWZC.WorldBox.Core.Tests -c Release --no-build
bash scripts/publish-browser.sh
```

浏览器与公网检查按 [验证记录](verification.md#浏览器检查) 运行。CI 通过说明初始化流程在 GitHub runner 中可用；不代替当前云环境的启动、网络、身份和实际执行验证。

`executor_registration_failed` 表示平台执行器尚未注册成功，发生在仓库命令可执行之前。需在平台检查运行时配置并重试／重建实例；若仍失败，保留错误与实例标识交由平台诊断，不能通过改存档、版本号或安装项目依赖声称已修复。

仓库默认分支、云环境检出分支与 Pages 的 `github-pages` 部署分支策略是独立设置。迁移步骤见 [开发约定](development.md#主分支与环境迁移)；需允许 `main` 部署，并以实际部署和公网验证结果确认生效。
