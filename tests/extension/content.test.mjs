import assert from "node:assert/strict";
import test from "node:test";
import vm from "node:vm";
import { clone, event, extensionScript, track } from "./helpers.mjs";

function content() {
    const pageMessages = event();
    const runtimeMessages = event();
    const forwarded = [];
    const window = { addEventListener: (_type, listener) => pageMessages.addListener(listener) };
    vm.runInNewContext(extensionScript("content.js"), {
        window, location: { origin: "https://music.youtube.com" },
        document: { createElement: () => ({}), head: { appendChild() {} } },
        chrome: { runtime: {
            getURL: (path) => path,
            sendMessage: async (message) => { forwarded.push(clone(message)); },
            onMessage: runtimeMessages
        } }
    });
    return {
        forwarded,
        state(payload, overrides = {}) {
            pageMessages.emit({ source: window, origin: "https://music.youtube.com",
                data: { type: "YTM_STATE", payload }, ...overrides });
        },
        snapshot() {
            let response;
            runtimeMessages.emit({ type: "YTM_GET_STATE" }, {}, (value) => { response = clone(value); });
            return response.payload;
        }
    };
}

test("content script retains the latest state for a restarted background worker", () => {
    const app = content();
    assert.equal(app.snapshot(), null);
    app.state(track("Playing"));
    app.state(track("Paused", false));
    assert.deepEqual(app.snapshot(), track("Paused", false));
    assert.equal(app.forwarded.length, 2);
});

test("foreign page messages cannot replace the cached playback state", () => {
    const app = content();
    app.state(track("Selected"));
    app.state(track("Foreign"), { source: {} });
    app.state(track("Foreign"), { origin: "https://example.com" });
    assert.deepEqual(app.snapshot(), track("Selected"));
    assert.equal(app.forwarded.length, 1);
});
