const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { createHash } = require('node:crypto');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');
const { UiDriver, testUrl } = require('./ui-driver.cjs');

const output = process.env.WORLDBOX_ARTIFACT_DIR ? path.resolve(process.env.WORLDBOX_ARTIFACT_DIR) : path.resolve(__dirname, '../../artifacts/browser-tests');
const baseUrl = process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/';
fs.mkdirSync(output, { recursive: true });
const digest = world => createHash('sha256').update(JSON.stringify(world)).digest('hex');
const summary = world => ({ population: world.Residents.length, tick: world.Tick, hash: digest(world) });

(async () => {
    const browser = await chromium.launch(chromiumLaunchOptions());
    const context = await browser.newContext({
        viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true, deviceScaleFactor: 1
    });
    const page = await context.newPage();
    const diagnostics = observeBrowserErrors(page);
    const ui = new UiDriver(page, { touch: true });
    try {
        await page.goto(testUrl(baseUrl), { waitUntil: 'domcontentloaded' });
        await ui.ready();
        await ui.paused();
        await diagnostics.assertHealthy('mobile startup');
        const before = await ui.save();
        const home = before.Settlements[0];
        await ui.tool('life', 'Human');
        await ui.paused(false);
        await ui.clickTile(home.X, home.Y);
        const spawned = await ui.save();
        assert(spawned.Residents.length >= before.Residents.length + 12);
        assert.equal((await ui.snapshot()).paused, true, 'Editing must automatically pause');
        await page.waitForTimeout(1100);
        const paused = await ui.save();
        assert.equal(paused.Tick, spawned.Tick);
        console.log('PASS mobile touch placement, IndexedDB save, and automatic pause');

        const client = await context.newCDPSession(page);
        const touch = (type, points) => client.send('Input.dispatchTouchEvent', {
            type, touchPoints: points.map(point => ({ ...point, radiusX: 2, radiusY: 2, force: 1 }))
        });
        const snapshot = await ui.snapshot();
        const x = snapshot.map.x + snapshot.map.width * 0.53;
        const y = Math.min(snapshot.map.y + snapshot.map.height * 0.47, ui.control(snapshot, 'tool-category-life').y - 100);
        await touch('touchStart', [{ id: 1, x, y }]);
        for (let step = 1; step <= 6; step++) {
            await touch('touchMove', [{ id: 1, x: x + step * 8, y: y + step * 4 }]);
            await page.waitForTimeout(25);
        }
        await touch('touchEnd', []);
        await page.waitForTimeout(180);
        const draggedMap = (await ui.snapshot()).map;
        assert(draggedMap.tile0CenterX !== snapshot.map.tile0CenterX || draggedMap.tile0CenterY !== snapshot.map.tile0CenterY,
            'Single-finger drag must actually move the camera');
        const dragged = await ui.save();
        assert.equal(digest(dragged), digest(paused), 'Single-finger drag must not edit the world');
        await touch('touchStart', [{ id: 1, x: x - 55, y: y - 20 }]);
        await touch('touchStart', [{ id: 1, x: x - 55, y: y - 20 }, { id: 2, x: x + 55, y: y + 20 }]);
        for (let step = 1; step <= 6; step++) {
            await touch('touchMove', [
                { id: 1, x: x - 55 - step * 5, y: y - 20 - step * 2 },
                { id: 2, x: x + 55 + step * 5, y: y + 20 + step * 2 }
            ]);
            await page.waitForTimeout(25);
        }
        await touch('touchEnd', []);
        await page.waitForTimeout(180);
        const pinchedMap = (await ui.snapshot()).map;
        assert(pinchedMap.tileSize > draggedMap.tileSize, 'Outward pinch must actually zoom the map');
        const pinched = await ui.save();
        assert.equal(digest(pinched), digest(dragged), 'Pinch gesture must not edit the world');
        console.log('PASS mobile drag and pinch preserve every persisted world field');

        await ui.click('header-overview');
        await ui.click('inspector-residents');
        const actor = before.Residents[0];
        await ui.click(`resident-row-${actor.Id}`, { scroll: 'inspector-scroll' });
        await ui.click('resident-edit', { scroll: 'inspector-scroll' });
        await ui.fill('resident-name', 'Touch resident', { scroll: 'modal-scroll' });
        const footer = ui.control(await ui.snapshot(), 'resident-apply');
        const footerBounds = [footer.x, footer.y, footer.width, footer.height];
        for (const name of ['condition', 'belonging', 'magic', 'possessions', 'identity']) {
            await ui.click(`resident-tab-${name}`);
            const button = ui.control(await ui.snapshot(), 'resident-apply');
            assert(button.visible && button.enabled);
            assert.deepEqual([button.x, button.y, button.width, button.height], footerBounds, 'Editor tab must not move its submit button');
        }
        await page.setViewportSize({ width: 844, height: 390 });
        await page.waitForTimeout(350);
        assert(ui.control(await ui.snapshot(), 'resident-apply').visible, 'The open editor must adapt when the device rotates');
        assert(ui.control(await ui.snapshot(), 'modal-cancel').visible);
        await page.screenshot({ path: path.join(output, 'mobile-landscape-editor.png') });
        await ui.click('resident-apply');
        await page.setViewportSize({ width: 390, height: 844 });
        await page.waitForTimeout(350);
        const edited = await ui.save();
        assert.equal(edited.Residents.find(item => item.Id === actor.Id).Name, 'Touch resident');
        assert.equal(edited.Tick, pinched.Tick);
        assert.deepEqual(edited.Tiles, pinched.Tiles);
        console.log('PASS mobile resident editing, stable tab actions, and open-dialog rotation');

        const fixedControls = await ui.stableToolLayout();
        await ui.tool('inspect', 'inspect');
        await ui.click('map-fit');
        await page.screenshot({ path: path.join(output, 'mobile.png') });
        await diagnostics.assertHealthy('mobile after interactions');
        fs.writeFileSync(path.join(output, 'mobile-results.json'), JSON.stringify({
            url: baseUrl, viewport: { width: 390, height: 844 }, chromiumViewportEmulation: true,
            baseline: summary(before), spawned: summary(spawned), paused: summary(paused),
            dragUnchanged: true, pinchUnchanged: true, cameraMoved: true, pinchZoomed: true,
            gestureMaps: { before: snapshot.map, dragged: draggedMap, pinched: pinchedMap }, residentEdit: true, dialogRotation: true,
            fixedControls, errors: diagnostics.errors, fallbacks: diagnostics.fallbacks, rendererChecks: diagnostics.rendererChecks
        }, null, 2));
        console.log('ALL MOBILE FUNCTIONAL CHECKS PASSED');
    } catch (error) {
        await page.screenshot({ path: path.join(output, 'mobile-failure.png') }).catch(() => {});
        fs.writeFileSync(path.join(output, 'mobile-failure-ui.json'), JSON.stringify(await ui.snapshot().catch(() => null), null, 2));
        throw error;
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
