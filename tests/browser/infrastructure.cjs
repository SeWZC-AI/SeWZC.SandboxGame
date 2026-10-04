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
                assert.equal(before.FormatVersion, 12);
                assert(before.Society.Buildings.some(b => b.Kind === 19), 'Fixture lacks the new shipyard');
                assert(before.Society.Buildings.some(b => b.Kind === 24), 'Fixture lacks real housing');
                const structures = async () => { await ui.openOverview(); await ui.click('overview-structures', { scroll: 'inspector-scroll' }); };
                const screenshot = async name => page.screenshot({ path: path.join(output, `${device}-${name}.png`) });
                await structures(); await ui.click('structures-map', { scroll: 'inspector-scroll' }); await ui.click('map-fit');
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
