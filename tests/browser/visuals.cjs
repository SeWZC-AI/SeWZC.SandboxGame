// 图形和生命值回归；先用 --export-visual-fixture 导出夹具。
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');
const { UiDriver, testUrl } = require('./ui-driver.cjs');
const fixture = path.resolve(process.argv[2] || 'artifacts/visual-verification/visual.worldbox.json');
const output = path.resolve(process.env.WORLDBOX_ARTIFACT_DIR || 'artifacts/visual-verification/browser');
fs.mkdirSync(output, { recursive: true });

(async () => {
    const browser = await chromium.launch(chromiumLaunchOptions());
    try {
        for (const mobile of [false, true]) {
            const context = await browser.newContext({ viewport: mobile ? { width: 390, height: 844 } : { width: 1280, height: 900 }, isMobile: mobile, hasTouch: mobile });
            try {
                const page = await context.newPage(); const ui = new UiDriver(page, { touch: mobile }); const diagnostics = observeBrowserErrors(page);
                await page.goto(testUrl(process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/'));
                await ui.ready(); await ui.paused(); await ui.click('header-storage');
                const picker = page.waitForEvent('filechooser'); await ui.click('storage-import', { scroll: 'modal-scroll' });
                await (await picker).setFiles(fixture); await ui.waitFor(s => !s.modalOpen && s.status.includes('导入'), 'visual fixture import', 60000);
                const before = await ui.save(); const stations = before.Society.Buildings.filter(b => b.Kind === 3);
                assert.equal(stations.length, 4, 'Fixture must expose four styles of the same building');
                for (let i = 0; i < stations.length; i++) {
                    const station = stations[i]; await ui.openOverview(); await ui.click('overview-structures', { scroll: 'inspector-scroll' });
                    await ui.fill('structures-search', '驿站', { scroll: 'inspector-scroll' });
                    await ui.waitFor(s => s.controls.filter(c => c.id.startsWith('building-row-')).length === stations.length
                        && s.controls.some(c => c.id === `building-row-${station.Id}`), 'settled station search');
                    await ui.click(`building-row-${station.Id}`, { scroll: 'inspector-scroll' });
                    await ui.waitFor(s => s.inspector === 'building' && s.controls.some(c => c.id === 'building-health'), 'opened building details');
                    let s = await ui.snapshot(); const health = ui.control(s, 'building-health');
                    const status = ui.control(s, 'building-status');
                    assert(health.visible && health.value.includes('23 / 100') && status.visible && status.value.includes('低于 50'), 'Damage and consequences must be visible outside the fold');
                    assert.equal(ui.control(s, 'building-condition').value, 'False');
                    await ui.point('building-condition', { scroll: 'inspector-scroll' }); s = await ui.snapshot();
                    assert(ui.control(s, 'building-condition').height <= 34, 'Collapsed building header remains oversized');
                    const icons = ['inspector-back', 'inspector-expand', 'inspector-close'].map(id => ui.control(s, id)).filter(c => c.visible);
                    assert(icons.every(c => Math.abs(c.height - 30) <= 1), 'Header buttons have different heights');
                    const buttons = ['building-locate', 'building-repair', 'building-toggle'].map(id => ui.control(s, id));
                    for (let j = 1; j < buttons.length; j++) if (Math.abs(buttons[j].y - buttons[j - 1].y) < 1)
                        assert(buttons[j].x - buttons[j - 1].x - buttons[j - 1].width >= 3.9, 'Adjacent buttons have no gap');
                    if (i === 0) await page.screenshot({ path: path.join(output, `damaged-${mobile ? 'mobile' : 'desktop'}.png`) });
                    await ui.click('building-locate', { scroll: 'inspector-scroll' });
                    for (let zoom = 0; zoom < 12 && (await ui.snapshot()).map.tileSize < 48; zoom++)
                        await ui.click('map-zoom-in');
                    await ui.waitFor(s => s.renderedBuildingLabelCount > 0 && s.renderedWildlifeCount > 0 && s.renderedPlantCount > 0, 'close map labels and resources');
                    await page.screenshot({ path: path.join(output, `${mobile ? 'mobile' : 'desktop'}-race-${i}.png`) });
                }
                const centre = before.Society.Buildings.find(b => b.Kind === 18);
                assert(centre, 'Fixture has no tall town centre');
                await ui.openOverview(); await ui.click('overview-structures', { scroll: 'inspector-scroll' });
                await ui.fill('structures-search', '城镇中心', { scroll: 'inspector-scroll' });
                await ui.waitFor(s => s.controls.some(c => c.id === `building-row-${centre.Id}`), 'filtered town centre');
                await ui.click(`building-row-${centre.Id}`, { scroll: 'inspector-scroll' });
                await ui.click('building-locate', { scroll: 'inspector-scroll' });
                let camera = (await ui.snapshot()).map;
                const roof = { x: camera.tile0CenterX + centre.X * camera.tileSize, y: camera.tile0CenterY + (centre.Y - 1.1) * camera.tileSize };
                if (mobile) await page.touchscreen.tap(roof.x, roof.y); else await page.mouse.click(roof.x, roof.y);
                const rear = before.Society.Buildings.find(b => b.X === centre.X && b.Y === centre.Y - 1);
                assert(rear, 'Fixture lacks a building behind the tall centre');
                await ui.waitFor(s => s.selectedBuildingId === rear.Id, 'select the rear plot through an overlapping roof');
                await page.screenshot({ path: path.join(output, `occlusion-${mobile ? 'mobile' : 'desktop'}.png`) });
                await ui.openOverview();
                await ui.click('map-plants', { scroll: 'inspector-scroll' }); await ui.waitFor(s => s.renderedPlantCount === 0, 'hidden plant markers');
                await ui.click('map-plants', { scroll: 'inspector-scroll' });
                await ui.click('map-wildlife', { scroll: 'inspector-scroll' }); await ui.waitFor(s => s.renderedWildlifeCount === 0, 'hidden animal markers');
                await ui.click('map-wildlife', { scroll: 'inspector-scroll' });
                await ui.click('map-legend', { scroll: 'inspector-scroll' });
                await page.screenshot({ path: path.join(output, `legend-${mobile ? 'mobile' : 'desktop'}.png`) });
                await ui.click('inspector-close');
                assert.deepEqual(await ui.save(), before, 'Art, legends and resource visibility altered the paused world');
                await diagnostics.assertHealthy('race artwork, damage, folds and ecological markers');
                console.log(`PASS ${mobile ? 'mobile' : 'desktop'}: four architecture/race styles, visible damage, compact controls, plant/animal markers and read-only observation`);
            } finally { await context.close(); }
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
