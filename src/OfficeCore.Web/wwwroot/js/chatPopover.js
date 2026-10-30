(function (root) {
    "use strict";

    function isAnchorVisibleInContainer(anchor, container) {
        if (!container) return true;
        return anchor.bottom > container.top && anchor.top < container.bottom;
    }

    function calculateChatPopoverPosition(anchor, popup, viewport, side, container) {
        const gap = 8;
        const minLeft = container ? Math.max(gap, container.left + gap) : gap;
        const maxLeft = Math.max(
            minLeft,
            container
                ? Math.min(viewport.width - popup.width - gap, container.right - popup.width - gap)
                : viewport.width - popup.width - gap
        );
        const minTop = container ? Math.max(gap, container.top + gap) : gap;
        const maxTop = Math.max(
            minTop,
            container
                ? Math.min(viewport.height - popup.height - gap, container.bottom - popup.height - gap)
                : viewport.height - popup.height - gap
        );
        const preferredLeft = side === "sent" ? anchor.right - popup.width : anchor.left;
        let top = anchor.top - popup.height - gap;
        if (top < minTop) top = anchor.bottom + gap;
        return {
            left: Math.min(maxLeft, Math.max(minLeft, preferredLeft)),
            top: Math.min(maxTop, Math.max(minTop, top))
        };
    }

    function dismiss(popups) {
        popups.forEach(popup => {
            if (typeof popup.hidePopover === "function") popup.hidePopover();
            popup._chatComponent?.invokeMethodAsync("DismissChatPopovers").catch(() => {});
        });
    }

    function positionChatPopovers(component) {
        const viewport = { width: root.innerWidth, height: root.innerHeight };
        root.document.querySelectorAll("[data-chat-popover]").forEach(popup => {
            if (component?.invokeMethodAsync) popup._chatComponent = component;
            const button = root.document.getElementById(popup.dataset.chatAnchor);
            if (!button) return;
            const containerEl = button.closest("#chat-messages-container");
            const container = containerEl ? containerEl.getBoundingClientRect() : null;
            const bubble = button.closest(".chat-msg-bubble-group")?.querySelector(".chat-msg-bubble-wrap");
            const anchor = (bubble || button).getBoundingClientRect();
            if (!isAnchorVisibleInContainer(anchor, container)) {
                dismiss([popup]);
                return;
            }
            popup.style.maxWidth = `${Math.max(0, viewport.width - 16)}px`;
            popup.style.maxHeight = `${Math.max(0, viewport.height - 16)}px`;
            popup.style.overflow = "auto";
            popup.style.inset = "auto";
            // Native popovers enter the top layer, escaping all ancestor overflow/filter clipping.
            if (!popup.matches(":popover-open")) popup.showPopover();
            const position = calculateChatPopoverPosition(anchor, popup.getBoundingClientRect(), viewport, popup.dataset.chatSide, container);
            popup.style.left = `${position.left}px`;
            popup.style.top = `${position.top}px`;
        });
    }

    if (typeof module !== "undefined" && module.exports) {
        module.exports = { calculateChatPopoverPosition, isAnchorVisibleInContainer };
    } else {
        root.positionChatPopovers = positionChatPopovers;
        root.addEventListener("resize", positionChatPopovers);
        root.document.addEventListener("scroll", positionChatPopovers, true);
        root.document.addEventListener("pointerdown", event => {
            const open = [...root.document.querySelectorAll("[data-chat-popover]:popover-open")];
            if (open.some(popup => popup.contains(event.target) ||
                root.document.getElementById(popup.dataset.chatAnchor)?.contains(event.target))) return;
            dismiss(open);
        });
        root.document.addEventListener("keydown", event => {
            if (event.key === "Escape") dismiss([...root.document.querySelectorAll("[data-chat-popover]:popover-open")]);
        });
    }
})(typeof window !== "undefined" ? window : globalThis);
