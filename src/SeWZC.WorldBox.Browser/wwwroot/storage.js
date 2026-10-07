const DATABASE_NAME = "sewzc-worldbox";
const STORE_NAME = "worlds";
const AUTOSAVE_KEY = "autosave";
const MAX_FILE_BYTES = 64 * 1024 * 1024;
let databasePromise;
let pickerPending = false;
let saveWorker;
let nextSaveRequest = 0;
const saveRequests = new Map();
let yieldChannel;
const pendingYields = [];

export function yieldSave() {
    // 让输入和绘制获得执行机会，避免嵌套定时器给分块保存累积延迟。
    if (typeof globalThis.scheduler?.yield === "function") return globalThis.scheduler.yield();
    if (!yieldChannel) {
        yieldChannel = new MessageChannel();
        yieldChannel.port1.onmessage = () => pendingYields.shift()?.();
    }
    return new Promise(resolve => {
        pendingYields.push(resolve);
        yieldChannel.port2.postMessage(0);
    });
}

function checkSize(text) {
    if (typeof text !== "string") throw new Error("存档内容不是文本。");
    if (new Blob([text]).size > MAX_FILE_BYTES) throw new Error("存档不能超过 64 MiB。");
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
            database.onclose = () => {
                databasePromise = undefined;
            };
            resolve(database);
        };
    }).catch(error => {
        databasePromise = undefined;
        throw error;
    });
    return databasePromise;
}

export function beginSave() {
    if (typeof Worker === "function" && !saveWorker) {
        try {
            saveWorker = new Worker(import.meta.resolve("./storage-worker.js"), {type: "module"});
        } catch { /* Worker 不可用时回退到分块 Blob 保存。 */
        }
        if (saveWorker) {
            saveWorker.onmessage = ({data}) => {
                const request = saveRequests.get(data.id);
                if (!request) return;
                if (data.ok) {
                    saveRequests.delete(data.id);
                    request.resolve?.();
                } else {
                    request.error = new Error(data.error || "保存世界失败。");
                    request.reject?.(request.error);
                }
            };
            saveWorker.onerror = () => {
                for (const request of saveRequests.values()) {
                    if (request.worker !== saveWorker) continue;
                    request.error = new Error("后台存储启动失败，请尝试导出世界。");
                    request.reject?.(request.error);
                }
                saveWorker.terminate();
                saveWorker = undefined;
            };
        }
    }
    const id = ++nextSaveRequest;
    saveRequests.set(id, {worker: saveWorker, parts: [], bytes: 0});
    return id;
}

export function appendSave(id, chunk) {
    const request = saveRequests.get(id);
    if (!request || request.error) throw request?.error || new Error("保存已中断。");
    try {
        if (typeof chunk !== "string" || !chunk.length) throw new Error("存档数据块为空或不是文本。");
        if (request.worker) request.worker.postMessage({id, op: "append", chunk});
        else {
            const part = new Blob([chunk]);
            request.bytes += part.size;
            if (request.bytes > MAX_FILE_BYTES) throw new Error("存档不能超过 64 MiB。");
            request.parts.push(part);
        }
    } catch (error) {
        request.error = error;
        request.parts = [];
        throw error;
    }
}

export async function commitSave(id) {
    const request = saveRequests.get(id);
    if (!request || request.error) throw request?.error || new Error("保存已中断。");
    if (!request.worker) {
        try {
            await writeSave(new Blob(request.parts));
        } finally {
            saveRequests.delete(id);
        }
        return;
    }
    await new Promise((resolve, reject) => {
        request.resolve = resolve;
        request.reject = reject;
        // Worker 不继承页面的 import map，须传入解析后的模块 URL。
        request.worker.postMessage({id, op: "commit", moduleUrl: import.meta.url});
    });
}

export function discardSave(id) {
    const request = saveRequests.get(id);
    request?.worker?.postMessage({id, op: "discard"});
    saveRequests.delete(id);
}

export async function save(json) {
    if (typeof json !== "string") throw new Error("存档内容不是文本。");
    const id = beginSave();
    try {
        appendSave(id, json);
        await commitSave(id);
    } finally {
        discardSave(id);
    }
}

// Worker 与主线程回退路径共用的写入入口。
export async function writeSave(json) {
    // 用文本块构造 Blob，不拼接整个存档。
    const blob = json instanceof Blob ? json : new Blob([checkSize(json)], {type: "application/json"});
    if (!blob.size || blob.size > MAX_FILE_BYTES) throw new Error("存档为空或超过 64 MiB。");
    const compressed = typeof CompressionStream === "function" && typeof DecompressionStream === "function";
    const candidate = compressed
        ? await new Response(blob.stream().pipeThrough(new CompressionStream("gzip"))).blob()
        : blob;
    const useCompressed = compressed && candidate.size < blob.size;
    const record = {
        encoding: useCompressed ? "gzip" : "utf-8",
        bytes: blob.size,
        data: useCompressed ? candidate : blob
    };
    const database = await openDatabase();
    await new Promise((resolve, reject) => {
        const transaction = database.transaction(STORE_NAME, "readwrite");
        transaction.oncomplete = () => resolve();
        transaction.onerror = () => reject(transaction.error ?? new Error("保存世界失败。"));
        transaction.onabort = () => reject(transaction.error ?? new Error("保存世界已中断。"));
        transaction.objectStore(STORE_NAME).put(record, AUTOSAVE_KEY);
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
    if (value === null) return null;
    if (typeof value === "string") return checkSize(value);
    if (!(value.data instanceof Blob) || !Number.isInteger(value.bytes) || value.bytes < 0
        || value.bytes > MAX_FILE_BYTES || value.data.size > MAX_FILE_BYTES
        || !["gzip", "utf-8"].includes(value.encoding)) throw new Error("存档存储格式无效。");
    if (value.encoding === "gzip" && typeof DecompressionStream !== "function")
        throw new Error("此浏览器无法解压存档，请使用支持解压的浏览器。");
    const stream = value.encoding === "gzip"
        ? value.data.stream().pipeThrough(new DecompressionStream("gzip")) : value.data.stream();
    const reader = stream.getReader(), decoder = new TextDecoder("utf-8", {fatal: true});
    const parts = [];
    let bytes = 0;
    try {
        for (; ;) {
            const next = await reader.read();
            if (next.done) break;
            bytes += next.value.byteLength;
            if (bytes > MAX_FILE_BYTES || bytes > value.bytes) throw new Error("存档解压大小超出限制。");
            parts.push(decoder.decode(next.value, {stream: true}));
        }
        if (bytes !== value.bytes) throw new Error("存档不完整。");
        parts.push(decoder.decode());
        return parts.join("");
    } finally {
        await reader.cancel().catch(() => {
        });
        reader.releaseLock();
    }
}

export async function exportFile(json, fileName) {
    checkSize(json);
    const url = URL.createObjectURL(new Blob([json], {type: "application/json;charset=utf-8"}));
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName || "SeWZC.WorldBox.json";
    link.hidden = true;
    document.body.append(link);
    try {
        link.click();
    } finally {
        link.remove();
        // Safari 可能在 click() 返回后才读取 Blob，须延后释放 URL。
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
    }
}

export function importFile() {
    // 文件选择须在原始指针事件中触发，才能保留浏览器的用户操作授权。
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
            // 旧浏览器没有 cancel 事件；延后检查，给文件选择的 change 事件留出时间。
            focusTimer = setTimeout(() => {
                if (!reading && !input.files?.length) finish(null);
            }, 500);
        };
        input.addEventListener("cancel", () => finish(null), {once: true});
        input.addEventListener("change", async () => {
            reading = true;
            const file = input.files?.[0];
            if (!file) {
                finish(null);
                return;
            }
            try {
                if (file.size > MAX_FILE_BYTES) throw new Error("存档不能超过 64 MiB。");
                const bytes = await file.arrayBuffer();
                const text = new TextDecoder("utf-8", {fatal: true}).decode(bytes);
                finish(checkSize(text));
            } catch (error) {
                finish(null, error);
            }
        }, {once: true});
        window.addEventListener("focus", onFocus);
        try {
            input.click();
        } catch (error) {
            finish(null, error);
        }
    });
}

export function isBackground() {
    return document.hidden;
}
