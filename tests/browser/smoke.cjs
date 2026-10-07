const { chromium } = require('playwright');
const fs = require('node:fs');
const assert = require('node:assert/strict');
const path = require('node:path');
const { chromiumLaunchOptions, observeBrowserErrors } = require('./browser-support.cjs');
const { UiDriver, testUrl } = require('./ui-driver.cjs');

const output = process.env.WORLDBOX_ARTIFACT_DIR ? path.resolve(process.env.WORLDBOX_ARTIFACT_DIR) : path.resolve(__dirname, '../../artifacts/browser-tests');
const baseUrl = process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/';
fs.mkdirSync(output, { recursive: true });
const scroll = { scroll: 'inspector-scroll' };
const modal = { scroll: 'modal-scroll' };
const value = (object, key) => object[key] ?? 0;
const resident = (world, id) => world.Residents.find(item => item.Id === id);
const town = (world, id) => world.Settlements.find(item => item.Id === id);
const stock = (world, id) => town(world, id).Resources;

(async () => {
    const browser = await chromium.launch(chromiumLaunchOptions());
    const context = await browser.newContext({ viewport: { width: 1440, height: 960 }, acceptDownloads: true });
    const page = await context.newPage();
    const diagnostics = observeBrowserErrors(page);
    const ui = new UiDriver(page);
    const results = [];
    const passed = text => { results.push(text); console.log('PASS', text); };
    try {
        // 普通站点地址不开放测试诊断入口。
        await page.goto(baseUrl, { waitUntil: 'domcontentloaded' });
        await page.waitForFunction(() => document.querySelector('canvas')?.width > 0 && !document.querySelector('.loading'), {}, { timeout: 60000 });
        assert.equal(await page.evaluate(() => typeof globalThis.worldboxTest), 'undefined');
        await diagnostics.assertHealthy('desktop normal URL');
        await page.goto(testUrl(baseUrl), { waitUntil: 'domcontentloaded' });
        await ui.ready();
        await ui.paused();
        await diagnostics.assertHealthy('desktop test URL');
        const initialTick = (await ui.snapshot()).worldTick;
        await ui.paused(false);
        await ui.waitFor(snapshot => snapshot.worldTick >= initialTick + 4, 'ordinary residents choosing work');
        await ui.paused();
        const baseline = await ui.save();
        assert.equal(baseline.FormatVersion, 17);
        assert.equal(baseline.Width, 256);
        assert.equal(baseline.Nations.length, 4);
        const home = baseline.Settlements[0];
        const actor = baseline.Residents[0];
        passed('published subpath, opt-in read-only inspector, current-format IndexedDB save');

        for (const settlement of baseline.Settlements) {
            assert.equal(baseline.Society.Buildings.filter(b => b.Kind === 18 && b.SettlementId === settlement.Id
                && b.X === settlement.X && b.Y === settlement.Y).length, 1);
            assert(settlement.Name.endsWith(settlement.Population >= 160 ? '城' : settlement.Population >= 60 ? '镇' : '村'));
        }
        await ui.click('header-overview');
        await ui.selectIndex('map-resources', 1, scroll);
        await ui.click('overview-structures', scroll);
        const center = baseline.Society.Buildings.find(b => b.Kind === 18 && b.SettlementId === home.Id);
        await ui.click(`building-row-${center.Id}`, scroll);
        assert.equal((await ui.snapshot()).inspector, 'building', 'A building must open its own details');
        assert(ui.control(await ui.snapshot(), 'building-repair').enabled);
        await ui.click('building-ground', scroll);
        assert.equal((await ui.snapshot()).inspector, 'tile');
        await ui.click('inspector-back');
        assert.equal((await ui.snapshot()).inspector, 'building', 'Back preserves the selected building');
        assert.deepEqual(await ui.save(), baseline, 'Resource choices and detailed observation are read-only');
        await ui.click('inspector-close');
        passed('village names, real centers, building details and resource display without changing resident knowledge');

        const worker = baseline.Residents.slice(0, 60).find(person => person.Agent.Goal.Kind === 3
            && baseline.Society.Buildings.some(building => building.Id === person.Agent.Goal.TargetEntityId));
        assert(worker, 'Normal simulation must produce an inspectable resident targeting actual building work');
        await ui.click('header-overview');
        await ui.openOverview(); await ui.click('inspector-residents', scroll);
        await ui.openResidentRow(worker.Id, scroll);
        await ui.click('resident-goal-edit', scroll);
        assert(Number(ui.control(await ui.snapshot(), 'resident-goal-entity').value) > 0,
            'The automatic building target must be selected in the goal editor');
        await ui.fill('resident-courage', '0.42', modal);
        await ui.click('resident-goal-apply');
        const personalityEdit = await ui.save();
        const editedWorker = resident(personalityEdit, worker.Id);
        assert.equal(editedWorker.Agent.Personality.Courage, 0.42);
        assert.deepEqual(editedWorker.Agent.Goal, worker.Agent.Goal,
            'Editing only personality must preserve the automatic target, deadline and work progress');
        assert.deepEqual(editedWorker.Inventory, worker.Inventory);
        assert.deepEqual(editedWorker.Agent.CarriedMessages, worker.Agent.CarriedMessages);
        assert.deepEqual(personalityEdit.Settlements.map(item => item.Resources), baseline.Settlements.map(item => item.Resources));
        await ui.click('header-storage');
        await ui.click('storage-undo', modal);
        assert.deepEqual(await ui.save(), baseline, 'Personality edit undo must restore the complete prior world');
        passed('real personality editing preserves an automatically selected building goal and actual supplies');

        await ui.tool('life', 'Human');
        await ui.clickTile(home.X, home.Y);
        const spawned = await ui.save();
        assert.equal(spawned.Residents.length, baseline.Residents.length + 12);
        assert.equal(spawned.Tick, baseline.Tick);
        const geometry = (await ui.snapshot()).map;
        const toolbarY = ui.control(await ui.snapshot(), 'tool-category-terrain').y;
        const paintIndex = baseline.Tiles.findIndex((tile, index) => {
            const x = geometry.tile0CenterX + (index % baseline.Width) * geometry.tileSize;
            const y = geometry.tile0CenterY + Math.floor(index / baseline.Width) * geometry.tileSize;
            return value(tile, 'Terrain') < 2 && x > geometry.x + 80 && x < geometry.x + geometry.width - 80 && y > geometry.y + 150 && y < toolbarY - 50;
        });
        assert(paintIndex >= 0, 'The visible seeded map needs an ocean tile for the brush check');
        await ui.tool('terrain', 'Grass');
        await ui.clickTile(paintIndex % baseline.Width, Math.floor(paintIndex / baseline.Width));
        const painted = await ui.save();
        assert.equal(painted.Tiles[paintIndex].Terrain, 3);
        await ui.click('header-storage');
        await ui.click('storage-undo', modal);
        const undone = await ui.save();
        assert.deepEqual(undone, baseline, 'Batch undo must restore the complete pre-edit world');
        passed('resident placement, real terrain painting, paused time, complete batch undo');

        await ui.click('header-overview');
        await ui.openOverview(); await ui.click('inspector-residents', scroll);
        await ui.openResidentRow(actor.Id, scroll);
        await ui.click('resident-edit', scroll);
        await ui.fill('resident-name', 'Smoke resident', modal);
        await ui.click('resident-apply');
        await ui.click('resident-goal-edit', scroll);
        await ui.selectIndex('resident-goal', 4, modal); // 休息。
        await ui.fill('resident-diligence', '0.73', modal);
        await ui.click('resident-goal-apply');
        await ui.click('resident-cognition', scroll);
        await ui.click('resident-memory-add', scroll);
        await ui.fill('memory-confidence', '0.65', modal);
        await ui.click('memory-apply');
        await ui.click('resident-history', scroll);
        await ui.click('resident-history-add', scroll);
        await ui.selectIndex('history-entry-experience', 5, modal); // 学习。
        await ui.fill('history-entry-impact', '0.2', modal);
        await ui.click('history-entry-apply');
        const edited = await ui.save();
        const editedActor = resident(edited, actor.Id);
        assert.equal(editedActor.Name, 'Smoke resident');
        assert.equal(editedActor.Health, actor.Health, 'Opening and saving an identity edit must not coerce health');
        assert.equal(editedActor.Age, actor.Age, 'Identity editing must preserve age');
        assert.equal(editedActor.Agent.Goal.Kind, 4);
        assert(editedActor.Agent.Goal.Reason.includes('玩家指定'));
        assert.equal(editedActor.Agent.Goal.PlayerDirected, true);
        assert(Math.abs(editedActor.Agent.Personality.Diligence - 0.75) < 1e-9);
        const addedMemory = editedActor.Agent.Memory.find(item => item.Text === '粮食供给（玩家设置）');
        assert.equal(addedMemory?.Confidence, 0.65);
        assert.equal(addedMemory.SourceResidentId, actor.Id);
        assert.equal(addedMemory.OriginProfession, editedActor.Profession, 'A player-added first-hand memory must retain its author\'s profession');
        assert.equal(addedMemory.ObservedTick, edited.Tick);
        assert(editedActor.History.some(item => item.Text === '学习成长（玩家设置）' && item.PlayerEdited && item.Experience === 5 && item.Impact === 0.2));
        assert.equal(edited.Tick, baseline.Tick);
        assert.deepEqual(edited.Tiles, baseline.Tiles);
        assert.deepEqual(edited.Settlements.map(item => item.Resources), baseline.Settlements.map(item => item.Resources));
        await page.screenshot({ path: path.join(output, 'resident-panel.png') });
        passed('resident identity, goal, personality, sourced memory and structured history edits without retrospective world changes');

        await ui.openOverview(); await ui.click('inspector-nations', scroll);
        await ui.click(`nation-row-${home.NationId}`, scroll);
        await ui.click('nation-governance', scroll);
        const currentCultureIndex = Number(ui.control(await ui.snapshot(), 'nation-culture').value);
        const nextCultureIndex = (currentCultureIndex + 1) % edited.Society.Cultures.length;
        await ui.selectIndex('nation-culture', nextCultureIndex, modal);
        await ui.selectIndex('nation-institution', 1, modal);
        await ui.click('nation-policy-autonomy');
        await ui.selectIndex('nation-policy', 1, modal);
        await ui.click('nation-governance-apply');
        await ui.click('nation-culture-edit', scroll);
        await ui.fill('culture-name', 'Valley scholars', modal);
        await ui.fill('culture-innovation', '0.82', modal);
        await ui.click('culture-apply');
        const governed = await ui.save();
        const institution = governed.Society.Institutions.find(item => item.NationId === home.NationId);
        assert.equal(institution.Kind, 1);
        assert.equal(institution.PlayerPolicy, 1);
        assert.equal(governed.Nations.find(item => item.Id === home.NationId).CultureId, edited.Society.Cultures[nextCultureIndex].Id);
        assert.equal(governed.Society.Cultures[nextCultureIndex].Name, 'Valley scholars');
        assert.equal(governed.Society.Cultures[nextCultureIndex].Innovation, 0.82);
        assert.deepEqual(governed.Residents.map(item => [item.Id, item.Race, item.CultureId]), edited.Residents.map(item => [item.Id, item.Race, item.CultureId]));
        passed('culture values and national institution/policy edits preserve independent resident race and culture');

        await ui.click('nation-edit', scroll);
        await ui.fill('nation-name', 'Name-only nation', modal);
        await ui.click('nation-apply');
        const renamed = await ui.save();
        assert.equal(renamed.Nations.find(item => item.Id === home.NationId).Name, 'Name-only nation');
        assert.deepEqual(renamed.Settlements.map(item => item.Resources), governed.Settlements.map(item => item.Resources),
            'A name-only nation edit must preserve every exact local resource amount');
        passed('nation identity edit preserves untouched local resource stocks');

        await ui.click('nation-edit', scroll);
        for (const resource of ['food', 'wood', 'stone', 'ore']) await ui.fill(`nation-${resource}`, '500', modal);
        await ui.click('nation-apply');
        const funded = await ui.save();
        await ui.openOverview();
        await ui.click('overview-research', scroll);
        const researchView = await ui.snapshot();
        assert.equal(ui.control(researchView, 'research-start').enabled, false);
        assert.match(ui.control(researchView, 'research-requirements').value, /学舍|学院/);
        const blockedResearch = await ui.save();
        assert.deepEqual(blockedResearch.Society.Research, funded.Society.Research);
        assert.deepEqual(stock(blockedResearch, home.Id), stock(funded, home.Id));
        await ui.click('settlement-tab-infrastructure');
        await ui.click('building-open', scroll);
        await ui.selectIndex('building-kind', 2, modal); // 学舍。
        const buildIndex = funded.Tiles.findIndex((tile, index) => {
            const x = index % funded.Width; const y = Math.floor(index / funded.Width);
            return ![0, 1, 5, 10].includes(value(tile, 'Terrain')) && Math.abs(x - home.X) + Math.abs(y - home.Y) <= 4 &&
                value(tile, 'ClaimedSettlementId') === home.Id && !funded.Society.Buildings.some(item => item.X === x && item.Y === y);
        });
        assert(buildIndex >= 0);
        await ui.fill('building-x', buildIndex % funded.Width, modal);
        await ui.fill('building-y', Math.floor(buildIndex / funded.Width), modal);
        await ui.click('building-apply');
        await ui.tool('build', 'road:Road');
        await ui.clickTile(home.X, home.Y);
        const constructed = await ui.save();
        assert.equal(constructed.Society.Buildings.length, funded.Society.Buildings.length + 1);
        const academy = constructed.Society.Buildings.find(item => item.Kind === 2 && item.SettlementId === home.Id);
        assert(academy && value(academy, 'ConstructionProgress') < academy.ConstructionRequired);
        const roads = constructed.Tiles.filter((tile, index) => value(tile, 'RoadLevel') > value(funded.Tiles[index], 'RoadLevel')).length;
        assert(roads > 0);
        await ui.click('header-overview');
        await ui.click('overview-structures', scroll);
        await ui.selectIndex('structures-kind', 1, scroll);
        assert((await ui.snapshot()).controls.some(c => c.value?.includes('道路 1 级')), 'Built roads must be findable in the list');
        assert.deepEqual(await ui.save(), constructed);
        await ui.click('inspector-close');
        assert.equal(stock(constructed, home.Id).Food, stock(funded, home.Id).Food - 20);
        assert.equal(stock(constructed, home.Id).Wood, stock(funded, home.Id).Wood - 30 - roads * 0.5);
        assert.equal(stock(constructed, home.Id).Stone, stock(funded, home.Id).Stone - 15 - roads);
        await ui.click('header-rules');
        await ui.click('rule-magic', modal);
        await ui.click('world-rules-apply');
        const noNewMagic = await ui.save();
        assert.equal(noNewMagic.Society.MagicEnabled, false);
        assert.deepEqual(noNewMagic.Society.Buildings, constructed.Society.Buildings);
        passed('research facility requirement, construction and roads consume actual resources, magic development switch preserves facilities');

        await ui.click('header-overview');
        await ui.openOverview(); await ui.click('inspector-history', scroll);
        const rowIds = snapshot => snapshot.controls.filter(item => item.id.startsWith('history-row-')).map(item => Number(item.id.slice(12)));
        assert.deepEqual(rowIds(await ui.snapshot()), noNewMagic.Events.filter(item => value(item, 'Importance') >= 2).reverse().slice(0, 100).map(item => item.Id));
        await ui.selectIndex('history-importance', 2, scroll);
        await ui.selectIndex('history-nation', 1, scroll);
        const filteredNationId = noNewMagic.Nations[0].Id;
        assert.deepEqual(rowIds(await ui.snapshot()), noNewMagic.Events.filter(item => item.NationId === filteredNationId || item.SecondNationId === filteredNationId).reverse().slice(0, 100).map(item => item.Id));
        await ui.fill('history-search', 'no-event-with-this-text', scroll);
        await ui.waitFor(snapshot => rowIds(snapshot).length === 0, 'debounced history search results');
        assert.deepEqual(rowIds(await ui.snapshot()), []);
        assert.deepEqual(await ui.save(), noNewMagic, 'Inspecting and filtering must not mutate the world');
        passed('history importance/nation/text filters match real event records and remain read-only');

        await ui.click('header-storage');
        const downloaded = page.waitForEvent('download');
        await ui.click('storage-export', modal);
        const exportPath = path.join(output, 'export.json');
        await (await downloaded).saveAs(exportPath);
        const exported = JSON.parse(fs.readFileSync(exportPath, 'utf8'));
        assert.deepEqual(exported, noNewMagic);
        exported.Seed = 777;
        const importPath = path.join(output, 'import.json');
        fs.writeFileSync(importPath, JSON.stringify(exported));
        const importFile = async file => {
            await ui.click('header-storage');
            const chooser = page.waitForEvent('filechooser');
            await ui.click('storage-import', modal);
            await (await chooser).setFiles(file);
        };
        await importFile(importPath);
        await ui.waitFor(snapshot => !snapshot.modalOpen && snapshot.status.startsWith('导入成功'), 'valid file import', 30000);
        const imported = await ui.save();
        assert.deepEqual(imported, exported, 'Current-format JSON must round-trip all fields, including explicit zero values');
        // 检查最初、上一版和未知后续格式；拒绝导入时须保留当前世界。
        for (const invalidVersion of [1, exported.FormatVersion - 1, 999]) {
            const invalidPath = path.join(output, `invalid-${invalidVersion}.json`);
            fs.writeFileSync(invalidPath, JSON.stringify({ ...exported, FormatVersion: invalidVersion }));
            await importFile(invalidPath);
            await ui.waitFor(snapshot => snapshot.status.startsWith('导入失败'), 'version rejection', 30000);
            await ui.click('modal-close');
            assert.deepEqual(await ui.save(), imported, 'Rejected import must preserve the whole current world');
        }
        passed('real download/export and file-picker import, complete current-format round-trip, rejection of old and unknown formats');
        await page.reload({ waitUntil: 'domcontentloaded' });
        await ui.ready();
        await ui.paused();
        const resumed = await ui.save();
        assert.equal(resumed.Seed, 777);
        assert(resumed.Tick >= imported.Tick);
        assert.equal(resident(resumed, actor.Id).Name, 'Smoke resident');
        assert.equal(resumed.Society.Cultures[nextCultureIndex].Name, 'Valley scholars');
        assert.equal(resumed.Society.MagicEnabled, false);
        const fixedControls = await ui.stableToolLayout();
        await ui.click('tool-suspend');
        await ui.tool('terrain', 'Tundra');
        assert.equal((await ui.snapshot()).activeTool, 'Tundra');
        await ui.tool('build', 'build:AetherForge');
        assert.equal((await ui.snapshot()).activeTool, 'build:AetherForge');
        await ui.click('tool-suspend');
        passed('terrain and building tools are selectable across pages without moving core controls');
        const beforeGift = await ui.save();
        await ui.tool('build', 'build:Farm');
        await ui.selectIndex('tool-context', 0);
        await ui.selectIndex('build-mode', 0);
        const town = beforeGift.Settlements[0];
        const occupied = beforeGift.Society.Buildings.find(b => b.SettlementId === town.Id);
        await ui.clickTile(occupied.X, occupied.Y);
        assert((await ui.snapshot()).status.includes('已有建筑'), 'Invalid placement must explain its actual cause');
        assert.deepEqual(await ui.save(), beforeGift, 'Invalid placement must preserve the complete world');
        const site = beforeGift.Tiles.findIndex((tile, index) => {
            const x = index % beforeGift.Width, y = Math.floor(index / beforeGift.Width);
            return ![0, 1, 5, 10].includes(tile.Terrain) && Math.abs(x - town.X) + Math.abs(y - town.Y) <= 7
                && value(tile, 'ClaimedSettlementId') === town.Id
                && !beforeGift.Society.Buildings.some(b => b.X === x && b.Y === y);
        });
        assert(site >= 0);
        const toolbarBeforeHover = ui.control(await ui.snapshot(), 'tool-category-build');
        const hover = await ui.tilePoint(site % beforeGift.Width, Math.floor(site / beforeGift.Width));
        await page.mouse.move(hover.x, hover.y);
        await page.waitForTimeout(200);
        const hoverState = await ui.snapshot();
        assert.equal(hoverState.pendingPlacement, false);
        assert.equal(ui.control(hoverState, 'placement-confirm').visible, false, 'Mouse preview must not open a confirmation strip');
        const toolbarAfterHover = ui.control(hoverState, 'tool-category-build');
        assert.deepEqual([toolbarAfterHover.x, toolbarAfterHover.y, toolbarAfterHover.width, toolbarAfterHover.height],
            [toolbarBeforeHover.x, toolbarBeforeHover.y, toolbarBeforeHover.width, toolbarBeforeHover.height]);
        await page.screenshot({ path: path.join(output, 'mouse-placement-preview.png') });
        assert.deepEqual(await ui.save(), beforeGift, 'Hover preview is read-only');
        await ui.clickTile(site % beforeGift.Width, Math.floor(site / beforeGift.Width));
        const gifted = await ui.save();
        const gift = gifted.Society.Buildings.find(b => !beforeGift.Society.Buildings.some(old => old.Id === b.Id));
        assert(gift && gift.ConstructionProgress >= gift.ConstructionRequired, 'God gift must be immediately completed');
        assert.deepEqual(gifted.Settlements.map(t => t.Resources), beforeGift.Settlements.map(t => t.Resources), 'God gift must not consume local materials');
        await ui.click('tool-suspend');
        await ui.click('header-rules');
        await ui.selectIndex('rules-preset', 3, modal);
        await ui.click('world-rules-apply');
        const turbulent = await ui.save();
        assert.equal(turbulent.Rules.Conflict, 3);
        assert.equal(turbulent.Rules.DisasterFrequency, 2);
        assert.equal(turbulent.Rules.DisasterStrength, 2);
        assert.equal(turbulent.NaturalDisasters, true);
        assert.equal((await ui.snapshot()).inspectorOpen, false);
        passed('map gift placement, precise failure protection, and independent persisted rule presets');
        await ui.click('header-overview');
        await ui.click('overview-structures', scroll);
        await ui.selectIndex('structures-kind', 0, scroll);
        await ui.click(`building-row-${academy.Id}`, scroll);
        await ui.click('building-finish', scroll);
        await ui.click('building-toggle', scroll);
        const controlled = await ui.save();
        assert.equal(controlled.Society.Buildings.find(b => b.Id === academy.Id).Enabled, false);
        assert.equal(controlled.Society.Buildings.find(b => b.Id === academy.Id).ConstructionProgress, academy.ConstructionRequired);
        assert.deepEqual(controlled.Settlements.map(t => t.Resources), turbulent.Settlements.map(t => t.Resources));
        await ui.click('building-toggle', scroll);
        assert.equal((await ui.save()).Society.Buildings.find(b => b.Id === academy.Id).Enabled, true);
        await ui.click('inspector-close');
        passed('building quick controls finish construction and stop or restore actual operation');
        if ((await ui.snapshot()).toolsOpen) await ui.click('tools-toggle');
        await ui.click('map-fit');
        const worldBeforeZoom = await ui.save();
        for (let i = 0; i < 12 && (await ui.snapshot()).map.tileSize < 24; i++) await ui.click('map-zoom-in');
        await ui.waitFor(s => s.renderedWildlifeCount > 0, 'wildlife icons actually drawn at close zoom');
        await page.screenshot({ path: path.join(output, 'wildlife-close.png') });
        assert.deepEqual(await ui.save(), worldBeforeZoom, 'Wildlife rendering and zoom do not modify simulation');
        passed('close zoom actually renders species icons while preserving the entire world');
        await ui.click('map-fit');
        await page.screenshot({ path: path.join(output, 'desktop.png') });
        await diagnostics.assertHealthy('desktop after reload and interactions');
        passed('reload restores the edited world; all four tool categories preserve header and time controls without opening panels');
        fs.writeFileSync(path.join(output, 'desktop-results.json'), JSON.stringify({ url: baseUrl, viewport: { width: 1440, height: 960 }, results, fixedControls,
            savedTick: imported.Tick, resumedTick: resumed.Tick, fallbacks: diagnostics.fallbacks, rendererChecks: diagnostics.rendererChecks, errors: diagnostics.errors }, null, 2));
        console.log('ALL DESKTOP FUNCTIONAL CHECKS PASSED');
    } catch (error) {
        await page.screenshot({ path: path.join(output, 'failure.png') }).catch(() => {});
        fs.writeFileSync(path.join(output, 'failure-ui.json'), JSON.stringify(await ui.snapshot().catch(() => null), null, 2));
        throw error;
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
