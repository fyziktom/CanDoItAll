(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const cache = new Map();
    const cacheLimit = 400;
    const canvas = document.createElement("canvas");
    const context = canvas.getContext("2d");

    function normalizeText(value) {
        return typeof value === "string"
            ? value.trim().replace(/\s+/g, " ")
            : "";
    }

    function asNumber(value, fallback) {
        const parsed = Number(value);
        return Number.isFinite(parsed) ? parsed : fallback;
    }

    function clamp(value, min, max) {
        return Math.max(min, Math.min(max, value));
    }

    function parseFontSize(font) {
        const match = typeof font === "string"
            ? font.match(/(\d+(?:\.\d+)?)px/)
            : null;
        return match ? asNumber(match[1], 12) : 12;
    }

    function normalizeFontSpec(spec) {
        const raw = spec && typeof spec === "object" ? spec : {};
        const cssFont = typeof raw.cssFont === "string" && raw.cssFont.trim().length > 0
            ? raw.cssFont.trim()
            : null;
        const sizePx = asNumber(raw.sizePx, cssFont ? parseFontSize(cssFont) : 12);
        const weight = Math.round(clamp(asNumber(raw.weight, 600), 100, 900));
        const family = typeof raw.family === "string" && raw.family.trim().length > 0
            ? raw.family.trim()
            : "\"DM Sans\", sans-serif";
        const lineHeightPx = asNumber(raw.lineHeightPx, Math.max(14, sizePx * 1.35));
        const letterSpacingPx = asNumber(raw.letterSpacingPx, 0);
        const normalizedCssFont = cssFont || `${weight} ${sizePx.toFixed(2).replace(/\.00$/, "")}px ${family}`;

        return {
            family,
            sizePx,
            weight,
            lineHeightPx,
            letterSpacingPx,
            cssFont: normalizedCssFont
        };
    }

    function createMeasureContext(font) {
        if (!context) {
            return null;
        }

        context.font = font.cssFont;
        return context;
    }

    function measureWidth(text, font) {
        const value = normalizeText(text);
        if (value.length === 0) {
            return 0;
        }

        const activeContext = createMeasureContext(font);
        const measured = activeContext
            ? activeContext.measureText(value).width
            : value.length * font.sizePx * 0.58;

        const letterSpacing = Math.max(0, value.length - 1) * font.letterSpacingPx;
        return measured + letterSpacing;
    }

    function buildCacheKey(request) {
        return [
            request.text,
            request.maxWidth,
            request.maxLines,
            request.truncationMode,
            request.font.cssFont,
            request.font.lineHeightPx,
            request.font.letterSpacingPx
        ].join("|");
    }

    function evictCache() {
        if (cache.size <= cacheLimit) {
            return;
        }

        const trimCount = Math.max(25, Math.round(cacheLimit * 0.2));
        for (let index = 0; index < trimCount; index += 1) {
            const firstKey = cache.keys().next();
            if (firstKey.done) {
                break;
            }

            cache.delete(firstKey.value);
        }
    }

    function fitToWidth(text, maxWidth, font, suffix) {
        const value = normalizeText(text);
        if (value.length === 0 || measureWidth(value, font) <= maxWidth) {
            return value;
        }

        const safeSuffix = typeof suffix === "string" && suffix.length > 0 ? suffix : "...";
        const suffixWidth = measureWidth(safeSuffix, font);
        if (suffixWidth >= maxWidth) {
            return safeSuffix;
        }

        let fitted = "";
        for (const symbol of Array.from(value)) {
            const candidate = fitted + symbol;
            if (measureWidth(candidate, font) + suffixWidth > maxWidth) {
                break;
            }

            fitted = candidate;
        }

        fitted = fitted.trimEnd();
        return fitted.length === 0 ? safeSuffix : `${fitted}${safeSuffix}`;
    }

    function measure(request) {
        const normalized = request && typeof request === "object" ? request : {};
        const text = normalizeText(normalized.text);
        const font = normalizeFontSpec(normalized.font);
        const maxWidth = Math.max(24, asNumber(normalized.maxWidth, 160));
        const maxLines = Math.max(1, Math.round(asNumber(normalized.maxLines, 1)));
        const truncationMode = typeof normalized.truncationMode === "string"
            ? normalized.truncationMode.trim().toLowerCase()
            : "ellipsis";
        const ellipsis = truncationMode === "clip" ? "" : (typeof normalized.ellipsis === "string" && normalized.ellipsis.length > 0 ? normalized.ellipsis : "...");
        const cacheKey = buildCacheKey({ text, font, maxWidth, maxLines, truncationMode });

        if (cache.has(cacheKey)) {
            return cache.get(cacheKey);
        }

        if (text.length === 0) {
            const emptyResult = {
                displayText: "",
                estimatedWidth: 0,
                estimatedHeight: Math.ceil(font.lineHeightPx),
                lineCount: 1,
                isTruncated: false,
                fullText: "",
                lines: []
            };
            cache.set(cacheKey, emptyResult);
            return emptyResult;
        }

        const tokens = text.split(" ");
        const lines = [];
        let currentLine = "";
        let truncated = false;
        let tokenIndex = 0;

        for (; tokenIndex < tokens.length; tokenIndex += 1) {
            const token = tokens[tokenIndex];
            const candidate = currentLine.length === 0
                ? token
                : `${currentLine} ${token}`;

            if (measureWidth(candidate, font) <= maxWidth) {
                currentLine = candidate;
                continue;
            }

            if (currentLine.length > 0) {
                lines.push(currentLine);
                if (lines.length >= maxLines) {
                    truncated = true;
                    break;
                }
            }

            if (measureWidth(token, font) <= maxWidth) {
                currentLine = token;
                continue;
            }

            currentLine = truncationMode === "clip"
                ? token
                : fitToWidth(token, maxWidth, font, ellipsis);
            truncated = truncated || currentLine !== token;
            lines.push(currentLine);
            currentLine = "";

            if (lines.length >= maxLines) {
                truncated = truncated || tokenIndex < tokens.length - 1;
                break;
            }
        }

        if (currentLine.length > 0 && lines.length < maxLines) {
            lines.push(currentLine);
        }

        if (lines.length === 0) {
            lines.push(truncationMode === "clip" ? text : fitToWidth(text, maxWidth, font, ellipsis));
            truncated = lines[0] !== text;
        }

        if (tokenIndex < tokens.length - 1) {
            truncated = true;
        }

        if (truncated && ellipsis.length > 0 && lines.length > 0) {
            const lastLine = lines[lines.length - 1];
            lines[lines.length - 1] = lastLine.endsWith(ellipsis)
                ? lastLine
                : fitToWidth(lastLine, maxWidth, font, ellipsis);
        }

        const lineEntries = lines.map((line, index) => ({
            index,
            text: line,
            estimatedWidth: Math.ceil(measureWidth(line, font)),
            isEllipsized: truncated && index === lines.length - 1 && ellipsis.length > 0
        }));

        const result = {
            displayText: lines.join("\n"),
            estimatedWidth: lineEntries.length === 0 ? 0 : Math.max(...lineEntries.map(line => line.estimatedWidth)),
            estimatedHeight: Math.ceil(lineEntries.length * font.lineHeightPx),
            lineCount: lineEntries.length,
            isTruncated: truncated,
            fullText: text,
            lines: lineEntries
        };

        cache.set(cacheKey, result);
        evictCache();
        return result;
    }

    function fitText(ctx, text, maxWidth, ellipsis) {
        const request = {
            text,
            maxWidth,
            maxLines: 1,
            ellipsis,
            font: {
                cssFont: ctx && typeof ctx.font === "string" ? ctx.font : undefined,
                sizePx: ctx && typeof ctx.font === "string" ? parseFontSize(ctx.font) : 12,
                lineHeightPx: ctx && typeof ctx.font === "string" ? parseFontSize(ctx.font) * 1.2 : 15
            }
        };

        return measure(request).displayText;
    }

    function wrapText(ctx, text, maxWidth, maxLines) {
        const request = {
            text,
            maxWidth,
            maxLines,
            font: {
                cssFont: ctx && typeof ctx.font === "string" ? ctx.font : undefined,
                sizePx: ctx && typeof ctx.font === "string" ? parseFontSize(ctx.font) : 12,
                lineHeightPx: ctx && typeof ctx.font === "string" ? parseFontSize(ctx.font) * 1.2 : 15
            }
        };

        return measure(request).lines.map(line => line.text);
    }

    function fitElementText(element, options) {
        if (!element) {
            return null;
        }

        const settings = options && typeof options === "object" ? options : {};
        const computed = window.getComputedStyle(element);
        const initialFontSize = asNumber(settings.initialFontSize, parseFloat(computed.fontSize) || 12);
        const minFontSize = Math.min(initialFontSize, asNumber(settings.minFontSize, Math.max(8, initialFontSize - 3)));
        const maxWidth = Math.max(20, asNumber(settings.maxWidth, element.clientWidth));
        const maxHeight = Math.max(12, asNumber(settings.maxHeight, element.clientHeight));
        const maxLines = Math.max(1, Math.round(asNumber(settings.maxLines, 2)));
        const fullText = normalizeText(settings.text || settings.fullText || element.dataset.fullText || element.textContent);

        let fontSize = initialFontSize;
        let result = null;
        do {
            result = measure({
                text: fullText,
                maxWidth,
                maxLines,
                truncationMode: settings.truncationMode || "ellipsis",
                ellipsis: settings.ellipsis || "...",
                font: {
                    family: computed.fontFamily,
                    sizePx: fontSize,
                    weight: asNumber(computed.fontWeight, 600),
                    lineHeightPx: computed.lineHeight === "normal" ? fontSize * 1.25 : asNumber(computed.lineHeight, fontSize * 1.25),
                    letterSpacingPx: asNumber(computed.letterSpacing, 0)
                }
            });

            if (fontSize <= minFontSize || (result.estimatedWidth <= maxWidth + 0.5 && result.estimatedHeight <= maxHeight + 0.5)) {
                break;
            }

            fontSize = Math.max(minFontSize, fontSize - 0.25);
        } while (fontSize >= minFontSize);

        element.style.maxWidth = `${Math.round(maxWidth)}px`;
        element.style.fontSize = `${fontSize}px`;
        element.dataset.textMeasureLines = String(result.lineCount);
        element.dataset.textMeasureTruncated = result.isTruncated ? "true" : "false";
        if (result.isTruncated) {
            element.title = fullText;
        } else {
            element.removeAttribute("title");
        }

        return result;
    }

    function clearCache() {
        cache.clear();
    }

    function getCacheMetrics() {
        return {
            entries: cache.size,
            limit: cacheLimit
        };
    }

    if (document.fonts && typeof document.fonts.addEventListener === "function") {
        document.fonts.addEventListener("loadingdone", clearCache);
    } else if (document.fonts && document.fonts.ready && typeof document.fonts.ready.then === "function") {
        document.fonts.ready.then(clearCache).catch(function () { });
    }

    root.textMeasureService = {
        measure,
        fitText,
        wrapText,
        fitElementText,
        clearCache,
        getCacheMetrics
    };
})();
