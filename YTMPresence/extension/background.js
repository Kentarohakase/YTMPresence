const DEFAULT_COMPANION_WS_URLS = [
    "ws://127.0.0.1:17373/ws",
    "ws://localhost:17373/ws"
];
const MIN_SEND_INTERVAL_MS = 1500;
const TEST_TIMEOUT_MS = 4000;
const KEEPALIVE_INTERVAL_MS = 20_000;

let companionWsUrls = [...DEFAULT_COMPANION_WS_URLS];
let webSocket = null;
let reconnectTimer = null;
let keepAliveTimer = null;
let reconnectDelayMs = 500;
let nextReconnectAt = 0;
let nextCompanionUrlIndex = 0;
let activeCompanionUrl = "";

let lastSentKey = "";
let lastSentAt = 0;
let pendingPayload = null;
let lastPayload = null;
let lastReceivedStateAt = 0;
let lastStatePreview = null;

let cachedToken = "";
let configLoaded = false;
let configLoadPromise = null;
let missingTokenLogged = false;
let connectionState = "idle";
let lastStatusMessage = "";
let lastError = "";
let lastConnectedAt = 0;
let lastDisconnectedAt = 0;

function t(key, subs) {
    const msg = chrome.i18n.getMessage(key, subs);
    return msg || key;
}

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
        title = `YTM Presence: ${t("connTokenMissing")}`;
    } else if (connectionState === "connected") {
        text = "OK";
        color = "#0a7a2f";
        title = `YTM Presence: ${t("connConnected")}`;
    } else if (connectionState === "connecting") {
        text = "...";
        color = "#555555";
        title = `YTM Presence: ${t("connConnecting")}`;
    } else if (connectionState === "disconnected") {
        text = "OFF";
        color = "#b45309";
        title = `YTM Presence: ${t("connDisconnected")}`;
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
        cachedToken ? lastStatusMessage : t("bgTokenMissing")
    );
}

async function ensureConfigLoaded() {
    if (configLoaded) return;

    configLoadPromise ||= loadConfig();
    try {
        await configLoadPromise;
    } finally {
        configLoadPromise = null;
    }
}

function applyStorageChanges(changes) {
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
        if (lastPayload) pendingPayload = lastPayload;
        restartConnection();
    }
}

chrome.storage.onChanged.addListener((changes, area) => {
    if (area !== "local") return;

    ensureConfigLoaded()
        .then(() => applyStorageChanges(changes))
        .catch((error) => {
            lastError = `${t("msgSettingsLoadFailed")} ${error?.message || error}`;
            updateBadge();
        });
});

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
    if (!message || !message.type) return false;

    if (message.type === "YTM_GET_STATUS") {
        ensureConfigLoaded()
            .then(() => sendResponse(getRuntimeStatus()))
            .catch((error) => {
                sendResponse({
                    ...getRuntimeStatus(),
                    lastError: `${t("msgStatusLoadFailed")} ${error?.message || error}`
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
                    message: `${t("msgTestFailed")} ${error?.message || error}`
                });
            });
        return true;
    }

    if (message.type === "YTM_COMMAND_RESULT") {
        const ok = Boolean(message.ok);
        const command = clampText(message.command, "unknown");
        lastStatusMessage = ok
            ? t("bgCommandOk", [command])
            : t("bgCommandFailed", [command]);
        if (!ok) lastError = lastStatusMessage;
        return false;
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
    lastPayload = payload;

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
        setConnectionState("idle", t("bgTokenMissing"));
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
        setConnectionState("connecting", t("bgConnecting", [activeCompanionUrl]));
        const socket = new WebSocket(activeCompanionUrl);
        webSocket = socket;

        socket.onopen = () => {
            reconnectDelayMs = 500;
            nextReconnectAt = 0;
            lastConnectedAt = Date.now();
            lastError = "";
            setConnectionState("connected", t("bgConnected", [activeCompanionUrl]));
            console.info(`[YTM Bridge] Connected to ${activeCompanionUrl}.`);
            flushPendingState();
            if (webSocket === socket) startKeepAlive(socket);
        };

        socket.onclose = (event) => {
            if (webSocket !== socket) return;

            console.warn(
                `[YTM Bridge] Companion disconnected (${activeCompanionUrl}). ` +
                `Code: ${event.code || "?"}, reason: ${event.reason || "n/a"}`
            );

            webSocket = null;
            stopKeepAlive();
            lastDisconnectedAt = Date.now();
            lastError = t("bgDisconnectReason", [String(event.code || "?"), event.reason || "n/a"]);
            setConnectionState("disconnected", t("bgDisconnectedFrom", [activeCompanionUrl]));
            nextCompanionUrlIndex = (nextCompanionUrlIndex + 1) % companionWsUrls.length;
            scheduleReconnect();
        };

        socket.onerror = () => {
            lastError = t("bgWsError", [activeCompanionUrl]);
            console.warn(`[YTM Bridge] WebSocket error at ${activeCompanionUrl}.`);
        };

        socket.onmessage = (event) => {
            handleCompanionMessage(event.data).catch((error) => {
                lastError = `Companion message failed: ${error?.message || error}`;
                console.warn(`[YTM Bridge] ${lastError}`);
            });
        };

        return socket;
    } catch (error) {
        lastError = `${t("bgWsCreateFailed")} ${error?.message || error}`;
        setConnectionState("disconnected", lastError);
        console.warn(`[YTM Bridge] ${lastError}`);
        webSocket = null;
        stopKeepAlive();
        nextCompanionUrlIndex = (nextCompanionUrlIndex + 1) % companionWsUrls.length;
        scheduleReconnect();
        return null;
    }
}

function startKeepAlive(socket) {
    stopKeepAlive();

    keepAliveTimer = setInterval(() => {
        if (webSocket !== socket || socket.readyState !== WebSocket.OPEN) {
            stopKeepAlive();
            return;
        }

        if (!cachedToken) return;

        try {
            // YTM_TEST gehört bereits zum bestehenden Protokoll und verändert
            // weder Track- noch Pause-/Idle-Zustand im Companion.
            socket.send(JSON.stringify({
                type: "YTM_TEST",
                token: cachedToken,
                ts: Date.now()
            }));
        } catch (error) {
            lastError = `${t("bgWsError", [activeCompanionUrl])} ${error?.message || error}`;
            webSocket = null;
            stopKeepAlive();
            try { socket.close(); } catch { /* best effort */ }
            setConnectionState("disconnected", lastError);
            scheduleReconnect();
        }
    }, KEEPALIVE_INTERVAL_MS);
}

function stopKeepAlive() {
    if (!keepAliveTimer) return;
    clearInterval(keepAliveTimer);
    keepAliveTimer = null;
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
    stopKeepAlive();

    if (webSocket) {
        const socket = webSocket;
        webSocket = null;
        socket.onclose = null;
        socket.close();
    }

    connect();
}

async function handleCompanionMessage(data) {
    let message;
    try {
        message = JSON.parse(data);
    } catch {
        return;
    }

    if (message?.type !== "YTM_COMMAND") return;

    const command = clampText(message.command);
    if (!isAllowedCommand(command)) return;

    const result = await forwardCommandToYtmTabs(command);
    lastStatusMessage = result.ok
        ? t("bgCommandSent", [command])
        : result.message;

    if (!result.ok) {
        lastError = result.message;
    }
}

function isAllowedCommand(command) {
    return command === "play-pause" || command === "next" || command === "previous";
}

async function forwardCommandToYtmTabs(command) {
    const tabs = await chrome.tabs.query({ url: "https://music.youtube.com/*" });
    if (!tabs.length) {
        return {
            ok: false,
            message: t("bgNoYtmTab")
        };
    }

    let delivered = 0;

    await Promise.allSettled(tabs.map(async (tab) => {
        if (!tab.id) return;

        const response = await chrome.tabs.sendMessage(tab.id, {
            type: "YTM_COMMAND",
            command,
            ts: Date.now()
        });

        if (response?.ok) delivered++;
    }));

    return delivered > 0
        ? { ok: true, message: t("bgCommandSent", [command]) }
        : { ok: false, message: t("bgCommandRejected") };
}

function testConnection() {
    const companionUrl = companionWsUrls[0] || DEFAULT_COMPANION_WS_URLS[0];
    const token = cachedToken;

    return new Promise((resolve) => {
        if (!token) {
            resolve({
                ok: false,
                message: t("errTokenMissingInput")
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
                message: t("testNoResponse")
            });
        }, TEST_TIMEOUT_MS);

        try {
            socket = new WebSocket(companionUrl);
        } catch {
            finish({
                ok: false,
                message: t("bgWsCreateFailed")
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
                    message: t("testUnexpected")
                });
                return;
            }

            if (response?.type !== "YTM_TEST_RESULT") {
                finish({
                    ok: false,
                    message: t("testNotRecognized")
                });
                return;
            }

            finish({
                ok: Boolean(response.ok),
                message: response.ok
                    ? t("testOk")
                    : t("testBadToken")
            });
        };

        socket.onerror = () => {
            finish({
                ok: false,
                message: t("testUnreachable")
            });
        };

        socket.onclose = () => {
            finish({
                ok: false,
                message: t("testClosedEarly")
            });
        };
    });
}

ensureConfigLoaded().then(connect).catch((error) => {
    console.warn(`[YTM Bridge] Config could not be loaded: ${error?.message || error}`);
});
