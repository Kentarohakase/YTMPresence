const tokenInput = document.getElementById("token");
const companionUrlInput = document.getElementById("companionUrl");
const saveBtn = document.getElementById("save");
const testBtn = document.getElementById("test");
const resetUrlBtn = document.getElementById("resetUrl");
const refreshBtn = document.getElementById("refresh");
const openYtmBtn = document.getElementById("openYtm");
const msg = document.getElementById("msg");
const stateBadge = document.getElementById("stateBadge");
const subtitle = document.getElementById("subtitle");
const connectionText = document.getElementById("connectionText");
const tokenText = document.getElementById("tokenText");
const activeUrlText = document.getElementById("activeUrlText");
const lastTrackText = document.getElementById("lastTrackText");
const lastUpdateText = document.getElementById("lastUpdateText");

const DEFAULT_COMPANION_URL = "ws://127.0.0.1:17373/ws";
const TEST_TIMEOUT_MS = 4000;

function show(text, cls = "") {
    msg.className = `message ${cls}`.trim();
    msg.textContent = text || "";
}

function setBusy(isBusy) {
    saveBtn.disabled = isBusy;
    testBtn.disabled = isBusy;
    resetUrlBtn.disabled = isBusy;
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

function formatAge(timestamp) {
    if (!timestamp) return "-";

    const seconds = Math.max(0, Math.round((Date.now() - timestamp) / 1000));
    if (seconds < 2) return "gerade eben";
    if (seconds < 60) return `vor ${seconds}s`;

    const minutes = Math.round(seconds / 60);
    if (minutes < 60) return `vor ${minutes}m`;

    const hours = Math.round(minutes / 60);
    return `vor ${hours}h`;
}

function getConnectionLabel(status) {
    if (!status?.tokenConfigured) return "Token fehlt";

    switch (status.connectionState) {
        case "connected":
            return "Verbunden";
        case "connecting":
            return "Verbindet";
        case "disconnected":
            return "Getrennt";
        default:
            return "Bereit";
    }
}

function renderBadge(status) {
    stateBadge.className = "badge";

    if (!status?.tokenConfigured) {
        stateBadge.textContent = "Setup";
        stateBadge.classList.add("err");
        return;
    }

    if (status.connectionState === "connected") {
        stateBadge.textContent = "Online";
        stateBadge.classList.add("ok");
        return;
    }

    if (status.connectionState === "connecting") {
        stateBadge.textContent = "Verbinden";
        stateBadge.classList.add("warn");
        return;
    }

    stateBadge.textContent = "Offline";
    stateBadge.classList.add(status.lastReceivedStateAt ? "warn" : "err");
}

function renderStatus(status) {
    renderBadge(status);

    const state = status?.lastState;
    const activeUrl = status?.activeCompanionUrl || status?.companionUrls?.[0] || "-";
    const track = state?.title
        ? `${state.title}${state.artist ? ` - ${state.artist}` : ""}`
        : "-";

    subtitle.textContent = status?.lastStatusMessage || getConnectionLabel(status);
    connectionText.textContent = getConnectionLabel(status);
    tokenText.textContent = status?.tokenConfigured ? "gesetzt" : "fehlt";
    activeUrlText.textContent = activeUrl;
    activeUrlText.title = activeUrl;
    lastTrackText.textContent = track;
    lastTrackText.title = track;
    lastUpdateText.textContent = formatAge(status?.lastReceivedStateAt);

    if (status?.lastError && status.connectionState !== "connected") {
        show(status.lastError, "err");
    }
}

async function refreshStatus() {
    try {
        const status = await chrome.runtime.sendMessage({ type: "YTM_GET_STATUS" });
        renderStatus(status);
    } catch (error) {
        show(`Status konnte nicht geladen werden: ${error?.message || error}`, "err");
    }
}

async function load() {
    const result = await chrome.storage.local.get({
        securityToken: "",
        companionUrl: ""
    });

    tokenInput.value = result.securityToken || "";
    companionUrlInput.value = result.companionUrl || DEFAULT_COMPANION_URL;
    await refreshStatus();
}

function getCurrentSettings() {
    const token = (tokenInput.value || "").trim();
    const companionUrl = normalizeCompanionUrl(companionUrlInput.value);

    if (!token) {
        return { error: "Token fehlt. Kopiere ihn aus dem Tray-Menue und fuege ihn hier ein." };
    }

    if (!companionUrl) {
        return { error: "Ungueltige Companion URL. Erlaubt sind localhost oder 127.0.0.1." };
    }

    return { token, companionUrl };
}

function testConnection(companionUrl, token) {
    return new Promise((resolve) => {
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

            resolve(result);
        };

        const timeoutId = setTimeout(() => {
            finish({
                ok: false,
                message: "Keine Antwort vom Companion. Laeuft die Tray-App und ist die URL korrekt?"
            });
        }, TEST_TIMEOUT_MS);

        try {
            socket = new WebSocket(companionUrl);
        } catch {
            finish({
                ok: false,
                message: "WebSocket konnte nicht erstellt werden. Pruefe die Companion URL."
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
                    message: "Der Companion hat den Verbindungstest nicht erkannt."
                });
                return;
            }

            finish(response.ok
                ? { ok: true, message: "Verbindung OK. Token und Companion URL passen." }
                : { ok: false, message: "Companion erreichbar, aber der Token ist falsch." });
        };

        socket.onerror = () => {
            finish({
                ok: false,
                message: "Companion nicht erreichbar. Starte die Tray-App oder pruefe die URL."
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

saveBtn.addEventListener("click", async () => {
    const settings = getCurrentSettings();

    if (settings.error) {
        show(settings.error, "err");
        return;
    }

    setBusy(true);

    try {
        await chrome.storage.local.set({
            securityToken: settings.token,
            companionUrl: settings.companionUrl
        });

        companionUrlInput.value = settings.companionUrl;
        show("Gespeichert. YouTube Music Tab neu laden, falls er schon offen war.", "ok");
        await refreshStatus();
    } finally {
        setBusy(false);
    }
});

testBtn.addEventListener("click", async () => {
    const settings = getCurrentSettings();

    if (settings.error) {
        show(settings.error, "err");
        return;
    }

    setBusy(true);
    show("Teste Verbindung...");

    try {
        const result = await testConnection(settings.companionUrl, settings.token);
        companionUrlInput.value = settings.companionUrl;
        show(result.message, result.ok ? "ok" : "err");
        await refreshStatus();
    } finally {
        setBusy(false);
    }
});

resetUrlBtn.addEventListener("click", () => {
    companionUrlInput.value = DEFAULT_COMPANION_URL;
    show("");
});

refreshBtn.addEventListener("click", () => {
    show("");
    refreshStatus();
});

openYtmBtn.addEventListener("click", () => {
    if (chrome.tabs?.create) {
        chrome.tabs.create({ url: "https://music.youtube.com/" });
        return;
    }

    window.open("https://music.youtube.com/", "_blank", "noopener");
});

load().catch(() => show("Konnte Einstellungen nicht laden.", "err"));
setInterval(() => {
    if (document.visibilityState === "visible") {
        refreshStatus();
    }
}, 3000);
