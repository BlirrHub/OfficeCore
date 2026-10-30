const { test } = require("node:test");
const assert = require("node:assert/strict");
const { calculateChatPopoverPosition, isAnchorVisibleInContainer } = require("../../src/OfficeCore.Web/wwwroot/js/chatPopover.js");

const viewport = { width: 1053, height: 831 };
const popup = { width: 244, height: 48 };

test("sent reactions align right and open above the bubble", () => {
    assert.deepEqual(calculateChatPopoverPosition({ left: 785, right: 1015, top: 595, bottom: 660 }, popup, viewport, "sent"),
        { left: 771, top: 539 });
});

test("received reactions align left and extend to the right", () => {
    assert.deepEqual(calculateChatPopoverPosition({ left: 675, right: 905, top: 595, bottom: 660 }, popup, viewport, "received"),
        { left: 675, top: 539 });
});

test("popups near the top open below the bubble", () => {
    const result = calculateChatPopoverPosition({ left: 20, right: 160, top: 10, bottom: 40 }, popup, viewport, "sent");
    assert.deepEqual(result, { left: 8, top: 48 });
});

test("popups near the top of the chat messages container flip below to avoid overlapping the chat header", () => {
    const container = { left: 645, right: 1029, top: 280, bottom: 680 };
    const anchor = { left: 785, right: 1015, top: 300, bottom: 350 };
    const result = calculateChatPopoverPosition(anchor, popup, viewport, "sent", container);
    assert.deepEqual(result, { left: 771, top: 358 });
});

test("scrolled-out anchors are detected outside the chat container bounds", () => {
    const container = { left: 645, right: 1029, top: 280, bottom: 680 };
    assert.equal(isAnchorVisibleInContainer({ top: 220, bottom: 275 }, container), false);
    assert.equal(isAnchorVisibleInContainer({ top: 685, bottom: 730 }, container), false);
    assert.equal(isAnchorVisibleInContainer({ top: 270, bottom: 310 }, container), true);
});

test("action menus are clamped at the left and right viewport edges", () => {
    const menu = { width: 144, height: 112 };
    assert.equal(calculateChatPopoverPosition({ left: 0, right: 60, top: 200, bottom: 260 }, menu, viewport, "sent").left, 8);
    assert.equal(calculateChatPopoverPosition({ left: 1020, right: 1053, top: 200, bottom: 260 }, menu, viewport, "received").left, 901);
});

test("reactions fit a narrow mobile viewport without crossing screen edges", () => {
    const result = calculateChatPopoverPosition({ left: 230, right: 300, top: 150, bottom: 200 }, popup, { width: 320, height: 480 }, "received");
    assert.ok(result.left >= 8);
    assert.ok(result.left + popup.width <= 312);
    assert.ok(result.top >= 8);
});

test("oversized anchors keep the popup visible within the viewport", () => {
    const result = calculateChatPopoverPosition({ left: 10, right: 300, top: -400, bottom: 900 }, popup, { width: 320, height: 480 }, "received");
    assert.ok(result.top >= 8);
    assert.ok(result.top + popup.height <= 472);
});
