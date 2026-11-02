import assert from "node:assert/strict";
import test from "node:test";
import { background, settle, track } from "./helpers.mjs";

test("socket open is not authentication; playback waits for the current socket's positive reply", async () => {
    const app = await background();
    await app.state(1, track("Selected"));
    const socket = app.sockets[0];
    assert.equal((await app.status()).connectionState, "connecting");
    socket.open();
    assert.equal((await app.status()).connectionState, "authenticating");
    assert.deepEqual(socket.sent, [{ type: "YTM_TEST", token: "test-token", extensionVersion: "1.5.1", ts: 10000 }]);
    socket.receive({ type: "YTM_TEST_RESULT", ok: "true" });
    assert.equal((await app.status()).connectionState, "authenticating");
    socket.receive({ type: "YTM_TEST_RESULT", ok: true });
    assert.equal((await app.status()).connectionState, "connected");
    assert.equal(socket.playback[0].title, "Selected");
    assert.equal((await app.status()).companionVersion, ""); // Older companions remain compatible.
});

test("the popup test ignores unsaved input and checks the saved configuration on the active socket", async () => {
    const app = await background();
    app.sockets[0].authenticate();
    const result = app.request({ type: "YTM_TEST_CONNECTION", token: "unsaved", companionUrl: "ws://localhost:9999" });
    await settle();
    assert.equal(app.sockets.length, 1);
    assert.equal(app.sockets[0].sent.at(-1).token, "test-token");
    app.sockets[0].receive({ type: "YTM_TEST_RESULT", ok: true, companionVersion: "9.9.9" });
    assert.equal((await result).ok, true);
    assert.equal((await app.status()).companionVersion, "9.9.9");
});

test("missing token and invalid URL do not save or change the running connection", async () => {
    const app = await background({ token: "" });
    assert.equal((await app.request({ type: "YTM_TEST_CONNECTION" })).ok, false);
    for (const settings of [{ token: " " }, { token: "new", companionUrl: "https://example.com/" }]) {
        assert.equal((await app.request({ type: "YTM_SAVE_AND_TEST", ...settings })).saved, false);
    }
    assert.equal(app.stored.securityToken, "");
    assert.equal(app.sockets.length, 0);
});

test("storage failure never switches the active connection or claims settings were saved", async () => {
    const app = await background({ saveError: true });
    app.sockets[0].authenticate();
    const result = await app.request({ type: "YTM_SAVE_AND_TEST", token: "new" });
    assert.deepEqual(result, { saved: false, ok: false, message: "msgSaveFailed" });
    assert.equal(app.sockets.length, 1);
    assert.equal((await app.status()).connectionState, "connected");
    assert.equal(app.stored.securityToken, "test-token");
});

test("save and its storage event cause only one reconnect, and success follows authentication", async () => {
    const app = await background();
    const result = app.request({ type: "YTM_SAVE_AND_TEST", token: " new ", companionUrl: "http://localhost:17373" });
    await settle();
    assert.equal(app.stored.securityToken, "new");
    assert.equal(app.stored.companionUrl, "ws://localhost:17373/ws");
    assert.equal(app.sockets.length, 2);
    app.sockets[1].authenticate();
    assert.equal((await result).saved, true);
    assert.equal((await result).ok, true);
    assert.equal(app.sockets.length, 2);
});

test("wrong token is saved but rejected and does not repeatedly reconnect", async () => {
    const app = await background({ token: "" });
    const result = app.request({ type: "YTM_SAVE_AND_TEST", token: "wrong" });
    await settle();
    app.sockets[0].open();
    app.sockets[0].receive({ type: "YTM_TEST_RESULT", ok: false });
    assert.equal((await result).message, "msgSavedRejected");
    assert.equal((await result).saved, true);
    assert.equal((await app.status()).connectionState, "rejected");
    await app.state(1, track("Track"));
    await app.time.advance(10000);
    assert.equal(app.sockets.length, 1);
    assert.deepEqual(app.sockets[0].playback, []);
});

for (const failure of ["error", "close", "timeout", "connecting-timeout"]) {
    test(`saved but unreachable: ${failure}; retries respect the existing delay`, async () => {
        const app = await background({ token: "" });
        const result = app.request({ type: "YTM_SAVE_AND_TEST", token: "new" });
        await settle();
        const socket = app.sockets[0];
        if (failure !== "connecting-timeout") socket.open();
        if (failure === "error") socket.onerror();
        else if (failure === "close") socket.close();
        else await app.time.advance(4000);
        assert.equal((await result).saved, true);
        assert.equal((await result).ok, false);
        assert.equal((await result).message, "msgSavedUnreachable");
        assert.equal((await app.status()).connectionState, "disconnected");
        await app.state(1, track("Pending"));
        await app.time.advance(499);
        assert.equal(app.sockets.length, 1);
        await app.time.advance(1);
        assert.equal(app.sockets.length, 2);
        socket.open();
        socket.receive({ type: "YTM_TEST_RESULT", ok: true });
        socket.onerror();
        assert.equal((await app.status()).connectionState, "connecting");
        app.sockets[1].authenticate();
        assert.equal(app.sockets[1].playback[0].title, "Pending");
    });
}

test("socket construction failure still reports saved settings", async () => {
    const app = await background({ token: "", socketError: true });
    const result = await app.request({ type: "YTM_SAVE_AND_TEST", token: "new" });
    assert.equal(result.saved, true);
    assert.equal(result.ok, false);
});

test("token changes settle the old test; late open, error and replies cannot affect the new socket", async () => {
    const app = await background();
    const old = app.sockets[0];
    const first = app.request({ type: "YTM_TEST_CONNECTION" });
    await settle();
    const second = app.request({ type: "YTM_SAVE_AND_TEST", token: "replacement" });
    await settle();
    assert.equal((await first).reason, "changed");
    old.open();
    old.receive({ type: "YTM_TEST_RESULT", ok: true, companionVersion: "old" });
    old.onerror();
    assert.equal(old.sent.length, 0);
    assert.equal((await app.status()).connectionState, "connecting");
    const current = app.sockets.at(-1);
    current.authenticate();
    assert.equal((await second).ok, true);
    old.receive({ type: "YTM_TEST_RESULT", ok: false });
    assert.equal((await app.status()).connectionState, "connected");
    assert.equal(current.sent[0].token, "replacement");
});

test("concurrent saves do not let the older test confirm the new token", async () => {
    const app = await background();
    const first = app.request({ type: "YTM_SAVE_AND_TEST", token: "first" });
    const second = app.request({ type: "YTM_SAVE_AND_TEST", token: "second" });
    await settle();
    assert.equal((await first).message, "msgSavedChanged");
    app.sockets.at(-1).authenticate();
    assert.equal((await second).ok, true);
    assert.equal(app.stored.securityToken, "second");
    assert.equal(app.sockets.at(-1).sent[0].token, "second");
});

test("reconnect replays the paused selection, but successful keepalives do not replay playback", async () => {
    const app = await background();
    app.sockets[0].authenticate();
    await app.state(1, track("Paused", false));
    app.sockets[0].close();
    await app.time.advance(500);
    const current = app.sockets.at(-1);
    current.authenticate();
    assert.equal(current.playback[0].title, "Paused");
    const status = await app.status();
    await app.time.advance(20000);
    assert.equal(current.sent.at(-1).type, "YTM_TEST");
    current.receive({ type: "YTM_TEST_RESULT", ok: true });
    assert.equal(current.playback.length, 1);
    assert.equal((await app.status()).lastSentAt, status.lastSentAt);
    assert.equal((await app.status()).lastReceivedStateAt, status.lastReceivedStateAt);
});

test("an unanswered keepalive drops authentication after four seconds", async () => {
    const app = await background();
    app.sockets[0].authenticate();
    await app.time.advance(24000);
    assert.equal((await app.status()).connectionState, "disconnected");
});

test("an unreadable manifest only omits the extension version from the connection test", async () => {
    const app = await background({ manifest: () => { throw new Error("unavailable"); } });
    await app.state(1, track("Selected"));
    const socket = app.sockets[0];
    socket.open();
    assert.deepEqual(socket.sent, [{ type: "YTM_TEST", token: "test-token", ts: 10000 }]);
    socket.receive({ type: "YTM_TEST_RESULT", ok: true });
    assert.equal((await app.status()).connectionState, "connected");
});
