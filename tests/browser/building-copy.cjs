const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { UiDriver, testUrl } = require('./ui-driver.cjs');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');

const output = path.resolve(process.env.WORLDBOX_ARTIFACT_DIR || 'artifacts/building-copy');
const baseUrl = process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/';
const scroll = { scroll: 'inspector-scroll' };
const modal = { scroll: 'modal-scroll' };
fs.mkdirSync(output, { recursive: true });

(async () => {
    const browser = await chromium.launch(chromiumLaunchOptions());
    try {
        for (const touch of [false, true]) {
            const context = await browser.newContext({
                viewport: touch ? { width: 390, height: 844 } : { width: 1440, height: 960 },
                isMobile: touch, hasTouch: touch
            });
            const page = await context.newPage();
            const ui = new UiDriver(page, { touch });
            const diagnostics = observeBrowserErrors(page);
            const name = touch ? 'mobile' : 'desktop';
            try {
                await page.goto(testUrl(baseUrl));
                await ui.ready(); await ui.paused(); await ui.openOverview();
                await ui.click('overview-infrastructure', scroll);
                await ui.click('building-open', scroll);
                assert.equal(ui.control(await ui.snapshot(), 'building-bridge-direction').visible, false);
                await ui.click('building-x', modal);
                await page.keyboard.press('ControlOrMeta+A');
                await page.keyboard.press('Backspace'); await page.keyboard.press('Tab');
                await ui.waitFor(s => /整数坐标/.test(ui.control(s, 'building-placement-status').value), 'empty coordinate feedback');
                await ui.fill('building-x', '0', modal); await ui.fill('building-y', '0', modal);
                assert.match(ui.control(await ui.snapshot(), 'building-placement-status').value, /暂不能建造/);
                await ui.click('building-gift', modal);
                assert.match(ui.control(await ui.snapshot(), 'building-placement-status').value, /暂不能建造/,
                    'Gifting must not hide an invalid site');
                await ui.point('building-placement-status', modal);
                await page.screenshot({ path: path.join(output, `${name}-placement.png`) });
                await ui.selectIndex('building-kind', 15, modal); // 桥梁。
                await ui.point('building-bridge-direction', modal);
                assert.equal(ui.control(await ui.snapshot(), 'building-bridge-direction').visible, true);
                await ui.selectIndex('building-kind', 16, modal); // 码头。
                assert.equal(ui.control(await ui.snapshot(), 'building-bridge-direction').visible, false);
                assert.match(ui.control(await ui.snapshot(), 'building-requirements').value, /紧邻自然陆岸/);
                await ui.click('modal-close');
                await ui.openOverview(); await ui.click('overview-structures', scroll);
                await ui.selectIndex('structures-building-kind', 19, scroll); // 城镇中心，索引包含“全部”选项。
                const centre = (await ui.snapshot()).controls.find(c => c.id.startsWith('building-row-'));
                assert(centre, 'Demo world must expose a town centre');
                await ui.click(centre.id, scroll);
                for (let level = 2; level <= 3; level++) {
                    await ui.click('building-upgrade', scroll);
                    assert.match(ui.control(await ui.snapshot(), 'upgrade-gift-status').value, /不扣施工材料/);
                    await ui.click('upgrade-gift', modal);
                    assert.match(ui.control(await ui.snapshot(), 'building-level').value, new RegExp(`等级 ${level} / 3`));
                }
                await ui.click('building-upgrade', scroll);
                assert(!(await ui.snapshot()).controls.some(c => c.id === 'upgrade-apply' || c.id === 'upgrade-gift'),
                    'Maximum level must not offer unusable upgrade actions');
                await ui.point('upgrade-close', modal);
                await page.screenshot({ path: path.join(output, `${name}-maximum-level.png`) });
                await diagnostics.assertHealthy(`${name} building copy`);
                console.log('PASS', name, 'placement feedback, contextual fields, gift upgrade and maximum level');
            } catch (error) {
                await page.screenshot({ path: path.join(output, `${name}-failure.png`) }).catch(() => {});
                throw error;
            } finally { await context.close(); }
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
