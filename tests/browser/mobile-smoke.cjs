const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const output = path.resolve(__dirname, '../../artifacts/browser-tests');
const baseUrl = process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/';
fs.mkdirSync(output, { recursive: true });

(async () => {
    const browser = await chromium.launch({
        ...(process.env.CHROMIUM_EXECUTABLE ? { executablePath: process.env.CHROMIUM_EXECUTABLE } : {}),
        headless: true,
        args: ['--no-sandbox', '--disable-dev-shm-usage', '--enable-unsafe-swiftshader']
    });
    const context = await browser.newContext({
        viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true, deviceScaleFactor: 1
    });
    const page = await context.newPage();
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });

    // Avalonia renders controls on a canvas; coordinates target this fixed viewport.
    const save = async () => {
        await page.touchscreen.tap(295, 34);
        await page.waitForTimeout(150);
        await page.touchscreen.tap(195, 375);
        await page.waitForTimeout(600);
        return page.evaluate(async () => {
            const json = await new Promise((resolve, reject) => {
                const request = indexedDB.open('sewzc-worldbox', 1);
                request.onerror = () => reject(request.error);
                request.onsuccess = () => {
                    const db = request.result;
                    const transaction = db.transaction('worlds');
                    const get = transaction.objectStore('worlds').get('autosave');
                    transaction.oncomplete = () => { db.close(); resolve(get.result); };
                    transaction.onerror = () => { db.close(); reject(transaction.error); };
                    transaction.onabort = () => { db.close(); reject(transaction.error); };
                };
            });
            if (typeof json !== 'string') throw new Error('Manual save did not create an IndexedDB snapshot');
            const state = JSON.parse(json);
            // Compare the entire persisted world without copying 65,536 tiles through Playwright.
            const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(json));
            const hash = Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, '0')).join('');
            return { population: state.Residents.length, tick: state.Tick, hash };
        });
    };

    try {
        await page.goto(baseUrl, { waitUntil: 'domcontentloaded' });
        await page.waitForFunction(() => document.querySelector('canvas')?.width > 0 && !document.querySelector('.loading'), {}, { timeout: 60000 });
        await page.waitForTimeout(1000);
        await page.touchscreen.tap(105, 785); // Pause for the baseline snapshot.
        await page.waitForTimeout(150);
        await page.touchscreen.tap(135, 630); // Life category selects humans.
        await page.waitForTimeout(150);
        const baseline = await save();
        await page.touchscreen.tap(105, 785); // Resume, then let editing pause automatically.
        await page.touchscreen.tap(253, 421); // Walkable land on the default seeded map.
        await page.waitForTimeout(250);
        const spawned = await save();
        assert(spawned.population >= baseline.population + 12, 'Touch placement must add at least 12 residents');
        await page.waitForTimeout(1100);
        const paused = await save();
        assert.equal(paused.tick, spawned.tick, 'Editing must pause simulation');
        console.log('PASS mobile touch placement, IndexedDB save, and automatic pause');

        const client = await context.newCDPSession(page);
        const touch = (type, points) => client.send('Input.dispatchTouchEvent', {
            type, touchPoints: points.map(point => ({ ...point, radiusX: 2, radiusY: 2, force: 1 }))
        });
        await touch('touchStart', [{ id: 1, x: 220, y: 420 }]);
        for (let step = 1; step <= 6; step++) {
            await touch('touchMove', [{ id: 1, x: 220 + step * 8, y: 420 + step * 4 }]);
            await page.waitForTimeout(25);
        }
        await touch('touchEnd', []);
        await page.waitForTimeout(150);
        const dragged = await save();
        assert.equal(dragged.hash, paused.hash, 'Single-finger drag must not edit the world');

        await touch('touchStart', [{ id: 1, x: 140, y: 400 }]);
        await touch('touchStart', [{ id: 1, x: 140, y: 400 }, { id: 2, x: 270, y: 450 }]);
        for (let step = 1; step <= 6; step++) {
            await touch('touchMove', [
                { id: 1, x: 140 - step * 5, y: 400 - step * 2 },
                { id: 2, x: 270 + step * 5, y: 450 + step * 2 }
            ]);
            await page.waitForTimeout(25);
        }
        await touch('touchEnd', []);
        await page.waitForTimeout(150);
        const pinched = await save();
        assert.equal(pinched.hash, dragged.hash, 'Pinch gesture must not edit the world');
        assert.deepEqual(errors, [], 'Browser errors');
        await page.touchscreen.tap(348, 184); // Fit the world for the evidence screenshot.
        await page.waitForTimeout(200);
        await page.screenshot({ path: path.join(output, 'mobile.png') });
        fs.writeFileSync(path.join(output, 'mobile-results.json'), JSON.stringify({
            viewport: { width: 390, height: 844 }, chromiumViewportEmulation: true,
            baseline, spawned, paused, dragUnchanged: true, pinchUnchanged: true, errors
        }, null, 2));
        console.log('PASS mobile drag and pinch preserve the world; no browser errors');
    } catch (error) {
        await page.screenshot({ path: path.join(output, 'mobile-failure.png') }).catch(() => {});
        throw error;
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
