const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { createHash } = require('node:crypto');
const { execFileSync } = require('node:child_process');
const { UiDriver, testUrl } = require('./ui-driver.cjs');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');
const output = path.resolve(process.env.WORLDBOX_ARTIFACT_DIR || 'artifacts/browser-tests');
const baseUrl = process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/';
const modal = { scroll: 'modal-scroll' }, inspector = { scroll: 'inspector-scroll' };
const digest = value => createHash('sha256').update(JSON.stringify(value)).digest('hex');
fs.mkdirSync(output, { recursive: true });
execFileSync('python3', ['-m', 'zipfile', '-e', path.resolve('docs/saves/empire-saves-20261004.zip'), path.join(output, 'saves')]);

(async () => {
    const browser = await chromium.launch(chromiumLaunchOptions());
    try {
        for (const mobile of [false, true]) for (const route of ['technology', 'magic']) {
            const label = `${route}-${mobile ? 'mobile' : 'desktop'}`;
            const context = await browser.newContext({ viewport: mobile ? { width: 390, height: 844 } : { width: 1440, height: 1040 }, hasTouch: mobile, isMobile: mobile, acceptDownloads: true });
            const page = await context.newPage(), ui = new UiDriver(page, { touch: mobile });
            const errors = observeBrowserErrors(page);
            try {
                await page.goto(testUrl(baseUrl)); await ui.ready(); await ui.paused();
                const filename = path.join(output, 'saves', `${route}-empire.worldbox.json`);
                const expected = JSON.parse(fs.readFileSync(filename, 'utf8'));
                await ui.click('header-storage'); const chooser = page.waitForEvent('filechooser');
                await ui.click('storage-import', modal); await (await chooser).setFiles(filename);
                await ui.waitFor(s => !s.modalOpen && s.status.startsWith('导入成功'), 'delivered empire save import', 60000);
                const before = await ui.save();
                assert.equal(digest(before), digest(expected), 'Import changed the delivered simulated world');
                await ui.click('header-overview'); await ui.click('overview-infrastructure', inspector);
                const completedTown = expected.Settlements.findIndex(t => expected.Society.Research.find(r => r.SettlementId === t.Id).Completed.includes(route === 'technology' ? 18 : 23));
                assert(completedTown >= 0, 'Delivered world has no empire knowledge');
                await ui.selectIndex('infrastructure-town', completedTown, inspector);
                await ui.click('research-expand', inspector);
                const endpoint = route === 'technology' ? 'TechnologicalEmpire' : 'MagicalEmpire';
                await ui.click(`research-node-${endpoint}`, inspector);
                let snapshot = await ui.snapshot();
                assert.match(ui.control(snapshot, 'research-selected').value, route === 'technology' ? /科技帝国/ : /魔法帝国/);
                assert.equal(ui.control(snapshot, `research-state-${endpoint}`).value, '已掌握');
                assert.equal(ui.control(snapshot, 'research-start').enabled, false);
                assert(!snapshot.controls.some(c => c.id === 'research-kind'), 'Research still uses a dropdown');
                assert.equal(snapshot.controls.filter(c => c.id.startsWith('research-node-')).length, 24);
                await ui.point('research-selected', inspector);
                await page.screenshot({ path: path.join(output, `${label}.png`) });
                assert.equal(digest(await ui.save()), digest(before), 'Reading or expanding the technology tree changed the world');
                await errors.assertHealthy(`real empire tree ${label}`);
                fs.writeFileSync(path.join(output, `${label}.json`), JSON.stringify({ tick: before.Tick, population: before.Residents.length, nodes: 24, stage: endpoint, errors: [] }, null, 2));
                console.log(`PASS ${label}: actual final save import, 24 nodes, endpoint status, responsive view, read-only tree`);
            } catch (error) {
                await page.screenshot({ path: path.join(output, `${label}-failure.png`) });
                fs.writeFileSync(path.join(output, `${label}-failure.json`), JSON.stringify(await ui.snapshot(), null, 2));
                throw error;
            } finally { await context.close(); }
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
