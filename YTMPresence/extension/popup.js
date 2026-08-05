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
    if (seconds < 2) return t("ageJustNow");
    if (seconds < 60) return t("ageSecondsAgo", [String(seconds)]);

    const minutes = Math.round(seconds / 60);
    if (minutes < 60) return t("ageMinutesAgo", [String(minutes)]);

    const hours = Math.round(minutes / 60);
    return t("ageHoursAgo", [String(hours)]);
}

function formatPlayback(state) {
    if (!state) return "";
    if (state.isAd) return t("playbackAd");
    return state.isPlaying ? t("playbackPlaying") : t("playbackPaused");
}

function getConnectionLabel(status) {
    if (!status?.tokenConfigured) return t("connTokenMissing");

    switch (status.connectionState) {
        case "connected":
            return t("connConnected");
        case "connecting":
            return t("connConnecting");
        case "disconnected":
            return t("connDisconnected");
        default:
            return t("connReady");
    }
}

function renderBadge(status) {
    stateBadge.className = "badge";

    if (!status?.tokenConfigured) {
        stateBadge.textContent = t("badgeSetup");
        stateBadge.classList.add("err");
        return;
    }

    if (status.connectionState === "connected") {
        stateBadge.textContent = t("badgeOnline");
        stateBadge.classList.add("ok");
        return;
    }

    if (status.connectionState === "connecting") {
        stateBadge.textContent = t("badgeConnecting");
        stateBadge.classList.add("warn");
        return;
    }

    stateBadge.textContent = t("badgeOffline");
    stateBadge.classList.add(status.lastReceivedStateAt ? "warn" : "err");
}

function renderTrack(status) {
    const state = status?.lastState;
    if (!state?.title) {
        trackTitle.textContent = t("trackNone");
        trackMeta.textContent = t("trackNoneHint");
        return;
    }

    const playback = formatPlayback(state);
    const artist = state.artist || "YouTube Music";
    trackTitle.textContent = state.title;
    trackMeta.textContent = [artist, playback].filter(Boolean).join(" - ");
}

function renderStatus(status) {
    renderBadge(status);
    renderTrack(status);

    const companionUrl = status?.activeCompanionUrl || status?.companionUrls?.[0] || "-";
    subtitle.textContent = status?.lastStatusMessage || getConnectionLabel(status);
    connectionText.textContent = getConnectionLabel(status);
    tokenText.textContent = status?.tokenConfigured ? t("tokenSet") : t("tokenMissingShort");
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
        showMessage(`${t("msgStatusLoadFailed")} ${error?.message || error}`, "err");
    }
}

testButton.addEventListener("click", async () => {
    testButton.disabled = true;
    showMessage(t("msgTesting"));

    try {
        const result = await chrome.runtime.sendMessage({ type: "YTM_TEST_CONNECTION" });
        showMessage(result.message, result.ok ? "ok" : "err");
        await refreshStatus();
    } catch (error) {
        showMessage(`${t("msgTestFailed")} ${error?.message || error}`, "err");
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

localizeDom();
refreshStatus();
setInterval(() => {
    if (document.visibilityState === "visible") {
        refreshStatus();
    }
}, 2000);
