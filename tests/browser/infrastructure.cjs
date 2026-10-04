// Run against the published app with the current --export-visual-fixture output.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');
const { UiDriver, testUrl } = require('./ui-driver.cjs');
const fixture = path.resolve(process.argv[2] || 'artifacts/town-verification/visual.worldbox.json');
const output = path.resolve(process.env.WORLDBOX_ARTIFACT_DIR || 'artifacts/town-verification/browser');
fs.mkdirSync(output, { recursive: true });

(async () => {
    const browser = await chromium.launch(chromiumLaunchOptions());
    try {
        for (const mobile of [false, true]) {
            const context = await browser.newContext({ viewport: mobile ? { width: 390, height: 844 } : { width: 1280, height: 900 },
                isMobile: mobile, hasTouch: mobile });
            try {
                const page = await context.newPage(); const ui = new UiDriver(page, { touch: mobile });
                const diagnostics = observeBrowserErrors(page); const device = mobile ? 'mobile' : 'desktop';
                await page.goto(testUrl(process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/'));
                await ui.ready(); await ui.paused(); await ui.click('header-storage');
                const picker = page.waitForEvent('filechooser'); await ui.click('storage-import', { scroll: 'modal-scroll' });
                await (await picker).setFiles(fixture);
                await ui.waitFor(s => !s.modalOpen && s.status.includes('导入'), 'infrastructure import', 60000);
                const before = await ui.save();
                assert.equal(before.FormatVersion, 13);
                assert(before.Society.Buildings.some(b => b.Kind === 19), 'Fixture lacks the new shipyard');
                assert(before.Society.Buildings.some(b => b.Kind === 24), 'Fixture lacks real housing');
                const structures = async () => { await ui.openOverview(); await ui.click('overview-structures', { scroll: 'inspector-scroll' }); };
                const screenshot = async name => page.screenshot({ path: path.join(output, `${device}-${name}.png`) });
                await structures();
                const center = before.Society.Buildings.find(b => b.Kind === 18);
                await ui.selectIndex('structures-town', before.Settlements.findIndex(t => t.Id === center.SettlementId) + 1, { scroll: 'inspector-scroll' });
                await ui.selectIndex('structures-building-kind', 19, { scroll: 'inspector-scroll' });
                await ui.click(`building-row-${center.Id}`, { scroll: 'inspector-scroll' });
                assert.match(ui.control(await ui.snapshot(), 'center-town-summary').value, /独占陆地.*城镇生效/s);
                await ui.click('center-town-info', { scroll: 'inspector-scroll' });
                let townInfo = await ui.waitFor(s => s.inspector === 'infrastructure', 'town center information link');
                assert.equal(ui.control(townInfo, 'infrastructure-town').value, String(before.Settlements.findIndex(t => t.Id === center.SettlementId)));
                assert.match(ui.control(townInfo, 'town-expansion-summary').value, /独占陆地/);
                if (mobile) {
                    await ui.click('inspector-expand');
                    assert(ui.control(await ui.snapshot(), 'inspector-panel').height > 500);
                }
                await screenshot('town-information');
                if (mobile) await ui.click('inspector-expand');
                await structures();
                await ui.selectIndex('structures-town', 0, { scroll: 'inspector-scroll' });
                await ui.selectIndex('structures-building-kind', 0, { scroll: 'inspector-scroll' });
                await ui.click('map-highlights-off', { scroll: 'inspector-scroll' });
                assert.equal(ui.control(await ui.snapshot(), 'map-overlay').value, '0');
                await structures();
                assert.equal(ui.control(await ui.snapshot(), 'map-overlay').value, '0', 'Reopening structures re-enabled markers');
                await ui.selectIndex('map-overlay', 10, { scroll: 'inspector-scroll' });
                await screenshot('resident-tasks');
                await ui.selectIndex('map-overlay', 9, { scroll: 'inspector-scroll' });
                await screenshot('fishing-boats');
                await ui.openOverview(); await ui.click('overview-guide', { scroll: 'inspector-scroll' });
                await ui.waitFor(s => s.inspector === 'guide', 'game guide');
                await structures(); await ui.selectIndex('map-overlay', 4, { scroll: 'inspector-scroll' });
                await ui.click('structures-map', { scroll: 'inspector-scroll' }); await ui.click('map-fit');
                const colored = await screenshot('infrastructure');
                await structures(); await ui.selectIndex('structures-town', 1, { scroll: 'inspector-scroll' });
                // Waystation remains enum 3, hence selection 4 after the All option.
                await ui.selectIndex('structures-building-kind', 4, { scroll: 'inspector-scroll' });
                const station = before.Society.Buildings.find(b => b.Kind === 3 && b.SettlementId === before.Settlements[0].Id);
                await ui.waitFor(s => s.controls.some(c => c.id === `building-row-${station.Id}`), 'town/type-filtered station');
                const filtered = await ui.snapshot();
                assert.equal(filtered.controls.filter(c => c.id.startsWith('building-row-')).length, 1, 'Town/type filter leaked unrelated rows');
                await screenshot('infrastructure-filters');
                await ui.click('structures-map', { scroll: 'inspector-scroll' });
                const selected = await screenshot('infrastructure-selected');
                assert(!colored.equals(selected), 'Town/type filtering produced no visible map change');
                await structures();
                await ui.click('structures-show-buildings', { scroll: 'inspector-scroll' });
                assert.equal(ui.control(await ui.snapshot(), 'structures-show-buildings').value, 'False');
                await ui.click('structures-map', { scroll: 'inspector-scroll' });
                const hidden = await screenshot('infrastructure-roads');
                assert(!selected.equals(hidden), 'Hiding buildings produced no visible map change');
                await structures(); await ui.click('structures-show-buildings', { scroll: 'inspector-scroll' });
                await ui.selectIndex('structures-town', 0, { scroll: 'inspector-scroll' });
                await ui.selectIndex('structures-building-kind', 0, { scroll: 'inspector-scroll' });
                await ui.selectIndex('structures-kind', 1, { scroll: 'inspector-scroll' });
                await ui.waitFor(s => s.controls.some(c => c.id.startsWith('road-row-')), 'real road rows');
                await ui.click('structures-map', { scroll: 'inspector-scroll' });
                assert.deepEqual(await ui.save(), before, 'Infrastructure observation changed the paused world');
                await diagnostics.assertHealthy(`${device} infrastructure colors, filters and roads`);
                console.log(`PASS ${device}: building colors, town/type filters, map visibility, real road list and read-only save`);
            } finally { await context.close(); }
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
