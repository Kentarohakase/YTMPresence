import assert from "node:assert/strict";
import test from "node:test";
import vm from "node:vm";
import { extensionScript, settle, clone } from "./helpers.mjs";

async function surface(name, { language = "en", installType = "development", metadataError = false, requestError = false, clipboardError = false, welcome = false,
    status = { tokenConfigured: true, connectionState: "connected" }, result = { saved: true, ok: true, message: "Saved" } } = {}) {
    const html = extensionScript(`${name}.html`);
    const messages = JSON.parse(extensionScript(`_locales/${language}/messages.json`));
    const elements = [...html.matchAll(/<[^>]+\bid="([^"]+)"[^>]*>/g)].map(([tag, id]) => ({
        id, value: "", textContent: "", className: "", disabled: false, hidden: /\shidden(?=[\s>=])/.test(tag), listeners: {},
        addEventListener(type, callback) { this.listeners[type] = callback; }
    }));
    const dom = Object.fromEntries(elements.map((e) => [e.id, e]));
    const calls = [];
    const clipboard = [];
    const state = { clipboardError };
    const context = vm.createContext({
        URLSearchParams, location: { search: welcome ? "?welcome=1" : "" },
        navigator: { userAgent: "TestBrowser/1.0", clipboard: { writeText: async (text) => {
            if (state.clipboardError) throw new Error("Denied");
            clipboard.push(text);
        } } },
        document: { getElementById: (id) => dom[id] || null, visibilityState: "visible" },
        t: (key) => { assert.ok(messages[key], `Missing ${language} translation: ${key}`); return messages[key].message; },
        localizeDom() {}, setInterval() {}, Date,
        chrome: {
            runtime: { getManifest: () => ({ version: "1.4.0" }), sendMessage: async (message) => {
                calls.push(clone(message));
                if (message.type === "YTM_GET_STATUS") return status;
                if (requestError) throw new Error("Worker stopped");
                return result;
            } },
            storage: { local: { get: async () => ({ securityToken: "saved-token", companionUrl: "" }) } },
            management: { getSelf: async () => {
                if (metadataError) throw new Error("Unavailable");
                return { installType };
            } }
        }
    });
    vm.runInContext(extensionScript("connection-ui.js"), context);
    vm.runInContext(extensionScript(`${name}.js`), context);
    await settle();
    return { dom, calls, clipboard, state, messages, render: (value) => context.renderStatus(value),
        submit: () => dom.setupForm.listeners.submit({ preventDefault() {} }) };
}

for (const language of ["de", "en"]) {
    for (const state of ["missing", "connecting", "authenticating", "connected", "rejected", "disconnected"]) {
        test(`${language}: popup and options agree on ${state} and a next action`, async () => {
            const status = { tokenConfigured: state !== "missing", connectionState: state };
            const options = await surface("options", { language, status });
            const popup = await surface("popup", { language, status });
            for (const id of ["stateBadge", "connectionText", "subtitle", "tokenText", "companionVersion"]) {
                assert.equal(options.dom[id].textContent, popup.dom[id].textContent);
                assert.ok(options.dom[id].textContent.length > 0);
            }
            assert.equal(options.dom.stateBadge.className.includes("ok"), state === "connected");
            if (state === "connected") assert.equal(options.dom.subtitle.textContent, options.messages.hintNoTrack.message);
        });
    }
}

for (const [installType, key] of [["development", "installLocal"], ["normal", "installPackage"], ["admin", "installManaged"], ["sideload", "installUnknown"], ["other", "installUnknown"]]) {
    test(`installation metadata: ${installType}`, async () => {
        const ui = await surface("options", { installType });
        assert.equal(ui.dom.installationType.textContent, ui.messages[key].message);
        assert.equal(ui.dom.extensionVersion.textContent, "1.4.0");
    });
}

test("failed installation lookup does not prevent connection status or save and check", async () => {
    const ui = await surface("options", { metadataError: true });
    assert.equal(ui.dom.installationType.textContent, ui.messages.installUnknown.message);
    assert.equal(ui.dom.stateBadge.className, "badge ok");
    await ui.submit();
    assert.equal(ui.dom.msg.textContent, "Saved");
});

test("unsaved input is explicitly separate from the saved connection; submit delegates saving and checking", async () => {
    const ui = await surface("options");
    ui.dom.token.value = "unsaved-token";
    ui.dom.token.listeners.input();
    ui.render({ tokenConfigured: true, connectionState: "connected", lastState: { title: "Track" } });
    assert.equal(ui.dom.msg.textContent, ui.messages.msgUnsaved.message);
    assert.equal(ui.dom.subtitle.textContent, ui.messages.hintConnected.message);
    assert.equal(ui.calls.filter((m) => m.type !== "YTM_GET_STATUS").length, 0);
    await ui.submit();
    assert.deepEqual(ui.calls.find((m) => m.type === "YTM_SAVE_AND_TEST"), {
        type: "YTM_SAVE_AND_TEST", token: "unsaved-token", companionUrl: ""
    });
    assert.equal(ui.dom.save.disabled, false);
});

test("saved-but-rejected feedback survives status refresh; changing input removes old success or failure", async () => {
    const ui = await surface("options", { result: { saved: true, ok: false, message: "Saved – token rejected" } });
    await ui.submit();
    assert.equal(ui.dom.msg.textContent, "Saved – token rejected");
    assert.equal(ui.dom.msg.className, "message err");
    ui.dom.token.listeners.input();
    assert.equal(ui.dom.msg.className, "message");
});

test("a failed worker response does not claim the save failed or succeeded and re-enables controls", async () => {
    const ui = await surface("options", { requestError: true });
    await ui.submit();
    assert.equal(ui.dom.msg.textContent, ui.messages.msgSaveResultUnknown.message);
    assert.equal(ui.dom.token.disabled, false);
    assert.equal(ui.dom.save.disabled, false);
});

test("popup retry sends no configuration and displays the optional companion version", async () => {
    const ui = await surface("popup", { status: { tokenConfigured: true, connectionState: "connected", companionVersion: "1.2.3" } });
    await ui.dom.testButton.listeners.click();
    assert.deepEqual(ui.calls.find((m) => m.type === "YTM_TEST_CONNECTION"), { type: "YTM_TEST_CONNECTION" });
    assert.equal(ui.dom.companionVersion.textContent, "1.2.3");
});

test("every declared DOM translation exists in both languages; no new permission is required", () => {
    for (const name of ["popup", "options"]) {
        const html = extensionScript(`${name}.html`);
        for (const language of ["de", "en"]) {
            const messages = JSON.parse(extensionScript(`_locales/${language}/messages.json`));
            for (const [, key] of html.matchAll(/data-i18n(?:-placeholder|-title)?="([^"]+)"/g)) {
                assert.ok(messages[key]?.message, `${name} ${language} ${key}`);
            }
        }
    }
    const manifest = JSON.parse(extensionScript("manifest.json"));
    assert.deepEqual(manifest.permissions, ["storage"]);
});

for (const surfaceName of ["popup", "options"]) {
    for (const language of ["de", "en"]) {
        const cases = [
            ["a companion older than the tested version", { connectionState: "connected", companionVersion: "1.4.0" }, "compatOutdated"],
            ["an older patch release", { connectionState: "connected", companionVersion: "1.5.0+abc123" }, "compatOutdated"],
            ["a companion that reports no version", { connectionState: "connected" }, "compatOutdatedUnknown"],
            ["the tested version with build metadata", { connectionState: "connected", companionVersion: "1.5.1+deadbeef" }, ""],
            ["a newer minor version", { connectionState: "connected", companionVersion: "1.6.0" }, ""],
            ["a newer major version", { connectionState: "connected", companionVersion: "2.0.0" }, ""],
            ["an unparseable version", { connectionState: "connected", companionVersion: "dev" }, ""],
            ["an old companion that is not connected", { connectionState: "disconnected", companionVersion: "1.4.0" }, ""],
            ["an old companion that rejected the token", { connectionState: "rejected" }, ""]
        ];
        for (const [label, extra, key] of cases) {
            test(`${surfaceName}/${language}: update hint for ${label}`, async () => {
                const ui = await surface(surfaceName, { language, status: { tokenConfigured: true, ...extra } });
                assert.equal(ui.dom.compatNotice.hidden, key === "");
                assert.equal(ui.dom.compatText.textContent, key ? ui.messages[key].message : "");
            });
        }
        test(`${surfaceName}/${language}: no token means no update hint`, async () => {
            const ui = await surface(surfaceName, { language, status: { tokenConfigured: false, connectionState: "connected" } });
            assert.equal(ui.dom.compatNotice.hidden, true);
        });
    }
}

test("the update hint appears and disappears with the status", async () => {
    const ui = await surface("popup", { status: { tokenConfigured: true, connectionState: "connected", companionVersion: "1.4.0" } });
    assert.equal(ui.dom.compatNotice.hidden, false);
    ui.render({ tokenConfigured: true, connectionState: "connected", companionVersion: "1.5.1" });
    assert.equal(ui.dom.compatNotice.hidden, true);
});

test("diagnostics copy versions and state but never the token, track title or artist", async () => {
    const status = {
        tokenConfigured: true, connectionState: "connected", companionVersion: "1.5.1+abc",
        activeCompanionUrl: "ws://127.0.0.1:17373/ws", lastError: "", lastConnectedAt: 1_700_000_000_000,
        selectedTabId: 7, securityToken: "SECRET-TOKEN-VALUE",
        lastState: { title: "Private Song", artist: "Private Artist", album: "Private Album", isPlaying: true }
    };
    const ui = await surface("popup", { status });
    await ui.dom.diagnosticsButton.listeners.click();
    assert.equal(ui.clipboard.length, 1);
    const report = ui.clipboard[0];
    for (const line of [
        "YTMPresence extension diagnostics", "Extension version: 1.4.0", "Installation type: development",
        "Browser: TestBrowser/1.0", "Connection: connected", "Token configured: yes",
        "Companion version: 1.5.1+abc", "Companion URL: ws://127.0.0.1:17373/ws",
        "Last connected: 2023-11-14T22:13:20.000Z", "Track state: playing", "Tab selected: yes"
    ]) assert.ok(report.includes(line), line);
    for (const secret of ["SECRET-TOKEN-VALUE", "Private Song", "Private Artist", "Private Album"]) {
        assert.ok(!report.includes(secret), secret);
    }
    assert.equal(ui.dom.message.textContent, ui.messages.msgDiagnosticsCopied.message);
    assert.equal(ui.dom.message.className, "message ok");
    assert.equal(ui.dom.diagnosticsText.hidden, true);
    assert.equal(ui.dom.diagnosticsButton.disabled, false);
});

test("diagnostics describe an unconfigured, disconnected extension without inventing values", async () => {
    const ui = await surface("popup", { installType: "normal", status: { tokenConfigured: false, connectionState: "idle", lastError: "Companion unreachable" } });
    await ui.dom.diagnosticsButton.listeners.click();
    const report = ui.clipboard[0];
    for (const line of ["Installation type: normal", "Token configured: no", "Companion version: not reported",
        "Last error: Companion unreachable", "Last connected: -", "Track state: none", "Tab selected: no"]) {
        assert.ok(report.includes(line), line);
    }
});

test("a lookup failure for the installation type is reported as unknown, not as a failure", async () => {
    const ui = await surface("popup", { metadataError: true });
    await ui.dom.diagnosticsButton.listeners.click();
    assert.ok(ui.clipboard[0].includes("Installation type: unknown"));
});

test("when the clipboard refuses, the report is shown for manual copying", async () => {
    const ui = await surface("popup", { clipboardError: true });
    await ui.dom.diagnosticsButton.listeners.click();
    assert.equal(ui.clipboard.length, 0);
    assert.equal(ui.dom.message.textContent, ui.messages.msgDiagnosticsCopyFailed.message);
    assert.equal(ui.dom.message.className, "message err");
    assert.equal(ui.dom.diagnosticsText.hidden, false);
    assert.ok(ui.dom.diagnosticsText.value.startsWith("YTMPresence extension diagnostics"));
    assert.equal(ui.dom.diagnosticsButton.disabled, false);
});

test("a copy after a failed copy hides the manual text again", async () => {
    const ui = await surface("popup", { clipboardError: true });
    await ui.dom.diagnosticsButton.listeners.click();
    assert.equal(ui.dom.diagnosticsText.hidden, false);
    ui.state.clipboardError = false;
    await ui.dom.diagnosticsButton.listeners.click();
    assert.equal(ui.dom.diagnosticsText.hidden, true);
    assert.equal(ui.clipboard.length, 1);
    assert.equal(ui.dom.message.className, "message ok");
});

test("the welcome card is hidden by default and shown only for the first-install page", async () => {
    const plain = await surface("options");
    assert.equal(plain.dom.welcomeCard.hidden, true);
    const first = await surface("options", { welcome: true });
    assert.equal(first.dom.welcomeCard.hidden, false);
});

test("the setup guide points to the releases page and every link is an https address", () => {
    for (const name of ["popup", "options"]) {
        const html = extensionScript(`${name}.html`);
        for (const [, href] of html.matchAll(/<a\b[^>]*\bhref="([^"]+)"/g)) {
            assert.match(href, /^https:\/\/github\.com\/Kentarohakase\/YTMPresence\/releases\/latest$/);
        }
        for (const [tag] of html.matchAll(/<a\b[^>]*>/g)) assert.match(tag, /rel="noopener noreferrer"/);
    }
});
