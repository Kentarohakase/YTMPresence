const tokenInput = document.getElementById("token");
const companionUrlInput = document.getElementById("companionUrl");
const saveBtn = document.getElementById("save");
const testBtn = document.getElementById("test");
const msg = document.getElementById("msg");

const DEFAULT_COMPANION_URL = "ws://127.0.0.1:17373/ws";
const TEST_TIMEOUT_MS = 4000;

function show(text, cls) {
    msg.className = cls || "";
    msg.textContent = text || "";
}

function setBusy(isBusy) {
    saveBtn.disabled = isBusy;
    testBtn.disabled = isBusy;
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

async function load() {
    const result = await chrome.storage.local.get({
        securityToken: "",
        companionUrl: ""
    });

    tokenInput.value = result.securityToken || "";
    companionUrlInput.value = result.companionUrl || DEFAULT_COMPANION_URL;
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

            if (response.ok) {
                finish({
                    ok: true,
                    message: "Verbindung OK. Token und Companion URL passen."
                });
                return;
            }

            finish({
                ok: false,
                message: "Companion erreichbar, aber der Token ist falsch."
            });
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

    await chrome.storage.local.set({
        securityToken: settings.token,
        companionUrl: settings.companionUrl
    });

    companionUrlInput.value = settings.companionUrl;
    show("Gespeichert. YT Music Tab neu laden, falls er schon offen war.", "ok");
});

testBtn.addEventListener("click", async () => {
    const settings = getCurrentSettings();

    if (settings.error) {
        show(settings.error, "err");
        return;
    }

    setBusy(true);
    show("Teste Verbindung...", "");

    try {
        const result = await testConnection(settings.companionUrl, settings.token);
        companionUrlInput.value = settings.companionUrl;
        show(result.message, result.ok ? "ok" : "err");
    } finally {
        setBusy(false);
    }
});

load().catch(() => show("Konnte Einstellungen nicht laden.", "err"));
