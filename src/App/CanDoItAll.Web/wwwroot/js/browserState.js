window.CanDoItAll = window.CanDoItAll || {};

const databaseSwitchStorageKey = "candoitall.database-switch";
const databaseSwitchAlertStorageKey = "candoitall.database-switch-alert";
const databaseStartupPromptStorageKey = "candoitall.database-startup-dismissed";
const databaseSwitchChannelName = "candoitall.database-switch";
let databaseSwitchStorageListener = null;
let databaseSwitchChannel = null;

window.CanDoItAll.browserState = {
    load: function (key) {
        return window.localStorage.getItem(key);
    },
    save: function (key, value) {
        window.localStorage.setItem(key, value);
    },
    remove: function (key) {
        window.localStorage.removeItem(key);
    },
    publishDatabaseSwitch: function (payload) {
        window.localStorage.setItem(databaseSwitchStorageKey, payload);
        if (typeof window.BroadcastChannel === "function") {
            if (databaseSwitchChannel === null) {
                databaseSwitchChannel = new window.BroadcastChannel(databaseSwitchChannelName);
            }

            databaseSwitchChannel.postMessage(payload);
        }
    },
    registerDatabaseSwitchListener: function (dotNetRef) {
        const notify = function (payload) {
            if (typeof payload !== "string" || payload.length === 0) {
                return;
            }

            dotNetRef.invokeMethodAsync("HandleBrowserDatabaseSwitchAsync", payload);
        };

        if (databaseSwitchStorageListener !== null) {
            window.removeEventListener("storage", databaseSwitchStorageListener);
            databaseSwitchStorageListener = null;
        }

        databaseSwitchStorageListener = function (event) {
            if (event.key === databaseSwitchStorageKey && typeof event.newValue === "string" && event.newValue.length > 0) {
                notify(event.newValue);
            }
        };
        window.addEventListener("storage", databaseSwitchStorageListener);

        if (typeof window.BroadcastChannel === "function") {
            if (databaseSwitchChannel !== null) {
                databaseSwitchChannel.close();
            }

            databaseSwitchChannel = new window.BroadcastChannel(databaseSwitchChannelName);
            databaseSwitchChannel.onmessage = function (event) {
                if (typeof event.data === "string" && event.data.length > 0) {
                    notify(event.data);
                }
            };
        }
    },
    rememberDatabaseSwitchAlert: function (payload) {
        window.sessionStorage.setItem(databaseSwitchAlertStorageKey, payload);
    },
    consumeDatabaseSwitchAlert: function () {
        const payload = window.sessionStorage.getItem(databaseSwitchAlertStorageKey);
        if (payload !== null) {
            window.sessionStorage.removeItem(databaseSwitchAlertStorageKey);
        }

        return payload;
    },
    isDatabaseStartupPromptDismissed: function () {
        return window.sessionStorage.getItem(databaseStartupPromptStorageKey) === "1";
    },
    dismissDatabaseStartupPrompt: function () {
        window.sessionStorage.setItem(databaseStartupPromptStorageKey, "1");
    },
    listKeys: function (prefix) {
        const keys = [];
        for (let index = 0; index < window.localStorage.length; index++) {
            const key = window.localStorage.key(index);
            if (typeof key !== "string") {
                continue;
            }

            if (!prefix || key.startsWith(prefix)) {
                keys.push(key);
            }
        }

        return keys;
    }
};
