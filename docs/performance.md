# 性能说明

本页保留最近的性能结论、适用负载与复现入口。历史调查和插桩工具通过 Git 查阅，逐次 JSON、日志、截图与夹具存放在已忽略的 `artifacts/`。本次文档清理没有重新计时，以下数字保留原测量版本的归属。

## 最近结论：2026-10-06

测量版本基于 `5906ff7af5ceaf4e49b6a6342d642f93fbb75942` 加未提交修改，97 个生产 C# 文件摘要为 `c22f06a02d4a4ce8ad459135a8115b74bd5b05c670663a15314188e9a17a8f86`。基线与优化版共同包含静态研究索引的初始化修复，原始 HEAD 不能单独作为可运行基线。合入注释后的源码没有重新计时，构建及功能验证归属见 [验证记录](verification.md#最近验证)。

动物优化复用栖息地物种掩码、捕食竞争汇总与迁移快照；近景批量取得生态容量及代表物种，按精确绘图输入复用指令。动物周期、增长、捕食、迁移与随机序列保持。

五倍调度采用 64 ms 软预算、最多八日欠账，每次最多执行四个完整模拟日；五倍近景动画按 20 Hz 调度。单步无法中断，软预算及动画调度频率不能当作硬帧时限或实际 FPS。

### 原生串行对照

环境为 Debian 13 x64、.NET SDK 10.0.401／Runtime 10.0.12、4 个可见 CPU、工作站 GC。每版独立三轮，生成、预热、序列化与 UI 不计入步进。

| 场景 | 优化前 ms/日 | 最终 ms/日 | 负载 |
| --- | ---: | ---: | --- |
| 密集森林，仅动物与植物 | 0.754 | 0.431 | 256×256；预热 256 日、测 512 日；降低约 42.8% |
| 上述动物阶段 | 0.683 | 0.368 | 隔离探针阶段；降低约 46.2%，该阶段分配为零 |
| 2,000 人完整模拟 | 6.144 | 5.421 | 16 国、8 战、平坦草地、无灾害；预热 60 日、测 120 日 |

完整模拟最终三轮为 6.266／5.014／4.983 ms/日，分配仍约 1,734 KiB/日。两类负载各六轮的完整保存摘要分别一致且可导入；这只证明这些场景的结果一致。密集生态数值来自历史隔离探针，下面的现行测量入口只测完整模拟。

### 浏览器正式产物

Chromium 151.0.7922.173／Playwright 1.57.0／Node 22.23.3，独立串行 Headless 测量，每场景 15 秒；全图 1440×960，近景 390×844 触屏仿真。

| 五倍场景 | 实际 日/秒 | 动画帧间隔 P95／最大 ms |
| --- | ---: | ---: |
| 默认全图 | 25.03 | 133.3／166.6 |
| 默认近景 | 22.65 | 83.4／166.6 |
| 2,000 人全图 | 5.40 | 200.1／250.0 |

默认全图接近五倍的 25 日/秒目标；近景和压力世界仍未达到目标。动画帧机会不等于实际绘制 FPS，云环境和短时观测也不能保证真机或任意人口的性能。压力世界仍主要受居民行动与通信成本限制。

## 复现测量

先按改动范围通过功能检查，完成 Release 构建与浏览器发布，再串行测量；不要与构建、其他测试或模拟负载并发。详细功能与发布命令见 [验证记录](verification.md#复现命令)。

完整原生模拟与分块保存有现成入口，无需改写源码：

```bash
dotnet tests/SeWZC.WorldBox.Core.Tests/bin/Release/net10.0/SeWZC.WorldBox.Core.Tests.dll \
  --profile-simulation --population 2000 --warmup 60 --ticks 120 --repetitions 3 \
  --output artifacts/performance/simulation.json
dotnet tests/SeWZC.WorldBox.Core.Tests/bin/Release/net10.0/SeWZC.WorldBox.Core.Tests.dll \
  --profile-save --output artifacts/performance/save.json
```

`--profile-simulation` 记录每步墙钟时间、分配、GC、进程 CPU 和终态保存摘要；CPU 包含该进程的工作线程，不能当作某个方法的 CPU 占比。`--profile-save` 使用 256×256、2,000 人受控世界，预热一次并测三次分块捕获，记录大小、同步切片、让出次数和分配。原生结果不能换算为浏览器帧率。

站点按 [浏览器检查](verification.md#浏览器检查) 运行后，生成同一大世界夹具并测正式产物：

```bash
dotnet tests/SeWZC.WorldBox.Core.Tests/bin/Release/net10.0/SeWZC.WorldBox.Core.Tests.dll \
  --export-browser-fixture artifacts/performance/large.worldbox.json
WORLDBOX_PROFILE_CASES=default-far-5,default-near-5,large-far-5 \
  node tests/browser/five-speed-profile.cjs \
  artifacts/performance/large.worldbox.json artifacts/performance/browser
```

`WORLDBOX_PROFILE_SECONDS` 设置观察时长（默认 15 秒），`WORLDBOX_PROFILE_DEFAULT_FIXTURE` 指定固定默认世界；站点与浏览器路径使用 `WORLDBOX_BASE_URL`、`CHROMIUM_EXECUTABLE`。对比版本时复用相同默认及大世界存档、视角、时长和环境，分别输出到独立目录。正式产物记录推进、长任务与动画帧机会；没有插桩时不提供托管阶段耗时。

保存的真实压缩、取消、Worker／压缩 API 回退及自动保存通过 [saving.cjs](../tests/browser/saving.cjs) 验证。性能报告记录提交或源码摘要、环境、种子、规模、配置及全部复测，避免用单次最好结果或旧版本结论替代当前测量。
