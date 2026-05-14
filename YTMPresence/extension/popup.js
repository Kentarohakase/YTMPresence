const stateBadge = document.getElementById("stateBadge");
const subtitle = document.getElementById("subtitle");
const trackTitle = document.getElementById("trackTitle");
const trackMeta = document.getElementById("trackMeta");
const connectionText = document.getElementById("connectionText");
const tokenText = document.getElementById("tokenText");
const urlText = document.getElementById("urlText");
const lastUpdateText = document.getElementById("lastUpdateText");
const testButton = document.getElementById("testButton");
const optionsButton = document.getElementById("optionsButton");
const ytmButton = document.getElementById("ytmButton");
const refreshButton = document.getElementById("refreshButton");
const message = document.getElementById("message");

function showMessage(text, cls = "") {
    message.className = `message ${cls}`.trim();
    message.textContent = text || "";
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

function formatPlayback(state) {
    if (!state) return "";
    if (state.isAd) return "Werbung";
    return state.isPlaying ? "läuft" : "pausiert";
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

function renderTrack(status) {
    const state = status?.lastState;
    if (!state?.title) {
        trackTitle.textContent = "Kein Track empfangen";
        trackMeta.textContent = "YouTube Music öffnen und einen Track starten";
        return;
    }

    const playback = formatPlayback(state);
    const artist = state.artist || "YouTube Music";
    trackTitle.textContent = state.title;
    trackMeta.textContent = [artist, playback].filter(Boolean).join(" · ");
}

function renderStatus(status) {
    renderBadge(status);
    renderTrack(status);

    const companionUrl = status?.activeCompanionUrl || status?.companionUrls?.[0] || "-";
    subtitle.textContent = status?.lastStatusMessage || getConnectionLabel(status);
    connectionText.textContent = getConnectionLabel(status);
    tokenText.textContent = status?.tokenConfigured ? "gesetzt" : "fehlt";
    urlText.textContent = companionUrl;
    urlText.title = companionUrl;
    lastUpdateText.textContent = formatAge(status?.lastReceivedStateAt);

    if (status?.lastError && status.connectionState !== "connected") {
        showMessage(status.lastError, "err");
    }
}

async function refreshStatus() {
    try {
        const status = await chrome.runtime.sendMessage({ type: "YTM_GET_STATUS" });
        renderStatus(status);
    } catch (error) {
        showMessage(`Status konnte nicht geladen werden: ${error?.message || error}`, "err");
    }
}

testButton.addEventListener("click", async () => {
    testButton.disabled = true;
    showMessage("Teste Verbindung...");

    try {
        const result = await chrome.runtime.sendMessage({ type: "YTM_TEST_CONNECTION" });
        showMessage(result.message, result.ok ? "ok" : "err");
        await refreshStatus();
    } catch (error) {
        showMessage(`Verbindungstest fehlgeschlagen: ${error?.message || error}`, "err");
    } finally {
        testButton.disabled = false;
    }
});

optionsButton.addEventListener("click", () => {
    chrome.runtime.openOptionsPage();
});

ytmButton.addEventListener("click", () => {
    if (chrome.tabs?.create) {
        chrome.tabs.create({ url: "https://music.youtube.com/" });
        return;
    }

    window.open("https://music.youtube.com/", "_blank", "noopener");
});

refreshButton.addEventListener("click", () => {
    showMessage("");
    refreshStatus();
});

refreshStatus();
setInterval(() => {
    if (document.visibilityState === "visible") {
        refreshStatus();
    }
}, 2000);
