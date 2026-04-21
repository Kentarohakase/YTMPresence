const DEFAULT_COMPANION_WS_URLS = [
    "ws://127.0.0.1:17373/ws",
    "ws://localhost:17373/ws"
];
const MIN_SEND_INTERVAL_MS = 1500;

let companionWsUrls = [...DEFAULT_COMPANION_WS_URLS];
let webSocket = null;
let reconnectTimer = null;
let reconnectDelayMs = 500;
let nextCompanionUrlIndex = 0;
let activeCompanionUrl = "";

let lastSentKey = "";
let lastSentAt = 0;

let cachedToken = "";
let configLoaded = false;
let missingTokenLogged = false;

function normalizeCompanionUrl(value) {
    let raw = (value || "").trim();
    if (!raw) return "";

    if (!/^[a-z][a-z0-9+.-]*:\/\//i.test(raw)) {
        raw = `ws://${raw}`;
    }

    try {
        const url = new URL(raw);

        if (url.protocol === "http:") url.protocol = "ws:";
        if (url.protocol === "https:") url.protocol = "wss:";

        if (url.protocol !== "ws:" && url.protocol !== "wss:") return "";

        const host = url.hostname.toLowerCase();
        if (host !== "127.0.0.1" && host !== "localhost") return "";

        if (!url.pathname || url.pathname === "/") {
            url.pathname = "/ws";
        }

        url.hash = "";
        return url.toString();
    } catch {
        return "";
    }
}

async function loadConfig() {
    const result = await chrome.storage.local.get({
        securityToken: "",
        companionUrl: ""
    });

    cachedToken = (result.securityToken || "").trim();
    const customUrl = normalizeCompanionUrl(result.companionUrl);
    companionWsUrls = customUrl ? [customUrl] : [...DEFAULT_COMPANION_WS_URLS];
    configLoaded = true;

    if (!cachedToken && !missingTokenLogged) {
        missingTokenLogged = true;
        console.warn("[YTM Bridge] No token configured. Open extension options and paste the tray token.");
    }
}

async function ensureConfigLoaded() {
    if (!configLoaded) {
        await loadConfig();
    }
}

chrome.storage.onChanged.addListener((changes, area) => {
    if (area !== "local") return;

    let shouldRestart = false;

    if (changes.securityToken) {
        cachedToken = (changes.securityToken.newValue || "").trim();
        missingTokenLogged = false;
        shouldRestart = true;
        console.info("[YTM Bridge] Token updated.");
    }

    if (changes.companionUrl) {
        const customUrl = normalizeCompanionUrl(changes.companionUrl.newValue);
        companionWsUrls = customUrl ? [customUrl] : [...DEFAULT_COMPANION_WS_URLS];
        nextCompanionUrlIndex = 0;
        shouldRestart = true;
        console.info(`[YTM Bridge] Companion URL updated: ${companionWsUrls.join(", ")}`);
    }

    if (shouldRestart) {
        restartConnection();
    }
});

chrome.runtime.onMessage.addListener((message) => {
    if (!message || message.type !== "YTM_STATE") return false;

    sendState(message.payload).catch((error) => {
        console.warn(`[YTM Bridge] State send failed: ${error?.message || error}`);
    });

    return false;
});

async function sendState(payload) {
    await ensureConfigLoaded();

    if (!cachedToken) {
        if (!missingTokenLogged) {
            missingTokenLogged = true;
            console.warn("[YTM Bridge] Dropping state because no token is configured.");
        }
        return;
    }

    const now = Date.now();
    const key = `${payload.isPlaying}|${payload.title}|${payload.artist}|${payload.url}|${payload.isAd}`;
    if (key === lastSentKey && (now - lastSentAt) < MIN_SEND_INTERVAL_MS) return;

    lastSentKey = key;
    lastSentAt = now;

    const socket = connect();
    if (!socket || socket.readyState !== WebSocket.OPEN) return;

    socket.send(JSON.stringify({ ...payload, token: cachedToken }));
}

function connect() {
    if (!cachedToken) return null;

    if (webSocket) {
        if (webSocket.readyState === WebSocket.OPEN || webSocket.readyState === WebSocket.CONNECTING) {
            return webSocket;
        }

        webSocket = null;
    }

    try {
        activeCompanionUrl = companionWsUrls[nextCompanionUrlIndex];
        const socket = new WebSocket(activeCompanionUrl);
        webSocket = socket;

        socket.onopen = () => {
            reconnectDelayMs = 500;
            console.info(`[YTM Bridge] Connected to ${activeCompanionUrl}.`);
        };

        socket.onclose = (event) => {
            if (webSocket !== socket) return;

            console.warn(
                `[YTM Bridge] Companion disconnected (${activeCompanionUrl}). ` +
                `Code: ${event.code || "?"}, reason: ${event.reason || "n/a"}`
            );

            webSocket = null;
            nextCompanionUrlIndex = (nextCompanionUrlIndex + 1) % companionWsUrls.length;
            scheduleReconnect();
        };

        socket.onerror = () => {
            console.warn(`[YTM Bridge] WebSocket error at ${activeCompanionUrl}.`);
        };

        return socket;
    } catch (error) {
        console.warn(`[YTM Bridge] WebSocket could not be created: ${error?.message || error}`);
        webSocket = null;
        nextCompanionUrlIndex = (nextCompanionUrlIndex + 1) % companionWsUrls.length;
        scheduleReconnect();
        return null;
    }
}

function scheduleReconnect() {
    if (!cachedToken || reconnectTimer) return;

    reconnectTimer = setTimeout(() => {
        reconnectTimer = null;
        connect();
    }, reconnectDelayMs);

    reconnectDelayMs = Math.min(10_000, reconnectDelayMs * 2);
}

function restartConnection() {
    if (reconnectTimer) {
        clearTimeout(reconnectTimer);
        reconnectTimer = null;
    }

    reconnectDelayMs = 500;

    if (webSocket) {
        const socket = webSocket;
        webSocket = null;
        socket.onclose = null;
        socket.close();
    }

    connect();
}

loadConfig().then(connect).catch((error) => {
    console.warn(`[YTM Bridge] Config could not be loaded: ${error?.message || error}`);
});
