let writer;
const captures = new Map();
const failed = new Set();
const MAX_FILE_BYTES = 64 * 1024 * 1024;

self.onmessage = async ({ data }) => {
    try {
        if (data.op === "discard") { captures.delete(data.id); failed.delete(data.id); return; }
        if (failed.has(data.id)) throw new Error("保存已中断。");
        if (data.op === "append") {
            const capture = captures.get(data.id) || { parts: [], bytes: 0 };
            if (typeof data.chunk !== "string" || !data.chunk.length) throw new Error("存档数据块为空。");
            const part = new Blob([data.chunk]);
            capture.bytes += part.size;
            if (capture.bytes > MAX_FILE_BYTES) throw new Error("存档不能超过 64 MiB。");
            capture.parts.push(part); captures.set(data.id, capture);
            return;
        }
        const capture = captures.get(data.id);
        if (!capture) throw new Error("保存内容为空或已中断。");
        captures.delete(data.id);
        writer ??= import(data.moduleUrl).then(module => module.writeSave);
        const writeSave = await writer;
        await writeSave(new Blob(capture.parts));
        self.postMessage({ id: data.id, ok: true });
    } catch (error) {
        captures.delete(data.id);
        failed.add(data.id);
        self.postMessage({ id: data.id, ok: false, error: error?.message || "保存世界失败。" });
    }
};
