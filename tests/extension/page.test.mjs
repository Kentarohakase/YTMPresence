import assert from "node:assert/strict";
import test from "node:test";
import vm from "node:vm";
import { clock, clone, extensionScript } from "./helpers.mjs";

// Minimal DOM/media fixtures around the real page script's published messages.
function page({ tag = "audio", media = {}, metadata, playbackState = "playing", url } = {}) {
    const time = clock();
    const posted = [];
    const elements = new Map();
    const mediaEvents = new Map();
    const player = {
        paused: false, seeking: false, currentTime: 12, duration: 180, ...media,
        addEventListener: (name, callback) => mediaEvents.set(name, callback)
    };
    if (tag) elements.set(tag, [player]);
    const window = { addEventListener() {}, postMessage: (value) => posted.push(clone(value)) };
    const navigator = { mediaSession: {
        playbackState,
        metadata: metadata === undefined ? { title: "Track", artist: "Artist", artwork: [] } : metadata
    } };
    vm.runInNewContext(extensionScript("page.js"), {
        window, navigator, URL,
        location: { href: url ?? "https://music.youtube.com/", origin: "https://music.youtube.com" },
        document: {
            title: "", addEventListener() {},
            querySelector: (selector) => elements.get(selector)?.[0] ?? null,
            querySelectorAll: (selector) => elements.get(selector) ?? []
        },
        getComputedStyle: () => ({ display: "block", visibility: "visible", opacity: "1" }),
        ...time.globals
    }, { filename: "page.js" });
    return { time, posted, elements, player, mediaEvents, navigator,
        state: () => posted.at(-1)?.payload };
}

function visible(properties = {}) {
    return { textContent: "", getBoundingClientRect: () => ({ width: 100, height: 20 }), ...properties };
}

for (const tag of ["audio", "video"]) {
    test(`${tag} pause overrides an incorrectly playing media session`, async () => {
        const app = page({ tag, media: { paused: true } });
        await app.time.advance(250);
        assert.equal(app.state().isPlaying, false);
        assert.equal(app.state().position, 12);
        assert.equal(app.state().duration, 180);
    });
}

test("playing media overrides an incorrectly paused media session", async () => {
    const app = page({ playbackState: "paused" });
    await app.time.advance(250);
    assert.equal(app.state().isPlaying, true);
});

test("seeking uses media-session state to avoid a transient pause", async () => {
    const app = page({ media: { paused: true, seeking: true } });
    await app.time.advance(250);
    assert.equal(app.state().isPlaying, true);
});

test("unknown playback without a media element is conservatively paused", async () => {
    const app = page({ tag: null, playbackState: "none" });
    await app.time.advance(250);
    assert.equal(app.state().isPlaying, false);
    assert.equal(app.state().position, null);
});

test("pause and play events publish immediately", async () => {
    const app = page();
    await app.time.advance(250);
    app.player.paused = true;
    app.mediaEvents.get("pause")();
    await app.time.advance(0);
    assert.equal(app.state().isPlaying, false);
    app.player.paused = false;
    app.mediaEvents.get("play")();
    await app.time.advance(0);
    assert.equal(app.state().isPlaying, true);
    assert.equal(app.posted.length, 3);
});

test("artwork identifies the current track ahead of stale page URLs", async () => {
    const app = page({
        metadata: { title: "Current", artwork: [{ src: "https://i.ytimg.com/vi/abcdefghijk/hqdefault.jpg" }] },
        url: "https://music.youtube.com/watch?v=old-location"
    });
    app.elements.set('ytmusic-player-bar a#song-title', [visible({ href: "https://music.youtube.com/watch?v=old-link" })]);
    await app.time.advance(250);
    assert.equal(app.state().shareUrl, "https://music.youtube.com/watch?v=abcdefghijk");
});

test("player-bar URL is used before the address bar when artwork has no ID", async () => {
    const app = page({ url: "https://music.youtube.com/watch?v=old-location" });
    app.elements.set('ytmusic-player-bar a#song-title', [visible({ href: "/watch?v=current-link" })]);
    await app.time.advance(250);
    assert.equal(app.state().shareUrl, "https://music.youtube.com/watch?v=current-link");
});

test("an unrelated watch link cannot override the address-bar fallback", async () => {
    const app = page({ url: "https://music.youtube.com/watch?v=current-location" });
    app.elements.set('a[href*="/watch?v="]', [{ href: "https://music.youtube.com/watch?v=recommendation" }]);
    await app.time.advance(250);
    assert.equal(app.state().shareUrl, "https://music.youtube.com/watch?v=current-location");
});

test("search fallback preserves spaces, Unicode and reserved characters as encoded text", async () => {
    const app = page({ metadata: { title: "Über & Out", artist: "Sam Smith" } });
    await app.time.advance(250);
    assert.equal(app.state().shareUrl, "https://music.youtube.com/search?q=%C3%9Cber%20%26%20Out%20Sam%20Smith");
});

test("visible player metadata is used when media-session metadata is missing", async () => {
    const app = page({ metadata: null });
    app.elements.set("ytmusic-player-bar .title", [visible({ textContent: "DOM Track" })]);
    app.elements.set("ytmusic-player-bar .byline", [visible({ textContent: "Artist • Album" })]);
    await app.time.advance(250);
    assert.equal(app.state().title, "DOM Track");
    assert.equal(app.state().artist, "Artist");
});

test("empty metadata does not invent a track", async () => {
    const app = page({ metadata: null });
    await app.time.advance(250);
    assert.deepEqual(app.posted, []);
});

test("a visible advertisement publishes even without a track title", async () => {
    const app = page({ metadata: null });
    app.elements.set(".ad-showing", [visible()]);
    await app.time.advance(250);
    assert.equal(app.state().isAd, true);
    assert.equal(app.state().title, "");
});
