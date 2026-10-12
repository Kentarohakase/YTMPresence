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
