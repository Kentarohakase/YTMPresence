(() => {
    // Läuft im Page-Context, damit navigator.mediaSession direkt verfügbar ist.

    const MAX_TEXT_LENGTH = 256;

    function clampText(text) {
        if (!text || typeof text !== "string") return "";
        const trimmed = text.trim();
        return trimmed.length > MAX_TEXT_LENGTH ? trimmed.slice(0, MAX_TEXT_LENGTH) : trimmed;
    }

    function getAudioElement() {
        return document.querySelector("audio");
    }

    function buildSearchUrl(title, artist) {
        const query = `${title || ""} ${artist || ""}`.trim();
        if (!query) return "https://music.youtube.com/";
        return `https://music.youtube.com/search?q=${encodeURIComponent(query)}`;
    }

    function buildWatchUrl(videoId) {
        return `https://music.youtube.com/watch?v=${encodeURIComponent(videoId)}`;
    }

    function looksSelected(element) {
        if (!element) return false;

        const ariaPressed = element.getAttribute("aria-pressed");
        if (ariaPressed === "true") return true;

        const ariaSelected = element.getAttribute("aria-selected");
        if (ariaSelected === "true") return true;

        const className = (element.className || "").toString().toLowerCase();
        if (
            className.includes("selected") ||
            className.includes("active") ||
            className.includes("checked")
        ) {
            return true;
        }

        return false;
    }

    function normalizeLabel(text) {
        return (text || "")
            .trim()
            .toLowerCase()
            .replace(/\s+/g, " ");
    }

    /**
     * Erkennt, ob aktuell "Titel/Song" oder "Video" aktiv ist.
     */
    function detectPlaybackMode() {
        const candidates = Array.from(
            document.querySelectorAll(
                [
                    "tp-yt-paper-button",
                    "button",
                    "ytmusic-player-page button",
                    "ytmusic-player-bar button",
                    "ytmusic-player-page tp-yt-paper-button",
                    "ytmusic-player-bar tp-yt-paper-button"
                ].join(",")
            )
        );

        let selectedVideo = false;
        let selectedTitle = false;

        for (const element of candidates) {
            const text = normalizeLabel(element.textContent);
            if (!text) continue;

            const isSelected = looksSelected(element);

            const isVideoLabel = text === "video";
            const isTitleLabel =
                text === "titel" ||
                text === "song" ||
                text === "track" ||
                text === "audio";

            if (isSelected && isVideoLabel) selectedVideo = true;
            if (isSelected && isTitleLabel) selectedTitle = true;
        }

        if (selectedVideo) return "video";
        if (selectedTitle) return "title";

        return "unknown";
    }

    function extractVideoIdFromArtwork(artworkArray) {
        if (!Array.isArray(artworkArray)) return null;

        const patterns = [
            /\/vi\/([a-zA-Z0-9_-]{11})\//,
            /\/vi_webp\/([a-zA-Z0-9_-]{11})\//,
            /\/vi\/([a-zA-Z0-9_-]{11})$/,
            /\/vi_webp\/([a-zA-Z0-9_-]{11})$/
        ];

        for (const item of artworkArray) {
            const src = item?.src;
            if (!src || typeof src !== "string") continue;

            for (const re of patterns) {
                const match = src.match(re);
                if (match && match[1]) return match[1];
            }
        }

        return null;
    }

    function getPlayerBarWatchUrl() {
        const anchors = [
            document.querySelector('ytmusic-player-bar a#song-title'),
            document.querySelector('ytmusic-player-bar a[href*="watch?v="]'),
            document.querySelector('a#song-title[href*="watch?v="]'),
            document.querySelector('a[href*="music.youtube.com/watch?v="]'),
            document.querySelector('a[href*="/watch?v="]')
        ].filter(Boolean);

        for (const anchor of anchors) {
            try {
                const href = anchor.href || anchor.getAttribute?.("href");
                if (!href) continue;

                const url = new URL(href, "https://music.youtube.com/");
                const videoId = url.searchParams.get("v");
                if (!videoId) continue;

                return buildWatchUrl(videoId);
            } catch {
                // ignore
            }
        }

        return null;
    }

    function getCanonicalOrOgUrl() {
        const canonical = document.querySelector('link[rel="canonical"]')?.href;
        const ogUrl = document.querySelector('meta[property="og:url"]')?.content;
        return canonical || ogUrl || null;
    }

    function extractVideoIdFromUrl(candidateUrl) {
        if (!candidateUrl) return null;

        try {
            const url = new URL(candidateUrl, "https://music.youtube.com/");
            const videoId = url.searchParams.get("v");
            if (videoId) return videoId;

            const pathParts = url.pathname.split("/").filter(Boolean);
            if (pathParts.length >= 2) {
                const kind = pathParts[0].toLowerCase();
                const id = pathParts[1];
                const looksLikeId = typeof id === "string" && id.length >= 8 && id.length <= 20;

                if (looksLikeId && (kind === "shorts" || kind === "embed")) {
                    return id;
                }
            }
        } catch {
            // ignore
        }

        return null;
    }

    function isVisible(element) {
        if (!element) return false;

        const rect = element.getBoundingClientRect?.();
        if (!rect || rect.width <= 0 || rect.height <= 0) return false;

        const style = getComputedStyle(element);
        return style.display !== "none" && style.visibility !== "hidden" && style.opacity !== "0";
    }

    function detectAd() {
        const adSelectors = [
            "ytmusic-ad-info-renderer",
            "ytmusic-player-bar ytmusic-player-ad-info-renderer",
            "ytmusic-player-page ytmusic-player-ad-info-renderer",
            ".ytp-ad-module",
            ".ytp-ad-player-overlay",
            ".ad-showing"
        ];

        for (const selector of adSelectors) {
            const element = document.querySelector(selector);
            if (isVisible(element)) return true;
        }

        const labels = Array.from(
            document.querySelectorAll("ytmusic-player-bar, ytmusic-player-page")
        )
            .map((element) => normalizeLabel(element.textContent))
            .join(" ");

        return /\b(advertisement|werbung|anzeige|anuncio|publicidad)\b/.test(labels);
    }

    /**
     * Baut den bestmöglichen Discord-Link:
     * - Video-Modus: echter watch?v=... bevorzugt
     * - Titel/Song-Modus: stabiler Search-Link
     */
    function buildShareUrl({ mode, title, artist, metadataArtwork }) {
        const playerBarWatchUrl = getPlayerBarWatchUrl();
        const canonicalOrOgUrl = getCanonicalOrOgUrl();

        const artworkVideoId = extractVideoIdFromArtwork(metadataArtwork);
        const canonicalVideoId = extractVideoIdFromUrl(canonicalOrOgUrl);
        const playerBarVideoId = extractVideoIdFromUrl(playerBarWatchUrl);

        if (mode === "video") {
            if (playerBarVideoId) return buildWatchUrl(playerBarVideoId);
            if (canonicalVideoId) return buildWatchUrl(canonicalVideoId);
            if (artworkVideoId) return buildWatchUrl(artworkVideoId);

            return buildSearchUrl(title, artist);
        }

        if (mode === "title") {
            return buildSearchUrl(title, artist);
        }

        if (playerBarVideoId) return buildWatchUrl(playerBarVideoId);
        if (canonicalVideoId) return buildWatchUrl(canonicalVideoId);
        if (artworkVideoId) return buildWatchUrl(artworkVideoId);

        return buildSearchUrl(title, artist);
    }

    function readState() {
        const mediaSession = navigator.mediaSession;
        const metadata = mediaSession?.metadata;

        const audio = getAudioElement();
        const position = Number.isFinite(audio?.currentTime) ? audio.currentTime : null;
        const duration = Number.isFinite(audio?.duration) ? audio.duration : null;

        const playbackState =
            mediaSession?.playbackState || (audio?.paused ? "paused" : "playing");
        const isPlaying = playbackState === "playing";

        const title = clampText(metadata?.title || "");
        const artist = clampText(metadata?.artist || "");
        const album = clampText(metadata?.album || "");

        const mode = detectPlaybackMode();

        const isAd = detectAd();

        const shareUrl = buildShareUrl({
            mode,
            title,
            artist,
            metadataArtwork: metadata?.artwork
        });

        return {
            source: "ytmusic",
            title,
            artist,
            album,
            isPlaying,
            position,
            duration,
            url: location.href,
            shareUrl,
            mode,
            isAd,
            ts: Date.now()
        };
    }

    let lastJson = "";

    setInterval(() => {
        try {
            const state = readState();

            if (!state.title || !state.artist) return;

            const json = JSON.stringify(state);
            if (json !== lastJson) {
                lastJson = json;
                window.postMessage({ type: "YTM_STATE", payload: state }, location.origin);
            }
        } catch {
            // bewusst still
        }
    }, 1000);
})();
