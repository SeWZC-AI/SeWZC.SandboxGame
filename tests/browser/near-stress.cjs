// 移动视口性能对比；两个版本使用同一存档夹具。
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');
const { UiDriver, testUrl } = require('./ui-driver.cjs');
const fixture = path.resolve(process.argv[2]);
const reportPath = path.resolve(process.argv[3] || 'artifacts/near-stress.json');

(async () => {
    const browser = await chromium.launch(chromiumLaunchOptions());
    try {
        const page = await browser.newPage({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });
        const diagnostics = observeBrowserErrors(page); const ui = new UiDriver(page, { touch: true });
        await page.goto(testUrl(process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/'));
        await ui.ready(); await ui.paused(); await ui.click('header-storage');
        const picker = page.waitForEvent('filechooser'); await ui.click('storage-import', { scroll: 'modal-scroll' });
        await (await picker).setFiles(fixture); await ui.waitFor(s => !s.modalOpen, 'import', 60000);
        const before = await ui.save(); await ui.openOverview(); await ui.click('inspector-residents');
        await ui.fill('resident-search', before.Residents[0].Id, { scroll: 'inspector-scroll' });
        await ui.openResidentRow(before.Residents[0].Id); await ui.click('resident-locate', { scroll: 'inspector-scroll' });
        for (let i = 0; i < 5; i++) await ui.click('map-zoom-in');
        await page.evaluate(() => {
            globalThis.tasks = []; globalThis.po = new PerformanceObserver(list => tasks.push(...list.getEntries().map(e => e.duration)));
            po.observe({ type: 'longtask', buffered: false });
        });
        await ui.paused(false); const started = Date.now(); await page.waitForTimeout(15000);
        const running = await ui.snapshot(); await ui.paused();
        const tasks = await page.evaluate(() => { po.disconnect(); return tasks; });
        await diagnostics.assertHealthy('mobile near scale check');
        const report = { elapsedMs: Date.now() - started, tickBefore: before.Tick, tickAfter: running.worldTick,
            longTaskCount: tasks.length, totalLongTaskMs: tasks.reduce((a, b) => a + b, 0), longestTaskMs: Math.max(0, ...tasks),
            tileSize: running.map.tileSize, viewport: [390, 844], errors: diagnostics.errors,
            limitation: 'Headless Chromium mobile viewport, not actual Android hardware or an FPS guarantee' };
        fs.mkdirSync(path.dirname(reportPath), { recursive: true }); fs.writeFileSync(reportPath, JSON.stringify(report, null, 2));
        console.log(JSON.stringify(report));
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
