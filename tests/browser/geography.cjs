// Exercise generated geography and the added terrain tools through real UI input.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');
const { UiDriver, testUrl } = require('./ui-driver.cjs');
const output = path.resolve(process.env.WORLDBOX_ARTIFACT_DIR || 'artifacts/geography-browser');
fs.mkdirSync(output, { recursive: true });
(async () => {
    const browser = await chromium.launch(chromiumLaunchOptions());
    try {
        for (const mobile of [false, true]) {
            const context = await browser.newContext(mobile ? { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true }
                : { viewport: { width: 1440, height: 1040 } });
            try {
                const page = await context.newPage(), ui = new UiDriver(page, { touch: mobile });
                const diagnostics = observeBrowserErrors(page);
                await page.goto(testUrl(process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/'));
                await ui.ready(); await ui.paused();
                const generated = await ui.save();
                assert.equal(generated.FormatVersion, 14);
                assert.equal(generated.Settlements.length, 4);
                assert(generated.Tiles.every(t => Number.isFinite(t.rain) && t.rain >= 0));
                assert(generated.Tiles.some(t => t.RiverWidth === 1));
                assert(generated.Tiles.some(t => t.RiverWidth >= 2));
                // Generation above uses the real default map. Repeated editing/save
                // assertions need only the smaller UI-supported map, reducing payloads.
                const modal = { scroll: 'modal-scroll' };
                await ui.click('header-new-world'); await ui.fill('world-seed', 42, modal);
                await ui.selectIndex('world-size', 0, modal); await ui.click('world-create-apply');
                await ui.waitFor(s => !s.modalOpen && s.paused, 'new small geography world');
                for (const [terrain, code, width] of [['Stream',14,1], ['LargeRiver',15,4], ['Meadow',16,0], ['Woodland',17,0],
                    ['Rainforest',18,0], ['Savanna',19,0], ['Scrub',20,0], ['Floodplain',21,0], ['AlpineMeadow',22,0]]) {
                    await ui.tool('terrain', terrain);
                    await ui.clickTile(64,64);
                    if (mobile && (await ui.snapshot()).pendingPlacement) await ui.click('placement-confirm');
                    await ui.click('tool-suspend');
                    const saved = await ui.save(), tile = saved.Tiles[64 * saved.Width + 64];
                    assert.equal(tile.Terrain,code, `${terrain} tool failed`);
                    assert.equal(tile.RiverWidth || 0,width, `${terrain} width failed`);
                    await ui.clickTile(64,64); await ui.click('selection-view');
                    const detail = ui.control(await ui.snapshot(),'tile-water').value;
                    assert.match(detail,/生成海拔/); assert.match(detail,/降水/); assert.match(detail,/河湖补水/);
                    assert.match(detail,/人类：/); assert.match(detail,/精灵：/); assert.match(detail,/矮人：/); assert.match(detail,/兽人：/);
                    assert.doesNotMatch(detail,/水道宽度|未取用的水不累计/);
                    if (terrain === 'Stream') assert.match(detail,/可涉水，速度较慢/);
                    if (terrain === 'AlpineMeadow') await page.screenshot({path:path.join(output,`geography-${mobile?'mobile':'desktop'}.png`)});
                    await ui.click('inspector-close');
                    assert.deepEqual(await ui.save(),saved,'Geography inspection changed the paused world');
                }
                await page.reload(); await ui.ready(); await ui.paused();
                const restored = await ui.save(); assert.equal(restored.Tiles[64 * restored.Width + 64].Terrain,22);
                await diagnostics.assertHealthy('geography generation, nine terrain tools and save reload');
                console.log(`PASS ${mobile?'mobile':'desktop'}: climate fields, river widths, nine terrains, racial adaptation details and read-only save reload`);
            } finally { await context.close(); }
        }
    } finally { await browser.close(); }
})().catch(error => {console.error(error);process.exit(1);});
