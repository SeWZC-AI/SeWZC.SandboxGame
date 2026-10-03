const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');
const { UiDriver, testUrl } = require('./ui-driver.cjs');
const baseUrl = process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/';
const output = path.resolve(process.env.WORLDBOX_ARTIFACT_DIR || path.join(__dirname, '../../artifacts/browser-tests'));
const modal = { scroll: 'modal-scroll' };
const inspector = { scroll: 'inspector-scroll' };
fs.mkdirSync(output, { recursive: true });

async function samples(page, duration) {
    return page.evaluate(async duration => {
        const result = []; const start = performance.now();
        while (performance.now() - start < duration) {
            const s = globalThis.worldboxTest.snapshot();
            result.push({ tick: s.worldTick, time: s.renderedEffectTime, count: s.renderedEffectCount });
            await new Promise(resolve => setTimeout(resolve, 25));
        }
        return result;
    }, duration);
}
(async () => {
    const browser = await chromium.launch(chromiumLaunchOptions());
    const page = await browser.newPage({ viewport: { width: 1440, height: 1040 } });
    const diagnostics = observeBrowserErrors(page); const ui = new UiDriver(page); const report = {};
    try {
        await page.goto(testUrl(baseUrl), { waitUntil: 'domcontentloaded' }); await ui.ready(); await ui.paused();
        await ui.click('header-new-world'); await ui.fill('world-seed', 42, modal);
        await ui.selectIndex('world-size', 0, modal); await ui.click('world-initial-life', modal); await ui.click('world-create-apply');
        await ui.tool('terrain', 'Forest'); await ui.clickTile(64, 64); await ui.click('tool-suspend');
        await ui.clickTile(64, 64);
        await ui.click('selection-view');
        await ui.click('tile-edit', inspector); await ui.fill('tile-resources', 155, modal);
        await ui.fill('tile-fertility', 75, modal); await ui.fill('tile-road', 2, modal); await ui.click('tile-apply', modal);
        let world = await ui.save();
        assert.equal(world.Tiles[64 * 128 + 64].ResourceAmount, 155);
        assert.equal(world.Tiles[64 * 128 + 64].RoadLevel, 2);
        await ui.click('inspector-close');
        await ui.click('header-rules'); await ui.click('rule-regeneration', modal); await ui.click('rule-fire-spread', modal);
        await ui.fill('rule-gathering-rate', 2, modal); await ui.fill('rule-combat-rate', '0.5', modal); await ui.click('world-rules-apply', modal);
        world = await ui.save();
        assert.equal(world.Rules.ResourceRegeneration, false); assert.equal(world.Rules.FireSpread, false);
        assert.equal(world.Rules.GatheringRate, 2); assert.equal(world.Rules.CombatDamageRate, .5);
        report.tileAndRules = true;
        await ui.tool('disaster', 'Fire'); await ui.clickTile(64, 64); await ui.click('tool-suspend');
        await ui.paused(false);
        const animated = await samples(page, 650);
        assert(animated.some((s, i) => i && s.count > 0 && s.tick === animated[i - 1].tick && s.time > animated[i - 1].time),
            'Actual drawn fire must animate within a simulation tick, even in an empty world');
        await ui.paused(); const frozen = await samples(page, 350);
        assert(frozen.every(s => s.tick === frozen[0].tick && s.time === frozen[0].time), 'Paused fire animation drifted');
        report.fire = { animated, frozen }; await page.screenshot({ path: path.join(output, 'effects-fire.png') });
        await ui.tool('disaster', 'Meteor'); await ui.clickTile(64, 64); await ui.click('tool-suspend');
        world = await ui.save(); const crater = world.Tiles[64 * 128 + 64];
        assert.equal(crater.Terrain, 2); assert.equal(crater.RoadLevel, 0); assert.equal(crater.ResourceAmount, 0);
        await ui.paused(false); await page.waitForTimeout(500); await page.screenshot({ path: path.join(output, 'effects-meteor.png') }); await ui.paused();
        report.meteor = true;
        const beforeCamera = await ui.save();
        for (let i = 0; i < 18; i++) await ui.click('map-zoom-in');
        assert(Math.abs((await ui.snapshot()).map.tileSize - 192) < .01, 'Zoom did not reach 24x');
        assert.deepEqual(await ui.save(), beforeCamera, 'High zoom changed world state');
        await page.screenshot({ path: path.join(output, 'effects-closeup.png') });
        report.zoom24 = true;
        await diagnostics.assertHealthy('effects and close-up rendering');
        fs.writeFileSync(path.join(output, 'effects-report.json'), JSON.stringify(report, null, 2));
        console.log('PASS real fire frames, pause freeze, meteor terrain impact, 24x read-only zoom, tile editor and ecology rules');
    } catch (error) {
        await page.screenshot({ path: path.join(output, 'effects-failure.png') }).catch(() => {}); throw error;
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
