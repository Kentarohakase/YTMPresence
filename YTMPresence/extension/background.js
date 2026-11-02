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
const tabStates = new Map();
const restoringTabs = new Set();
let selectedTabId = null;

let cachedToken = "";
let configLoaded = false;
let configLoadPromise = null;
let missingTokenLogged = false;
let connectionState = "idle";
let lastStatusMessage = "";
let lastError = "";
let lastConnectedAt = 0;
let lastDisconnectedAt = 0;
let companionVersion = "";
let socketAuthenticated = false;
let activeTest = null;
let configWork = Promise.resolve();

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
    } else if (connectionState === "connecting" || connectionState === "authenticating") {
        text = "...";
        color = "#555555";
        title = `YTM Presence: ${t(connectionState === "authenticating" ? "connAuthenticating" : "connConnecting")}`;
    } else if (connectionState === "disconnected" || connectionState === "rejected") {
        text = "OFF";
        color = "#b45309";
        title = `YTM Presence: ${t(connectionState === "rejected" ? "connRejected" : "connDisconnected")}`;
    }

    try {
        chrome.action.setBadgeText({ text });
        chrome.action.setBadgeBackgroundColor({ color });
        chrome.action.setTitle({ title });
    } catch {
        // Badge ist rein diagnostisch.
    }
}

function rememberState(payload, receivedAt) {
    lastReceivedStateAt = receivedAt;
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
        companionVersion,
        lastStatusMessage,
        lastError,
        lastConnectedAt,
        lastDisconnectedAt,
        lastReceivedStateAt,
        lastSentAt,
        nextReconnectAt,
        hasPendingPayload: Boolean(pendingPayload),
        selectedTabId,
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

function queueConfigWork(work) {
    const result = configWork.then(work);
    configWork = result.catch(() => {});
    return result;
}

function applyConfig(token, url) {
    const urls = url ? [url] : [...DEFAULT_COMPANION_WS_URLS];
    if (cachedToken === token && JSON.stringify(companionWsUrls) === JSON.stringify(urls)) return;
    cachedToken = token;
    companionWsUrls = urls;
    missingTokenLogged = false;
    nextCompanionUrlIndex = 0;
    restartConnection();
}

chrome.storage.onChanged.addListener((changes, area) => {
    if (area !== "local" || (!changes.securityToken && !changes.companionUrl)) return;
    // Read the latest complete pair, not a delayed event from a previous save.
    queueConfigWork(async () => {
        await ensureConfigLoaded();
        const settings = await chrome.storage.local.get({ securityToken: "", companionUrl: "" });
        applyConfig((settings.securityToken || "").trim(), normalizeCompanionUrl(settings.companionUrl));
    }).catch(() => { lastError = t("msgSettingsLoadFailed"); });
});

async function saveAndTest(message) {
    const token = typeof message.token === "string" ? message.token.trim() : "";
    const rawUrl = typeof message.companionUrl === "string" ? message.companionUrl.trim() : "";
    const url = normalizeCompanionUrl(rawUrl);
    if (!token || (rawUrl && !url)) {
        return { saved: false, ok: false, message: t(!token ? "errTokenMissingInput" : "errInvalidUrl") };
    }
    let operation;
    try {
        operation = await queueConfigWork(async () => {
            await ensureConfigLoaded();
            await chrome.storage.local.set({ securityToken: token, companionUrl: url });
            applyConfig(token, url);
            return { test: testConnection() };
        });
    } catch {
        return { saved: false, ok: false, message: t("msgSaveFailed") };
    }
    const result = await operation.test;
    return {
        ...result, saved: true,
        message: t(result.ok ? "msgSavedConnected" : result.reason === "rejected" ? "msgSavedRejected" :
            result.reason === "changed" ? "msgSavedChanged" : "msgSavedUnreachable")
    };
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
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

    if (message.type === "YTM_SAVE_AND_TEST") {
        saveAndTest(message).then(sendResponse);
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
        if (!isYtmSender(sender) || sender.tab.id !== selectedTabId) return false;
        const ok = Boolean(message.ok);
        const command = clampText(message.command, "unknown");
        lastStatusMessage = ok
            ? t("bgCommandOk", [command])
            : t("bgCommandFailed", [command]);
        if (!ok) lastError = lastStatusMessage;
        return false;
    }

    if (message.type !== "YTM_STATE") return false;

    const payload = message.payload;
    if (!isYtmSender(sender) || !isPlaybackState(payload)) return false;

    restoringTabs.delete(sender.tab.id);
    tabStates.set(sender.tab.id, { payload, receivedAt: Date.now() });
    const previousTabId = selectedTabId;
    selectPlaybackTab();
    if (sender.tab.id === selectedTabId || previousTabId !== selectedTabId) {
        publishSelectedState();
    }

    return false;
});

function isYtmUrl(value) {
    try {
        return new URL(value).origin === "https://music.youtube.com";
    } catch {
        return false;
    }
}

function isYtmSender(sender) {
    return Number.isInteger(sender?.tab?.id) && sender.tab.id >= 0 &&
        sender.frameId === 0 && isYtmUrl(sender.url);
}

function isPlaybackState(payload) {
    return payload?.source === "ytmusic" && typeof payload.isPlaying === "boolean";
}

async function restoreTabStates() {
    // A paused page may not publish again after the background worker restarts.
    // Ask content scripts for their latest state, without executing any command.
    try {
        const tabs = await chrome.tabs.query({ url: "https://music.youtube.com/*" });
        await Promise.allSettled(tabs.map(async ({ id }) => {
            if (!Number.isInteger(id) || tabStates.has(id)) return;
            restoringTabs.add(id);
            try {
                const response = await chrome.tabs.sendMessage(id, { type: "YTM_GET_STATE" }, { frameId: 0 });
                // A live state or navigation/removal event takes precedence.
                if (restoringTabs.delete(id) && isPlaybackState(response?.payload)) {
                    tabStates.set(id, { payload: response.payload, receivedAt: Date.now() });
                }
            } finally {
                restoringTabs.delete(id);
            }
        }));
        selectPlaybackTab();
        publishSelectedState();
    } catch (error) {
        console.warn(`[YTM Bridge] Tab state restore failed: ${error?.message || error}`);
    }
}

function selectPlaybackTab() {
    const current = tabStates.get(selectedTabId);
    if (current?.payload.isPlaying) return;

    // Keep the playing source, or the paused selection when nothing plays.
    // The lowest tab ID breaks ties when the previous source has disappeared.
    const candidates = [...tabStates.keys()].sort((a, b) => a - b);
    const playingId = candidates.find((id) => tabStates.get(id).payload.isPlaying);
    const nextId = playingId ?? (current ? selectedTabId : candidates[0] ?? null);
    if (nextId === selectedTabId) return;

    selectedTabId = nextId;
    lastSentKey = "";
    pendingPayload = null;
    lastPayload = null;
    lastStatePreview = null;
    lastReceivedStateAt = 0;
}

function forgetTab(tabId) {
    restoringTabs.delete(tabId);
    if (!tabStates.delete(tabId)) return;
    const previousTabId = selectedTabId;
    selectPlaybackTab();
    if (previousTabId !== selectedTabId) publishSelectedState();
}

chrome.tabs.onRemoved.addListener(forgetTab);
chrome.tabs.onReplaced.addListener((_addedId, removedId) => forgetTab(removedId));
chrome.tabs.onUpdated.addListener((tabId, change) => {
    if (change.status === "loading" || change.discarded === true ||
        (change.url && !isYtmUrl(change.url))) {
        forgetTab(tabId);
    }
});

function publishSelectedState() {
    sendSelectedState().catch((error) => {
        console.warn(`[YTM Bridge] State send failed: ${error?.message || error}`);
    });
}

async function sendSelectedState() {
    await ensureConfigLoaded();

    // Read after awaiting configuration: the tab may have closed or changed.
    const state = tabStates.get(selectedTabId);
    if (!state) return;
    const { payload, receivedAt } = state;
    rememberState(payload, receivedAt);
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
    if (!socket || socket.readyState !== WebSocket.OPEN || !socketAuthenticated) return;

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
        disconnect(socket, "disconnected", t("testUnreachable"));
    }
}

function finishTest(result) {
    if (!activeTest) return;
    const test = activeTest;
    activeTest = null;
    clearTimeout(test.timer);
    test.resolve({ ...result, companionUrl: activeCompanionUrl });
}

function beginTest(socket, showProgress = true) {
    if (activeTest?.socket === socket) return activeTest.promise;
    let resolve;
    const promise = new Promise((done) => { resolve = done; });
    activeTest = {
        socket, promise, resolve,
        timer: setTimeout(() => {
            if (webSocket === socket) disconnect(socket, "disconnected", t("testNoResponse"));
        }, TEST_TIMEOUT_MS)
    };
    if (socket.readyState === WebSocket.OPEN) sendTest(socket, showProgress);
    return promise;
}

function getExtensionVersion() {
    // Optional for the companion's diagnostics; reading it must never block the connection.
    try {
        return chrome.runtime.getManifest().version || undefined;
    } catch {
        return undefined;
    }
}

function sendTest(socket, showProgress = true) {
    if (webSocket !== socket) return;
    if (showProgress) setConnectionState("authenticating", t("connAuthenticating"));
    try {
        // This existing protocol message never updates playback/pause/idle timestamps.
        // Older companions ignore extensionVersion.
        socket.send(JSON.stringify({ type: "YTM_TEST", token: cachedToken, extensionVersion: getExtensionVersion(), ts: Date.now() }));
    } catch {
        disconnect(socket, "disconnected", t("testUnreachable"));
    }
}

function disconnect(socket, state, message) {
    if (webSocket !== socket) return;
    finishTest({ ok: false, reason: state, message });
    webSocket = null;
    socketAuthenticated = false;
    companionVersion = "";
    stopKeepAlive();
    try { socket.close(); } catch { /* best effort */ }
    lastDisconnectedAt = Date.now();
    lastError = message;
    setConnectionState(state, message);
    if (state !== "rejected") {
        nextCompanionUrlIndex = (nextCompanionUrlIndex + 1) % companionWsUrls.length;
        scheduleReconnect();
    }
}

function connect() {
    if (!cachedToken) {
        setConnectionState("idle", t("bgTokenMissing"));
        return null;
    }
    if (webSocket) return webSocket;
    if (reconnectTimer || connectionState === "rejected") return null;

    try {
        activeCompanionUrl = companionWsUrls[nextCompanionUrlIndex];
        setConnectionState("connecting", t("bgConnecting", [activeCompanionUrl]));
        const socket = new WebSocket(activeCompanionUrl);
        webSocket = socket;
        socketAuthenticated = false;
        beginTest(socket);

        socket.onopen = () => {
            if (webSocket === socket) sendTest(socket);
        };
        socket.onclose = () => disconnect(socket, "disconnected", t("testUnreachable"));
        socket.onerror = () => disconnect(socket, "disconnected", t("testUnreachable"));
        socket.onmessage = (event) => {
            if (webSocket !== socket) return;
            handleCompanionMessage(socket, event.data).catch(() => {
                if (webSocket === socket) lastError = t("msgTestFailed");
            });
        };
        return socket;
    } catch {
        lastError = t("testWsCreateFailed");
        setConnectionState("disconnected", lastError);
        nextCompanionUrlIndex = (nextCompanionUrlIndex + 1) % companionWsUrls.length;
        scheduleReconnect();
        return null;
    }
}

function startKeepAlive(socket) {
    stopKeepAlive();
    keepAliveTimer = setInterval(() => {
        if (webSocket === socket && socket.readyState === WebSocket.OPEN) beginTest(socket, false);
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
    finishTest({ ok: false, reason: "changed", message: t("msgConfigChanged") });
    if (reconnectTimer) clearTimeout(reconnectTimer);
    reconnectTimer = null;
    reconnectDelayMs = 500;
    nextReconnectAt = 0;
    stopKeepAlive();
    const socket = webSocket;
    webSocket = null;
    socketAuthenticated = false;
    companionVersion = "";
    lastError = "";
    lastSentKey = "";
    pendingPayload = null;
    try { socket?.close(); } catch { /* best effort */ }
    setConnectionState("idle");
    connect();
}

async function handleCompanionMessage(socket, data) {
    let message;
    try { message = JSON.parse(data); } catch { return; }

    if (message?.type === "YTM_TEST_RESULT") {
        if (activeTest?.socket !== socket || typeof message.ok !== "boolean") return;
        if (!message.ok) {
            disconnect(socket, "rejected", t("testBadToken"));
            return;
        }
        const firstAuthentication = !socketAuthenticated;
        socketAuthenticated = true;
        companionVersion = clampText(message.companionVersion);
        reconnectDelayMs = 500;
        nextReconnectAt = 0;
        lastConnectedAt = Date.now();
        lastError = "";
        setConnectionState("connected", t("testOk"));
        finishTest({ ok: true, message: t("testOk") });
        if (firstAuthentication) {
            // Replay the current selection, including a paused track, after reconnect.
            // Test/keepalive replies on an already authenticated socket do not replay it.
            lastSentKey = "";
            pendingPayload = tabStates.get(selectedTabId)?.payload || null;
            flushPendingState();
            if (webSocket === socket) startKeepAlive(socket);
        }
        return;
    }
    if (!socketAuthenticated || message?.type !== "YTM_COMMAND") return;
    const command = clampText(message.command);
    if (!isAllowedCommand(command)) return;
    const result = await forwardCommandToSelectedTab(command);
    if (webSocket !== socket) return;
    lastStatusMessage = result.ok ? t("bgCommandSent", [command]) : result.message;
    if (!result.ok) lastError = result.message;
}

function isAllowedCommand(command) {
    return command === "play-pause" || command === "next" || command === "previous";
}

async function forwardCommandToSelectedTab(command) {
    const tabId = selectedTabId;
    if (tabId === null) {
        return {
            ok: false,
            message: t("bgNoYtmTab")
        };
    }

    try {
        const response = await chrome.tabs.sendMessage(tabId, {
            type: "YTM_COMMAND",
            command,
            ts: Date.now()
        }, { frameId: 0 });

        if (response?.ok) return { ok: true, message: t("bgCommandSent", [command]) };
    } catch {
        // Never replay a potentially executed command on a different tab.
    }

    return { ok: false, message: t("bgCommandRejected") };
}

function testConnection() {
    if (!cachedToken) return Promise.resolve({ ok: false, message: t("errTokenMissingInput") });
    // A retry uses the same background connection, never a separate probe socket.
    if (!webSocket) restartConnection();
    const socket = webSocket;
    if (!socket) return Promise.resolve({ ok: false, message: t("testUnreachable") });
    return beginTest(socket);
}

ensureConfigLoaded().then(async () => {
    connect();
    await restoreTabStates();
}).catch((error) => {
    console.warn(`[YTM Bridge] Config could not be loaded: ${error?.message || error}`);
});
