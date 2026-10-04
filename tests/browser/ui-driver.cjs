const assert = require('node:assert/strict');

function testUrl(baseUrl) {
    const url = new URL(baseUrl);
    url.searchParams.set('e2e', '1');
    return url.href;
}

class UiDriver {
    constructor(page, { touch = false } = {}) {
        this.page = page;
        this.touch = touch;
    }

    async snapshot() {
        const result = await this.page.evaluate(() => globalThis.worldboxTest?.snapshot());
        assert(result, 'Read-only UI inspection is unavailable; open the published app with ?e2e=1');
        return result;
    }

    async waitFor(predicate, description, timeout = 10000) {
        const deadline = Date.now() + timeout;
        let latest;
        do {
            latest = await this.snapshot();
            if (predicate(latest)) return latest;
            await this.page.waitForTimeout(100);
        } while (Date.now() < deadline);
        throw new Error(`UI did not reach ${description}; status: ${latest?.status}`);
    }

    async ready() {
        await this.page.waitForFunction(() => globalThis.worldboxTest?.snapshot().ready, {}, { timeout: 60000 });
        return this.snapshot();
    }

    control(snapshot, id) {
        const matches = snapshot.controls.filter(control => control.id === id);
        assert.equal(matches.length, 1, `Expected one UI control named ${id}; found ${matches.length}`);
        return matches[0];
    }

    async point(id, { scroll } = {}) {
        for (let attempt = 0; attempt < 16; attempt++) {
            const snapshot = await this.snapshot();
            const control = this.control(snapshot, id);
            if (control.visible) {
                assert(control.enabled, `UI control is disabled: ${id}`);
                const canvas = await this.page.locator('#out canvas.avalonia-canvas').boundingBox();
                assert(canvas, 'Avalonia canvas has no visible bounds');
                return { x: canvas.x + control.x + control.width / 2, y: canvas.y + control.y + control.height / 2 };
            }
            assert(scroll, `UI control is outside its visible clip: ${id}`);
            const viewport = this.control(snapshot, scroll);
            assert(viewport.visible, `Scroll viewport is hidden: ${scroll}`);
            const canvas = await this.page.locator('#out canvas.avalonia-canvas').boundingBox();
            assert(canvas, 'Avalonia canvas has no visible bounds');
            // Stay in the outer viewport's margin: a research graph in its center
            // consumes wheels for zooming and cannot scroll an outer heading into view.
            const center = { x: canvas.x + viewport.x + 3, y: canvas.y + viewport.y + viewport.height / 2 };
            await this.page.mouse.move(center.x, center.y);
            await this.page.mouse.wheel(0, Math.sign(control.y - (viewport.y + viewport.height / 2)) * 250);
            await this.page.waitForTimeout(120);
        }
        throw new Error(`Could not scroll ${id} into view`);
    }

    async openResidentRow(id, options = { scroll: 'inspector-scroll' }) {
        if (!(await this.snapshot()).controls.some(c => c.id === `resident-row-${id}`)) await this.fill('resident-search', String(id), options);
        await this.waitFor(s => s.controls.some(c => c.id === `resident-row-${id}`), "resident search result");
        await this.click(`resident-row-${id}`, options);
    }

    async openOverview() {
        const snapshot = await this.snapshot();
        if (!snapshot.inspectorOpen || snapshot.inspector !== 'overview') await this.click('header-overview');
    }

    async click(id, options) {
        const point = await this.point(id, options);
        if (this.touch) await this.page.touchscreen.tap(point.x, point.y);
        else await this.page.mouse.click(point.x, point.y, { delay: 80 });
        await this.page.waitForTimeout(180);
        if ((await this.snapshot()).editCaptureActive) await this.waitFor(s => !s.editCaptureActive, "completed edit checkpoint", 30000);
    }

    async fill(id, value, options) {
        await this.click(id, options);
        await this.page.keyboard.press('Control+A');
        // Avalonia's browser text input consumes real keypresses. insertText alone
        // skips those events, so smoke fixtures use ordinary ASCII input.
        if (String(value).length === 0) await this.page.keyboard.press("Backspace");
        else await this.page.keyboard.type(String(value), { delay: 20 });
        await this.page.keyboard.press("Tab");
        await this.waitFor(snapshot => this.control(snapshot, id).value === String(value), `text in ${id}`);
    }

    async selectIndex(id, index, options) {
        await this.click(id, options);
        await this.page.keyboard.press('Home');
        for (let i = 0; i < index; i++) await this.page.keyboard.press('ArrowDown');
        await this.page.keyboard.press('Enter');
        await this.waitFor(snapshot => this.control(snapshot, id).value === String(index), `selection in ${id}`);
        // Choice-dependent descriptions can move fields in a compact dialog. Let the
        // popup close and Avalonia arrange its new content before the next real tap.
        await this.page.waitForTimeout(180);
        if ((await this.snapshot()).editCaptureActive) await this.waitFor(s => !s.editCaptureActive, "completed edit checkpoint", 30000);
    }

    async tilePoint(x, y) {
        const { map } = await this.snapshot();
        const point = { x: map.tile0CenterX + x * map.tileSize, y: map.tile0CenterY + y * map.tileSize };
        assert(point.x >= map.x && point.x < map.x + map.width && point.y >= map.y && point.y < map.y + map.height,
            `Tile ${x},${y} is outside the map viewport`);
        const canvas = await this.page.locator('#out canvas.avalonia-canvas').boundingBox();
        assert(canvas, 'Avalonia canvas has no visible bounds');
        return { x: canvas.x + point.x, y: canvas.y + point.y };
    }

    async clickTile(x, y) {
        const point = await this.tilePoint(x, y);
        if (this.touch) await this.page.touchscreen.tap(point.x, point.y);
        else await this.page.mouse.click(point.x, point.y, { delay: 80 });
        await this.page.waitForTimeout(180);
        if ((await this.snapshot()).editCaptureActive) await this.waitFor(s => !s.editCaptureActive, "completed edit checkpoint", 30000);
    }

    async paused(value = true) {
        if ((await this.snapshot()).paused !== value) await this.click('time-toggle');
        return this.waitFor(snapshot => snapshot.paused === value, value ? 'paused time' : 'running time');
    }

    async save() {
        const before = await this.snapshot();
        assert(!before.modalOpen, 'Close the current modal before saving');
        await this.click('header-storage');
        await this.click('storage-save', { scroll: 'modal-scroll' });
        const completed = await this.waitFor(snapshot => !snapshot.modalOpen && !snapshot.saving, 'completed IndexedDB save', 30000);
        assert(!completed.status?.startsWith('保存失败'), completed.status);
        const saved = await readSavedWorld(this.page);
        if (before.paused) assert.equal(saved.Tick, before.worldTick, 'Paused save must contain the current simulation tick');
        return saved;
    }

    async tool(category, key) {
        if (!(await this.snapshot()).toolsOpen) await this.click("tools-toggle");
        await this.click(`tool-category-${category}`);
        let snapshot = await this.snapshot();
        // Selecting an already open category keeps its page. Search from page one.
        while (this.control(snapshot, 'tool-page-prev').enabled) {
            await this.click('tool-page-prev');
            snapshot = await this.snapshot();
        }
        for (let page = 0; page < 8; page++) {
            const index = snapshot.toolSlots.indexOf(key);
            if (index >= 0) { await this.click(`tool-slot-${index}`); return; }
            if (!this.control(snapshot, 'tool-page-next').enabled) break;
            await this.click('tool-page-next');
            snapshot = await this.snapshot();
        }
        assert.fail(`Tool ${key} is absent from every page in ${category}`);
    }

    async stableToolLayout() {
        if (!(await this.snapshot()).toolsOpen) await this.click('tools-toggle');
        const categories = ['terrain', 'life', 'disaster', 'build'];
        const ids = ['header-new-world', 'header-storage', 'header-overview', 'header-rules', 'time-toggle',
            'time-speed-1', 'time-speed-2', 'time-speed-5', 'tools-toggle', 'tool-suspend'];
        const geometry = snapshot => Object.fromEntries(ids.map(id => {
            const item = this.control(snapshot, id);
            assert(item.visible, `Core control must remain visible: ${id}`);
            return [id, [item.x, item.y, item.width, item.height]];
        }));
        const baseline = geometry(await this.snapshot());
        for (const category of categories) {
            await this.click(`tool-category-${category}`);
            const snapshot = await this.waitFor(snapshot => JSON.stringify(geometry(snapshot)) === JSON.stringify(baseline),
                `settled controls after ${category}`, 5000);
            assert.equal(snapshot.category, category);
            assert.equal(snapshot.modalOpen, false, 'A tool category must not open a window');
            assert.equal(snapshot.inspectorOpen, false, 'A tool category must not open details');
            assert.equal(snapshot.activeTool, 'pan', 'A category must not activate its first tool');
            snapshot.toolSlots.forEach((key, index) => {
                const slot = this.control(snapshot, `tool-slot-${index}`);
                assert.equal(slot.enabled, key !== null);
                assert.equal(slot.visible, true, 'Empty slots keep the toolbar geometry stable');
            });
        }
        await this.click('tools-toggle');
        const hidden = await this.snapshot();
        assert(!hidden.toolsOpen && hidden.activeTool === 'pan', 'Hiding tools must restore navigation');
        return baseline;
    }

}

async function readSavedWorld(page) {
    const json = await page.evaluate(() => new Promise((resolve, reject) => {
        const open = indexedDB.open('sewzc-worldbox', 1);
        open.onerror = () => reject(open.error);
        open.onsuccess = () => {
            const database = open.result;
            const transaction = database.transaction('worlds');
            const request = transaction.objectStore('worlds').get('autosave');
            transaction.oncomplete = () => { database.close(); resolve(request.result); };
            transaction.onerror = () => { database.close(); reject(transaction.error); };
            transaction.onabort = () => { database.close(); reject(transaction.error); };
        };
    }));
    assert.equal(typeof json, 'string', 'A real save operation must populate IndexedDB');
    return JSON.parse(json);
}

module.exports = { testUrl, UiDriver, readSavedWorld };
