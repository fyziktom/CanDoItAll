window.CanDoItAll = window.CanDoItAll || {};

window.CanDoItAll.appTheme = (function () {
    const themeCookieName = "cda.ui-theme.v1";
    const menuCookieName = "cda.ui-main-menu-collapsed.v1";

    function readCookie(name) {
        const prefix = name + "=";
        const cookie = document.cookie.split(";").map(value => value.trim())
            .find(value => value.startsWith(prefix));
        return cookie === undefined ? null : decodeURIComponent(cookie.slice(prefix.length));
    }

    function getPreferences() {
        const theme = readCookie(themeCookieName);
        const collapsed = readCookie(menuCookieName);
        if (theme !== null && theme !== "light" && theme !== "dark") {
            throw new Error("The browser theme preference is malformed.");
        }
        if (collapsed !== null && collapsed !== "true" && collapsed !== "false") {
            throw new Error("The browser main menu preference is malformed.");
        }

        return { theme: theme === "dark" ? 1 : 0, mainMenuCollapsed: collapsed !== "false" };
    }

    function writeCookie(name, value) {
        document.cookie = name + "=" + encodeURIComponent(value)
            + "; Path=/; SameSite=Lax; Max-Age=31536000"
            + (window.location.protocol === "https:" ? "; Secure" : "");
        if (readCookie(name) !== value) {
            throw new Error("The browser did not store the UI preference cookie.");
        }
    }

    function updatePreferences(patch) {
        if (patch === null || typeof patch !== "object" || Array.isArray(patch)
            || Object.keys(patch).some(key => key !== "theme" && key !== "mainMenuCollapsed")) {
            throw new TypeError("The browser UI preference patch is invalid.");
        }
        if (patch.theme != null && patch.theme !== 0 && patch.theme !== 1) {
            throw new TypeError("The browser UI theme is unsupported.");
        }
        if (patch.mainMenuCollapsed != null && typeof patch.mainMenuCollapsed !== "boolean") {
            throw new TypeError("The browser main menu state must be a boolean.");
        }

        getPreferences();
        if (patch.theme != null) {
            writeCookie(themeCookieName, patch.theme === 1 ? "dark" : "light");
        }
        if (patch.mainMenuCollapsed != null) {
            writeCookie(menuCookieName, patch.mainMenuCollapsed ? "true" : "false");
        }
        return getPreferences();
    }

    return {
        apply: function (themeKey) {
            document.documentElement.setAttribute("data-ui-theme", themeKey);
            document.documentElement.style.colorScheme = themeKey;
        },
        getPreferences,
        updatePreferences
    };
})();
