const { chromium } = require('playwright');
const { createHash } = require('node:crypto');
const digest = value => createHash('sha256').update(JSON.stringify(value)).digest('hex');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { UiDriver, testUrl, readSavedWorld } = require('./ui-driver.cjs');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');

const output = path.resolve(process.env.WORLDBOX_ARTIFACT_DIR || 'artifacts/browser-tests');
const baseUrl = process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/';
const scroll = { scroll: 'inspector-scroll' };
fs.mkdirSync(output, { recursive: true });

// A current-format UI fixture imported through the ordinary file picker. Core tests
// separately verify that these states are produced by real work and physical reports.
function fixture(world) {
    const home = world.Settlements[0], nation = world.Nations[0], actor = world.Residents[0];
    const enemy = world.Settlements.find(t => t.NationId !== nation.Id);
    world.Tick = Math.max(24, world.Tick);
    const make = (kind, action, text, importance = 1, cause = 0) => {
        const event = { Id: world.NextId++, Tick: world.Tick, Kind: kind, Action: action, Message: text,
            Importance: importance, NationId: nation.Id, SecondNationId: 0, ResidentId: actor.Id,
            SettlementId: home.Id, SecondSettlementId: 0, EvidenceFactId: 0, CauseEventId: cause,
            AdditionalCauseEventIds: [], X: home.X, Y: home.Y };
        world.Events.push(event); return event;
    };
    const start = make(13, 1, '场景：开始建设');
    const deliveries = [1, 2, 3].map(i => make(2, 13, `场景：实物交付 ${i}`, 1, start.Id));
    const war = make(4, 4, '场景：有限占领军令', 2, deliveries[2].Id);
    war.ResidentId = 0; // Shared military event: this resident's participation is recorded in their history.
    nation.Military = { ...nation.Military, CampaignEventId: war.Id, EnemyNationId: enemy.NationId,
        Objective: 0, TargetSettlementId: enemy.Id, TargetX: enemy.X, TargetY: enemy.Y,
        StartedTick: world.Tick, Report: '尚未收到前线战报' };
    const building = world.Society.Buildings.find(b => b.SettlementId === home.Id);
    building.ConstructionProgress = 10; building.ConstructionRequired = 30;
    building.Observation = { StartEventId: start.Id, Contributors: [actor.Id], DevelopmentRate: world.Rules.DevelopmentRate,
        Samples: [1, 4, 7, 10].map((progress, i) => ({ Tick: world.Tick - 12 + i * 4, Progress: progress })) };
    actor.History.push({ Tick: world.Tick, Text: '场景：响应征召', Importance: 2, Experience: 0, Impact: 0,
        PlayerEdited: false, EventId: war.Id, SettlementId: home.Id, NationId: nation.Id, EvidenceFactId: 0 });
    return { world, home, nation, actor, start, deliveries, war };
}

(async () => {
    const browser = await chromium.launch(chromiumLaunchOptions());
    try {
        for (const mobile of [false, true]) {
            const label = mobile ? 'mobile' : 'desktop';
            const context = await browser.newContext({ viewport: mobile ? { width: 390, height: 844 } : { width: 1440, height: 960 },
                hasTouch: mobile, isMobile: mobile, deviceScaleFactor: 1, acceptDownloads: true });
            const page = await context.newPage(), ui = new UiDriver(page, { touch: mobile });
            const diagnostics = observeBrowserErrors(page);
            try {
                await page.goto(testUrl(baseUrl)); await ui.ready(); await ui.paused();
                const data = fixture(await ui.save());
                const filename = path.join(output, `story-fixture-${label}.json`);
                fs.writeFileSync(filename, JSON.stringify(data.world));
                await ui.click('header-storage');
                const chooser = page.waitForEvent('filechooser');
                await ui.click('storage-import', { scroll: 'modal-scroll' });
                await (await chooser).setFiles(filename);
                await ui.waitFor(s => !s.modalOpen && s.status.startsWith('导入成功'), 'story fixture import', 30000);
                const baseline = await ui.save();

                await ui.click('header-overview'); await ui.click('inspector-nations');
                await ui.click(`nation-row-${data.nation.Id}`, scroll); await ui.click('nation-follow', scroll);
                assert.match(ui.control(await ui.snapshot(), 'nation-military').value, /有限占领[\s\S]*尚未收到前线战报/);
                await ui.click('inspector-overview'); await ui.click('overview-infrastructure', scroll);
                await ui.click('settlement-watch', scroll);
                assert.match(ui.control(await ui.snapshot(), 'development-estimate').value, /预计还需约/);
                await ui.click('inspector-residents'); await ui.click(`resident-row-${data.actor.Id}`, scroll);
                await ui.click('resident-watch', scroll); await ui.click('resident-story', scroll);
                await ui.waitFor(s => s.inspector === 'story', 'resident story');
                assert.equal(ui.control(await ui.snapshot(), 'story-watch').value, 'True');
                await page.screenshot({ path: path.join(output, `story-${label}.png`) });

                await ui.click('inspector-overview'); await ui.click('overview-watched', scroll);
                const grouped = ui.control(await ui.snapshot(), `history-row-${data.deliveries[2].Id}`);
                assert.match(grouped.value, /同类事件 3 次/);
                assert(!(await ui.snapshot()).controls.some(c => c.id === `history-row-${data.deliveries[0].Id}`));
                await ui.click(`history-row-${data.deliveries[2].Id}`, scroll);
                for (const event of data.deliveries) ui.control(await ui.snapshot(), `event-member-${event.Id}`);
                await ui.click(`event-cause-${data.start.Id}`, scroll);
                await ui.waitFor(s => s.inspector === 'event', 'cause detail');
                ui.control(await ui.snapshot(), `history-row-${data.deliveries[0].Id}`);
                await page.screenshot({ path: path.join(output, `event-causes-${label}.png`) });

                await ui.click('inspector-nations'); await ui.click(`nation-row-${data.nation.Id}`, scroll);
                await ui.click('nation-follow', scroll);
                await ui.click('inspector-overview'); await ui.click('overview-infrastructure', scroll);
                await ui.click('settlement-watch', scroll);
                await ui.click('inspector-history'); await ui.click('history-watched', scroll);
                await ui.selectIndex('history-importance', 2, scroll);
                assert((await ui.snapshot()).controls.some(c => c.id === `history-row-${data.war.Id}`));
                assert.equal(digest(await ui.save()), digest(baseline), 'Attention, stories, causal navigation and estimates must leave the paused world unchanged');
                console.log(`PASS ${label}: observation leaves the paused world unchanged`);
                await page.reload(); await ui.ready();
                assert.equal(digest(await readSavedWorld(page)), digest(baseline), 'IndexedDB did not preserve complete story and project metadata');
                await ui.paused();
                await ui.click('header-overview'); await ui.click('overview-watched', scroll);
                assert.equal((await ui.snapshot()).controls.filter(c => c.id.startsWith('history-row-')).length, 0,
                    'Session attention must reset on loading a world');
                const resumed = await ui.save();
                assert(resumed.Tick >= baseline.Tick, 'Reload must resume this world');
                assert.equal(resumed.Nations.find(n => n.Id === data.nation.Id).Military.CampaignEventId, data.war.Id);
                assert(resumed.Residents.find(r => r.Id === data.actor.Id).History.some(h => h.EventId === data.war.Id));
                await diagnostics.assertHealthy(`stories ${label}`);
                console.log(`PASS ${label}: multi-object attention, stories, causal links, grouped originals, ETA, read-only observations and save/reload`);
            } catch (error) {
                await page.screenshot({ path: path.join(output, `stories-failure-${label}.png`) }).catch(() => {});
                fs.writeFileSync(path.join(output, `stories-failure-${label}.json`), JSON.stringify(await ui.snapshot().catch(() => ({})), null, 2));
                throw error;
            } finally { await context.close(); }
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
