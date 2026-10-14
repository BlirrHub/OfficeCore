let listeners;

export function cleanup() {
    listeners?.abort();
    listeners = null;
}

export function positionActiveMenu() {
    cleanup();
    const popup = document.querySelector('[data-chat-popup]');
    const anchor = document.querySelector('[data-chat-menu-anchor="active"]');
    const scroller = document.getElementById('chat-messages-container');
    if (!popup || !anchor || !scroller) return;

    popup.showPopover();
    const place = () => {
        if (!popup.isConnected) { cleanup(); return; }
        const bounds = scroller.getBoundingClientRect();
        const target = anchor.getBoundingClientRect();
        const width = popup.offsetWidth;
        const height = popup.offsetHeight;
        const leftEdge = Math.max(8, bounds.left + 8);
        const rightEdge = Math.min(window.innerWidth - 8, bounds.right - 8);
        const topEdge = Math.max(8, bounds.top + 8);
        const bottomEdge = Math.min(window.innerHeight - 8, bounds.bottom - 8);
        const above = target.top - height - 8;
        const top = above >= topEdge ? above : target.bottom + 8;
        popup.style.left = `${Math.max(leftEdge, Math.min(target.right - width, rightEdge - width))}px`;
        popup.style.top = `${Math.max(topEdge, Math.min(top, bottomEdge - height))}px`;
    };
    place();
    listeners = new AbortController();
    scroller.addEventListener('scroll', place, { passive: true, signal: listeners.signal });
    window.addEventListener('resize', place, { signal: listeners.signal });
}
