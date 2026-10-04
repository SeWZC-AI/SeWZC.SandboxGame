const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { createHash } = require('node:crypto');
const { execFileSync } = require('node:child_process');
const { UiDriver, testUrl } = require('./ui-driver.cjs');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');
const output = path.resolve(process.env.WORLDBOX_ARTIFACT_DIR || 'artifacts/browser-tests');
const baseUrl = process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/';
const modal = { scroll: 'modal-scroll' }, inspector = { scroll: 'inspector-scroll' };
const digest = value => createHash('sha256').update(JSON.stringify(value)).digest('hex');
fs.mkdirSync(output, { recursive: true });
execFileSync('python3', ['-m', 'zipfile', '-e', path.resolve('docs/saves/empire-saves-20261004.zip'), path.join(output, 'saves')]);

(async () => {
    const browser = await chromium.launch(chromiumLaunchOptions());
    try {
        for (const mobile of [false, true]) for (const route of ['technology', 'magic']) {
            const label = `${route}-${mobile ? 'mobile' : 'desktop'}`;
            if (process.env.WORLDBOX_RESEARCH_CASES && !process.env.WORLDBOX_RESEARCH_CASES.split(',').includes(label)) continue;
            const context = await browser.newContext({ viewport: mobile ? { width: 390, height: 844 } : { width: 1440, height: 1040 }, hasTouch: mobile, isMobile: mobile, acceptDownloads: true });
            const page = await context.newPage(), ui = new UiDriver(page, { touch: mobile });
            const errors = observeBrowserErrors(page);
            try {
                await page.goto(testUrl(baseUrl)); await ui.ready(); await ui.paused();
                const filename = path.join(output, 'saves', `${route}-empire.worldbox.json`);
                const expected = JSON.parse(fs.readFileSync(filename, 'utf8'));
                await ui.click('header-storage'); const chooser = page.waitForEvent('filechooser');
                await ui.click('storage-import', modal); await (await chooser).setFiles(filename);
                await ui.waitFor(s => !s.modalOpen && s.status.startsWith('导入成功'), 'delivered empire save import', 60000);
                const before = await ui.save();
                assert.equal(digest(before), digest(expected), 'Import changed the delivered simulated world');
                await ui.click('header-overview'); await ui.click('overview-infrastructure', inspector);
                const completedTown = expected.Settlements.findIndex(t => expected.Society.Research.find(r => r.SettlementId === t.Id).Completed.includes(route === 'technology' ? 18 : 23));
                assert(completedTown >= 0, 'Delivered world has no empire knowledge');
                await ui.selectIndex('infrastructure-town', completedTown, inspector);
                await ui.click('research-expand', inspector);
                const expanded = await ui.snapshot();
                const expandedGraph = ui.control(expanded, 'research-graph');
                assert(mobile ? expandedGraph.width <= 390 : expandedGraph.width > expanded.width * .95,
                    'Expanded tree did not use the available screen width');
                const endpoint = route === 'technology' ? 'TechnologicalEmpire' : 'MagicalEmpire';
                assert.equal((await ui.snapshot()).researchGraph.nodes, 15);
                assert.equal((await ui.snapshot()).researchGraph.edges, 23);
                await ui.click(`research-route-${route}`, inspector);
                await ui.click('research-jump-end', inspector);
                await ui.point('research-graph', inspector);
                await ui.click(`research-node-${endpoint}`, inspector);
                let snapshot = await ui.snapshot();
                assert.match(ui.control(snapshot, 'research-selected').value, route === 'technology' ? /科技帝国/ : /魔法帝国/);
                assert.equal(ui.control(snapshot, `research-state-${endpoint}`).value, '已掌握');
                assert.equal(ui.control(snapshot, 'research-start').enabled, false);
                assert(!snapshot.controls.some(c => c.id === 'research-kind'), 'Research still uses a dropdown');
                assert.equal(snapshot.controls.filter(c => c.id.startsWith('research-node-')).length, 24);
                assert.equal(snapshot.researchGraph.nodes, route === 'technology' ? 15 : 14);
                // Enlarge even a fully fitting route so panning has real overflow to move.
                await ui.click('research-zoom-in', inspector); await ui.click('research-zoom-in', inspector);
                await ui.click('research-focus', inspector);
                async function showGraph() {
                    await ui.point('research-graph', inspector);
                    for (let i = 0; i < 6; i++) {
                        const s = await ui.snapshot(), g = ui.control(s, 'research-graph'), v = ui.control(s, inspector.scroll);
                        const delta = g.y < v.y + 10 ? g.y - v.y - 10 : g.y + g.height > v.y + v.height - 10 ? g.y + g.height - v.y - v.height + 10 : 0;
                        if (Math.abs(delta) < 2) break;
                        await page.mouse.move(v.x + v.width / 2, v.y + 25); await page.mouse.wheel(0, delta);
                        await page.waitForTimeout(150);
                    }
                    return ui.point('research-graph', inspector);
                }
                await showGraph();
                snapshot = await ui.snapshot();
                const beforePan = snapshot.researchGraph;
                // Start on a real button: its lost capture must not truncate the graph drag.
                const start = await ui.point(`research-node-${endpoint}`, inspector);
                if (mobile) {
                    const cdp = await context.newCDPSession(page);
                    const touch = point => ({ ...point, id: 1, radiusX: 2, radiusY: 2, force: 1 });
                    await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [touch(start)] });
                    for (let i = 1; i <= 8; i++) {
                        await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [touch({ x: start.x - i * 5.5, y: start.y + i * 11 })] });
                        await page.waitForTimeout(25);
                    }
                    await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
                    await cdp.detach();
                } else {
                    await page.mouse.move(start.x, start.y); await page.mouse.down();
                    await page.mouse.move(start.x - 44, start.y + 88, { steps: 8 }); await page.mouse.up();
                }
                await page.waitForTimeout(250);
                snapshot = await ui.snapshot();
                assert(Math.abs(snapshot.researchGraph.offsetY - beforePan.offsetY) > 30,
                    'Dragging did not pan the actual tree: ' + JSON.stringify({ before: beforePan, after: snapshot.researchGraph }));
                assert(Math.abs(snapshot.researchGraph.offsetX - beforePan.offsetX) > 25, 'Dragging did not move between branch columns');
                assert.match(ui.control(snapshot, 'research-selected').value, route === 'technology' ? /科技帝国/ : /魔法帝国/);
                await ui.click('research-zoom-out', inspector);
                assert((await ui.snapshot()).researchGraph.zoom < beforePan.zoom, 'Zoom did not change tree geometry');
                await ui.click('research-zoom-in', inspector);
                await ui.click('research-fit', inspector);
                await showGraph();
                snapshot = await ui.snapshot();
                assert.equal(snapshot.controls.filter(c => c.id.startsWith('research-node-') && c.visible).length, route === 'technology' ? 15 : 14,
                    'Fit-to-tree overview clips a research node');
                const readableBranch = route === 'technology' ? 'Industry' : 'Crystalcraft';
                await ui.click(`research-node-${readableBranch}`, inspector);
                assert.match(ui.control(await ui.snapshot(), 'research-path-caption').value, route === 'technology' ? /农业改良、驿路运输/ : /奥术基础/);
                if (mobile) {
                    while ((await ui.snapshot()).researchGraph.zoom < .95) await ui.click('research-zoom-in', inspector);
                    await ui.click('research-focus', inspector);
                }
                await showGraph();
                snapshot = await ui.snapshot();
                await page.waitForTimeout(1200);
                const refreshed = await ui.snapshot();
                assert.deepEqual(refreshed.researchGraph, snapshot.researchGraph, 'Timed refresh reset the tree viewport');
                await page.screenshot({ path: path.join(output, `${label}.png`) });
                assert.equal(digest(await ui.save()), digest(before), 'Reading or expanding the technology tree changed the world');
                await errors.assertHealthy(`real empire tree ${label}`);
                fs.writeFileSync(path.join(output, `${label}.json`), JSON.stringify({ tick: before.Tick, population: before.Residents.length, graph: snapshot.researchGraph, stage: endpoint, errors: [] }, null, 2));
                console.log(`PASS ${label}: actual save import, connected research graph, pan, zoom, endpoint status and read-only view`);
            } catch (error) {
                await page.screenshot({ path: path.join(output, `${label}-failure.png`) });
                fs.writeFileSync(path.join(output, `${label}-failure.json`), JSON.stringify(await ui.snapshot(), null, 2));
                throw error;
            } finally { await context.close(); }
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
