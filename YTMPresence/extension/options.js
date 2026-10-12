const tokenInput = document.getElementById("token");
const companionUrlInput = document.getElementById("companionUrl");
const saveBtn = document.getElementById("save");
const resetUrlBtn = document.getElementById("resetUrl");
const msg = document.getElementById("msg");

function show(text, cls = "") {
    msg.className = ("message " + cls).trim();
    msg.textContent = text || "";
}
function setBusy(isBusy) {
    for (const element of [saveBtn, resetUrlBtn, tokenInput, companionUrlInput]) element.disabled = isBusy;
}
function renderStatus(status) {
    renderConnectionStatus(status);
    const state = status?.lastState;
    const track = state?.title ? state.title + (state.artist ? " - " + state.artist : "") : "-";
    const trackElement = document.getElementById("lastTrackText");
    trackElement.textContent = track;
    trackElement.title = track;
}
async function refreshStatus() {
    try {
        renderStatus(await chrome.runtime.sendMessage({ type: "YTM_GET_STATUS" }));
    } catch {
        show(t("msgStatusLoadFailed"), "err");
    }
}
async function load() {
    const result = await chrome.storage.local.get({ securityToken: "", companionUrl: "" });
    tokenInput.value = result.securityToken || "";
    companionUrlInput.value = result.companionUrl || "";
    await refreshStatus();
}
document.getElementById("setupForm").addEventListener("submit", async (event) => {
    event.preventDefault();
    setBusy(true);
    show(t("msgSavingTesting"));
    try {
        const result = await chrome.runtime.sendMessage({
            type: "YTM_SAVE_AND_TEST",
            token: tokenInput.value,
            companionUrl: companionUrlInput.value
        });
        show(result.message, result.ok ? "ok" : "err");
        await refreshStatus();
    } catch {
        show(t("msgSaveResultUnknown"), "err");
    } finally {
        setBusy(false);
    }
});
for (const input of [tokenInput, companionUrlInput]) {
    input.addEventListener("input", () => show(t("msgUnsaved")));
}
resetUrlBtn.addEventListener("click", () => {
    companionUrlInput.value = "";
    show(t("msgUnsaved"));
});
document.getElementById("refresh").addEventListener("click", refreshStatus);
document.getElementById("openYtm").addEventListener("click", () => chrome.tabs.create({ url: "https://music.youtube.com/" }));

localizeDom();
renderInstallationMetadata();
load().catch(() => show(t("msgSettingsLoadFailed"), "err"));
setInterval(() => {
    if (document.visibilityState === "visible") refreshStatus();
}, 2000);
