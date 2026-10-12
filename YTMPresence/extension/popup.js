const testButton = document.getElementById("testButton");
const message = document.getElementById("message");
function showMessage(text, cls = "") {
    message.className = ("message " + cls).trim();
    message.textContent = text || "";
}
function renderStatus(status) {
    renderConnectionStatus(status);
    const state = status?.lastState;
    document.getElementById("trackTitle").textContent = state?.title || t("trackNone");
    document.getElementById("trackMeta").textContent = state?.title
        ? [state.artist || "YouTube Music", t(state.isAd ? "playbackAd" : state.isPlaying ? "playbackPlaying" : "playbackPaused")].join(" - ")
        : t("trackNoneHint");
}
async function refreshStatus() {
    try {
        renderStatus(await chrome.runtime.sendMessage({ type: "YTM_GET_STATUS" }));
    } catch {
        showMessage(t("msgStatusLoadFailed"), "err");
    }
}
testButton.addEventListener("click", async () => {
    testButton.disabled = true;
    showMessage(t("msgTesting"));
    try {
        const result = await chrome.runtime.sendMessage({ type: "YTM_TEST_CONNECTION" });
        showMessage(result.message, result.ok ? "ok" : "err");
        await refreshStatus();
    } catch {
        showMessage(t("msgTestFailed"), "err");
    } finally {
        testButton.disabled = false;
    }
});
document.getElementById("optionsButton").addEventListener("click", () => chrome.runtime.openOptionsPage());
document.getElementById("ytmButton").addEventListener("click", () => chrome.tabs.create({ url: "https://music.youtube.com/" }));
document.getElementById("refreshButton").addEventListener("click", refreshStatus);
localizeDom();
renderInstallationMetadata();
refreshStatus();
setInterval(() => {
    if (document.visibilityState === "visible") refreshStatus();
}, 2000);
