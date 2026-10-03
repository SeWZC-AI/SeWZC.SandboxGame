// All browser persistence stays on this device. No server or external storage is used.
const DATABASE_NAME = "sewzc-worldbox";
const STORE_NAME = "worlds";
const AUTOSAVE_KEY = "autosave";
const MAX_FILE_BYTES = 32 * 1024 * 1024;
let databasePromise;
let pickerPending = false;
let saveWorker;
let nextSaveRequest = 0;
const saveRequests = new Map();

function checkSize(text) {
    if (typeof text !== "string") throw new Error("存档内容不是文本。");
    if (new Blob([text]).size > MAX_FILE_BYTES) throw new Error("存档不能超过 32 MiB。");
    return text;
}

function openDatabase() {
    if (databasePromise) return databasePromise;
    databasePromise = new Promise((resolve, reject) => {
        if (!globalThis.indexedDB) {
            reject(new Error("浏览器未提供本机存储，请通过导出保存世界。"));
            return;
        }
        const request = indexedDB.open(DATABASE_NAME, 1);
        let rejected = false;
        request.onupgradeneeded = () => {
            const database = request.result;
            if (!database.objectStoreNames.contains(STORE_NAME)) database.createObjectStore(STORE_NAME);
        };
        request.onerror = () => reject(request.error ?? new Error("无法打开本机存储。"));
        request.onblocked = () => {
            rejected = true;
            reject(new Error("其他标签页阻止了存储更新，请关闭旧标签页后重试。"));
        };
        request.onsuccess = () => {
            const database = request.result;
            if (rejected) {
                database.close();
                return;
            }
            database.onversionchange = () => {
                database.close();
                databasePromise = undefined;
            };
            database.onclose = () => { databasePromise = undefined; };
            resolve(database);
        };
    }).catch(error => {
        databasePromise = undefined;
        throw error;
    });
    return databasePromise;
}

// IndexedDB encoding and structured cloning of a large save belong off the
// rendering thread. This ordinary worker needs no WASM threads or special headers.
export function save(json) {
    if (typeof json !== "string") return Promise.reject(new Error("存档内容不是文本。"));
    if (typeof Worker !== "function") return writeSave(json);
    if (!saveWorker) {
        try { saveWorker = new Worker(import.meta.resolve("./storage-worker.js"), { type: "module" }); }
        catch { return writeSave(json); }
        saveWorker.onmessage = ({ data }) => {
            const request = saveRequests.get(data.id);
            if (!request) return;
            saveRequests.delete(data.id);
            if (data.ok) request.resolve();
            else request.reject(new Error(data.error || "保存世界失败。"));
        };
        saveWorker.onerror = () => {
            for (const request of saveRequests.values()) request.reject(new Error("后台存储启动失败，请尝试导出世界。"));
            saveRequests.clear(); saveWorker.terminate(); saveWorker = undefined;
        };
    }
    return new Promise((resolve, reject) => {
        const id = ++nextSaveRequest;
        saveRequests.set(id, { resolve, reject });
        // Worker modules have no document import map. Pass this already-resolved
        // module URL so the shared writer also works with fingerprinted assets.
        try { saveWorker.postMessage({ id, json, moduleUrl: import.meta.url }); }
        catch (error) { saveRequests.delete(id); reject(error); }
    });
}

// Shared by the worker and the fallback for browsers without worker support.
export async function writeSave(json) {
    checkSize(json);
    const database = await openDatabase();
    await new Promise((resolve, reject) => {
        const transaction = database.transaction(STORE_NAME, "readwrite");
        transaction.oncomplete = () => resolve();
        transaction.onerror = () => reject(transaction.error ?? new Error("保存世界失败。"));
        transaction.onabort = () => reject(transaction.error ?? new Error("保存世界已中断。"));
        transaction.objectStore(STORE_NAME).put(json, AUTOSAVE_KEY);
    });
}

export async function load() {
    const database = await openDatabase();
    const value = await new Promise((resolve, reject) => {
        const transaction = database.transaction(STORE_NAME, "readonly");
        const request = transaction.objectStore(STORE_NAME).get(AUTOSAVE_KEY);
        transaction.oncomplete = () => resolve(request.result ?? null);
        transaction.onerror = () => reject(transaction.error ?? new Error("读取世界失败。"));
        transaction.onabort = () => reject(transaction.error ?? new Error("读取世界已中断。"));
    });
    return value === null ? null : checkSize(value);
}

export async function exportFile(json, fileName) {
    checkSize(json);
    const url = URL.createObjectURL(new Blob([json], { type: "application/json;charset=utf-8" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName || "SeWZC.WorldBox.json";
    link.hidden = true;
    document.body.append(link);
    try { link.click(); }
    finally {
        link.remove();
        // Safari can consume the blob after click() returns.
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
    }
}

export function importFile() {
    // Keep click() inside the original pointer event to preserve user activation.
    if (pickerPending) return Promise.reject(new Error("文件选择窗口已经打开。"));
    pickerPending = true;
    return new Promise((resolve, reject) => {
        const input = document.createElement("input");
        input.type = "file";
        input.accept = ".json,.worldbox,application/json";
        input.hidden = true;
        document.body.append(input);
        let finished = false;
        let reading = false;
        let focusTimer;
        const finish = (value, error) => {
            if (finished) return;
            finished = true;
            pickerPending = false;
            clearTimeout(focusTimer);
            window.removeEventListener("focus", onFocus);
            input.remove();
            if (error) reject(error); else resolve(value);
        };
        const onFocus = () => {
            // Older browsers have no cancel event. Allow change to arrive first.
            focusTimer = setTimeout(() => {
                if (!reading && !input.files?.length) finish(null);
            }, 500);
        };
        input.addEventListener("cancel", () => finish(null), { once: true });
        input.addEventListener("change", async () => {
            reading = true;
            const file = input.files?.[0];
            if (!file) { finish(null); return; }
            try {
                if (file.size > MAX_FILE_BYTES) throw new Error("存档不能超过 32 MiB。");
                const bytes = await file.arrayBuffer();
                const text = new TextDecoder("utf-8", { fatal: true }).decode(bytes);
                finish(checkSize(text));
            } catch (error) { finish(null, error); }
        }, { once: true });
        window.addEventListener("focus", onFocus);
        try { input.click(); }
        catch (error) { finish(null, error); }
    });
}

export function isBackground() {
    return document.hidden;
}
