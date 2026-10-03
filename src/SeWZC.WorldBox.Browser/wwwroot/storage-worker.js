let writer;

self.onmessage = async ({ data }) => {
    try {
        writer ??= import(data.moduleUrl).then(module => module.writeSave);
        const writeSave = await writer;
        await writeSave(data.json);
        self.postMessage({ id: data.id, ok: true });
    } catch (error) {
        self.postMessage({ id: data.id, ok: false, error: error?.message || "保存世界失败。" });
    }
};
