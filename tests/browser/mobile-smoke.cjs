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
const chromeIds = ['header-new-world', 'header-storage', 'header-overview', 'header-rules',
    'map-zoom-in', 'map-zoom-out', 'map-fit', 'tools-toggle', 'tool-suspend', 'time-toggle',
    'time-speed-1', 'time-speed-2', 'time-speed-5'];
const chromeLayout = (ui, snapshot) => ({
    size: [snapshot.width, snapshot.height, snapshot.map.x, snapshot.map.y, snapshot.map.width, snapshot.map.height],
    controls: Object.fromEntries(chromeIds.map(id => {
        const control = ui.control(snapshot, id);
        assert(control.visible, `Zoom must keep ${id} visible`);
        return [id, [control.x, control.y, control.width, control.height]];
    }))
});
const viewport = page => page.evaluate(() => ({
    width: innerWidth, height: innerHeight, scale: visualViewport.scale,
    visualWidth: visualViewport.width, visualHeight: visualViewport.height,
    left: visualViewport.offsetLeft, top: visualViewport.offsetTop,
    scrollX, scrollY
}));

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
        const start = await ui.snapshot();
        assert(!start.toolsOpen && !start.inspectorOpen, 'Mobile must start with an unobstructed map');
        const bottom = ui.control(start, 'event-spotlight').y;
        assert((bottom - start.map.y) / start.height >= .75, 'At least 75% of mobile height must be unobstructed map');
        const before = await ui.save();
        const zoomBaseline = await ui.snapshot();
        const fixedChrome = chromeLayout(ui, zoomBaseline);
        const fixedViewport = await viewport(page);
        assert.equal(fixedViewport.scale, 1, 'Mobile must start at the unscaled page viewport');
        await ui.click('map-zoom-in');
        const zoomedIn = await ui.snapshot();
        assert(zoomedIn.map.tileSize > zoomBaseline.map.tileSize, 'Zoom-in button must enlarge the map');
        assert.deepEqual(chromeLayout(ui, zoomedIn), fixedChrome, 'Zoom-in must leave the interface fixed');
        await ui.click('map-zoom-out');
        const zoomedOut = await ui.snapshot();
        assert(zoomedOut.map.tileSize < zoomedIn.map.tileSize, 'Zoom-out button must shrink the map');
        assert.deepEqual(chromeLayout(ui, zoomedOut), fixedChrome, 'Zoom-out must leave the interface fixed');
        const wheelX = zoomedOut.map.x + zoomedOut.map.width / 2;
        const wheelY = zoomedOut.map.y + zoomedOut.map.height / 2;
        await page.mouse.move(wheelX, wheelY);
        await page.mouse.wheel(0, -120);
        await ui.waitFor(s => s.map.tileSize > zoomedOut.map.tileSize, 'wheel map zoom');
        assert.deepEqual(chromeLayout(ui, await ui.snapshot()), fixedChrome, 'Wheel zoom must leave the interface fixed');
        assert.deepEqual(await viewport(page), fixedViewport, 'Zoom controls must not scale or pan the page viewport');
        await ui.click('map-fit');
        assert.deepEqual(await ui.save(), before, 'Map zoom must not change the paused world');
        console.log('PASS mobile zoom buttons and wheel change only the map');
        const home = before.Settlements[0];
        await ui.tool('life', 'Human');
        await ui.paused(false);
        await ui.clickTile(home.X, home.Y);
        assert((await ui.snapshot()).pendingPlacement, 'Touch placement must first show a preview');
        await ui.click('placement-confirm');
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
        const gestureChrome = chromeLayout(ui, snapshot);
        const gestureViewport = await viewport(page);
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
        assert.deepEqual(chromeLayout(ui, await ui.snapshot()), gestureChrome, 'Pinch must leave the interface fixed');
        assert.deepEqual(await viewport(page), gestureViewport, 'Pinch must not scale or pan the page viewport');
        const pinched = await ui.save();
        assert.equal(digest(pinched), digest(dragged), 'Pinch gesture must not edit the world');
        console.log('PASS mobile drag and pinch preserve every persisted world field');

        // Exercise the event fallback with CSS and viewport restrictions disabled,
        // as on browsers that do not enforce those restrictions for native zoom.
        const css = await page.addStyleTag({ content: 'html, body, #out, #out * { touch-action: auto !important; }' });
        const originalViewport = await page.locator('meta[name="viewport"]').getAttribute('content');
        await page.locator('meta[name="viewport"]').evaluate(meta => {
            meta.content = 'width=device-width, initial-scale=1.0, viewport-fit=cover';
        });
        try {
            await page.waitForTimeout(180);
            const fallbackBaseline = await ui.snapshot();
            const fallbackViewport = await viewport(page);
            await touch('touchStart', [{ id: 1, x: x - 45, y }]);
            await touch('touchStart', [{ id: 1, x: x - 45, y }, { id: 2, x: x + 45, y }]);
            for (let step = 1; step <= 8; step++) {
                await touch('touchMove', [
                    { id: 1, x: x - 45 - step * 9, y }, { id: 2, x: x + 45 + step * 9, y }
                ]);
                await page.waitForTimeout(25);
            }
            await touch('touchEnd', []);
            await page.waitForTimeout(180);
            const fallbackPinched = await ui.snapshot();
            assert(fallbackPinched.map.tileSize > fallbackBaseline.map.tileSize, 'Fallback pinch must still reach the map');
            assert.deepEqual(await viewport(page), fallbackViewport, 'Native pinch fallback must keep the page viewport fixed');
            assert.deepEqual(chromeLayout(ui, fallbackPinched), chromeLayout(ui, fallbackBaseline),
                'Native pinch fallback must keep all interface controls fixed');
        } finally {
            await css.evaluate(style => style.remove());
            await page.locator('meta[name="viewport"]').evaluate((meta, content) => { meta.content = content; }, originalViewport);
        }
        assert.deepEqual(await ui.save(), pinched, 'Fallback pinch must not modify the paused world');
        console.log('PASS mobile native pinch fallback preserves viewport, interface and map navigation');

        await ui.click('header-overview');
        await ui.click('inspector-residents');
        const actor = before.Residents[0];
        await ui.openResidentRow(actor.Id, { scroll: 'inspector-scroll' });
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
        await ui.click('tool-suspend');
        await ui.click('map-fit');
        await page.screenshot({ path: path.join(output, 'mobile.png') });

        // Keep an unconfirmed touch preview while replacing the world through its real dialog.
        await ui.tool('life', 'Human');
        await ui.clickTile(home.X, home.Y);
        assert((await ui.snapshot()).pendingPlacement, 'Touch preview must be pending before world replacement');
        await ui.click('header-new-world');
        await ui.selectIndex('world-size', 0, { scroll: 'modal-scroll' });
        await ui.click('world-create-apply');
        const replaced = await ui.snapshot();
        assert(!replaced.pendingPlacement, 'A new world must discard the previous world\'s placement');
        assert(!ui.control(replaced, 'placement-confirm').visible, 'The old confirmation action must disappear');
        const small = await ui.save();
        assert.equal(small.Width, 128);
        await ui.click('header-storage');
        await ui.click('storage-undo', { scroll: 'modal-scroll' });
        assert(!(await ui.snapshot()).pendingPlacement, 'Undo must also clear pending placement');
        assert.deepEqual(await ui.save(), edited, 'World replacement undo must restore the complete previous world');
        console.log('PASS mobile world replacement clears pending touch placement and preserves undo');

        await diagnostics.assertHealthy('mobile after interactions');
        fs.writeFileSync(path.join(output, 'mobile-results.json'), JSON.stringify({
            url: baseUrl, viewport: { width: 390, height: 844 }, chromiumViewportEmulation: true,
            baseline: summary(before), spawned: summary(spawned), paused: summary(paused),
            dragUnchanged: true, pinchUnchanged: true, cameraMoved: true, pinchZoomed: true,
            zoomKeepsChromeFixed: true, zoomKeepsViewportFixed: true, nativePinchFallback: true,
            fixedViewport,
            gestureMaps: { before: snapshot.map, dragged: draggedMap, pinched: pinchedMap }, residentEdit: true, dialogRotation: true,
            fixedControls, replacementClearsPlacement: true, errors: diagnostics.errors, fallbacks: diagnostics.fallbacks, rendererChecks: diagnostics.rendererChecks
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
