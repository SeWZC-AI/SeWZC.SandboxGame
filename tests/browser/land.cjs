const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { UiDriver, testUrl } = require('./ui-driver.cjs');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');
const output = path.resolve(process.env.WORLDBOX_ARTIFACT_DIR || 'artifacts/browser-tests');
const baseUrl = process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/';
const inspector = { scroll: 'inspector-scroll' }, modal = { scroll: 'modal-scroll' };
fs.mkdirSync(output, { recursive: true });

(async () => {
    const browser = await chromium.launch(chromiumLaunchOptions());
    try {
        for (const mobile of [false, true]) {
            const label = mobile ? 'mobile' : 'desktop';
            const context = await browser.newContext({ viewport: mobile ? { width: 390, height: 844 } : { width: 1440, height: 1040 },
                hasTouch: mobile, isMobile: mobile });
            const page = await context.newPage(), ui = new UiDriver(page, { touch: mobile });
            const errors = observeBrowserErrors(page);
            try {
                await page.goto(testUrl(baseUrl)); await ui.ready(); await ui.paused();
                const fixture = await ui.save(), town = fixture.Settlements[0];
                const worker = fixture.Residents.find(r => r.SettlementId === town.Id);
                const bridge = { x: town.X + 4, y: town.Y };
                for (const tile of fixture.Tiles) Object.assign(tile, { Terrain: 3, Fertility: 90, Improvement: 0, FireTicks: 0 });
                for (const resident of fixture.Residents) {
                    const home = fixture.Settlements.find(t => t.Id === resident.SettlementId);
                    Object.assign(resident, { X: home.X - 3, FromX: home.X - 3, Y: home.Y, FromY: home.Y,
                        MoveStartedTick: fixture.Tick, MoveDurationTicks: 1, ArmyId: 0, TravelMode: 0 });
                    Object.assign(resident.Agent, { DestinationSettlementId: 0, MissionOriginSettlementId: 0, CarriedMessages: [], NextThinkTick: fixture.Tick + 1000 });
                    resident.Agent.Goal = { Kind: 4, TargetX: resident.X, TargetY: resident.Y, StartedTick: fixture.Tick,
                        ReviewTick: fixture.Tick + 1000, PlayerDirected: true, Reason: '等待运输建设' };
                }
                Object.assign(worker, { X: bridge.x - 1, FromX: bridge.x - 1, Y: bridge.y, FromY: bridge.y, Profession: 5, Age: 25 });
                worker.Agent.Goal = { Kind: 0, TargetX: worker.X, TargetY: worker.Y, Reason: '等待附近的实际施工' };
                worker.Agent.NextThinkTick = fixture.Tick;
                fixture.Armies = [];
                Object.assign(fixture.Rules, { Births: false, Aging: false, Hunger: false, Thirst: false, Disease: false, Construction: false,
                    Research: false, Expansion: false, Wars: false, Alliances: false, Migration: false, Secession: false });
                fixture.NaturalDisasters = false; fixture.Society.MagicEnabled = false;
                Object.assign(town.Resources, { Wood: 100, Stone: 100 });
                const research = fixture.Society.Research.find(r => r.SettlementId === town.Id);
                Object.assign(research, { Completed: [0, 1], ActiveProject: null, Progress: 0, RequiredProgress: 0 });
                Object.assign(fixture.Tiles[bridge.y * fixture.Width + bridge.x], { Terrain: 10, Improvement: 0,
                    Deposit: null, DepositAmount: 0, DepositDiscovered: false, RoadLevel: 0 });
                const filename = path.join(output, `land-fixture-${label}.json`); fs.writeFileSync(filename, JSON.stringify(fixture));
                await ui.click('header-storage'); const chooser = page.waitForEvent('filechooser');
                await ui.click('storage-import', modal); await (await chooser).setFiles(filename);
                await ui.waitFor(s => !s.modalOpen && s.status.startsWith('导入成功'), 'land fixture import');
                await ui.click('header-overview'); await ui.click('inspector-residents');
                await ui.fill('resident-search', worker.Id, inspector); await ui.openResidentRow(worker.Id, inspector);
                await ui.click('resident-locate', inspector);
                if ((await ui.snapshot()).inspectorOpen) await ui.click('inspector-close');
                await ui.clickTile(worker.X, worker.Y);
                let state = await ui.snapshot();
                assert.equal(state.selectionKind, 'resident'); assert.equal(state.inspectorOpen, false); assert.equal(state.modalOpen, false);
                await ui.click('selection-view'); state = await ui.snapshot();
                assert.equal(state.inspectorOpen, true);
                if (mobile) {
                    const height = ui.control(state, 'inspector-panel').height;
                    assert(height < state.map.height * .5, 'Mobile detail obscures too much of the map');
                    await ui.click('inspector-expand');
                    assert(ui.control(await ui.snapshot(), 'inspector-panel').height > height, 'Mobile detail cannot expand');
                    await ui.click('inspector-expand');
                }
                await ui.click('inspector-close');
                const farm = fixture.Society.Buildings.find(b => b.SettlementId === town.Id && b.Kind === 0);
                await ui.clickTile(farm.X, farm.Y); state = await ui.snapshot();
                assert.equal(state.selectedBuildingId, farm.Id); assert.equal(state.inspectorOpen, false);
                await ui.click('selection-view'); await ui.click('inspector-close');
                await ui.clickTile(bridge.x, bridge.y); state = await ui.snapshot();
                assert.equal(state.selectionKind, 'tile'); assert.equal(state.inspectorOpen, false);
                await ui.click('selection-view'); await ui.click('tile-improve', inspector); await ui.click('land-apply', modal);
                let world = await ui.save();
                let project = world.Society.Buildings.find(b => b.Kind === 15 && b.X === bridge.x && b.Y === bridge.y);
                assert(project && project.ConstructionProgress === 0, 'Bridge bypassed resident construction');
                assert.equal(world.Settlements.find(t => t.Id === town.Id).Resources.Wood, 92);
                const tick = world.Tick;
                await ui.click('time-speed-5'); await ui.paused(false);
                await ui.waitFor(s => s.worldTick >= tick + 100, 'bridge labor', 30000); await ui.paused();
                world = await ui.save(); project = world.Society.Buildings.find(b => b.Id === project.Id);
                assert(project.ConstructionProgress === project.ConstructionRequired, 'Worker failed on-site bridge construction');
                assert.equal(world.Tiles[bridge.y * world.Width + bridge.x].Improvement, 3);
                if ((await ui.snapshot()).inspectorOpen) await ui.click('inspector-close');
                await ui.clickTile(bridge.x, bridge.y); await ui.click('selection-view');
                // 重复点击会在建筑与地格之间轮换选择。
                if ((await ui.snapshot()).inspector === 'tile') await ui.click(`building-row-${project.Id}`, inspector);
                await ui.click('building-upgrade', inspector); await ui.click('upgrade-apply', modal);
                world = await ui.save(); project = world.Society.Buildings.find(b => b.Id === project.Id);
                assert.equal(project.Level, 1); assert(project.UpgradeRequired > project.UpgradeProgress, 'Upgrade bypassed labor');
                const upgradeTick = world.Tick;
                await ui.paused(false); await ui.waitFor(s => s.worldTick >= upgradeTick + 100, 'bridge upgrade labor', 30000);
                await ui.paused(); world = await ui.save(); project = world.Society.Buildings.find(b => b.Id === project.Id);
                assert.equal(project.Level, 2); assert.equal(world.Tiles[bridge.y * world.Width + bridge.x].BridgeLevel, 2);
                await ui.click('building-reorient', inspector); await ui.click('upgrade-gift', modal);
                world = await ui.save(); project = world.Society.Buildings.find(b => b.Id === project.Id);
                assert.equal(project.Direction, 1); assert.equal(world.Tiles[bridge.y * world.Width + bridge.x].BridgeDirection, 1);
                assert.equal(project.Level, 2, 'Reorientation also changed the level');
                await page.screenshot({ path: path.join(output, `land-${label}.png`) });
                await errors.assertHealthy(`selection and land ${label}`);
                console.log(`PASS land ${label}: selection, compact panel, paid bridge construction and upgrades, direction change`);
            } catch (error) {
                await page.screenshot({ path: path.join(output, `land-${label}-failure.png`) });
                fs.writeFileSync(path.join(output, `land-${label}-failure.json`), JSON.stringify(await ui.snapshot(), null, 2));
                throw error;
            } finally { await context.close(); }
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
