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
        if (!event.data || event.data.type !== "YTM_STATE") return;

        chrome.runtime.sendMessage({
            type: "YTM_STATE",
            payload: event.data.payload
        }).catch((error) => {
            console.warn(`[YTM Bridge] Background message failed: ${error?.message || error}`);
        });
    });
}
