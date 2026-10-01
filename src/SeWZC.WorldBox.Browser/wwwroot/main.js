import { dotnet } from './_framework/dotnet.js';
import * as storage from './storage.js';

try {
    const runtime = await dotnet.withDiagnosticTracing(false).create();
    runtime.setModuleImports('worldbox', storage);
    await runtime.runMain(runtime.getConfig().mainAssemblyName, []);
    document.querySelector('.loading')?.remove();
} catch (error) {
    console.error('WorldBox startup failed', error);
    const status = document.getElementById('load-status');
    if (status) status.textContent = '世界加载失败。请刷新重试，或检查浏览器是否支持 WebAssembly。';
}
