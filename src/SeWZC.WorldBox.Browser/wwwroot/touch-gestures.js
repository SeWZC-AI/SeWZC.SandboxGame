// 部分移动浏览器会忽略 CSS 缩放限制；阻止整页缩放，保留地图与控件的指针事件。
export function installTouchGestures(root) {
    const options = {capture: true, passive: false};
    const preventPinch = event => {
        if (event.touches.length > 1 && event.cancelable) event.preventDefault();
    };
    root.addEventListener('touchstart', preventPinch, options);
    root.addEventListener('touchmove', preventPinch, options);
    const preventGesture = event => {
        if (event.cancelable) event.preventDefault();
    };
    for (const type of ['gesturestart', 'gesturechange', 'gestureend']) {
        root.addEventListener(type, preventGesture, options);
    }
}
