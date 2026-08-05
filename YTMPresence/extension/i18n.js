// Geteilte i18n-Helfer für Popup- und Optionsseite.
// Nutzt chrome.i18n (_locales) und funktioniert in Chrome, Edge und Firefox.

function t(key, subs) {
    const msg = chrome.i18n.getMessage(key, subs);
    return msg || key;
}

// Ersetzt statische Texte im DOM anhand von data-i18n-Attributen:
//   data-i18n              -> textContent
//   data-i18n-placeholder  -> placeholder-Attribut
//   data-i18n-title        -> title-Attribut
function localizeDom(root = document) {
    const uiLanguage = chrome.i18n.getUILanguage?.();
    if (uiLanguage && root.documentElement) {
        root.documentElement.lang = uiLanguage;
    }

    root.querySelectorAll("[data-i18n]").forEach((el) => {
        const msg = t(el.getAttribute("data-i18n"));
        if (msg) el.textContent = msg;
    });

    root.querySelectorAll("[data-i18n-placeholder]").forEach((el) => {
        const msg = t(el.getAttribute("data-i18n-placeholder"));
        if (msg) el.setAttribute("placeholder", msg);
    });

    root.querySelectorAll("[data-i18n-title]").forEach((el) => {
        const msg = t(el.getAttribute("data-i18n-title"));
        if (msg) el.setAttribute("title", msg);
    });
}
