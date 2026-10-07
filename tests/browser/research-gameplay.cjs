const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { UiDriver, testUrl } = require('./ui-driver.cjs');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');
const output = path.resolve(process.env.WORLDBOX_ARTIFACT_DIR || 'artifacts/browser-tests');
const baseUrl = process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/';
const modal = { scroll: 'modal-scroll' }, inspector = { scroll: 'inspector-scroll' };
fs.mkdirSync(output, { recursive: true });
(async () => {
    const browser = await chromium.launch(chromiumLaunchOptions());
    try {
        for (const mobile of [false, true]) {
            const label = mobile ? 'mobile' : 'desktop';
            if (process.env.WORLDBOX_GAMEPLAY_CASES && !process.env.WORLDBOX_GAMEPLAY_CASES.split(',').includes(label)) continue;
            const context = await browser.newContext({ viewport: mobile ? { width: 390, height: 844 } : { width: 1440, height: 1040 }, hasTouch: mobile, isMobile: mobile });
            const page = await context.newPage(), ui = new UiDriver(page, { touch: mobile });
            const errors = observeBrowserErrors(page);
            try {
                await page.goto(testUrl(baseUrl)); await ui.ready(); await ui.paused();
                const world = await ui.save(); assert.equal(world.FormatVersion, 17);
                world.Tick = Math.max(1, world.Tick); // 避免居民处于日序零的未完成移动中。
                const town = world.Settlements[0], person = world.Residents.find(r => r.SettlementId === town.Id);
                Object.assign(town.Resources, { Food: 1000, Water: 1000, Wood: 1000, Stone: 1000, Alloy: 1000, Crystals: 1000, Medicine: 100 });
                const research = world.Society.Research.find(r => r.SettlementId === town.Id);
                research.Completed = Array.from({ length: 40 }, (_, i) => i);
                research.ActiveProject = null; research.Progress = research.RequiredProgress = 0;
                world.Society.MagicEnabled = true;
                const sites = [];
                for (let y = town.Y - 5; y <= town.Y + 5; y++) for (let x = town.X - 5; x <= town.X + 5; x++) {
                    if (x < 0 || y < 0 || x >= world.Width || y >= world.Height) continue;
                    const tile = world.Tiles[y * world.Width + x];
                    if (tile.ClaimedSettlementId !== town.Id) continue;
                    tile.Terrain = 3; tile.FireTicks = 0; tile.Fertility = 100;
                    if (!world.Society.Buildings.some(b => b.X === x && b.Y === y)) sites.push({ x, y });
                }
                assert(sites.length >= 4);
                const template = world.Society.Buildings.find(b => b.SettlementId === town.Id);
                const gates = sites.slice(0, 2).map(at => ({ ...structuredClone(template), Id: world.NextId++, Kind: 48, X: at.x, Y: at.y,
                    Enabled: true, Health: 100, ConstructionProgress: 30, ConstructionRequired: 30, UpgradeProgress: 0, UpgradeRequired: 0,
                    ServiceActions: 0, LastServiceTick: -100, LastWorkedTick: -100, Workers: [], ProductionBatches: 0 }));
                world.Society.Buildings.push(...gates);
                Object.assign(person, { Age: 30, X: gates[0].X, Y: gates[0].Y, FromX: gates[0].X, FromY: gates[0].Y,
                    MoveStartedTick: 0, MoveDurationTicks: 1, TravelMode: 0, MagicTalent: 80, MagicTraining: 50, Mana: 100, ArmyId: 0 });
                person.Agent.DestinationSettlementId = 0; person.Inventory.Crystals = 5; person.Inventory.Ore = 7;
                const rail = world.Tiles[town.Y * world.Width + town.X]; rail.RoadLevel = 1;
                const filename = path.join(output, `research-gameplay-${label}-fixture.json`);
                fs.writeFileSync(filename, JSON.stringify(world));
                await ui.click('header-storage'); const chooser = page.waitForEvent('filechooser');
                await ui.click('storage-import', modal); await (await chooser).setFiles(filename);
                await ui.waitFor(s => !s.modalOpen && s.status.startsWith('导入成功'), 'gameplay fixture import', 30000);
                await ui.click('header-overview'); await ui.click('overview-research', inspector); await ui.click('research-expand', inspector);
                async function node(route, kind) {
                    await ui.click(`research-route-${route}`, inspector); await ui.click('research-fit', inspector);
                    await ui.point('research-graph', inspector); await ui.click(`research-node-${kind}`, inspector);
                }
                await node('common', 'Sanitation'); await ui.click('research-job-Physician', inspector);
                await ui.selectIndex('research-job-person', 0, modal); await ui.click('research-job-apply', modal);
                assert.equal((await ui.save()).Residents.find(r => r.Id === person.Id).Profession, 13);
                await ui.click('research-build-Hospital', inspector);
                assert.equal(ui.control(await ui.snapshot(), 'building-kind').value, '36');
                await ui.fill('building-x', sites[2].x, modal); await ui.fill('building-y', sites[2].y, modal); await ui.click('building-apply', modal);
                assert((await ui.save()).Society.Buildings.some(b => b.Kind === 36 && b.X === sites[2].x && b.Y === sites[2].y && b.ConstructionProgress === 0));
                await node('technology', 'RailTransport'); await ui.click('research-action-rail', inspector);
                await ui.fill('rail-x', town.X, modal); await ui.fill('rail-y', town.Y, modal); await ui.fill('rail-radius', 0, modal); await ui.click('rail-apply', modal);
                let saved = await ui.save(); assert.equal(saved.Tiles[town.Y * world.Width + town.X].RoadLevel, 2);
                assert.equal(saved.Settlements.find(t => t.Id === town.Id).Resources.Alloy, 999.5);
                await node('magic', 'SpatialMagic'); await ui.click('research-action-waygate', inspector);
                await ui.selectIndex('waygate-target', 1, modal); assert.equal(ui.control(await ui.snapshot(), 'waygate-apply').enabled, true);
                await ui.click('waygate-apply', modal); saved = await ui.save();
                let moved = saved.Residents.find(r => r.Id === person.Id);
                assert.deepEqual([moved.X, moved.Y, moved.Inventory.Crystals, moved.Inventory.Ore, moved.Mana], [gates[1].X, gates[1].Y, 3, 7, 70]);
                await node('magic', 'Warding'); await ui.click('research-spell-RuneWard', inspector);
                assert.match(ui.control(await ui.snapshot(), 'spell-requirements').value, /基础魔力：20/);
                await ui.fill('spell-x', moved.X, modal); await ui.fill('spell-y', moved.Y, modal); await ui.click('spell-apply', modal);
                saved = await ui.save(); moved = saved.Residents.find(r => r.Id === person.Id);
                assert.equal(moved.Mana, 50); assert(moved.PersonalWard > 0);
                await node('common', 'Agriculture'); await ui.click('research-build-Pasture', inspector);
                assert.equal(ui.control(await ui.snapshot(), 'building-kind').value, '49');
                assert.equal(ui.control(await ui.snapshot(), 'building-bridge-direction').visible, false);
                await ui.fill('building-x', sites[3].x, modal); await ui.fill('building-y', sites[3].y, modal); await ui.click('building-apply', modal);
                assert((await ui.save()).Society.Buildings.some(b => b.Kind === 49 && b.X === sites[3].x && b.Y === sites[3].y && b.LivestockPopulation === 0));
                await node('technology', 'Industry'); await ui.click('research-build-Aquaculture', inspector);
                assert.equal(ui.control(await ui.snapshot(), 'building-kind').value, '50');
                assert.equal(ui.control(await ui.snapshot(), 'building-bridge-direction').visible, false);
                await ui.click('modal-close');
                await ui.click('research-route-magic', inspector);
                await ui.click('research-branch-元素与结界', inspector); await ui.click('research-fit', inspector);
                await ui.point('research-graph', inspector); await ui.click('research-node-BattleMagic', inspector);
                if (mobile) { while ((await ui.snapshot()).researchGraph.zoom < .95) await ui.click('research-zoom-in', inspector); await ui.click('research-focus', inspector); }
                await page.screenshot({ path: path.join(output, `research-gameplay-${label}.png`) });
                await errors.assertHealthy(`research gameplay ${label}`);
                console.log(`PASS research gameplay ${label}: branch tree, contextual profession/building/rail/portal/spell actions and physical costs`);
            } catch (error) {
                await page.screenshot({ path: path.join(output, `research-gameplay-${label}-failure.png`) });
                fs.writeFileSync(path.join(output, `research-gameplay-${label}-failure.json`), JSON.stringify(await ui.snapshot(), null, 2));
                throw error;
            } finally { await context.close(); }
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
