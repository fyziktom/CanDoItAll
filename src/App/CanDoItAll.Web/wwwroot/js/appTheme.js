window.CanDoItAll = window.CanDoItAll || {};

window.CanDoItAll.appTheme = {
    apply: function (themeKey) {
        document.documentElement.setAttribute("data-ui-theme", themeKey);
        document.documentElement.style.colorScheme = themeKey;
    }
};
