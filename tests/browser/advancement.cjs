const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { createHash } = require('node:crypto');
const { UiDriver, testUrl, readSavedWorld } = require('./ui-driver.cjs');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');
const output = path.resolve(process.env.WORLDBOX_ARTIFACT_DIR || 'artifacts/browser-tests');
const baseUrl = process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/';
const modal = { scroll: 'modal-scroll' }, inspector = { scroll: 'inspector-scroll' };
fs.mkdirSync(output, { recursive: true });
const digest = value => createHash('sha256').update(JSON.stringify(value)).digest('hex');

(async () => {
    const browser = await chromium.launch(chromiumLaunchOptions());
    try {
        for (const mobile of [false, true]) {
            const label = mobile ? 'mobile' : 'desktop';
            const context = await browser.newContext({ viewport: mobile ? { width: 390, height: 844 } : { width: 1440, height: 1040 },
                hasTouch: mobile, isMobile: mobile, acceptDownloads: true });
            const page = await context.newPage(), ui = new UiDriver(page, { touch: mobile });
            const errors = observeBrowserErrors(page);
            try {
                await page.goto(testUrl(baseUrl)); await ui.ready(); await ui.paused();
                const world = await ui.save(); assert.equal(world.FormatVersion, 12);
                const town = world.Settlements[0];
                Object.assign(town.Resources, { Food: 1000, Wood: 1000, Stone: 1000, Ore: 1000, Alloy: 100, EnergyCells: 100, Crystals: 100,
                    Coal: 30, Oil: 20, RareEarth: 10, Boats: 2, Aircraft: 1 });
                const research = world.Society.Research.find(r => r.SettlementId === town.Id);
                research.Completed = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];
                research.ActiveProject = null; research.Progress = 0; research.RequiredProgress = 0;
                for (let y = town.Y - 6; y <= town.Y + 6; y++) for (let x = town.X - 6; x <= town.X + 6; x++) {
                    if (x < 0 || y < 0 || x >= world.Width || y >= world.Height) continue;
                    const t = world.Tiles[y * world.Width + x];
                    if (t.SettlementId && t.SettlementId !== town.Id) continue;
                    t.Terrain = 3; t.Fertility = 100; t.FireTicks = 0; t.NationId = town.NationId;
                }
                const at = [];
                for (let y = town.Y - 3; y <= town.Y + 3; y++) for (let x = town.X - 3; x <= town.X + 3; x++)
                    if (x >= 0 && y >= 0 && x < world.Width && y < world.Height && !world.Society.Buildings.some(b => b.X === x && b.Y === y)) at.push({ x, y });
                const filename = path.join(output, `advancement-${label}.json`);
                fs.writeFileSync(filename, JSON.stringify(world));
                await ui.click('header-storage'); const chooser = page.waitForEvent('filechooser');
                await ui.click('storage-import', modal); await (await chooser).setFiles(filename);
                await ui.waitFor(s => !s.modalOpen && s.status.startsWith('导入成功'), 'advanced world import', 30000);
                const before = await ui.save();
                await ui.click('header-overview'); await ui.click('overview-infrastructure', inspector);
                assert.match(ui.control(await ui.snapshot(), 'advancement-stage').value, /科技：未来制造.*魔法：以太文明/s);
                await ui.selectIndex('research-kind', 7, inspector);
                assert.equal(digest(await ui.save()), digest(before), 'Reading either route changed the world');
                await ui.click('building-open', inspector);
                await ui.selectIndex('building-kind', 10, modal); // Fabricator, ordinary enum picker.
                await ui.fill('building-x', at[0].x, modal); await ui.fill('building-y', at[0].y, modal);
                await ui.click('building-gift', modal); await ui.click('building-apply', modal);
                let saved = await ui.save();
                assert(saved.Society.Buildings.some(b => b.Kind === 10 && b.X === at[0].x && b.Y === at[0].y && b.ProductionBatches === 0));
                const stock = saved.Settlements.find(t => t.Id === town.Id).Resources;
                assert.equal(stock.Alloy, 100); assert.equal(stock.EnergyCells, 100); assert.equal(stock.Crystals, 100);
                assert.deepEqual([stock.Coal, stock.Oil, stock.RareEarth, stock.Boats, stock.Aircraft], [30, 20, 10, 2, 1]);
                await ui.click('building-open', inspector);
                await ui.selectIndex('building-kind', 13, modal); // AetherForge, no industrial fuel required.
                await ui.fill('building-x', at[1].x, modal); await ui.fill('building-y', at[1].y, modal);
                await ui.click('building-apply', modal);
                saved = await ui.save();
                const after = saved.Settlements.find(t => t.Id === town.Id).Resources;
                assert.equal(after.Crystals, 80); assert.equal(after.Alloy, 100); assert.equal(after.EnergyCells, 100);
                assert(saved.Society.Buildings.some(b => b.Kind === 13 && b.ConstructionProgress === 0));
                await page.screenshot({ path: path.join(output, `advancement-${label}.png`) });
                await page.reload(); await ui.ready(); await ui.paused();
                assert.equal(digest(await readSavedWorld(page)), digest(saved), 'IndexedDB changed the complete saved world');
                // Startup resumes time. Explicit load pauses at the saved tick for an exact comparison.
                await ui.click('header-storage'); await ui.click('storage-load', modal);
                await ui.waitFor(s => !s.modalOpen && s.paused, 'loaded paused world', 30000);
                assert.equal(digest(await ui.save()), digest(saved), 'Advanced stocks, research and facilities changed after restore');
                await errors.assertHealthy(`advancement ${label}`);
                console.log(`PASS advancement ${label}: independent routes, real gift/construction, resources and format-8 restore`);
            } catch (error) {
                await page.screenshot({ path: path.join(output, `advancement-${label}-failure.png`) });
                fs.writeFileSync(path.join(output, `advancement-${label}-failure.json`), JSON.stringify(await ui.snapshot(), null, 2));
                throw error;
            } finally { await context.close(); }
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
