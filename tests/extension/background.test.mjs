import assert from "node:assert/strict";
import test from "node:test";
import { background, settle, tabSender, track } from "./helpers.mjs";

test("a paused background tab cannot replace the playing track", async () => {
    const app = await background();
    app.sockets[0].open();
    await app.state(1, track("Playing"));
    await app.state(2, track("Paused", false));
    assert.equal((await app.status()).lastState.title, "Playing");
    assert.deepEqual(app.sockets[0].sent.map((state) => state.title), ["Playing"]);
});

for (const command of ["play-pause", "next", "previous"]) {
    test(`${command} reaches only the tab supplying the presence`, async () => {
        const app = await background();
        app.sockets[0].open();
        await app.state(4, track("Selected"));
        await app.state(2, track("Other", false));
        await app.command(command);
        assert.deepEqual(app.deliveries.map((item) => item.id), [4]);
        assert.equal(app.deliveries[0].message.command, command);
        assert.deepEqual(app.deliveries[0].options, { frameId: 0 });
    });
}

test("a playing selection stays stable when another tab also plays", async () => {
    const app = await background();
    await app.state(8, track("First"));
    await app.state(2, track("Second"));
    assert.equal((await app.status()).lastState.title, "First");
});

test("pause preserves the selected tab until another tab plays", async () => {
    const app = await background();
    await app.state(8, track("First"));
    await app.state(2, track("Other", false));
    await app.state(8, track("First", false));
    await app.command();
    assert.deepEqual(app.deliveries.map((item) => item.id), [8]);
    await app.state(2, track("Other"));
    assert.equal((await app.status()).lastState.title, "Other");
});

test("pausing the selected tab chooses a playing fallback by lowest tab ID", async () => {
    const app = await background();
    await app.state(8, track("First"));
    await app.state(4, track("Fourth"));
    await app.state(2, track("Second"));
    await app.state(8, track("First", false));
    assert.equal((await app.status()).lastState.title, "Second");
});

test("closing the selection publishes a fallback and routes the next command there", async () => {
    const app = await background();
    app.sockets[0].open();
    await app.state(1, track("First"));
    await app.state(2, track("Fallback", false));
    await app.remove(1);
    assert.equal(app.sockets[0].sent.at(-1).title, "Fallback");
    assert.equal((await app.status()).selectedTabId, 2);
    await app.command();
    assert.deepEqual(app.deliveries.map((item) => item.id), [2]);
});

test("closing an unrelated tab leaves the selection intact", async () => {
    const app = await background();
    await app.state(1, track("First"));
    await app.state(2, track("Other", false));
    await app.remove(2);
    assert.equal((await app.status()).lastState.title, "First");
});

for (const change of [{ status: "loading" }, { url: "https://example.com/" }, { discarded: true }]) {
    test(`invalidating the selected document chooses a fallback: ${JSON.stringify(change)}`, async () => {
        const app = await background();
        await app.state(1, track("First"));
        await app.state(2, track("Fallback", false));
        app.chrome.tabs.onUpdated.emit(1, change, {});
        await settle();
        await app.command();
        assert.equal((await app.status()).lastState.title, "Fallback");
        assert.deepEqual(app.deliveries.map((item) => item.id), [2]);
    });
}

test("YouTube Music SPA navigation and ordinary tab updates preserve selection", async () => {
    const app = await background();
    await app.state(1, track("First"));
    app.chrome.tabs.onUpdated.emit(1, { url: "https://music.youtube.com/watch?v=next", audible: false }, {});
    await app.command();
    assert.deepEqual(app.deliveries.map((item) => item.id), [1]);
});

test("replacing a tab drops its old document until the replacement reports state", async () => {
    const app = await background();
    await app.state(1, track("Old"));
    app.chrome.tabs.onReplaced.emit(3, 1);
    await settle();
    assert.equal((await app.status()).lastState, null);
    await app.state(3, track("New"));
    await app.command();
    assert.deepEqual(app.deliveries.map((item) => item.id), [3]);
});

test("removing the final tab clears queued state and prevents replay after token changes", async () => {
    const app = await background();
    await app.state(1, track("Gone"));
    await app.remove(1);
    app.chrome.storage.onChanged.emit({ securityToken: { newValue: "new-token" } }, "local");
    await settle();
    app.sockets.at(-1).open();
    assert.deepEqual(app.sockets.at(-1).sent, []);
    const status = await app.status();
    assert.equal(status.lastState, null);
    assert.equal(status.selectedTabId, null);
    assert.equal(status.hasPendingPayload, false);
    await app.command();
    assert.deepEqual(app.deliveries, []);
});

test("selection changes replace pending data while the socket connects", async () => {
    const app = await background();
    await app.state(1, track("Paused", false));
    await app.state(2, track("Playing"));
    app.sockets[0].open();
    assert.deepEqual(app.sockets[0].sent.map((state) => state.title), ["Playing"]);
});

test("a failed command is not retried on another tab", async () => {
    const app = await background();
    await app.state(1, track("First"));
    await app.state(2, track("Other", false));
    app.tabs.delete(1); // Disappears before the browser delivers onRemoved.
    await app.command();
    assert.deepEqual(app.deliveries.map((item) => item.id), [1]);
    assert.equal((await app.status()).lastError, "bgCommandRejected");
});

test("only main-frame YouTube Music state can select a tab", async () => {
    const app = await background();
    for (const sender of [
        {}, tabSender(1, { frameId: 2 }), tabSender(2, { url: "https://example.com/" })
    ]) {
        app.chrome.runtime.onMessage.emit({ type: "YTM_STATE", payload: track("Invalid") }, sender, () => {});
    }
    await settle();
    assert.equal((await app.status()).lastState, null);
    await app.state(0, track("Valid"));
    await app.command();
    assert.deepEqual(app.deliveries.map((item) => item.id), [0]);
});

test("late command feedback from another tab cannot overwrite selected feedback", async () => {
    const app = await background();
    await app.state(1, track("First"));
    await app.state(2, track("Other", false));
    await app.command();
    app.chrome.runtime.onMessage.emit(
        { type: "YTM_COMMAND_RESULT", command: "next", ok: false }, tabSender(2), () => {}
    );
    assert.equal((await app.status()).lastError, "");
});

test("closing a tab during config loading cannot resurrect its queued state", async () => {
    let release;
    const app = await background({ storageReady: new Promise((resolve) => { release = resolve; }) });
    await app.state(1, track("Gone"));
    await app.remove(1);
    release();
    await settle();
    app.sockets[0].open();
    assert.equal((await app.status()).lastState, null);
    assert.deepEqual(app.sockets[0].sent, []);
});

test("the selected source survives reconnect and ignored tab updates", async () => {
    const app = await background();
    app.sockets[0].open();
    await app.state(1, track("First"));
    app.sockets[0].close();
    await app.state(1, track("Updated"));
    await app.state(2, track("Other", false));
    await app.time.advance(500);
    app.sockets.at(-1).open();
    assert.deepEqual(app.sockets.at(-1).sent.map((state) => state.title), ["Updated"]);
    await app.command();
    assert.deepEqual(app.deliveries.map((item) => item.id), [1]);
});

test("an old socket cannot deliver commands after settings restart the connection", async () => {
    const app = await background();
    const oldSocket = app.sockets[0];
    oldSocket.open();
    await app.state(1, track("First"));
    app.chrome.storage.onChanged.emit({ securityToken: { newValue: "new-token" } }, "local");
    await settle();
    oldSocket.receive({ type: "YTM_COMMAND", command: "next" });
    await settle();
    assert.deepEqual(app.deliveries, []);
    app.sockets.at(-1).open();
    assert.equal(app.sockets.at(-1).sent[0].token, "new-token");
    await app.command("next");
    assert.deepEqual(app.deliveries.map((item) => item.id), [1]);
});

test("without a token state stays local until configuration is supplied", async () => {
    const app = await background({ token: "" });
    await app.state(1, track("Selected"));
    await app.state(2, track("Other", false));
    assert.equal(app.sockets.length, 0);
    assert.equal((await app.status()).lastState.title, "Selected");
    app.chrome.storage.onChanged.emit({ securityToken: { newValue: "configured" } }, "local");
    await settle();
    app.sockets[0].open();
    assert.deepEqual(app.sockets[0].sent.map((state) => state.title), ["Selected"]);
});

test("an unknown command cannot reach a tab", async () => {
    const app = await background();
    await app.state(1, track("Selected"));
    await app.command("unknown");
    assert.deepEqual(app.deliveries, []);
});

test("worker restart restores a playing source from existing content scripts", async () => {
    const app = await background({ initialTabs: [
        [1, track("Paused", false)], [8, track("Eighth")], [4, track("Fourth")]
    ] });
    app.sockets[0].open();
    assert.equal(app.sockets[0].sent.at(-1).title, "Fourth");
    await app.command();
    assert.deepEqual(app.deliveries.map((item) => item.id), [4]);
});

test("worker restart restores a paused source when no tab plays", async () => {
    const app = await background({ initialTabs: [[8, track("Eighth", false)], [4, track("Fourth", false)]] });
    assert.equal((await app.status()).selectedTabId, 4);
    await app.command();
    assert.deepEqual(app.deliveries.map((item) => item.id), [4]);
});

test("a late restore response cannot resurrect a closed tab", async () => {
    let release;
    const app = await background({
        initialTabs: [[1, track("Old")]],
        readState: () => new Promise((resolve) => { release = resolve; })
    });
    await app.remove(1);
    release({ payload: track("Old") });
    await settle();
    assert.equal((await app.status()).lastState, null);
});

test("live state takes precedence over an older restore response", async () => {
    let release;
    const app = await background({
        initialTabs: [[1, track("Old")]],
        readState: () => new Promise((resolve) => { release = resolve; })
    });
    await app.state(1, track("New"));
    release({ payload: track("Old") });
    await settle();
    assert.equal((await app.status()).lastState.title, "New");
});
