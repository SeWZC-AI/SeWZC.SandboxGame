# 构建与验证记录

记录日期：2026-10-01。以下描述当前原型的实际检查结果，不代表所有浏览器或设备都已通过验收。

## 已完成

| 检查 | 结果与覆盖范围 |
| --- | --- |
| 核心行为验证 | 13 / 13 场景通过，包含种子复现、世界推进、存档续算、地形编辑、灾害、粮食与战争的实际影响 |
| 非法存档 | 27 种无效输入被拒绝，覆盖格式、数值、尺寸和实体关系等约束 |
| 确定性恢复 | 包含军队行军、火灾与运输中的存档；恢复后与连续模拟结果一致 |
| 陆路约束 | 不可通行海域阻止行军及占领，不通过瞬移绕过地理屏障 |
| 国家编辑一致性 | 资源写入权威聚落库存；领土转移与聚落独立维护实体归属；在已有领土投放其他种族加入原有国家 |
| 原生模拟基准 | 完成 256×256 地图、约 2,000 名初始居民、16 国及 8 场战争的模拟基准；此项不包含浏览器渲染与触控开销 |
| 静态产物 | `publish-browser.sh` 成功生成 `artifacts/site`，资源检查通过，WebAssembly 载荷和指纹引用完整 |
| Chromium 启动 | 实际发布目录在 `/SeWZC.SandboxGame/` 子路径启动成功，已打开界面截图，应用所需资源无缺失 |
| 桌面浏览器交互 | 1440×960 窗口实际执行 IndexedDB 保存、投放 12 位居民并保持暂停时间、地形绘制、整轮撤销、下载导出、文件选择导入、无效导入保留旧世界、刷新恢复存档；全部通过，未记录 JavaScript 错误 |
| 界面渲染 | Chromium 桌面 1440×960 与触屏模拟尺寸 390×844 均显示中文、地图和菜单，加载遮罩正常消失；此项不等于真实手机验收 |
| 触屏模拟交互 | 390×844 Chromium 触屏上下文完成居民投放与 IndexedDB 保存，暂停时间保持稳定；CDP 单指平移及双指捏合前后的完整世界存档一致，手势没有误编辑世界；未记录 JavaScript 错误 |
| CI 软件回退复现 | 禁用 WebGL 后重新运行完整桌面与触屏检查，两套均通过；实际 2D 画布在启动及最终交互后均有非透明多色像素，桌面刷新后重新取证。已额外检查 `pageerror`、近似诊断文本及无关 `console.error` 均被拒绝 |
| 浏览器短时压力 | 软件 WebGL 的 Chromium 中，256×256 地图、2,000 名初始居民、16 国及 8 场战争运行 15 秒，世界从 tick 6 推进到 76，人口增至 2,126，保持 16 国 / 16 支军队；控制台、页面异常、HTTP 请求失败及世界状态校验均无错误 |

软件 WebGL 压力场景记录到主线程长任务，最长约 441 ms（统计包含保存操作）。短时模拟能够推进，但该结果不支持 30 FPS 或真实手机流畅运行的结论。

## 复现命令

在安装固定 SDK 与 `wasm-tools` 后，从仓库根目录运行：

```bash
dotnet build -c Release
dotnet run --project tests/SeWZC.WorldBox.Core.Tests -c Release --no-build
dotnet run --project tests/SeWZC.WorldBox.Core.Tests -c Release --no-build -- --benchmark
bash scripts/publish-browser.sh
```

测试程序任何场景失败都会返回非零退出码。`--benchmark` 在行为测试成功后执行原生基准，输出本机测量结果；比较不同修改时应使用相同机器、构建配置及负载，不应直接将其作为 WebAssembly 或手机帧率。

可生成同样规模的存档，在浏览器里手动导入进行后续测量：

```bash
dotnet run --project tests/SeWZC.WorldBox.Core.Tests -c Release --no-build -- \
  --export-browser-fixture artifacts/stress-world.json
```

此选项只生成并校验场景，不运行完整测试套件。场景以平坦草地、充足资源及固定八场战争隔离人口与军队负载，不覆盖复杂山海地形或长期资源匮乏情况。

## 浏览器检查

浏览器脚本位于 `tests/browser/smoke.cjs`。先发布站点，再安装固定版本依赖：

```bash
npm ci --prefix tests/browser
cd tests/browser
npx playwright install --with-deps chromium
cd ../..
mkdir -p artifacts/browser-preview/SeWZC.SandboxGame
cp -a artifacts/site/. artifacts/browser-preview/SeWZC.SandboxGame/
python3 -m http.server 8080 --bind 127.0.0.1 --directory artifacts/browser-preview
```

保持 HTTP 服务运行，在另一终端的仓库根目录执行：

```bash
npm test --prefix tests/browser
npm run test:mobile --prefix tests/browser
```

默认地址为 `http://127.0.0.1:8080/SeWZC.SandboxGame/`。可通过以下环境变量覆盖：

| 变量 | 用途 |
| --- | --- |
| `WORLDBOX_BASE_URL` | 已运行站点的完整地址，包含项目子路径及结尾 `/` |
| `CHROMIUM_EXECUTABLE` | 使用已有 Chromium 的可执行文件路径；未设置时使用 Playwright 安装的版本 |
| `WORLDBOX_TEST_DISABLE_WEBGL` | 仅在值为 `1` 时为测试浏览器禁用 WebGL，用于复现软件渲染回退；默认不禁用，不改变应用渲染优先顺序 |

例如使用本机已有的 Chromium：

```bash
WORLDBOX_BASE_URL=http://127.0.0.1:8080/SeWZC.SandboxGame/ \
CHROMIUM_EXECUTABLE=/usr/bin/chromium npm test --prefix tests/browser
```

检查在独立浏览器上下文中运行，使用示例世界，不操作日常浏览器中的存档。截图、导出及导入样本写入 `artifacts/browser-tests`；CI 额外保存 HTTP 与测试日志，并在成功或失败时上传 `worldbox-browser-tests`。

Avalonia 使用画布呈现界面，因此桌面脚本固定 1440×960 视口并按坐标点击；按下与松开间隔 80 ms，以跨越输入分发过程。触屏脚本 `mobile-smoke.cjs` 固定 390×844 视口并派发触摸手势。界面布局变化后需要同步检查这些坐标；脚本会读取实际世界存档验证行为，不能只凭截图判断成功。

CI 机器可能无法创建 WebGL2 / WebGL 上下文，Avalonia 会记录后端诊断并转用软件渲染。共享检查器只接受以下两条完整匹配的 `console.error` 文本；所有 `pageerror` 和其他 `console.error` 仍导致失败：

```text
Failed to create render target for mode 3 : HTMLCanvasElement.getContext returned null.
Failed to create render target for mode 2 : HTMLCanvasElement.getContext returned null.
```

观察到这些诊断时，检查器必须从当前页面的 `#out canvas.avalonia-canvas` 获取实际 2D 像素，至少采到 32 个非透明点和 8 种 RGB 颜色，才允许测试通过。刷新会重置当前页面的诊断状态，刷新后的画布需重新检查，避免使用旧页面的绘制证据。桌面与触屏测试在启动和最后交互后执行检查，记录渲染器及像素采样信息；原有模拟、编辑、存档和触屏断言全部保留。

部署任务成功后，独立的在线验证任务会以实际 GitHub Pages URL 重新执行桌面及触屏检查，并上传 `worldbox-live-browser-tests`，用于验证真实公网部署。

## 限制与后续验证

- 尚未在真实 Android / iOS 手机上测量运行性能、内存与存储行为。
- Chromium 的手机尺寸模拟不能替代移动 Safari / Chrome 真机验证；Firefox 与 Safari 仍需检查。
- 原生人口基准不包含渲染开销；浏览器短时压力运行不等于帧率、首次下载时间、复杂地形或长期稳定性验收。
- 存档仍依赖本设备与当前站点的存储；需持续检查配额耗尽、多标签页及浏览器关闭等情形。
- 远端 GitHub Pages 由 `alpha` 分支的工作流发布；本记录中的本地子路径验证不替代远端验收，实际部署结果以对应 Actions 运行和 `github-pages` 环境为准。首次部署需要 Pages 使用 GitHub Actions，并允许 `alpha` 分支进入该环境。
- 独立文化 / 文明模型、完整科技树、魔法、海战与多人联机不在本次已实现范围内。
