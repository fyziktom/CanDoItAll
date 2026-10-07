window.CanDoItAll = window.CanDoItAll || {};

const databaseSwitchStorageKey = "candoitall.database-switch";
const databaseSwitchAlertStorageKey = "candoitall.database-switch-alert";
const databaseStartupPromptStorageKey = "candoitall.database-startup-dismissed";
const databaseSwitchChannelName = "candoitall.database-switch";
const databaseSwitchListeners = new Map();

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
            const channel = new window.BroadcastChannel(databaseSwitchChannelName);
            channel.postMessage(payload);
            channel.close();
        }
    },
    registerDatabaseSwitchListener: function (listenerId, dotNetRef) {
        window.CanDoItAll.browserState.unregisterDatabaseSwitchListener(listenerId);
        const owner = { channel: null, storage: null };
        const notify = async function (payload) {
            if (databaseSwitchListeners.get(listenerId) !== owner || typeof payload !== "string" || payload.length === 0) {
                return;
            }
            try {
                await dotNetRef.invokeMethodAsync("HandleBrowserDatabaseSwitchAsync", payload);
            } catch {
                if (databaseSwitchListeners.get(listenerId) === owner) {
                    window.CanDoItAll.browserState.unregisterDatabaseSwitchListener(listenerId);
                }
            }
        };
        owner.storage = function (event) {
            if (event.key === databaseSwitchStorageKey) {
                void notify(event.newValue);
            }
        };
        databaseSwitchListeners.set(listenerId, owner);
        window.addEventListener("storage", owner.storage);
        if (typeof window.BroadcastChannel === "function") {
            owner.channel = new window.BroadcastChannel(databaseSwitchChannelName);
            owner.channel.onmessage = event => { void notify(event.data); };
        }
    },
    unregisterDatabaseSwitchListener: function (listenerId) {
        const owner = databaseSwitchListeners.get(listenerId);
        if (!owner) {
            return;
        }
        databaseSwitchListeners.delete(listenerId);
        window.removeEventListener("storage", owner.storage);
        owner.channel?.close();
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
