const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');
const { UiDriver, testUrl } = require('./ui-driver.cjs');
const output = path.resolve(process.env.WORLDBOX_ARTIFACT_DIR || 'artifacts/browser-tests');
fs.mkdirSync(output, { recursive: true });

(async () => {
    const browser = await chromium.launch(chromiumLaunchOptions());
    try {
        for (const mobile of [false, true]) {
            const context = await browser.newContext({ viewport: mobile ? { width: 390, height: 844 } : { width: 1280, height: 900 }, isMobile: mobile, hasTouch: mobile });
            const page = await context.newPage(); const ui = new UiDriver(page, { touch: mobile });
            const diagnostics = observeBrowserErrors(page);
            try {
                await page.goto(testUrl(process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/'));
                await ui.ready(); await ui.paused(); const before = await ui.save();
                await ui.openOverview(); await ui.click('inspector-residents');
                let snapshot = await ui.snapshot();
                assert.equal(ui.control(snapshot, 'residents-deceased').value, 'False');
                const rows = snapshot.controls.filter(c => /^resident-row-/.test(c.id));
                assert(rows.length <= 20 && rows.length > 0, 'Resident list must page its visible results');
                assert(rows[0].height < 90, 'Compact resident rows still waste vertical space');
                await ui.click('residents-next', { scroll: 'inspector-scroll' });
                assert.notEqual((await ui.snapshot()).controls.find(c => /^resident-row-/.test(c.id)).id, rows[0].id);
                await ui.click('resident-search', { scroll: 'inspector-scroll' });
                const virtualInput = async (inputType, data) => page.evaluate(({ inputType, data }) => {
                    const input = document.activeElement;
                    if (!input?.classList.contains('avalonia-input-element')) throw new Error('Native browser editor lost focus');
                    // Android 可能上报未知按键，仅通过 beforeinput 提供字符。
                    input.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: 'Unidentified', code: '' }));
                    input.dispatchEvent(new InputEvent('beforeinput', { bubbles: true, cancelable: true, inputType, data }));
                }, { inputType, data });
                await virtualInput('insertText', '林'); await virtualInput('insertText', '林');
                await ui.waitFor(s => ui.control(s, 'resident-search').value === '林林', 'repeated virtual keyboard characters');
                await virtualInput('deleteContentBackward', null);
                await ui.waitFor(s => ui.control(s, 'resident-search').value === '林', 'virtual keyboard deletion');
                await page.evaluate(() => {
                    const input = document.activeElement;
                    input.dispatchEvent(new CompositionEvent('compositionstart', { bubbles: true, data: '' }));
                    input.dispatchEvent(new CompositionEvent('compositionupdate', { bubbles: true, data: '山' }));
                    input.dispatchEvent(new CompositionEvent('compositionend', { bubbles: true, data: '山' }));
                    input.dispatchEvent(new InputEvent('beforeinput', { bubbles: true, cancelable: true, inputType: 'insertText', data: '山' }));
                });
                await ui.waitFor(s => ui.control(s, 'resident-search').value === '林山', 'single IME commit without duplication');
                await page.waitForTimeout(1500);
                assert.equal(ui.control(await ui.snapshot(), 'resident-search').value, '林山');
                assert.deepEqual(await ui.save(), before, 'Filtering, paging and IME editing modified the paused world');
                await ui.paused(false); await ui.click('resident-search', { scroll: 'inspector-scroll' });
                await page.keyboard.press('Space');
                await page.waitForTimeout(1300); snapshot = await ui.snapshot();
                assert.equal(snapshot.paused, false, 'Typing space paused the simulation');
                assert(snapshot.worldTick > before.Tick, 'World failed to advance while searching');
                assert.equal(ui.control(snapshot, 'resident-search').value, '林山 ');
                if (mobile) {
                    await page.setViewportSize({ width: 390, height: 500 });
                    await ui.waitFor(s => s.height === 500, 'layout after the keyboard opens');
                    assert.equal(ui.control(await ui.snapshot(), 'resident-search').value, '林山 ', 'Keyboard viewport change erased the editor');
                    await page.setViewportSize({ width: 390, height: 844 });
                    await ui.waitFor(s => s.height === 844 && ui.control(s, 'time-toggle').y > 700, 'layout after the keyboard closes');
                }
                await ui.paused(); await ui.fill('resident-search', '', { scroll: 'inspector-scroll' });
                await ui.waitFor(s => s.controls.some(c => /^resident-row-/.test(c.id)), 'cleared search results');
                const actor = (await ui.snapshot()).controls.find(c => /^resident-row-/.test(c.id));
                await ui.click(actor.id, { scroll: 'inspector-scroll' }); snapshot = await ui.snapshot();
                for (const tab of ['overview', 'residents', 'nations', 'history'])
                    assert(!snapshot.controls.some(c => c.id === `inspector-${tab}`), 'Object details retained unrelated global tabs');
                await ui.point('resident-body', { scroll: 'inspector-scroll' });
                assert(ui.control(await ui.snapshot(), 'resident-body').height <= 34, 'Collapsed headers still waste vertical space');
                await page.screenshot({ path: path.join(output, `details-${mobile ? 'mobile' : 'desktop'}.png`) });
                await diagnostics.assertHealthy('compact details and Android input sequence');
                console.log(`PASS ${mobile ? 'mobile' : 'desktop'}: compact pages, living defaults, virtual input, IME, focus and contextual detail`);
            } finally { await context.close(); }
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
