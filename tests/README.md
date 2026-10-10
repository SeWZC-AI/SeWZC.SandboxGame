# 单元测试

当前两个项目使用 xUnit 2 和 .NET Test SDK；测试包引用统一维护在本目录的 [Directory.Build.props](Directory.Build.props)，版本由根目录的 [Directory.Packages.props](../Directory.Packages.props) 集中管理。

在仓库根目录执行：

```bash
dotnet restore scripts/ci-build.slnf
dotnet build scripts/ci-build.slnf -c Release --no-restore
dotnet test scripts/ci-build.slnf -c Release --no-build --no-restore
```

测试命令加 `--list-tests` 查看用例，加 `--filter FullyQualifiedName~ResidentEditingTests` 筛选类型；需要 TRX 时加 `--logger trx --results-directory artifacts/unit-tests`。

核心项目覆盖资源字段映射与副本隔离、动物稀疏载荷、研究目录与操作分派、通行与植物规则、原子编辑、研究扣费、现场加工、伤害防护和保存取消。界面项目测试运动插值及研究布局，无需启动窗口或图形会话。

每个 `[Fact]` 或 `[Theory]` 验证一个明确行为。安排最小前置状态，执行操作，检查具体结果；同一行为可检查多个相关状态，例如材料扣除与产物位置。参数化数据的各边界独立报告。非法输入检查异常及提交前后状态，副本隔离检查提交后继续修改草稿。

`WorldFixture` 每次建立独立的 32×32 地图和一名居民，只供世界命令使用。其他规则直接构造被测对象。测试合计应明显低于 10 秒，包含发现、运行器和进程启动，不含还原与构建；用例不按耗时判失败。新发现的缺陷应先写能复现的用例，再修改实现。

长程模拟、真实控件与性能测量独立运行。长程演化使用 [开发约定中的独立工具](../docs/development.md#按改动选择验证)，实际浏览器交互和平台存储检查见 [部署指南](../docs/deployment.md#检查仓库子路径)。
