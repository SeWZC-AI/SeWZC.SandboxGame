const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');
const { UiDriver, testUrl } = require('./ui-driver.cjs');

const baseUrl = process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/';
const output = process.env.WORLDBOX_ARTIFACT_DIR
    ? path.resolve(process.env.WORLDBOX_ARTIFACT_DIR)
    : path.resolve(__dirname, '../../artifacts/browser-tests');
const inspector = { scroll: 'inspector-scroll' };
const modal = { scroll: 'modal-scroll' };
fs.mkdirSync(output, { recursive: true });

// Find a real, locally walkable route from a real save. The chosen destination is
// entered through the product's resident editor; no test hook changes the world.
function chooseRoute(world) {
    const walkable = (x, y) => x >= 0 && y >= 0 && x < world.Width && y < world.Height &&
        ![0, 1, 5, 10].includes(world.Tiles[y * world.Width + x].Terrain ?? 0);
    let result;
    for (const resident of world.Residents.slice(0, 60)) {
        if (resident.Age < 16 || resident.ArmyId) continue;
        for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
            let length = 0;
            while (length < 64 && walkable(resident.X + dx * (length + 1), resident.Y + dy * (length + 1))) length++;
            if (length >= 16 && (!result || length > result.length))
                result = { resident, target: { x: resident.X + dx * length, y: resident.Y + dy * length }, length };
        }
    }
    assert(result, 'The seeded world needs a straight, walkable route of at least 16 tiles');
    return result;
}

async function sampleRendering(page, milliseconds, interval = 25) {
    return page.evaluate(async ({ milliseconds, interval }) => {
        const samples = [];
        const start = performance.now();
        while (performance.now() - start < milliseconds) {
            const state = globalThis.worldboxTest.snapshot();
            samples.push({ at: performance.now(), tick: state.worldTick, point: state.selectedResidentPoint,
                paused: state.paused, speed: state.speed, map: state.map });
            await new Promise(resolve => setTimeout(resolve, interval));
        }
        return samples;
    }, { milliseconds, interval });
}

const distance = (first, second) => Math.hypot(first.x - second.x, first.y - second.y);

(async () => {
    const browser = await chromium.launch(chromiumLaunchOptions());
    const context = await browser.newContext({ viewport: { width: 1440, height: 1040 }, deviceScaleFactor: 1 });
    const page = await context.newPage();
    const diagnostics = observeBrowserErrors(page);
    const ui = new UiDriver(page);
    const report = { url: baseUrl, viewport: { width: 1440, height: 1040 } };
    try {
        await page.goto(testUrl(baseUrl), { waitUntil: 'domcontentloaded' });
        await ui.ready();
        await ui.paused();
        const baseline = await ui.save();
        const route = chooseRoute(baseline);
        const actor = route.resident;
        report.start = { tick: baseline.Tick, id: actor.Id, x: actor.X, y: actor.Y, target: route.target, routeLength: route.length };
        console.log('SCENARIO', JSON.stringify(report.start));
        await ui.click('header-overview');
        await ui.click('inspector-residents');
        await ui.fill('resident-search', actor.Id, inspector);
        await ui.click(`resident-row-${actor.Id}`, inspector);
        await ui.click('resident-locate', inspector);
        await ui.click('resident-goal-edit', inspector);
        await ui.selectIndex('resident-goal', 3, modal); // Work at an entered location.
        await ui.fill('resident-goal-x', route.target.x, modal);
        await ui.fill('resident-goal-y', route.target.y, modal);
        await ui.selectIndex('resident-goal-entity', 0, modal);
        await ui.fill('resident-goal-duration', 240, modal);
        await ui.click('resident-goal-apply', modal);
        await ui.waitFor(state => !state.modalOpen && state.status.includes('目标与人格已更新'), 'committed resident goal');
        await ui.waitFor(state => state.renderedRouteSegmentCount > 0, 'drawn current-goal route');
        await ui.paused(false);

        const normal = await sampleRendering(page, 2400);
        fs.writeFileSync(path.join(output, 'motion-samples.json'), JSON.stringify(normal, null, 2));
        const intermediate = normal.slice(1).flatMap((sample, index) => {
            const prior = normal[index];
            return prior.tick === sample.tick && prior.point && sample.point && distance(prior.point, sample.point) > .03
                ? [{ prior, next: sample }] : [];
        });
        assert(intermediate.length > 0, 'Actual rendered geometry must move between simulation ticks');
        // An early visual arrival followed by waiting used to leave whole 200 ms windows still.
        // Sample the actual canvas geometry throughout a long, uninterrupted walking goal.
        for (let i = 0; i < normal.length; i++) {
            const first = normal[i];
            if (first.at - normal[0].at < 300) continue;
            const next = normal.slice(i + 1).find(s => s.at - first.at >= 200);
            if (next) assert(first.point && next.point && distance(first.point, next.point) > .03,
                'The movement animation arrived early and waited before the next committed step');
        }
        report.normalSpeed = { samples: normal.length, tickFrom: normal[0].tick, tickTo: normal.at(-1).tick,
            sameTickPositionChanges: intermediate.length, example: intermediate[0] };
        console.log('PASS actual rendered positions change within the same simulation tick');

        await ui.paused();
        const frozen = await sampleRendering(page, 450, 35);
        assert(frozen.every(sample => sample.tick === frozen[0].tick), 'Paused simulation advanced a tick');
        assert(frozen.every(sample => sample.point && distance(sample.point, frozen[0].point) < .001), 'Paused rendering drifted');
        report.pause = { tick: frozen[0].tick, samples: frozen.length, position: frozen[0].point };
        const beforeCamera = await ui.save();
        await ui.click('map-zoom-in');
        await ui.click('map-zoom-out');
        const afterCamera = await ui.save();
        assert.deepEqual(afterCamera, beforeCamera, 'Rendering and camera changes must not alter any world field or random state');
        report.cameraPreservesWorldAndRandomState = true;
        console.log('PASS pause freezes positions and ticks; camera rendering leaves the complete saved world unchanged');

        await ui.click('time-speed-2');
        await ui.paused(false);
        const fast = await sampleRendering(page, 850, 20);
        assert(fast.every(sample => sample.speed === 2), 'The real speed control did not select 2x');
        assert(fast[0].point && fast.at(-1).point && distance(fast[0].point, fast.at(-1).point) > .1, 'Movement stopped after changing speed');
        report.doubleSpeed = { samples: fast.length, tickFrom: fast[0].tick, tickTo: fast.at(-1).tick,
            screenDistance: distance(fast[0].point, fast.at(-1).point) };
        await ui.paused();
        await ui.click('time-speed-1');
        // The walking actor may have moved behind the fixed tool palette. Use the real
        // locate action to bring its visible sprite back to the unobscured map centre.
        await ui.click('resident-locate', inspector);
        const selected = await ui.snapshot();
        const canvas = await page.locator('#out canvas.avalonia-canvas').boundingBox();
        assert(canvas && selected.selectedResidentPoint);
        await page.mouse.click(canvas.x + selected.selectedResidentPoint.x, canvas.y + selected.selectedResidentPoint.y, { delay: 70 });
        const clicked = await ui.waitFor(state => !state.inspectorOpen && state.selectedResidentId > 0, 'resident map selection');
        report.pointSelection = { id: clicked.selectedResidentId, point: clicked.selectedResidentPoint };
        assert.equal(clicked.selectedResidentId, actor.Id, 'The clicked walking resident must remain selected');

        await ui.click('selection-view');
        await ui.click('resident-follow', inspector);
        await ui.paused(false);
        const following = await sampleRendering(page, 1200, 30);
        assert(following.every(sample => sample.point), 'Follow lost the selected resident');
        const errors = following.map(sample => distance(sample.point,
            { x: sample.map.x + sample.map.width / 2, y: sample.map.y + sample.map.height / 2 }));
        assert(Math.max(...errors) < 2, 'The followed resident must remain centered in the map');
        const cameraMoved = following.some(sample => Math.abs(sample.map.tile0CenterX - following[0].map.tile0CenterX) > .1 ||
            Math.abs(sample.map.tile0CenterY - following[0].map.tile0CenterY) > .1);
        assert(cameraMoved, 'The camera must actually track the resident while walking');
        report.follow = { samples: following.length, maxCenterErrorPixels: Math.max(...errors), cameraMoved };
        console.log('PASS live speed control, real map point selection, and moving camera follow');

        await ui.paused();
        const final = await ui.save();
        const finalActor = final.Residents.find(resident => resident.Id === actor.Id);
        report.final = { tick: final.Tick, residentId: actor.Id, x: finalActor.X, y: finalActor.Y,
            fromX: finalActor.FromX, fromY: finalActor.FromY, moveDurationTicks: finalActor.MoveDurationTicks };
        await page.screenshot({ path: path.join(output, 'motion-detail.png') });
        for (let i = 0; i < 6; i++) await ui.click('map-zoom-in');
        await page.screenshot({ path: path.join(output, 'resident-closeup.png') });
        await ui.click('map-fit');
        await ui.click('header-overview');
        await page.screenshot({ path: path.join(output, 'world-v3.png') });
        await diagnostics.assertHealthy('motion, selection and follow');
        report.errors = diagnostics.errors;
        report.rendererChecks = diagnostics.rendererChecks;
        report.notes = ['Position continuity is measured, not FPS or mobile performance.',
            'All mutations used actual UI input; the opt-in test bridge only read rendered geometry.',
            'The default world and a validated local straight route provide the repeatable movement scenario.'];
        fs.writeFileSync(path.join(output, 'motion-results.json'), JSON.stringify(report, null, 2));
        console.log('ALL MOTION CHECKS PASSED');
    } catch (error) {
        await page.screenshot({ path: path.join(output, 'motion-failure.png') }).catch(() => {});
        fs.writeFileSync(path.join(output, 'motion-failure-ui.json'), JSON.stringify(await ui.snapshot().catch(() => null), null, 2));
        try {
            await ui.paused();
            fs.writeFileSync(path.join(output, 'motion-failure-world.json'), JSON.stringify(await ui.save()));
        } catch { /* Preserve the original failure if the UI can no longer save. */ }
        throw error;
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
