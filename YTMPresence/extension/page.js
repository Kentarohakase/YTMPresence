(() => {
    // Läuft im Page-Context, damit navigator.mediaSession direkt verfügbar ist.

    if (window.__ytmPresencePageBridgeLoaded) return;
    window.__ytmPresencePageBridgeLoaded = true;

    const MAX_TEXT_LENGTH = 256;
    const EMPTY_METADATA_GRACE_MS = 5000;
    const SEND_POSITION_BUCKET_SECONDS = 5;

    let lastGoodMetadata = null;

    function clampText(text) {
        if (!text || typeof text !== "string") return "";
        const trimmed = text.trim();
        return trimmed.length > MAX_TEXT_LENGTH ? trimmed.slice(0, MAX_TEXT_LENGTH) : trimmed;
    }

    function getAudioElement() {
        return document.querySelector("audio");
    }

    function getVisibleText(selectors) {
        for (const selector of selectors) {
            const elements = Array.from(document.querySelectorAll(selector));

            for (const element of elements) {
                if (!isVisible(element)) continue;

                const text = clampText(element.textContent || "");
                if (text) return text;
            }
        }

        return "";
    }

    function getMetaContent(selectors) {
        for (const selector of selectors) {
            const value = clampText(document.querySelector(selector)?.content || "");
            if (value) return value;
        }

        return "";
    }

    function stripYtmSuffix(text) {
        return clampText((text || "")
            .replace(/\s+-\s+YouTube Music\s*$/i, "")
            .replace(/\s+\|\s+YouTube Music\s*$/i, ""));
    }

    function splitTitleAndArtist(text) {
        const normalized = clampText(text);
        if (!normalized) return null;

        const separators = [" - ", " – ", " — ", " | "];
        for (const separator of separators) {
            const parts = normalized.split(separator).map(clampText).filter(Boolean);
            if (parts.length === 2) {
                return { title: parts[0], artist: parts[1] };
            }
        }

        return null;
    }

    function normalizeArtist(text) {
        const value = clampText(text);
        if (!value) return "";

        const parts = value
            .split(/\s+[•·]\s+/)
            .map(clampText)
            .filter(Boolean)
            .filter((part) => !/^\d+:\d{2}/.test(part));

        return parts[0] || value;
    }

    function readDomMetadata() {
        const title = getVisibleText([
            "ytmusic-player-bar a#song-title",
            "ytmusic-player-bar .title",
            "ytmusic-player-bar yt-formatted-string.title",
            "ytmusic-player-page .title",
            "ytmusic-player-page h1",
            "#song-title"
        ]);

        const artist = normalizeArtist(getVisibleText([
            "ytmusic-player-bar .byline",
            "ytmusic-player-bar .subtitle",
            "ytmusic-player-bar yt-formatted-string.subtitle",
            "ytmusic-player-bar a[href*='/channel/']",
            "ytmusic-player-page .byline",
            "ytmusic-player-page .subtitle"
        ]));

        const album = getVisibleText([
            "ytmusic-player-bar .album",
            "ytmusic-player-page .album",
            "ytmusic-player-page a[href*='browse']"
        ]);

        const metaTitle = stripYtmSuffix(getMetaContent([
            "meta[property='og:title']",
            "meta[name='title']"
        ]));

        const fallback = splitTitleAndArtist(metaTitle || document.title);

        return {
            title: title || fallback?.title || metaTitle,
            artist: artist || fallback?.artist || "",
            album
        };
    }

    function mergeMetadata(metadata, domMetadata) {
        let title = clampText(metadata?.title || domMetadata.title || "");
        let artist = normalizeArtist(metadata?.artist || domMetadata.artist || "");
        const album = clampText(metadata?.album || domMetadata.album || "");

        if (!artist) {
            const split = splitTitleAndArtist(title);
            if (split) {
                title = split.title;
                artist = split.artist;
            }
        }

        if (!artist && album && album !== title) {
            artist = album;
        }

        return { title, artist, album };
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

    function getBestArtworkUrl(artworkArray) {
        if (!Array.isArray(artworkArray)) return "";

        const candidates = artworkArray
            .map((item) => {
                const sizesText = typeof item?.sizes === "string" ? item.sizes : "";
                const sizeFromText = Number(sizesText.split("x")[0]) || 0;

                return {
                    src: typeof item?.src === "string" ? item.src : "",
                    size: Math.max(sizeFromText, Number(item?.width) || 0, Number(item?.height) || 0)
                };
            })
            .filter((item) => item.src);

        candidates.sort((a, b) => b.size - a.size);
        return clampText(candidates[0]?.src || "");
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
        const domMetadata = readDomMetadata();

        const audio = getAudioElement();
        const position = Number.isFinite(audio?.currentTime) ? audio.currentTime : null;
        const duration = Number.isFinite(audio?.duration) ? audio.duration : null;

        const playbackState =
            mediaSession?.playbackState || (audio?.paused ? "paused" : "playing");
        const isPlaying = playbackState === "playing";

        let { title, artist, album } = mergeMetadata(metadata, domMetadata);
        const albumArtUrl = getBestArtworkUrl(metadata?.artwork);

        const mode = detectPlaybackMode();

        const isAd = detectAd();

        if (!title && !artist && lastGoodMetadata && (Date.now() - lastGoodMetadata.ts) < EMPTY_METADATA_GRACE_MS) {
            title = lastGoodMetadata.title;
            artist = lastGoodMetadata.artist;
            album = lastGoodMetadata.album;
        }

        if (title) {
            lastGoodMetadata = { title, artist, album, ts: Date.now() };
        }

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
            albumArtUrl,
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

    function getSendKey(state) {
        const positionBucket = Number.isFinite(state.position)
            ? Math.floor(state.position / SEND_POSITION_BUCKET_SECONDS)
            : -1;
        const duration = Number.isFinite(state.duration) ? Math.round(state.duration) : -1;

        return [
            state.title,
            state.artist,
            state.album,
            state.albumArtUrl,
            state.isPlaying,
            state.url,
            state.shareUrl,
            state.mode,
            state.isAd,
            positionBucket,
            duration
        ].join("|");
    }

    let lastSendKey = "";
    let publishTimer = null;
    let queuedForcePublish = false;

    function publishState(force) {
        try {
            const state = readState();

            if (!state.title && !state.isAd) return;

            const sendKey = getSendKey(state);
            if (force || sendKey !== lastSendKey) {
                lastSendKey = sendKey;
                window.postMessage({ type: "YTM_STATE", payload: state }, location.origin);
            }
        } catch {
            // bewusst still
        }
    }

    function schedulePublish(delayMs = 0, force = false) {
        queuedForcePublish = queuedForcePublish || force;
        if (publishTimer) return;

        publishTimer = setTimeout(() => {
            const shouldForce = queuedForcePublish;
            queuedForcePublish = false;
            publishTimer = null;
            publishState(shouldForce);
        }, delayMs);
    }

    function bindAudioEvents() {
        const audio = getAudioElement();
        if (!audio || audio.__ytmPresenceEventsBound) return;

        audio.__ytmPresenceEventsBound = true;

        const forceEvents = new Set([
            "durationchange",
            "loadedmetadata",
            "pause",
            "play",
            "seeked"
        ]);

        [
            "durationchange",
            "loadedmetadata",
            "pause",
            "play",
            "seeked",
            "timeupdate"
        ].forEach((eventName) => {
            audio.addEventListener(
                eventName,
                () => schedulePublish(0, forceEvents.has(eventName)),
                true
            );
        });
    }

    setInterval(() => publishState(false), 1000);
    setInterval(bindAudioEvents, 2000);

    document.addEventListener("visibilitychange", () => schedulePublish(0, true), true);
    window.addEventListener("yt-navigate-finish", () => schedulePublish(250, true), true);

    bindAudioEvents();
    schedulePublish(250, true);
})();
