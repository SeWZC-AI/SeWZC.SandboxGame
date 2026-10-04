const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');
const { UiDriver, testUrl } = require('./ui-driver.cjs');
const baseUrl = process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/';
const output = path.resolve(process.env.WORLDBOX_ARTIFACT_DIR || 'artifacts/browser-tests');
const modal = { scroll: 'modal-scroll' }, inspector = { scroll: 'inspector-scroll' };
fs.mkdirSync(output, { recursive: true });

(async () => {
    const browser = await chromium.launch(chromiumLaunchOptions());
    try {
        for (const mobile of [false, true]) {
            const context = await browser.newContext(mobile
                ? { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true }
                : { viewport: { width: 1440, height: 1040 } });
            const page = await context.newPage(); const ui = new UiDriver(page, { touch: mobile });
            const diagnostics = observeBrowserErrors(page);
            async function place(x, y) {
                await ui.clickTile(x, y);
                if (mobile && (await ui.snapshot()).pendingPlacement) await ui.click('placement-confirm');
                await ui.click('tool-suspend');
            }
            try {
                await page.goto(testUrl(baseUrl), { waitUntil: 'domcontentloaded' }); await ui.ready(); await ui.paused();
                await ui.click('header-new-world'); await ui.fill('world-seed', 42, modal);
                await ui.selectIndex('world-size', 0, modal); await ui.click('world-initial-life', modal); await ui.click('world-create-apply');
                await ui.tool('terrain', 'DryFertile');
                const snapshot = await ui.snapshot(); const slot = snapshot.toolSlots.indexOf('DryFertile');
                assert(ui.control(snapshot, `tool-slot-${slot}`).value.includes('旱原'), 'Terrain retained the requirement as a literal name');
                await place(64, 64);
                assert.equal((await ui.save()).Tiles[64 * 128 + 64].Terrain, 13, 'Dry terrain placement failed');
                await ui.tool('terrain', 'Wetland'); await place(64, 64);
                await ui.clickTile(64, 64); await ui.click('selection-view');
                const water = ui.control(await ui.snapshot(), 'tile-water').value;
                assert.match(water, /每日供水 1\.0000/); assert.match(water, /今日可取水 1\.0000/);
                await ui.point('tile-water', inspector);
                await page.screenshot({ path: path.join(output, `wetland-${mobile ? 'mobile' : 'desktop'}.png`) });
                await ui.click('inspector-close');
                await ui.tool('disaster', 'Fire'); await place(64, 64);
                let saved = await ui.save(); assert.equal(saved.FormatVersion, 10);
                assert.equal(saved.Tiles[64 * 128 + 64].FireTicks, 0, 'Normal wetland ignited');
                const before = saved.Tiles.filter(t => t.FireTicks > 0).length;
                await ui.tool('terrain', 'Forest'); await place(48, 48);
                assert.equal((await ui.save()).Tiles[48 * 128 + 48].Terrain, 4, 'Forest placement failed');
                await ui.tool('disaster', 'Fire'); await place(48, 48);
                saved = await ui.save();
                assert(saved.Tiles[48 * 128 + 48].FireTicks >= 60, 'Fire retained a very short burn duration');
                assert(saved.Tiles.filter(t => t.FireTicks > 0).length - before <= 3, 'A fire brush instantly ignited many tiles');
                await page.screenshot({ path: path.join(output, `local-fire-${mobile ? 'mobile' : 'desktop'}.png`) });
                for (const [terrain, code] of [['River', 10], ['Lake', 12]]) {
                    await ui.tool('terrain', terrain); await place(64, 64);
                    await ui.clickTile(64, 64); await ui.click('selection-view');
                    const details = ui.control(await ui.snapshot(), 'tile-water').value;
                    assert.match(details, /每日供水 无限/); assert.match(details, /今日可取水 无限/);
                    assert(!details.includes('Infinity'), 'An internal infinity value leaked into the inspector');
                    await ui.point('tile-water', inspector);
                    await page.screenshot({ path: path.join(output, `${terrain.toLowerCase()}-water-${mobile ? 'mobile' : 'desktop'}.png`) });
                    await ui.click('inspector-close');
                    assert.equal((await ui.save()).Tiles[64 * 128 + 64].Terrain, code, 'Fresh-water placement or saving failed');
                }
                await page.reload({ waitUntil: 'domcontentloaded' }); await ui.ready(); await ui.paused();
                await ui.clickTile(64, 64); await ui.click('selection-view');
                assert.match(ui.control(await ui.snapshot(), 'tile-water').value, /今日可取水 无限/);
                await ui.click('inspector-close');
                assert.equal((await ui.save()).Tiles[64 * 128 + 64].Terrain, 12, 'Unlimited lake water did not survive browser reload');
                await diagnostics.assertHealthy('terrain naming, finite wetland supply, unlimited fresh water and gradual fire');
                console.log(`PASS ${mobile ? 'mobile' : 'desktop'}: 旱原, wetland quota, unlimited rivers and lakes with reload, wet ground and local ignition`);
            } finally { await context.close(); }
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
