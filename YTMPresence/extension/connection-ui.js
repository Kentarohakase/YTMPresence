// One interpretation for both surfaces. Only the background confirms saved settings.
function formatAge(timestamp) {
    if (!timestamp) return "-";
    const seconds = Math.max(0, Math.round((Date.now() - timestamp) / 1000));
    if (seconds < 2) return t("ageJustNow");
    if (seconds < 60) return t("ageSecondsAgo", [String(seconds)]);
    const minutes = Math.round(seconds / 60);
    if (minutes < 60) return t("ageMinutesAgo", [String(minutes)]);
    return t("ageHoursAgo", [String(Math.round(minutes / 60))]);
}

function renderConnectionStatus(status) {
    const state = !status?.tokenConfigured ? "missing" : status.connectionState;
    const labels = {
        missing: ["connTokenMissing", "err", "hintTokenMissing"],
        connecting: ["connConnecting", "warn", "hintConnecting"],
        authenticating: ["connAuthenticating", "warn", "hintAuthenticating"],
        connected: ["connConnected", "ok", status?.lastState?.title ? "hintConnected" : "hintNoTrack"],
        rejected: ["connRejected", "err", "hintRejected"],
        disconnected: ["connDisconnected", "err", "hintDisconnected"]
    };
    const [label, css, hint] = labels[state] || labels.disconnected;
    const badge = document.getElementById("stateBadge");
    badge.className = "badge " + css;
    badge.textContent = t(label);
    document.getElementById("connectionText").textContent = t(label);
    document.getElementById("subtitle").textContent = t(hint);
    document.getElementById("tokenText").textContent = t(status?.tokenConfigured ? "tokenSet" : "tokenMissingShort");
    document.getElementById("lastUpdateText").textContent = formatAge(status?.lastReceivedStateAt);
    document.getElementById("companionVersion").textContent = status?.companionVersion || t("versionNotReported");
    const urlElement = document.getElementById("activeUrlText") || document.getElementById("urlText");
    urlElement.textContent = status?.activeCompanionUrl || status?.companionUrls?.[0] || "-";
    urlElement.title = urlElement.textContent;
    const noticeKey = companionUpdateHint(status);
    const notice = document.getElementById("compatNotice");
    if (notice) {
        notice.hidden = !noticeKey;
        const noticeText = document.getElementById("compatText");
        if (noticeText) noticeText.textContent = noticeKey ? t(noticeKey) : "";
    }
}

async function renderInstallationMetadata() {
    const versionElement = document.getElementById("extensionVersion");
    const installationElement = document.getElementById("installationType");
    versionElement.textContent = t("versionNotReported");
    installationElement.textContent = t("installUnknown");
    try { versionElement.textContent = chrome.runtime.getManifest().version || t("versionNotReported"); } catch { /* informational */ }
    try {
        const info = await chrome.management.getSelf();
        const key = { development: "installLocal", normal: "installPackage", admin: "installManaged" }[info?.installType];
        installationElement.textContent = t(key || "installUnknown");
    } catch { /* Metadata failure must not prevent connecting. */ }
}

// Oldest companion this extension release was tested with. Raise it when a
// release depends on newer companion fixes.
const RECOMMENDED_COMPANION_VERSION = "1.5.1";

function parseVersion(value) {
    const match = /^(\d+)\.(\d+)\.(\d+)/.exec(String(value || "").trim());
    return match ? [Number(match[1]), Number(match[2]), Number(match[3])] : null;
}

// Returns the message key of the update hint, or "" when nothing needs saying.
// A companion that does not report its version predates 1.5.0.
function companionUpdateHint(status) {
    if (!status?.tokenConfigured || status.connectionState !== "connected") return "";
    const reported = String(status.companionVersion || "").trim();
    if (!reported) return "compatOutdatedUnknown";
    const have = parseVersion(reported);
    const need = parseVersion(RECOMMENDED_COMPANION_VERSION);
    if (!have || !need) return "";
    for (let index = 0; index < 3; index++) {
        if (have[index] !== need[index]) return have[index] < need[index] ? "compatOutdated" : "";
    }
    return "";
}

function formatTimestamp(value) {
    return Number.isFinite(value) && value > 0 ? new Date(value).toISOString() : "-";
}

async function readInstallType() {
    try {
        return (await chrome.management.getSelf())?.installType || "unknown";
    } catch {
        return "unknown";
    }
}

// Plain-text report for bug reports. It never includes the token, track titles or artists.
function buildDiagnosticsReport(status, env = {}) {
    const track = !status?.lastState?.title ? "none"
        : status.lastState.isAd ? "ad"
        : status.lastState.isPlaying ? "playing" : "paused";
    const companionUrl = status?.activeCompanionUrl || (status?.companionUrls || []).join(", ") || "-";
    return [
        "YTMPresence extension diagnostics",
        "Created: " + formatTimestamp(env.now),
        "Extension version: " + (env.extensionVersion || "unknown"),
        "Installation type: " + (env.installType || "unknown"),
        "Browser: " + (env.userAgent || "unknown"),
        "Browser language: " + (env.language || "unknown"),
        "Connection: " + (status?.connectionState || "unknown"),
        "Token configured: " + (status?.tokenConfigured ? "yes" : "no"),
        "Companion version: " + (status?.companionVersion || "not reported"),
        "Companion URL: " + companionUrl,
        "Last error: " + (status?.lastError || "-"),
        "Last connected: " + formatTimestamp(status?.lastConnectedAt),
        "Last disconnected: " + formatTimestamp(status?.lastDisconnectedAt),
        "Track state: " + track,
        "Last track update: " + formatTimestamp(status?.lastReceivedStateAt),
        "Tab selected: " + (Number.isInteger(status?.selectedTabId) ? "yes" : "no")
    ].join("\n") + "\n";
}
