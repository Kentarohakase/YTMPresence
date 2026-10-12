import { readFileSync } from "node:fs";
import vm from "node:vm";

export const extensionScript = (name) => readFileSync(
    new URL(`../../YTMPresence/extension/${name}`, import.meta.url), "utf8"
);
export const settle = () => new Promise((resolve) => setImmediate(resolve));
export const clone = (value) => JSON.parse(JSON.stringify(value));

export function event() {
    const listeners = [];
    return {
        addListener: (listener) => listeners.push(listener),
        emit: (...args) => listeners.map((listener) => listener(...args))
    };
}

export function clock() {
    let now = 10_000;
    let nextId = 1;
    const timers = new Map();
    const add = (callback, delay, repeat) => {
        const id = nextId++;
        timers.set(id, { callback, at: now + delay, repeat });
        return id;
    };
    return {
        globals: {
            Date: class extends Date { static now() { return now; } },
            setTimeout: (callback, delay = 0) => add(callback, delay, 0),
            clearTimeout: (id) => timers.delete(id),
            setInterval: (callback, delay) => add(callback, delay, delay),
            clearInterval: (id) => timers.delete(id)
        },
        async advance(ms) {
            const end = now + ms;
            for (;;) {
                const next = [...timers].sort((a, b) => a[1].at - b[1].at)[0];
                if (!next || next[1].at > end) break;
                const [id, timer] = next;
                now = timer.at;
                if (timer.repeat) timer.at += timer.repeat;
                else timers.delete(id);
                timer.callback();
                await settle();
            }
            now = end;
            await settle();
        }
    };
}

export function tabSender(id, overrides = {}) {
    return { tab: { id }, frameId: 0, url: "https://music.youtube.com/", ...overrides };
}

export function track(title, isPlaying = true, overrides = {}) {
    return { source: "ytmusic", title, artist: "Artist", isPlaying, ...overrides };
}

export async function background({ token = "test-token", storageReady, initialTabs = [], readState } = {}) {
    const time = clock();
    const sockets = [];
    const deliveries = [];
    const tabs = new Map(initialTabs);
    const chrome = {
        i18n: { getMessage: (key) => key },
        action: { setBadgeText() {}, setBadgeBackgroundColor() {}, setTitle() {} },
        storage: {
            local: { get: async () => {
                if (storageReady) await storageReady;
                return { securityToken: token, companionUrl: "" };
            } },
            onChanged: event()
        },
        runtime: { onMessage: event() },
        tabs: {
            onRemoved: event(), onUpdated: event(), onReplaced: event(),
            query: async () => [...tabs.keys()].map((id) => ({ id })),
            sendMessage: async (id, message, options) => {
                if (message.type === "YTM_GET_STATE") {
                    return readState ? readState(id) : { payload: tabs.get(id) };
                }
                deliveries.push(clone({ id, message, options }));
                if (!tabs.has(id)) throw new Error("Tab is gone");
                return { ok: true };
            }
        }
    };
    class Socket {
        static CONNECTING = 0;
        static OPEN = 1;
        static CLOSED = 3;
        readyState = Socket.CONNECTING;
        sent = [];
        constructor(url) { this.url = url; sockets.push(this); }
        open() { this.readyState = Socket.OPEN; this.onopen?.(); }
        send(data) { this.sent.push(JSON.parse(data)); }
        close() { this.readyState = Socket.CLOSED; this.onclose?.({ code: 1000 }); }
        receive(message) { this.onmessage?.({ data: JSON.stringify(message) }); }
    }
    vm.runInNewContext(extensionScript("background.js"), {
        chrome, WebSocket: Socket, URL, console: { info() {}, warn() {} }, ...time.globals
    }, { filename: "background.js" });
    await settle();
    return {
        chrome, sockets, deliveries, tabs, time,
        async state(id, payload, overrides) {
            tabs.set(id, payload);
            chrome.runtime.onMessage.emit({ type: "YTM_STATE", payload }, tabSender(id, overrides), () => {});
            await settle();
        },
        async status() {
            return new Promise((resolve) => chrome.runtime.onMessage.emit(
                { type: "YTM_GET_STATUS" }, {}, (value) => resolve(clone(value))
            ));
        },
        async remove(id) {
            tabs.delete(id);
            chrome.tabs.onRemoved.emit(id, {});
            await settle();
        },
        async command(command = "play-pause") {
            sockets.at(-1).receive({ type: "YTM_COMMAND", command });
            await settle();
        }
    };
}
