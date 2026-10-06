const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { createHash } = require('node:crypto');
const { chromium } = require('playwright');
const { UiDriver, testUrl } = require('./ui-driver.cjs');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');

const baseUrl = process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/';
const output = path.resolve(process.env.WORLDBOX_ARTIFACT_DIR || 'artifacts/browser-tests');
const inspector = { scroll: 'inspector-scroll' }, modal = { scroll: 'modal-scroll' };
const modes = ['settlement', 'infrastructure', 'research', 'communication'];
const digest = world => createHash('sha256').update(JSON.stringify(world)).digest('hex');
fs.mkdirSync(output, { recursive: true });

// Use the app's current-format world, changing only the local conditions needed
// to expose a railway action and to select empty ground next to known residents.
function fixture(world) {
    const towns = [...world.Settlements].sort((a, b) => a.Id - b.Id);
    assert(towns.length >= 2, 'Entry checks need two settlements');
    const home = towns[0];
    const people = world.Residents.filter(r => r.SettlementId === home.Id);
    const actor = people.find(r => r.Health > 0 && r.Age >= 14);
    assert(actor, 'Entry checks need a living local adult');
    const x = Math.max(0, home.X - 3), y = home.Y;
    for (const resident of people) Object.assign(resident, { X: x, Y: y, FromX: x, FromY: y,
        MoveStartedTick: world.Tick, MoveDurationTicks: 1 });
    Object.assign(actor, { MagicTalent: 80, MagicTraining: 50, Mana: 100 });
    const research = world.Society.Research.find(r => r.SettlementId === home.Id);
    Object.assign(research, { Completed: Array.from({ length: 40 }, (_, i) => i),
        ActiveProject: null, Progress: 0, RequiredProgress: 0 });
    const candidates = [[4, 2], [4, 3], [3, 4], [-4, 3], [-4, -3], [4, -3]]
        .map(([dx, dy]) => ({ x: x + dx, y: y + dy }));
    const ground = candidates.find(p => p.x >= 0 && p.y >= 0 && p.x < world.Width && p.y < world.Height
        && !world.Society.Buildings.some(b => Math.abs(b.X - p.x) <= 1 && Math.abs(b.Y - p.y) <= 2)
        && !world.Residents.some(r => Math.abs(r.X - p.x) <= 1 && Math.abs(r.Y - p.y) <= 1));
    assert(ground, 'Entry fixture needs empty ground within the nearby-resident range');
    return { world, home, actor, ground, towns };
}

(async () => {
    const browser = await chromium.launch(chromiumLaunchOptions());
    try {
        for (const mobile of [false, true]) {
            const name = mobile ? 'mobile' : 'desktop';
            if (process.env.WORLDBOX_ENTRY_CASES && !process.env.WORLDBOX_ENTRY_CASES.split(',').includes(name)) continue;
            const viewport = mobile ? { width: 390, height: 844 } : { width: 1280, height: 900 };
            const context = await browser.newContext({ viewport, isMobile: mobile, hasTouch: mobile });
            const page = await context.newPage(), ui = new UiDriver(page, { touch: mobile });
            const diagnostics = observeBrowserErrors(page), results = [];
            const passed = description => { results.push(description); console.log('PASS', name, description); };
            try {
                await page.goto(testUrl(baseUrl)); await ui.ready(); await ui.paused();
                const data = fixture(await ui.save());
                const filename = path.join(output, `${name}-fixture.worldbox.json`);
                fs.writeFileSync(filename, JSON.stringify(data.world));
                await ui.click('header-storage'); const chooser = page.waitForEvent('filechooser');
                await ui.click('storage-import', modal); await (await chooser).setFiles(filename);
                await ui.waitFor(s => !s.modalOpen && s.status.startsWith('导入成功'), 'entry fixture import', 30000);
                const baseline = await ui.save(), beforeDigest = digest(baseline);

                function tabs(snapshot) {
                    return Object.fromEntries(modes.map(mode => {
                        const tab = ui.control(snapshot, `settlement-tab-${mode}`);
                        assert(tab.visible && tab.enabled && tab.width > 0, `Settlement tab is unreachable: ${mode}`);
                        assert(tab.x >= 0 && tab.x + tab.width <= snapshot.width, `Settlement tab exceeds the screen: ${mode}`);
                        return [mode, [tab.x, tab.y, tab.width, tab.height]];
                    }));
                }
                await ui.openOverview(); await ui.click('overview-settlements', inspector);
                await ui.waitFor(s => s.inspector === 'settlements', 'settlement list');
                await ui.click(`settlement-row-${data.home.Id}`, inspector);
                await ui.waitFor(s => s.inspector === 'settlement', 'settlement overview');
                assert.match(ui.control(await ui.snapshot(), 'town-expansion-summary').value, /城镇等级/);
                await ui.selectIndex('infrastructure-town', 1, inspector);
                const townContext = ui.control(await ui.snapshot(), 'settlement-context').value;
                const initialTabs = tabs(await ui.snapshot());
                for (const mode of ['infrastructure', 'research', 'communication', 'settlement']) {
                    await ui.click(`settlement-tab-${mode}`);
                    const snapshot = await ui.waitFor(s => s.inspector === mode, `settlement ${mode}`);
                    assert.equal(ui.control(snapshot, 'infrastructure-town').value, '1', 'Switching tabs changed the chosen settlement');
                    assert.equal(ui.control(snapshot, 'settlement-context').value, townContext, 'Fixed header lost the chosen settlement');
                    assert.deepEqual(tabs(snapshot), initialTabs, 'Changing pages moved the fixed settlement tabs');
                    if (mode === 'infrastructure') assert(!snapshot.researchGraph, 'Construction still embeds the research tree');
                }
                for (const [entry, mode] of [['overview-research', 'research'], ['overview-communication', 'communication'],
                    ['overview-infrastructure', 'infrastructure']]) {
                    await ui.openOverview(); await ui.click(entry, inspector);
                    const snapshot = await ui.waitFor(s => s.inspector === mode, entry);
                    assert.equal(ui.control(snapshot, 'infrastructure-town').value, '1'); tabs(snapshot);
                }
                await ui.click('settlement-tab-research');
                const fixedTabs = tabs(await ui.snapshot());
                await ui.point('research-graph', inspector);
                assert.deepEqual(tabs(await ui.snapshot()), fixedTabs, 'Scrolling the research content hid or moved settlement tabs');
                assert(ui.control(await ui.snapshot(), 'settlement-context').visible, 'Scrolling research hid the current settlement context');
                await page.screenshot({ path: path.join(output, `${name}-tabs.png`) });
                passed('settlement list, direct research/communication/construction entries, fixed tabs and retained settlement');

                await ui.openOverview(); await ui.click('overview-guide', inspector);
                assert.equal(ui.control(await ui.snapshot(), 'inspector-title').value, '玩法说明');
                passed('game guide displays its own title');

                async function resident() {
                    await ui.openOverview(); await ui.click('inspector-residents');
                    await ui.openResidentRow(data.actor.Id, inspector);
                }
                await resident(); await ui.click('resident-locate', inspector);
                if ((await ui.snapshot()).inspectorOpen) await ui.click('inspector-close');
                await ui.clickTile(data.ground.x, data.ground.y);
                let snapshot = await ui.snapshot();
                assert.equal(snapshot.selectionKind, 'tile', 'Empty ground selected a different object');
                const selection = ui.control(snapshot, 'selection-name').value;
                await ui.click('selection-view'); await ui.click('tile-residents', inspector);
                const nearby = (await ui.snapshot()).controls.find(c => c.id.startsWith('resident-row-'));
                assert(nearby, 'Ground details have no nearby resident entry');
                await ui.click(nearby.id, inspector);
                await ui.waitFor(s => s.inspector === 'resident', 'nearby resident details');
                await ui.click('inspector-back');
                snapshot = await ui.waitFor(s => s.inspector === 'tile', 'returned ground details');
                assert.equal(snapshot.selectionKind, 'tile'); assert.equal(snapshot.selectedResidentId, 0);
                assert.equal(ui.control(snapshot, 'tile-residents').value, 'True', 'Back lost the expanded nearby-resident section');
                await ui.click('inspector-close'); snapshot = await ui.snapshot();
                assert(ui.control(snapshot, 'selection-summary').visible, 'Closing details lost the selected-ground summary');
                assert.equal(ui.control(snapshot, 'selection-name').value, selection);
                await ui.click('selection-view'); await ui.click('inspector-back');
                assert.equal((await ui.snapshot()).inspectorOpen, false, 'A new map entry reused an old navigation session');
                passed('ground → nearby resident → back → close preserves selection, expanded section and navigation session');

                async function mapPick(kind, open) {
                    await open(); const source = await ui.snapshot();
                    const original = [ui.control(source, `${kind}-x`).value, ui.control(source, `${kind}-y`).value];
                    await ui.click(`map-pick-${kind}_x`, modal);
                    snapshot = await ui.waitFor(s => !s.modalOpen && s.activeTool === 'inspect', `${kind} map selection`);
                    assert(!snapshot.inspectorOpen && !snapshot.toolsOpen);
                    assert(ui.control(snapshot, 'map-pick-prompt').visible, 'Map selection has no visible return prompt');
                    const tick = snapshot.worldTick;
                    if (kind === 'spell') {
                        const context = state => Object.fromEntries(['inspector', 'selectedResidentId', 'selectedNationId',
                            'selectedBuildingId', 'selectionKind', 'activeTool', 'category', 'worldTick'].map(key => [key, state[key]]));
                        const pickingContext = context(snapshot);
                        for (const id of ['tools-toggle', 'header-overview', 'event-spotlight']) {
                            const control = ui.control(await ui.snapshot(), id);
                            assert(control.visible, `Picker boundary check needs the visible entry ${id}`);
                            const canvas = await page.locator('#out canvas.avalonia-canvas').boundingBox();
                            assert(canvas, 'Avalonia canvas has no visible bounds');
                            // A disabled entry still receives a real attempted tap. UiDriver.click
                            // intentionally rejects disabled controls, so use its read-only geometry.
                            const point = { x: canvas.x + control.x + control.width / 2,
                                y: canvas.y + control.y + control.height / 2 };
                            if (mobile) await page.touchscreen.tap(point.x, point.y);
                            else await page.mouse.click(point.x, point.y, { delay: 80 });
                            await page.waitForTimeout(100);
                            snapshot = await ui.snapshot();
                            assert.deepEqual(context(snapshot), pickingContext, `${id} escaped map selection or changed its source`);
                            assert(!snapshot.modalOpen && !snapshot.inspectorOpen && !snapshot.toolsOpen,
                                `${id} opened another UI during map selection`);
                            assert(ui.control(snapshot, 'map-pick-prompt').visible, `${id} dismissed the original map picker`);
                        }
                    }
                    await ui.click('map-pick-return');
                    snapshot = await ui.waitFor(s => s.modalOpen, `${kind} return without selection`);
                    assert.deepEqual([ui.control(snapshot, `${kind}-x`).value, ui.control(snapshot, `${kind}-y`).value], original,
                        'Returning from map selection changed the coordinates');
                    await ui.click(`map-pick-${kind}_x`, modal);
                    await ui.clickTile(data.ground.x, data.ground.y);
                    snapshot = await ui.waitFor(s => s.modalOpen, `${kind} selected coordinates`);
                    assert.deepEqual([ui.control(snapshot, `${kind}-x`).value, ui.control(snapshot, `${kind}-y`).value],
                        [String(data.ground.x), String(data.ground.y)]);
                    assert.equal(snapshot.worldTick, tick, 'Map selection advanced the paused world');
                    assert.equal(snapshot.inspectorOpen, source.inspectorOpen, 'Map selection did not restore the source details');
                    assert.equal(snapshot.selectionKind, source.selectionKind, 'Map selection lost the source selection kind');
                    assert.equal(snapshot.selectedResidentId, source.selectedResidentId, 'Map selection lost the source resident');
                    if (kind === 'spell') assert.equal(ui.control(snapshot, 'resident-follow').value, 'True', 'Map selection stopped following the source resident');
                    await ui.click('modal-cancel');
                    assert.equal((await ui.snapshot()).paused, true, 'Cancelling changed the original paused state');
                }
                await mapPick('building', async () => {
                    await ui.openOverview(); await ui.click('overview-infrastructure', inspector);
                    await ui.selectIndex('infrastructure-town', 0, inspector);
                    assert(ui.control(await ui.snapshot(), 'building-open').visible, 'Construction action is hidden below the compact first screen');
                    await ui.click('building-open', inspector);
                });
                await mapPick('spell', async () => { await resident(); await ui.click('resident-follow', inspector); await ui.click('resident-spell', inspector); });
                await mapPick('rail', async () => {
                    await ui.openOverview(); await ui.click('overview-research', inspector);
                    await ui.selectIndex('infrastructure-town', 0, inspector);
                    await ui.click('research-route-technology', inspector);
                    await ui.click('research-branch-运输与计算', inspector); await ui.click('research-fit', inspector);
                    await ui.point('research-graph', inspector); await ui.click('research-node-RailTransport', inspector);
                    await ui.click('research-action-rail', inspector);
                });
                const after = await ui.save();
                assert.equal(digest(after), beforeDigest, 'Navigation, map selection or cancelled forms changed the complete paused world');
                passed('building/spell/rail map selection, guarded tool/overview/event entries, return and cancellation preserve the complete saved world');

                // Actual running time may advance between windows. Each window must freeze
                // the tick while open and release that temporary pause when cancelled.
                await ui.paused(false);
                for (const [label, open] of [
                    ['rules', async () => ui.click('header-rules')],
                    ['resident', async () => { await resident(); await ui.click('resident-edit', inspector); }],
                    ['nation', async () => { await ui.openOverview(); await ui.click('inspector-nations');
                        await ui.click(`nation-row-${data.home.NationId}`, inspector); await ui.click('nation-edit', inspector); }],
                    ['culture', async () => ui.click('nation-culture-edit', inspector)],
                    ['spell', async () => { await resident(); await ui.click('resident-spell', inspector); }]
                ]) {
                    await open(); snapshot = await ui.waitFor(s => s.modalOpen, `${label} running window`);
                    assert.equal(snapshot.paused, false, `${label} changed the player's running preference`);
                    const tick = snapshot.worldTick;
                    await page.waitForTimeout(450);
                    assert.equal((await ui.snapshot()).worldTick, tick, `${label} did not freeze simulation while open`);
                    await ui.click('modal-cancel');
                    await ui.waitFor(s => !s.modalOpen && !s.paused && s.worldTick > tick, `${label} cancellation resumed simulation`);
                }
                await ui.paused();
                passed('rules, resident, nation, culture and spell cancellation restore running time');
                await diagnostics.assertHealthy(`${name} interface entries`);
                fs.writeFileSync(path.join(output, `${name}-results.json`), JSON.stringify({ url: baseUrl, viewport, results,
                    fixtureTick: baseline.Tick, observationSha256: beforeDigest, finalObservationSha256: digest(after),
                    fallbacks: diagnostics.fallbacks, rendererChecks: diagnostics.rendererChecks, errors: diagnostics.errors }, null, 2));
            } catch (error) {
                await page.screenshot({ path: path.join(output, `${name}-failure.png`) }).catch(() => {});
                fs.writeFileSync(path.join(output, `${name}-failure.json`), JSON.stringify({ error: error.message, results,
                    ui: await ui.snapshot().catch(() => null) }, null, 2));
                throw error;
            } finally { await context.close(); }
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
