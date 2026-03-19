window.CanDoItAll = window.CanDoItAll || {};

window.CanDoItAll.browserState = {
    load: function (key) {
        return window.localStorage.getItem(key);
    },
    save: function (key, value) {
        window.localStorage.setItem(key, value);
    },
    remove: function (key) {
        window.localStorage.removeItem(key);
    }
};
