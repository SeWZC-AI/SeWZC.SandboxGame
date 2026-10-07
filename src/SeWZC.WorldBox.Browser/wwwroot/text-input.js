// 补齐 Avalonia 12.1.3 未接收的软键盘输入：Android 可能只通过 beforeinput 提供字符。
export function installTextInputBridge(root) {
    const states = new WeakMap();
    const stateFor = target => {
        if (!(target instanceof HTMLInputElement) || !target.classList.contains('avalonia-input-element')) return null;
        let state = states.get(target);
        if (!state) {
            state = {composing: false, key: null, keyAt: 0, commit: null, commitAt: 0};
            states.set(target, state);
        }
        return state;
    };
    root.addEventListener('keydown', event => {
        const state = stateFor(event.target);
        if (state && !state.bridging) {
            state.key = event.key;
            state.keyAt = performance.now();
            if (!state.composing) state.commit = null;
        }
    }, true);
    root.addEventListener('compositionstart', event => {
        const state = stateFor(event.target);
        if (state) state.composing = true;
    }, true);
    root.addEventListener('compositionend', event => {
        const state = stateFor(event.target);
        if (state) {
            state.composing = false;
            if (!state.bridging) {
                state.commit = event.data;
                state.commitAt = performance.now();
            }
        }
    }, true);
    root.addEventListener('beforeinput', event => {
        const state = stateFor(event.target);
        if (!state || event.isComposing || state.composing) return;
        const recentKey = performance.now() - state.keyAt < 150 ? state.key : null;
        if (event.inputType === 'insertText' && event.data) {
            // 实体按键和输入法提交已由 Avalonia 处理，不能重复输入。
            if (recentKey === event.data && recentKey.length === 1 || state.commit === event.data && performance.now() - state.commitAt < 150) {
                event.preventDefault();
                state.commit = null;
                return;
            }
            event.preventDefault();
            state.bridging = true;
            event.target.dispatchEvent(new CompositionEvent('compositionstart', {bubbles: true, data: ''}));
            event.target.dispatchEvent(new CompositionEvent('compositionend', {bubbles: true, data: event.data}));
            state.bridging = false;
        } else if (event.inputType === 'deleteContentBackward' || event.inputType === 'deleteContentForward') {
            const key = event.inputType === 'deleteContentBackward' ? 'Backspace' : 'Delete';
            event.preventDefault();
            if (recentKey !== key) {
                state.bridging = true;
                event.target.dispatchEvent(new KeyboardEvent('keydown', {bubbles: true, key, code: key}));
                state.bridging = false;
            }
        }
    }, true);
}
