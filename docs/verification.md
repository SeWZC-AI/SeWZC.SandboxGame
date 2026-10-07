# 构建与验证

本页维护检查入口、CI 策略和覆盖范围。逐次结果随交付报告，日志与 TRX 留在 `artifacts/` 或 Actions 产物中。

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

- 单元覆盖资源隔离、通行与植物、研究目录／命令／操作、现场生产、防护、居民与国家编辑、保存格式与取消，以及运动插值和研究布局。
- 未覆盖完整战争、灾害、通信链、自主演化、长期保存续演和大规模人口平衡；删除旧套件后尚无这些场景的替代自动验收。
- 单元不覆盖实际控件事件、浏览器输入／触屏及平台落盘。
- 真机、Firefox／Safari、长期性能和线上部署均未验证。历史性能结果的源码归属见 [性能说明](performance.md)。
