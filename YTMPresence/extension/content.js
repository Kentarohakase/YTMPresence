if (!window.__ytmPresenceBridgeContentLoaded) {
    window.__ytmPresenceBridgeContentLoaded = true;

    const injectedScript = document.createElement("script");
    injectedScript.src = chrome.runtime.getURL("page.js");
    injectedScript.type = "text/javascript";
    (document.head || document.documentElement).appendChild(injectedScript);
    injectedScript.onload = () => injectedScript.remove();

    window.addEventListener("message", (event) => {
        if (event.source !== window) return;
        if (event.origin !== location.origin) return;
        if (!event.data) return;

        if (event.data.type === "YTM_COMMAND_RESULT") {
            chrome.runtime.sendMessage({
                type: "YTM_COMMAND_RESULT",
                command: event.data.command,
                ok: event.data.ok
            }).catch(() => {
                // Background feedback is best effort.
            });
            return;
        }

        if (event.data.type !== "YTM_STATE") return;

        chrome.runtime.sendMessage({
            type: "YTM_STATE",
            payload: event.data.payload
        }).catch((error) => {
            console.warn(`[YTM Bridge] Background message failed: ${error?.message || error}`);
        });
    });

    chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
        if (!message || message.type !== "YTM_COMMAND") return false;

        window.postMessage({
            type: "YTM_COMMAND",
            command: message.command,
            ts: message.ts || Date.now()
        }, location.origin);

        sendResponse({ ok: true });
        return false;
    });
}
