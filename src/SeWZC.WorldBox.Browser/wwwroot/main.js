import { dotnet } from './_framework/dotnet.js';
import * as storage from './storage.js';

try {
    const runtime = await dotnet.withDiagnosticTracing(false).create();
    runtime.setModuleImports('worldbox', storage);
    const enableUiTests = new URL(globalThis.location.href).searchParams.get('e2e') === '1';
    const assembly = runtime.getConfig().mainAssemblyName;
    await runtime.runMain(assembly, enableUiTests ? ['--e2e'] : []);
    if (enableUiTests) {
        const exports = await runtime.getAssemblyExports(assembly);
        const readSnapshot = exports.SeWZC.WorldBox.Browser.BrowserTestBridge.ReadSnapshot;
        Object.defineProperty(globalThis, 'worldboxTest', {
            value: Object.freeze({ snapshot: () => JSON.parse(readSnapshot()) })
        });
    }
    document.querySelector('.loading')?.remove();
} catch (error) {
    console.error('WorldBox startup failed', error);
    const status = document.getElementById('load-status');
    if (status) status.textContent = '世界加载失败。请刷新重试，或检查浏览器是否支持 WebAssembly。';
}
