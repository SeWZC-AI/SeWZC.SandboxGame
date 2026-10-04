// Optional scale check: generate a current-format fixture with Core.Tests first.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');
const { UiDriver, testUrl } = require('./ui-driver.cjs');

const fixturePath = process.argv[2] ? path.resolve(process.argv[2])
    : path.resolve(__dirname, '../../artifacts/stress-world-v3.json');
const output = path.resolve(__dirname, '../../artifacts/browser-tests');
const baseUrl = process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/';

(async () => {
    const fixture = JSON.parse(fs.readFileSync(fixturePath, 'utf8'));
    assert.equal(fixture.FormatVersion, 13, 'Generate a fixture for the current alpha version');
    assert.equal(fixture.Width, 256);
    assert(fixture.Residents.length >= 2000, 'Scale scenario must start with at least 2,000 residents');
    fs.mkdirSync(output, { recursive: true });
    const browser = await chromium.launch(chromiumLaunchOptions());
    const context = await browser.newContext({ viewport: { width: 1440, height: 960 } });
    const page = await context.newPage();
    const diagnostics = observeBrowserErrors(page);
    const failedRequests = [];
    page.on('requestfailed', request => failedRequests.push(`${request.url()}: ${request.failure()?.errorText}`));
    page.on('response', response => {
        if (response.status() >= 400) failedRequests.push(`${response.status()} ${response.url()}`);
    });
    try {
        await page.goto(testUrl(baseUrl), { waitUntil: 'domcontentloaded' });
        const ui = new UiDriver(page);
        await ui.ready();
        await ui.paused();
        await ui.click('header-storage');
        const picker = page.waitForEvent('filechooser');
        await ui.click('storage-import', { scroll: 'modal-scroll' });
        await (await picker).setFiles(fixturePath);
        await ui.waitFor(snapshot => !snapshot.modalOpen && snapshot.status?.includes('导入'), 'imported scale world', 60000);
        const before = await ui.save();
        assert.equal(before.Residents.length, fixture.Residents.length);
        assert.equal(before.Seed, fixture.Seed);
        assert.equal(typeof before.Tick, 'number', 'Current saves must preserve zero-valued world state');
        await diagnostics.assertHealthy('scale world before simulation');
        await page.evaluate(() => {
            globalThis.worldboxScaleLongTasks = [];
            globalThis.worldboxScaleObserver = new PerformanceObserver(list => {
                for (const entry of list.getEntries()) globalThis.worldboxScaleLongTasks.push(entry.duration);
            });
            globalThis.worldboxScaleObserver.observe({ type: 'longtask', buffered: false });
        });
        await ui.click('time-speed-1');
        const started = Date.now();
        await ui.paused(false);
        await page.waitForTimeout(15000);
        const runningSnapshot = await ui.snapshot();
        assert(runningSnapshot.worldTick > before.Tick, 'Large world did not advance on screen');
        await ui.paused();
        const elapsedMs = Date.now() - started;
        const longTasks = await page.evaluate(() => {
            globalThis.worldboxScaleObserver.disconnect();
            return globalThis.worldboxScaleLongTasks;
        });
        const after = await ui.save();
        assert(after.Tick > before.Tick, 'Large world did not advance');
        assert(after.Residents.length > 0, 'Large world lost all residents');
        const saveBytes = await page.evaluate(() => new Promise((resolve, reject) => {
            const open = indexedDB.open('sewzc-worldbox', 1);
            open.onerror = () => reject(open.error);
            open.onsuccess = () => {
                const database = open.result;
                const transaction = database.transaction('worlds');
                const request = transaction.objectStore('worlds').get('autosave');
                transaction.oncomplete = () => {
                    database.close();
                    if (typeof request.result !== 'string') reject(new Error('Saved JSON is missing'));
                    else resolve(new Blob([request.result]).size);
                };
                transaction.onerror = () => { database.close(); reject(transaction.error); };
                transaction.onabort = () => { database.close(); reject(transaction.error); };
            };
        }));
        assert(saveBytes <= 64 * 1024 * 1024, 'Current world exceeds the import/export limit');
        await diagnostics.assertHealthy('scale world after simulation and save');
        assert.deepEqual(failedRequests, [], 'Scale run had failed HTTP requests');
        const report = {
            timestamp: new Date().toISOString(), elapsedMs, width: after.Width, height: after.Height,
            tickBefore: before.Tick, tickAfter: after.Tick,
            populationBefore: before.Residents.length, populationAfter: after.Residents.length,
            nations: after.Nations.length, armies: after.Armies.length, saveBytes,
            rememberedFacts: after.Residents.reduce((sum, resident) => sum + resident.Agent.Memory.length, 0),
            longTaskCount: longTasks.length, longestTaskMs: Math.max(0, ...longTasks),
            errors: diagnostics.errors, failedRequests, rendererChecks: diagnostics.rendererChecks,
            limitation: 'Headless Chromium short run; shared CPU and software rendering. Not a real phone or FPS guarantee.'
        };
        await page.screenshot({ path: path.join(output, 'stress.png') });
        fs.writeFileSync(path.join(output, 'stress-report.json'), JSON.stringify(report, null, 2));
        console.log('PASS browser scale scenario', JSON.stringify(report));
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
