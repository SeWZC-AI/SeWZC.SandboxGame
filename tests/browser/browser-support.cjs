const assert = require('node:assert/strict');

// Avalonia 依次尝试 WebGL2、WebGL1 和软件渲染；无 GPU 的 CI 环境会产生这些诊断。
const softwareFallbackMessages = new Set([
    'Failed to create render target for mode 3 : HTMLCanvasElement.getContext returned null.',
    'Failed to create render target for mode 2 : HTMLCanvasElement.getContext returned null.'
]);

function chromiumLaunchOptions() {
    const args = ['--no-sandbox', '--disable-dev-shm-usage', '--enable-unsafe-swiftshader'];
    if (process.env.WORLDBOX_TEST_DISABLE_WEBGL === '1') args.push('--disable-webgl');
    return {
        ...(process.env.CHROMIUM_EXECUTABLE ? { executablePath: process.env.CHROMIUM_EXECUTABLE } : {}),
        headless: true,
        args
    };
}

function observeBrowserErrors(page) {
    const errors = [];
    const fallbacks = [];
    const rendererChecks = [];
    let currentDocument = { fallbacks: [] };

    page.on('framenavigated', frame => {
        if (frame === page.mainFrame()) currentDocument = { fallbacks: [] };
    });
    page.on('pageerror', error => {
        errors.push(`pageerror: ${error.message}`);
        console.log('ERROR', error.message);
    });
    page.on('console', message => {
        if (message.type() !== 'error') return;
        const text = message.text();
        if (softwareFallbackMessages.has(text)) {
            currentDocument.fallbacks.push(text);
            fallbacks.push(text);
            console.log('RENDERER FALLBACK', text);
        } else {
            errors.push(`console.error: ${text}`);
            console.log('ERROR', text);
        }
    });

    async function assertHealthy(stage) {
        assert.deepEqual(errors, [], `Unexpected browser errors (${stage})`);
        const document = currentDocument;
        let result = { stage, renderer: 'no fallback diagnostics' };
        if (document.fallbacks.length > 0) {
            const pixels = await page.waitForFunction(() => {
                const canvas = globalThis.document.querySelector('#out canvas.avalonia-canvas');
                if (!canvas || !canvas.isConnected || canvas.width < 100 || canvas.height < 100) return false;
                const bounds = canvas.getBoundingClientRect();
                if (bounds.width < 100 || bounds.height < 100) return false;
                const context = canvas.getContext('2d');
                if (!context) return false;
                const { width, height } = canvas;
                const data = context.getImageData(0, 0, width, height).data;
                const stride = Math.max(1, Math.floor(Math.min(width, height) / 64));
                const colors = new Set();
                let nontransparentSamples = 0;
                for (let y = 0; y < height; y += stride) {
                    for (let x = 0; x < width; x += stride) {
                        const index = (y * width + x) * 4;
                        if (data[index + 3] === 0) continue;
                        nontransparentSamples++;
                        colors.add((data[index] << 16) | (data[index + 1] << 8) | data[index + 2]);
                    }
                }
                if (nontransparentSamples < 32 || colors.size < 8) return false;
                return { width, height, nontransparentSamples, colors: colors.size };
            }, {}, { timeout: 10000 });
            try {
                result = { stage, renderer: 'software-2d', ...await pixels.jsonValue() };
            } finally {
                await pixels.dispose();
            }
            assert.equal(currentDocument, document, 'Page navigated during renderer verification');
        }
        assert.deepEqual(errors, [], `Unexpected browser errors (${stage})`);
        rendererChecks.push(result);
        console.log('PASS renderer verification', JSON.stringify(result));
        return result;
    }

    return { errors, fallbacks, rendererChecks, assertHealthy };
}

module.exports = { chromiumLaunchOptions, observeBrowserErrors };
