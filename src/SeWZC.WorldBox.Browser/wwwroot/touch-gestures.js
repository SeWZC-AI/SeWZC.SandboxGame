// Keep native page zoom from magnifying the entire Avalonia canvas. Some mobile
// browsers do not enforce touch-action or viewport zoom limits for every gesture.
// Cancel only the native default; pointer events still reach the map and controls.
export function installTouchGestures(root) {
    const options = { capture: true, passive: false };
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
