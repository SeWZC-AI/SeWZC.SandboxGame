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
        const cases = [
            { name: 'worker', fallback: false, compression: true }, { name: 'fallback', fallback: true, compression: true },
            { name: 'uncompressed', fallback: true, compression: false }
        ];
        const selected = process.env.WORLDBOX_SAVE_CASES?.split(',') || cases.map(c => c.name);
        assert(selected.every(name => cases.some(c => c.name === name)), 'Unknown WORLDBOX_SAVE_CASES entry');
        for (const { fallback, compression } of cases.filter(c => selected.includes(c.name))) {
            const context = await browser.newContext({ viewport: { width: 1440, height: 960 } });
            try {
                if (fallback) await context.addInitScript(() => { globalThis.Worker = undefined; });
                else await context.addInitScript(() => {
                    const WorkerType = globalThis.Worker;
                    globalThis.saveTransfer = { chunks: 0, characters: 0, suffix: '' };
                    globalThis.Worker = class extends WorkerType {
                        postMessage(data, ...args) {
                            if (data.op === 'append') {
                                saveTransfer.chunks++;
                                saveTransfer.characters += data.chunk.length;
                                saveTransfer.suffix = data.chunk.slice(-100);
                            }
                            return super.postMessage(data, ...args);
                        }
                    };
                });
                if (!compression) await context.addInitScript(() => {
                    globalThis.CompressionStream = undefined;
                    // 同时检查让出执行的回退路径和未压缩 Blob 存储。
                    Object.defineProperty(globalThis, 'scheduler', { value: undefined, configurable: true });
                });
                const page = await context.newPage(), ui = new UiDriver(page), errors = observeBrowserErrors(page);
                const workers = []; page.on('worker', worker => workers.push(worker.url()));
                await page.goto(testUrl(baseUrl)); await ui.ready(); await ui.paused();
                if (compression) {
                    await ui.click('header-storage'); const picker = page.waitForEvent('filechooser');
                    await ui.click('storage-import', { scroll: 'modal-scroll' }); await (await picker).setFiles(fixture);
                    await ui.waitFor(s => !s.modalOpen && s.status?.includes('导入'), 'import large save', 60000);
                }
                await ui.click('tool-suspend'); await ui.paused(false);
                await page.evaluate(() => {
                    globalThis.saveTasks = [];
                    globalThis.saveObserver = new PerformanceObserver(list => saveTasks.push(...list.getEntries().map(e => e.duration)));
                    saveObserver.observe({ type: 'longtask', buffered: false });
                });
                await ui.click('header-storage');
                await ui.click('storage-save', { scroll: 'modal-scroll' });
                const capture = await ui.waitFor(s => s.saveCaptureActive, 'cooperative capture', 10000);
                const saveStarted = performance.now(), samples = [];
                console.log('START SAVE', { fallback, compression, tick: capture.worldTick });
                if ((await ui.snapshot()).modalOpen) await ui.click('modal-close');
                const cameraBefore = await ui.snapshot();
                await page.mouse.move(250, 400); await page.mouse.down();
                await page.mouse.move(350, 440, { steps: 12 }); await page.mouse.up();
                const cameraAfter = await ui.snapshot();
                assert.notEqual(cameraAfter.map.tile0CenterX, cameraBefore.map.tile0CenterX, 'Camera could not pan during save');
                let heldSamples = 1; // 捕获快照时已观察到一次界面让出。
                await ui.waitFor(s => {
                    samples.push({ ms: performance.now() - saveStarted, capture: s.saveCaptureActive, tick: s.worldTick });
                    if (samples.length % 100 === 0) console.log('SAVING', samples.at(-1));
                    if (s.saveCaptureActive) { assert.equal(s.worldTick, capture.worldTick, 'World advanced during capture'); heldSamples++; }
                    return !s.saving;
                }, 'finished buffered save', 90000).catch(async error => {
                    fs.writeFileSync(path.join(output, 'saving-failure.json'), JSON.stringify({ fallback, compression, samples, transfer: await page.evaluate(() => globalThis.saveTransfer) }, null, 2));
                    throw error;
                });
                const saveMilliseconds = performance.now() - saveStarted;
                assert.ok(heldSamples > 0, 'Save did not offer observable event-loop opportunities');
                const status = (await ui.snapshot()).status;
                assert.ok(!status?.startsWith('保存失败'), status || 'Save failed');
                const longTasks = await page.evaluate(() => { saveObserver.disconnect(); return saveTasks; });
                await ui.paused();
                const storage = await page.evaluate(() => new Promise((resolve, reject) => {
                    const open = indexedDB.open('sewzc-worldbox', 1);
                    open.onerror = () => reject(open.error);
                    open.onsuccess = () => {
                        const database = open.result, transaction = database.transaction('worlds');
                        const request = transaction.objectStore('worlds').get('autosave');
                        transaction.oncomplete = () => {
                            database.close(); const record = request.result;
                            resolve({ encoding: record.encoding, jsonBytes: record.bytes, storedBytes: record.data.size });
                        };
                        transaction.onerror = () => reject(transaction.error);
                    };
                }));
                console.log('STORAGE', storage, await page.evaluate(() => globalThis.saveTransfer));
                const saved = await readSavedWorld(page);
                assert.equal(storage.encoding, compression ? 'gzip' : 'utf-8');
                if (compression) assert(storage.storedBytes < storage.jsonBytes * .5, 'Storage did not meaningfully compress the save');
                assert.equal(saved.Tick, capture.worldTick, 'Stored save combines different simulation days');
                assert.equal(workers.length, fallback ? 0 : 1, 'Worker path or its fallback did not execute');

                // 新编辑中断捕获，保留上次已提交存档。
                await ui.tool('terrain', 'Grass');
                const paintPoint = await ui.tilePoint(Math.floor(saved.Width / 2), Math.floor(saved.Height / 2));
                await ui.click('header-storage'); await ui.click('storage-save', { scroll: 'modal-scroll' });
                const editing = await ui.waitFor(s => s.saveCaptureActive, 'capture before editing', 10000);
                assert.equal(editing.activeTool, 'Grass'); assert.equal(editing.modalOpen, false);
                await page.mouse.click(paintPoint.x, paintPoint.y, { delay: 80 });
                await ui.waitFor(s => !s.saving && s.status?.includes('更新'), 'edit canceled capture', 30000).catch(async error => {
                    fs.writeFileSync(path.join(output, 'editing-failure.json'), JSON.stringify({ editing, after: await ui.snapshot(), paintPoint }, null, 2));
                    await page.screenshot({ path: path.join(output, 'editing-failure.png') });
                    throw error;
                });
                assert.deepEqual(await readSavedWorld(page), saved, 'Canceled capture overwrote the last valid save');
                await ui.click('header-storage'); // 检查损坏元数据时推迟自动重试。
                // 元数据损坏时仍保留上次完整存档。
                await page.evaluate(async () => {
                    const storage = await import('./storage.js');
                    const open = indexedDB.open('sewzc-worldbox', 1);
                    const database = await new Promise((resolve, reject) => {
                        open.onsuccess = () => resolve(open.result); open.onerror = () => reject(open.error);
                    });
                    const transaction = database.transaction('worlds');
                    const request = transaction.objectStore('worlds').get('autosave');
                    const original = await new Promise((resolve, reject) => {
                        transaction.oncomplete = () => resolve(request.result); transaction.onerror = () => reject(transaction.error);
                    });
                    const put = value => new Promise((resolve, reject) => {
                        const transaction = database.transaction('worlds', 'readwrite');
                        transaction.objectStore('worlds').put(value, 'autosave');
                        transaction.oncomplete = resolve; transaction.onerror = () => reject(transaction.error);
                    });
                    try {
                        for (const bytes of [original.bytes - 1, 64 * 1024 * 1024 + 1]) {
                            await put({ ...original, bytes });
                            let rejected = false;
                            try { await storage.load(); } catch { rejected = true; }
                            if (!rejected) throw new Error('Decoder accepted corrupt save size');
                        }
                    } finally { await put(original); database.close(); }
                    const id = storage.beginSave(); storage.appendSave(id, '{unfinished'); storage.discardSave(id);
                    let rejected = false;
                    try { await storage.commitSave(id); } catch { rejected = true; }
                    if (!rejected) throw new Error('Discarded save was committed');
                });
                assert.deepEqual(await readSavedWorld(page), saved, 'Aborted storage changed the last complete save');
                let automatic;
                if (!fallback) {
                    await ui.click('modal-close');
                    const completed = await ui.waitFor(s => !s.saving && s.status?.startsWith('已自动保存'), 'automatic save after edit', 60000);
                    const automaticWorld = await readSavedWorld(page);
                    assert.equal(automaticWorld.Tick, completed.worldTick, 'Paused automatic save lost the current day');
                    const transfers = await page.evaluate(() => saveTransfer.chunks);
                    for (let check = 0; check < 11; check++) {
                        await page.waitForTimeout(3000);
                        const unchanged = await ui.snapshot();
                        assert(!unchanged.saving && !unchanged.saveCaptureActive, 'Unchanged paused world triggered another capture');
                    }
                    assert.equal(await page.evaluate(() => saveTransfer.chunks), transfers, 'Unchanged world was stored again');
                    assert.deepEqual(await readSavedWorld(page), automaticWorld, 'Skipped save changed stored state');
                    automatic = { tick: automaticWorld.Tick, unchangedObservationMilliseconds: 33000 };
                    console.log('PASS automatic save and unchanged-world skip');
                }
                await errors.assertHealthy(fallback ? 'save without worker support' : 'worker save');
                results.push({ fallback, compression, workers, storage, longTasks, saveMilliseconds, samples, heldSamples, automatic, capturedTick: capture.worldTick, population: saved.Residents.length, errors: errors.errors });
                console.log('PASS', fallback ? 'buffered save fallback' : 'worker save, responsive camera and edit cancellation');
            } finally { await context.close(); }
        }
    } finally { await browser.close(); }
    fs.writeFileSync(path.join(output, 'saving-report.json'), JSON.stringify(results, null, 2));
})().catch(error => { console.error(error); process.exit(1); });
