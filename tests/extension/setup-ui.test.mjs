import assert from "node:assert/strict";
import test from "node:test";
import vm from "node:vm";
import { extensionScript, settle, clone } from "./helpers.mjs";

async function surface(name, { language = "en", installType = "development", metadataError = false, requestError = false,
    status = { tokenConfigured: true, connectionState: "connected" }, result = { saved: true, ok: true, message: "Saved" } } = {}) {
    const html = extensionScript(`${name}.html`);
    const messages = JSON.parse(extensionScript(`_locales/${language}/messages.json`));
    const elements = [...html.matchAll(/<[^>]+\bid="([^"]+)"[^>]*>/g)].map(([tag, id]) => ({
        id, value: "", textContent: "", className: "", disabled: false, listeners: {},
        addEventListener(type, callback) { this.listeners[type] = callback; }
    }));
    const dom = Object.fromEntries(elements.map((e) => [e.id, e]));
    const calls = [];
    const context = vm.createContext({
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
    return { dom, calls, messages, render: (value) => context.renderStatus(value),
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
