// Optional investigation: same saved worlds, real speed buttons and independent runs.
// An isolated build from scripts/profile-browser-stages.py supplies managed timings.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');
const { UiDriver, testUrl } = require('./ui-driver.cjs');
assert.ok(process.argv[2], 'Usage: node tests/browser/five-speed-profile.cjs <fixture.worldbox.json> [output-directory]');
const fixturePath = path.resolve(process.argv[2]);
const output = path.resolve(process.argv[3] || 'artifacts/speed-investigation');
const baseUrl = process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/';
const cases = (process.env.WORLDBOX_PROFILE_CASES || 'default-far-1,default-far-5,default-near-5,large-far-1,large-far-5,large-near-5,large-auto-5').split(',');
const seconds = Number(process.env.WORLDBOX_PROFILE_SECONDS || 15);
assert.ok(Number.isFinite(seconds) && seconds > 0, 'WORLDBOX_PROFILE_SECONDS must be a positive number');
const supportedCases = new Set(['default-far-1', 'default-far-5', 'default-near-5', 'large-far-1', 'large-far-5', 'large-near-5', 'large-auto-5']);
assert.ok(cases.every(name => supportedCases.has(name)), 'WORLDBOX_PROFILE_CASES contains an unsupported case');
const large = JSON.parse(fs.readFileSync(fixturePath, 'utf8'));
fs.mkdirSync(output, { recursive: true });
const defaultFixture = process.env.WORLDBOX_PROFILE_DEFAULT_FIXTURE;

function stats(values) {
    const ordered = [...values].sort((a, b) => a - b);
    const percentile = p => ordered[Math.max(0, Math.ceil(p * ordered.length) - 1)] || 0;
    return { count: ordered.length, totalMs: values.reduce((a, b) => a + b, 0), meanMs: values.reduce((a, b) => a + b, 0) / Math.max(1, values.length), p50Ms: percentile(.5), p95Ms: percentile(.95), maxMs: percentile(1) };
}

(async () => {
    const browser = await chromium.launch(chromiumLaunchOptions());
    const reports = [];
    let defaultWorld = defaultFixture ? JSON.parse(fs.readFileSync(defaultFixture, 'utf8')) : undefined;
    if (defaultWorld) fs.writeFileSync(path.join(output, 'default.worldbox.json'), JSON.stringify(defaultWorld));
    try {
        for (const name of cases) {
            const [scale, view, speedText] = name.split('-'); const speed = Number(speedText);
            const mobile = view === 'near';
            const duration = view === 'auto' ? 38 : seconds;
            const context = await browser.newContext({ viewport: mobile ? { width: 390, height: 844 } : { width: 1440, height: 960 }, isMobile: mobile, hasTouch: mobile });
            try {
                const page = await context.newPage(); const diagnostics = observeBrowserErrors(page); const ui = new UiDriver(page, { touch: mobile });
                console.log('START', name);
                await page.goto(testUrl(baseUrl)); await ui.ready(); await ui.paused();
                if (process.env.WORLDBOX_PROFILE_EXACT_TERRAIN === '1')
                    await page.evaluate(() => worldboxTest.setExactResourceHash(true));
                if (process.env.WORLDBOX_PROFILE_LEGACY_TERRAIN === '1')
                    await page.evaluate(() => worldboxTest.setExactResourceHash(false));
                const savedPath = scale === 'large' ? fixturePath : defaultWorld ? path.join(output, 'default.worldbox.json') : null;
                if (savedPath) {
                    await ui.click('header-storage'); const picker = page.waitForEvent('filechooser');
                    await ui.click('storage-import', { scroll: 'modal-scroll' }); await (await picker).setFiles(savedPath);
                    await ui.waitFor(s => !s.modalOpen && s.status?.includes('导入'), 'import profiling world', 60000);
                }
                if (!defaultWorld && scale === 'default') {
                    defaultWorld = await ui.save(); fs.writeFileSync(path.join(output, 'default.worldbox.json'), JSON.stringify(defaultWorld));
                }
                const world = scale === 'large' ? large : defaultWorld;
                if (mobile) {
                    await ui.openOverview(); await ui.click('inspector-residents');
                    await ui.fill('resident-search', world.Residents[0].Id, { scroll: 'inspector-scroll' });
                    await ui.openResidentRow(world.Residents[0].Id); await ui.click('resident-locate', { scroll: 'inspector-scroll' });
                    for (let i = 0; i < 5; i++) await ui.click('map-zoom-in');
                }
                await ui.click(`time-speed-${speed}`); await ui.paused(false);
                const cdp = await context.newCDPSession(page); await cdp.send('Performance.enable');
                const metricsBefore = (await cdp.send('Performance.getMetrics')).metrics;
                const before = await page.evaluate(() => {
                    globalThis.worldboxTest.resetPerformance?.();
                    globalThis.measurement = { start: performance.now(), frames: [], tasks: [], active: true };
                    const m = measurement;
                    m.observer = new PerformanceObserver(list => m.tasks.push(...list.getEntries().map(e => ({ startMs: e.startTime - m.start, ms: e.duration }))));
                    m.observer.observe({ type: 'longtask', buffered: false });
                    const frame = now => { if (!m.active) return; m.frames.push(now - m.start); requestAnimationFrame(frame); };
                    requestAnimationFrame(frame);
                    return worldboxTest.snapshot();
                });
                await page.waitForTimeout(duration * 1000);
                const raw = await page.evaluate(() => {
                    const m = measurement; m.active = false; m.observer.disconnect();
                    return { elapsedMs: performance.now() - m.start, tasks: m.tasks, frames: m.frames, after: worldboxTest.snapshot(), probe: worldboxTest.performance?.() || null };
                });
                await ui.paused(); const metricsAfter = (await cdp.send('Performance.getMetrics')).metrics;
                await diagnostics.assertHealthy(`5x investigation ${name}`);
                const metricDelta = Object.fromEntries(['TaskDuration', 'ScriptDuration', 'LayoutDuration', 'RecalcStyleDuration'].map(key => [key, 1000 * ((metricsAfter.find(m => m.name === key)?.value || 0) - (metricsBefore.find(m => m.name === key)?.value || 0))]));
                const groups = {};
                if (raw.probe) {
                    assert.equal(raw.probe.overflow, 0, 'Timing buffer overflowed');
                    for (const sample of raw.probe.samples) {
                        const label = raw.probe.names[sample.stage]; (groups[label] ||= []).push(sample.ms);
                    }
                }
                const frameGaps = raw.frames.slice(1).map((time, i) => time - raw.frames[i]);
                const report = { name, speed, baseUrl, population: world.Residents.length, width: world.Width, height: world.Height,
                    exactTerrainCounterfactual: process.env.WORLDBOX_PROFILE_EXACT_TERRAIN === '1',
                    legacyTerrainCounterfactual: process.env.WORLDBOX_PROFILE_LEGACY_TERRAIN === '1',
                    tickBefore: before.worldTick, tickAfter: raw.after.worldTick, elapsedMs: raw.elapsedMs,
                    ticksPerSecond: (raw.after.worldTick - before.worldTick) * 1000 / raw.elapsedMs,
                    longTasks: stats(raw.tasks.map(task => task.ms)), frameGaps: stats(frameGaps), gapsOver100ms: frameGaps.filter(ms => ms >= 100).length,
                    managed: Object.fromEntries(Object.entries(groups).map(([key, values]) => [key, stats(values)])), metricDelta,
                    metricDeltaScope: 'CDP metrics also include observer setup, snapshot/probe export and pause interaction outside the observation window.',
                    tileSize: raw.after.map.tileSize, limitStatus: raw.after.controls.find(c => c.id === 'simulation-status')?.value,
                    errors: diagnostics.errors, limitation: 'Independent headless Chromium software-rendered runs; timing probes add overhead. Frame gaps are animation-frame opportunities, not rendered FPS.' };
                fs.writeFileSync(path.join(output, `${name}-raw.json`), JSON.stringify(raw));
                fs.writeFileSync(path.join(output, `${name}.json`), JSON.stringify(report, null, 2));
                reports.push(report); fs.writeFileSync(path.join(output, 'summary.json'), JSON.stringify(reports, null, 2));
                console.log('RESULT', name, JSON.stringify({ ticksPerSecond: report.ticksPerSecond, longTasks: report.longTasks, frameGaps: report.frameGaps, engine: report.managed['Engine.Step'], wildlife: report.managed['Core.Wildlife'], refresh: report.managed['Map.RefreshWorld'], render: report.managed['Map.Render'], save: report.managed['Save.Serialize'] }));
            } finally { await context.close(); }
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
