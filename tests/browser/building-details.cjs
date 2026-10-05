const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');
const { UiDriver, testUrl } = require('./ui-driver.cjs');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');
const fixture = path.resolve(process.argv[2] || 'artifacts/building-details/world.json');
const world = JSON.parse(fs.readFileSync(fixture, 'utf8'));
const output = path.resolve(process.env.WORLDBOX_ARTIFACT_DIR || 'artifacts/building-details');
const scroll = { scroll: 'inspector-scroll' };
fs.mkdirSync(output, { recursive: true });

(async () => {
    const browser = await chromium.launch(chromiumLaunchOptions());
    try {
        for (const mobile of [false, true]) {
            const context = await browser.newContext({ viewport: mobile ? { width: 390, height: 844 } : { width: 1280, height: 900 }, isMobile: mobile, hasTouch: mobile });
            const page = await context.newPage(); const ui = new UiDriver(page, { touch: mobile });
            const diagnostics = observeBrowserErrors(page); const name = mobile ? 'mobile' : 'desktop';
            try {
                await page.goto(testUrl(process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/'));
                await ui.ready(); await ui.paused(); await ui.click('header-storage');
                const chooser = page.waitForEvent('filechooser'); await ui.click('storage-import', { scroll: 'modal-scroll' });
                await (await chooser).setFiles(fixture);
                await ui.waitFor(s => !s.modalOpen && s.status.startsWith('导入成功'), 'fixture import', 60000);
                await ui.paused();
                const before = await ui.save();
                async function open(kind) {
                    const b = world.Society.Buildings.find(b => b.Kind === kind); assert(b, `Missing fixture kind ${kind}`);
                    await ui.openOverview(); await ui.click('overview-structures', scroll);
                    await ui.selectIndex('structures-building-kind', kind + 1, scroll);
                    await ui.click(`building-row-${b.Id}`, scroll);
                    if (mobile && !(await ui.snapshot()).inspectorExpanded) {
                        // The compact view initially shows half the screen; expand for review.
                        const expand = ui.control(await ui.snapshot(), 'inspector-expand');
                        if (expand.value === '展开') await ui.click('inspector-expand');
                    }
                    return ui.snapshot();
                }
                for (const kind of [26, 23, 24, 22, 3, 16, 27, 29, 31, 32, 33, 34]) {
                    const s = await open(kind);
                    const text = ['building-health', 'building-effects', 'building-next-level', 'building-status'].map(id => ui.control(s, id).value || '').join('\n');
                    for (const unwanted of ['结构完好', '完工且健康时', '需要完工、健康与运营条件', '来源：升级完工后'])
                        assert(!text.includes(unwanted), `Boilerplate survived: ${unwanted}`);
                    assert(!ui.control(s, 'building-effects').value.includes('建筑耐火'));
                    if (kind === 26) {
                        assert.match(ui.control(s, 'building-effects').value, /同聚落.*3 至 4 格/);
                        assert.match(ui.control(s, 'building-next-level').value, /4 至 5 格/);
                        assert.equal(ui.control(s, 'building-status').visible, false);
                    }
                    if (kind === 3) assert.match(ui.control(s, 'building-status').value, /至少 50/);
                    if (kind === 22) {
                        assert.equal((ui.control(s, 'building-effects').value.match(/供水量/g) || []).length, 1);
                        assert.doesNotMatch(text, /今日剩余|每日可取水|降水|补水/);
                        const well = world.Society.Buildings.find(b => b.Kind === kind);
                        const ground = world.Tiles[well.Y * world.Width + well.X];
                        const expected = Math.max(0, 30 * ground.w - .6);
                        assert(ui.control(s, 'building-effects').value.includes(`供水量 ${Number(expected.toFixed(3))} / 日`), 'Well kept its old surface-only yield');
                    }
                    if ([27, 29, 32, 33, 34].includes(kind)) assert(!ui.control(s, 'building-effects').value.includes('每批产出'));
                    await page.screenshot({ path: path.join(output, `${name}-${kind}.png`) });
                }
                assert.deepEqual(await ui.save(), before, 'Reading building details changed the world');
                await open(26); await ui.click('building-toggle', scroll);
                assert.match(ui.control(await ui.snapshot(), 'building-status').value, /已停用/);
                await page.screenshot({ path: path.join(output, `${name}-watchtower-disabled.png`) });
                await ui.click('building-toggle', scroll);
                assert.equal(ui.control(await ui.snapshot(), 'building-status').visible, false);
                await ui.click('building-upgrade', scroll); await ui.click('upgrade-gift', { scroll: 'modal-scroll' });
                assert.match(ui.control(await ui.snapshot(), 'building-effects').value, /3 至 5 格/);
                assert.match(ui.control(await ui.snapshot(), 'building-next-level').value, /5 至 6 格/);
                await page.screenshot({ path: path.join(output, `${name}-watchtower-upgraded.png`) });
                await diagnostics.assertHealthy(name + ' building details');
                console.log('PASS', name, '12 building kinds, unified well supply, read-only inspection, concrete disabled/damaged states, live upgrade values');
            } catch (error) {
                await page.screenshot({ path: path.join(output, `${name}-failure.png`) }).catch(() => {}); throw error;
            } finally { await context.close(); }
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
