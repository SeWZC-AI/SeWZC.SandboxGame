const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');
const { UiDriver, testUrl, readSavedWorld } = require('./ui-driver.cjs');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');
const baseUrl = process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/';
assert.ok(process.argv[2], 'Usage: node tests/browser/saving.cjs <fixture.worldbox.json>');
const fixture = path.resolve(process.argv[2]);
const output = path.resolve(process.env.WORLDBOX_ARTIFACT_DIR || 'artifacts/browser-tests');
fs.mkdirSync(output, { recursive: true });

(async () => {
    const browser = await chromium.launch(chromiumLaunchOptions());
    const results = [];
    try {
        for (const fallback of [false, true]) {
            const context = await browser.newContext({ viewport: { width: 1440, height: 960 } });
            try {
                if (fallback) await context.addInitScript(() => { globalThis.Worker = undefined; });
                const page = await context.newPage(), ui = new UiDriver(page), errors = observeBrowserErrors(page);
                const workers = []; page.on('worker', worker => workers.push(worker.url()));
                await page.goto(testUrl(baseUrl)); await ui.ready(); await ui.paused();
                if (!fallback) {
                    await ui.click('header-storage'); const picker = page.waitForEvent('filechooser');
                    await ui.click('storage-import', { scroll: 'modal-scroll' }); await (await picker).setFiles(fixture);
                    await ui.waitFor(s => !s.modalOpen && s.status?.includes('导入'), 'import large save', 60000);
                }
                await ui.click('tool-suspend'); await ui.paused(false);
                await ui.click('header-storage');
                await ui.click('storage-save', { scroll: 'modal-scroll' });
                const capture = await ui.waitFor(s => s.saveCaptureActive, 'cooperative capture', 10000);
                if ((await ui.snapshot()).modalOpen) await ui.click('modal-close');
                const cameraBefore = await ui.snapshot();
                await page.mouse.move(250, 400); await page.mouse.down();
                await page.mouse.move(350, 440, { steps: 12 }); await page.mouse.up();
                const cameraAfter = await ui.snapshot();
                assert.notEqual(cameraAfter.map.tile0CenterX, cameraBefore.map.tile0CenterX, 'Camera could not pan during save');
                let heldSamples = 0;
                await ui.waitFor(s => {
                    if (s.saveCaptureActive) { assert.equal(s.worldTick, capture.worldTick, 'World advanced during capture'); heldSamples++; }
                    return !s.saving;
                }, 'finished buffered save', 30000);
                assert.ok(heldSamples > 0, 'Save did not offer observable event-loop opportunities');
                assert.ok(!(await ui.snapshot()).status?.startsWith('保存失败'), 'Save failed');
                await ui.paused();
                const saved = await readSavedWorld(page);
                assert.equal(saved.Tick, capture.worldTick, 'Stored save combines different simulation days');
                assert.equal(workers.length, fallback ? 0 : 1, 'Worker path or its fallback did not execute');

                // A new edit interrupts the capture and preserves the previous committed save.
                await ui.tool('terrain', 'Grass');
                await ui.click('header-storage'); await ui.click('storage-save', { scroll: 'modal-scroll' });
                await ui.waitFor(s => s.saveCaptureActive, 'capture before editing', 10000);
                if ((await ui.snapshot()).modalOpen) await ui.click('modal-close');
                const map = (await ui.snapshot()).map;
                await page.mouse.click(map.x + map.width * .5, map.y + map.height * .45);
                await ui.waitFor(s => !s.saving && s.status?.includes('更新'), 'edit canceled capture', 30000);
                assert.deepEqual(await readSavedWorld(page), saved, 'Canceled capture overwrote the last valid save');
                await errors.assertHealthy(fallback ? 'save without worker support' : 'worker save');
                results.push({ fallback, workers, heldSamples, capturedTick: capture.worldTick, population: saved.Residents.length, errors: errors.errors });
                console.log('PASS', fallback ? 'buffered save fallback' : 'worker save, responsive camera and edit cancellation');
            } finally { await context.close(); }
        }
    } finally { await browser.close(); }
    fs.writeFileSync(path.join(output, 'saving-report.json'), JSON.stringify(results, null, 2));
})().catch(error => { console.error(error); process.exit(1); });
