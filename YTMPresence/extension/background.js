const DEFAULT_COMPANION_WS_URLS = [
    "ws://127.0.0.1:17373/ws",
    "ws://localhost:17373/ws"
];
const MIN_SEND_INTERVAL_MS = 1500;
const TEST_TIMEOUT_MS = 4000;

let companionWsUrls = [...DEFAULT_COMPANION_WS_URLS];
let webSocket = null;
let reconnectTimer = null;
let reconnectDelayMs = 500;
let nextReconnectAt = 0;
let nextCompanionUrlIndex = 0;
let activeCompanionUrl = "";

let lastSentKey = "";
let lastSentAt = 0;
let pendingPayload = null;
let lastReceivedStateAt = 0;
let lastStatePreview = null;

let cachedToken = "";
let configLoaded = false;
let missingTokenLogged = false;
let connectionState = "idle";
let lastStatusMessage = "";
let lastError = "";
let lastConnectedAt = 0;
let lastDisconnectedAt = 0;

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

function clampText(value, fallback = "") {
    if (typeof value !== "string") return fallback;
    const trimmed = value.trim();
    return trimmed.length > 256 ? trimmed.slice(0, 256) : trimmed;
}

function setConnectionState(state, message = "") {
    connectionState = state;
    if (message) lastStatusMessage = message;
    updateBadge();
}

function updateBadge() {
    if (!chrome.action) return;

    let text = "";
    let color = "#6b7280";
    let title = "YTM Presence";

    if (!cachedToken) {
        text = "!";
        color = "#b00020";
        title = "YTM Presence: Token fehlt";
    } else if (connectionState === "connected") {
        text = "OK";
        color = "#0a7a2f";
        title = "YTM Presence: verbunden";
    } else if (connectionState === "connecting") {
        text = "...";
        color = "#555555";
        title = "YTM Presence: verbindet";
    } else if (connectionState === "disconnected") {
        text = "OFF";
        color = "#b45309";
        title = "YTM Presence: Companion getrennt";
    }

    try {
        chrome.action.setBadgeText({ text });
        chrome.action.setBadgeBackgroundColor({ color });
        chrome.action.setTitle({ title });
    } catch {
        // Badge ist rein diagnostisch.
    }
}

function rememberState(payload) {
    lastReceivedStateAt = Date.now();
    lastStatePreview = {
        title: clampText(payload?.title),
        artist: clampText(payload?.artist),
        album: clampText(payload?.album),
        isPlaying: Boolean(payload?.isPlaying),
        isAd: Boolean(payload?.isAd),
        mode: clampText(payload?.mode, "unknown"),
        shareUrl: clampText(payload?.shareUrl),
        url: clampText(payload?.url),
        position: Number.isFinite(payload?.position) ? payload.position : null,
        duration: Number.isFinite(payload?.duration) ? payload.duration : null
    };
    updateBadge();
}

function getRuntimeStatus() {
    return {
        configLoaded,
        tokenConfigured: Boolean(cachedToken),
        companionUrls: [...companionWsUrls],
        activeCompanionUrl,
        connectionState,
        lastStatusMessage,
        lastError,
        lastConnectedAt,
        lastDisconnectedAt,
        lastReceivedStateAt,
        lastSentAt,
        nextReconnectAt,
        hasPendingPayload: Boolean(pendingPayload),
        lastState: lastStatePreview
    };
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

    setConnectionState(
        cachedToken ? connectionState : "idle",
        cachedToken ? lastStatusMessage : "Token fehlt."
    );
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
        lastSentKey = "";
        shouldRestart = true;
        console.info("[YTM Bridge] Token updated.");
    }

    if (changes.companionUrl) {
        const customUrl = normalizeCompanionUrl(changes.companionUrl.newValue);
        companionWsUrls = customUrl ? [customUrl] : [...DEFAULT_COMPANION_WS_URLS];
        nextCompanionUrlIndex = 0;
        lastSentKey = "";
        shouldRestart = true;
        console.info(`[YTM Bridge] Companion URL updated: ${companionWsUrls.join(", ")}`);
    }

    if (shouldRestart) {
        restartConnection();
    }
});

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
    if (!message || !message.type) return false;

    if (message.type === "YTM_GET_STATUS") {
        ensureConfigLoaded()
            .then(() => sendResponse(getRuntimeStatus()))
            .catch((error) => {
                sendResponse({
                    ...getRuntimeStatus(),
                    lastError: `Status konnte nicht geladen werden: ${error?.message || error}`
                });
            });
        return true;
    }

    if (message.type === "YTM_TEST_CONNECTION") {
        ensureConfigLoaded()
            .then(() => testConnection())
            .then((result) => sendResponse(result))
            .catch((error) => {
                sendResponse({
                    ok: false,
                    message: `Verbindungstest fehlgeschlagen: ${error?.message || error}`
                });
            });
        return true;
    }

    if (message.type !== "YTM_STATE") return false;

    sendState(message.payload).catch((error) => {
        console.warn(`[YTM Bridge] State send failed: ${error?.message || error}`);
    });

    return false;
});

async function sendState(payload) {
    await ensureConfigLoaded();

    if (!payload || typeof payload !== "object") return;
    rememberState(payload);

    if (!cachedToken) {
        if (!missingTokenLogged) {
            missingTokenLogged = true;
            console.warn("[YTM Bridge] Dropping state because no token is configured.");
        }
        return;
    }

    pendingPayload = payload;
    flushPendingState();
}

function buildPayloadKey(payload) {
    return [
        payload.isPlaying,
        payload.title,
        payload.artist,
        payload.album,
        payload.albumArtUrl,
        payload.url,
        payload.shareUrl,
        payload.mode,
        payload.isAd
    ].join("|");
}

function flushPendingState() {
    if (!pendingPayload || !cachedToken) return;

    const socket = connect();
    if (!socket || socket.readyState !== WebSocket.OPEN) return;

    const now = Date.now();
    const payload = pendingPayload;
    const key = buildPayloadKey(payload);
    if (key === lastSentKey && (now - lastSentAt) < MIN_SEND_INTERVAL_MS) return;

    try {
        socket.send(JSON.stringify({ ...payload, token: cachedToken }));
        pendingPayload = null;
        lastSentKey = key;
        lastSentAt = now;
    } catch (error) {
        console.warn(`[YTM Bridge] WebSocket send failed: ${error?.message || error}`);
        webSocket = null;
        scheduleReconnect();
    }
}

function connect() {
    if (!cachedToken) {
        setConnectionState("idle", "Token fehlt.");
        return null;
    }

    if (webSocket) {
        if (webSocket.readyState === WebSocket.OPEN || webSocket.readyState === WebSocket.CONNECTING) {
            return webSocket;
        }

        webSocket = null;
    }

    try {
        activeCompanionUrl = companionWsUrls[nextCompanionUrlIndex];
        setConnectionState("connecting", `Verbinde mit ${activeCompanionUrl}.`);
        const socket = new WebSocket(activeCompanionUrl);
        webSocket = socket;

        socket.onopen = () => {
            reconnectDelayMs = 500;
            nextReconnectAt = 0;
            lastConnectedAt = Date.now();
            lastError = "";
            setConnectionState("connected", `Verbunden mit ${activeCompanionUrl}.`);
            console.info(`[YTM Bridge] Connected to ${activeCompanionUrl}.`);
            flushPendingState();
        };

        socket.onclose = (event) => {
            if (webSocket !== socket) return;

            console.warn(
                `[YTM Bridge] Companion disconnected (${activeCompanionUrl}). ` +
                `Code: ${event.code || "?"}, reason: ${event.reason || "n/a"}`
            );

            webSocket = null;
            lastDisconnectedAt = Date.now();
            lastError = `Getrennt (${event.code || "?"}): ${event.reason || "n/a"}`;
            setConnectionState("disconnected", `Getrennt von ${activeCompanionUrl}.`);
            nextCompanionUrlIndex = (nextCompanionUrlIndex + 1) % companionWsUrls.length;
            scheduleReconnect();
        };

        socket.onerror = () => {
            lastError = `WebSocket-Fehler bei ${activeCompanionUrl}.`;
            console.warn(`[YTM Bridge] WebSocket error at ${activeCompanionUrl}.`);
        };

        return socket;
    } catch (error) {
        lastError = `WebSocket konnte nicht erstellt werden: ${error?.message || error}`;
        setConnectionState("disconnected", lastError);
        console.warn(`[YTM Bridge] ${lastError}`);
        webSocket = null;
        nextCompanionUrlIndex = (nextCompanionUrlIndex + 1) % companionWsUrls.length;
        scheduleReconnect();
        return null;
    }
}

function scheduleReconnect() {
    if (!cachedToken || reconnectTimer) return;

    nextReconnectAt = Date.now() + reconnectDelayMs;
    updateBadge();

    reconnectTimer = setTimeout(() => {
        reconnectTimer = null;
        nextReconnectAt = 0;
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
    nextReconnectAt = 0;

    if (webSocket) {
        const socket = webSocket;
        webSocket = null;
        socket.onclose = null;
        socket.close();
    }

    connect();
}

function testConnection() {
    const companionUrl = companionWsUrls[0] || DEFAULT_COMPANION_WS_URLS[0];
    const token = cachedToken;

    return new Promise((resolve) => {
        if (!token) {
            resolve({
                ok: false,
                message: "Token fehlt. Öffne die Optionen und füge den Tray-Token ein."
            });
            return;
        }

        let settled = false;
        let socket = null;

        const finish = (result) => {
            if (settled) return;
            settled = true;
            clearTimeout(timeoutId);

            try {
                if (socket && socket.readyState === WebSocket.OPEN) {
                    socket.close(1000, "test finished");
                }
            } catch {
                // ignore
            }

            resolve({
                ...result,
                companionUrl
            });
        };

        const timeoutId = setTimeout(() => {
            finish({
                ok: false,
                message: "Keine Antwort vom Companion. Läuft die Tray-App?"
            });
        }, TEST_TIMEOUT_MS);

        try {
            socket = new WebSocket(companionUrl);
        } catch {
            finish({
                ok: false,
                message: "WebSocket konnte nicht erstellt werden."
            });
            return;
        }

        socket.onopen = () => {
            socket.send(JSON.stringify({
                type: "YTM_TEST",
                token,
                ts: Date.now()
            }));
        };

        socket.onmessage = (event) => {
            let response;
            try {
                response = JSON.parse(event.data);
            } catch {
                finish({
                    ok: false,
                    message: "Unerwartete Antwort vom Companion."
                });
                return;
            }

            if (response?.type !== "YTM_TEST_RESULT") {
                finish({
                    ok: false,
                    message: "Der Companion hat den Test nicht erkannt."
                });
                return;
            }

            finish({
                ok: Boolean(response.ok),
                message: response.ok
                    ? "Verbindung OK. Token und Companion URL passen."
                    : "Companion erreichbar, aber der Token ist falsch."
            });
        };

        socket.onerror = () => {
            finish({
                ok: false,
                message: "Companion nicht erreichbar. Starte die Tray-App oder prüfe die URL."
            });
        };

        socket.onclose = () => {
            finish({
                ok: false,
                message: "Verbindung wurde geschlossen, bevor der Test beantwortet wurde."
            });
        };
    });
}

loadConfig().then(connect).catch((error) => {
    console.warn(`[YTM Bridge] Config could not be loaded: ${error?.message || error}`);
});
