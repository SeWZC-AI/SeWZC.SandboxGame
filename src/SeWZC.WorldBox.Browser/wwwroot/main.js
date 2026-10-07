import {dotnet} from './_framework/dotnet.js';
import * as storage from './storage.js';
import {installTextInputBridge} from './text-input.js';
import {installTouchGestures} from './touch-gestures.js';

const root = document.getElementById('out');
installTouchGestures(root);
installTextInputBridge(root);

try {
    const runtime = await dotnet.withDiagnosticTracing(false).create();
    runtime.setModuleImports('worldbox', storage);
    const assembly = runtime.getConfig().mainAssemblyName;
    await runtime.runMain(assembly, []);
    document.querySelector('.loading')?.remove();
} catch (error) {
    console.error('WorldBox startup failed', error);
    const status = document.getElementById('load-status');
    if (status) status.textContent = '世界加载失败。请刷新重试，或检查浏览器是否支持 WebAssembly。';
}
