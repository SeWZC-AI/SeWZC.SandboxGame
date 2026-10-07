const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');
const { UiDriver, testUrl } = require('./ui-driver.cjs');

const baseUrl = process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/';
const expectedRevision = process.env.WORLDBOX_EXPECTED_REVISION;
const output = path.resolve(process.env.WORLDBOX_ARTIFACT_DIR || path.join(__dirname, '../../artifacts/browser-tests'));
fs.mkdirSync(output, { recursive: true });

async function waitForRevision(request) {
    if (!expectedRevision) return;
    assert.match(expectedRevision, /^[0-9a-f]{40}$/, 'Expected a full Git revision');
    const deadline = Date.now() + 60000;
    let last = 'no response';
    do {
        try {
            const url = new URL(baseUrl);
            url.searchParams.set('revision-check', `${expectedRevision}-${Date.now()}`);
            const response = await request.get(url.href, { timeout: 5000 });
            try {
                last = `HTTP ${response.status()}`;
                if (response.ok()) {
                    const html = await response.text();
                    if (html.includes(`<meta name="worldbox-revision" content="${expectedRevision}">`)) return;
                    last = 'HTML belongs to a different revision';
                }
            } finally { await response.dispose(); }
        } catch (error) { last = error.message; }
        await new Promise(resolve => setTimeout(resolve, 2000));
    } while (Date.now() < deadline);
    throw new Error(`Published revision ${expectedRevision} did not become available: ${last}`);
}

(async () => {
    const started = performance.now();
    const browser = await chromium.launch(chromiumLaunchOptions());
    const page = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await page.setExtraHTTPHeaders({ 'Cache-Control': 'no-cache' });
    const diagnostics = observeBrowserErrors(page);
    const ui = new UiDriver(page);
    const modal = { scroll: 'modal-scroll' };
    try {
        await waitForRevision(page.request);
        const response = await page.goto(testUrl(baseUrl), { waitUntil: 'domcontentloaded' });
        assert(response?.ok(), 'Published page did not load successfully');
        await ui.ready();
        const revision = await page.locator('meta[name="worldbox-revision"]').getAttribute('content');
        assert.match(revision, /^[0-9a-f]{40}$/);
        if (expectedRevision) assert.equal(revision, expectedRevision, 'Loaded an older deployed artifact');
        await diagnostics.assertHealthy('deployed startup');

        // 使用不同的小世界核对刷新后是否读取 IndexedDB 存档。
        await ui.click('header-new-world');
        await ui.fill('world-seed', 13579, modal);
        await ui.selectIndex('world-size', 0, modal);
        await ui.click('world-create-apply', modal);
        await ui.waitFor(snapshot => !snapshot.modalOpen, 'new world');
        const initialTick = (await ui.snapshot()).worldTick;
        await ui.paused(false);
        await ui.waitFor(snapshot => snapshot.worldTick > initialTick, 'live simulation advancement');
        await ui.paused();
        const saved = await ui.save();
        assert.equal(saved.Seed, 13579);
        assert.equal(saved.Width, 128);
        assert(saved.Residents.length > 0);

        await page.reload({ waitUntil: 'domcontentloaded' });
        await ui.ready();
        assert((await ui.snapshot()).status.includes('已恢复本机世界'), 'Startup did not restore IndexedDB');
        await ui.paused();
        const restored = await ui.save();
        assert.equal(restored.Seed, saved.Seed);
        assert.equal(restored.Width, saved.Width);
        assert.equal(restored.FormatVersion, saved.FormatVersion);
        assert(restored.Tick >= saved.Tick);
        assert(restored.Residents.length > 0);
        await diagnostics.assertHealthy('deployed save recovery');
        await page.screenshot({ path: path.join(output, 'deploy-smoke.png') });
        const report = { url: baseUrl, revision, seconds: (performance.now() - started) / 1000,
            savedTick: saved.Tick, restoredTick: restored.Tick, rendererChecks: diagnostics.rendererChecks };
        fs.writeFileSync(path.join(output, 'deploy-smoke.json'), JSON.stringify(report, null, 2));
        console.log('PASS deployed revision, rendering, simulation and IndexedDB recovery', JSON.stringify(report));
    } catch (error) {
        await page.screenshot({ path: path.join(output, 'deploy-smoke-failure.png') }).catch(() => {});
        throw error;
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
