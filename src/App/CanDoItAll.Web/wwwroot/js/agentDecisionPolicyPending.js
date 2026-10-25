const databaseName = "candoitall.agents.pending-policy";
const storeName = "submissions";
const schema = 1;
const maximumRecords = 128;
const maximumRequestBytes = 8192;
const maximumOutcomeBytes = 4096;
const stages = Object.freeze({ prepared: 0, dispatching: 1, accepted: 2, rejected: 3 });
const encoder = new TextEncoder();
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const emptyUuid = "00000000-0000-0000-0000-000000000000";
let database;
let opening;

function fail(code) {
    throw new Error(`Agent policy recovery: ${code}.`);
}

function requireUuid(value) {
    if (typeof value !== "string" || !uuid.test(value) || value === emptyUuid) {
        fail("InvalidIdentity");
    }
    return value;
}

function captureScope(key) {
    if (!key || typeof key.caller !== "string" || !/^ap1:[0-9a-f]{64}$/.test(key.caller) ||
        !Number.isInteger(key.scopeKind) || key.scopeKind < 0 || key.scopeKind > 4 ||
        typeof key.scopeKey !== "string" || key.scopeKey.length > 256) {
        fail("InvalidScope");
    }
    return { caller: key.caller, profileId: requireUuid(key.profileId), scopeKind: key.scopeKind, scopeKey: key.scopeKey };
}

function captureKey(key) {
    return { ...captureScope(key), agentId: requireUuid(key.agentId) };
}

function address(key) {
    return [key.caller, key.profileId, key.scopeKind, key.scopeKey, key.agentId];
}

function bounded(text, maximum) {
    if (typeof text !== "string" || !text.length || text.length > maximum || encoder.encode(text).byteLength > maximum) {
        fail("PayloadLimit");
    }
    return text;
}

function validate(record, key) {
    if (record === undefined) {
        return null;
    }
    if (!record || record.schema !== schema || indexedDB.cmp(address(captureKey(record.key)), address(key)) !== 0 ||
        !Object.values(stages).includes(record.stage)) {
        fail("CorruptRecord");
    }
    requireUuid(record.operationId);
    bounded(record.payload, maximumRequestBytes);
    const terminal = record.stage === stages.accepted || record.stage === stages.rejected;
    if (terminal !== (typeof record.outcome === "string") || !terminal && record.outcome !== null) {
        fail("CorruptOutcome");
    }
    if (terminal) {
        bounded(record.outcome, maximumOutcomeBytes);
    }
    return record;
}

export async function initialize() {
    if (!database) {
        opening ??= new Promise((resolve, reject) => {
            const request = indexedDB.open(databaseName, schema);
            request.onupgradeneeded = () => {
                if (!request.result.objectStoreNames.contains(storeName)) {
                    request.result.createObjectStore(storeName);
                }
            };
            request.onerror = () => reject(request.error);
            request.onblocked = () => reject(new Error("Agent policy recovery: StorageBlocked."));
            request.onsuccess = () => {
                database = request.result;
                database.onversionchange = () => {
                    database.close();
                    database = null;
                    opening = null;
                };
                resolve();
            };
        }).catch(error => {
            opening = null;
            throw error;
        });
        await opening;
    }
    return { schema, maximumRecords, maximumRequestBytes, maximumOutcomeBytes };
}

export function list(scope) {
    scope = captureScope(scope);
    if (!database) {
        fail("StorageUnavailable");
    }
    const prefix = [scope.caller, scope.profileId, scope.scopeKind, scope.scopeKey];
    const range = IDBKeyRange.bound([...prefix, ""], [...prefix, "\uffff"]);
    return new Promise((resolve, reject) => {
        const rows = [];
        let failure;
        const tx = database.transaction(storeName, "readonly");
        tx.oncomplete = () => resolve(rows);
        tx.onabort = () => reject(failure ?? tx.error ?? new Error("Agent policy recovery: TransactionAborted."));
        tx.onerror = () => {};
        const request = tx.objectStore(storeName).openCursor(range);
        request.onsuccess = () => {
            try {
                const cursor = request.result;
                if (!cursor) {
                    return;
                }
                if (rows.length === maximumRecords) {
                    fail("CapacityExceeded");
                }
                const key = captureKey(cursor.value?.key);
                const record = validate(cursor.value, { ...scope, agentId: key.agentId });
                if (indexedDB.cmp(cursor.key, address(key)) !== 0) {
                    fail("CorruptRecord");
                }
                rows.push({ agentId: key.agentId, operationId: record.operationId, stage: record.stage });
                cursor.continue();
            } catch (error) {
                failure = error;
                tx.abort();
            }
        };
    });
}

function transact(key, mode, action) {
    key = captureKey(key);
    if (!database) {
        fail("StorageUnavailable");
    }
    return new Promise((resolve, reject) => {
        let result;
        let failure;
        const tx = database.transaction(storeName, mode, { durability: "strict" });
        const store = tx.objectStore(storeName);
        const guard = work => {
            try {
                work();
            } catch (error) {
                failure = error;
                tx.abort();
            }
        };
        tx.oncomplete = () => resolve(result);
        tx.onabort = () => reject(failure ?? tx.error ?? new Error("Agent policy recovery: TransactionAborted."));
        tx.onerror = () => {};
        guard(() => {
            if (mode === "readwrite" && tx.durability !== "strict") {
                fail("UnsupportedDurability");
            }
            const request = store.get(address(key));
            request.onsuccess = () => guard(() => action(validate(request.result, key), store,
                value => { result = value; }, guard, key));
        });
    });
}

export function read(key) {
    return transact(key, "readonly", (record, store, finish) => finish(record));
}

export function prepare(key, operationId, payload) {
    requireUuid(operationId);
    bounded(payload, maximumRequestBytes);
    return transact(key, "readwrite", (record, store, finish, guard, captured) => {
        if (record) {
            finish({ changed: false, record });
            return;
        }
        const count = store.count();
        count.onsuccess = () => guard(() => {
            if (count.result >= maximumRecords) {
                fail("CapacityExceeded");
            }
            const retained = { schema, key: captured, operationId, payload, stage: stages.prepared, outcome: null };
            store.add(retained, address(captured));
            finish({ changed: true, record: retained });
        });
    });
}

function change(key, operationId, payload, action) {
    requireUuid(operationId);
    bounded(payload, maximumRequestBytes);
    return transact(key, "readwrite", (record, store, finish) => {
        if (!record || record.operationId !== operationId || record.payload !== payload) {
            finish({ changed: false, record });
            return;
        }
        action(record, store, finish);
    });
}

export function beginDispatch(key, operationId, payload) {
    return change(key, operationId, payload, (record, store, finish) => {
        if (record.stage !== stages.prepared) {
            finish({ changed: false, record });
            return;
        }
        record.stage = stages.dispatching;
        store.put(record, address(record.key));
        finish({ changed: true, record });
    });
}

export function recordOutcome(key, operationId, payload, outcome) {
    bounded(outcome, maximumOutcomeBytes);
    const result = JSON.parse(outcome);
    if (result.operationId !== operationId || !["Accepted", "Rejected"].includes(result.outcome)) {
        fail("InvalidOutcome");
    }
    const next = result.outcome === "Accepted" ? stages.accepted : stages.rejected;
    return change(key, operationId, payload, (record, store, finish) => {
        if (record.stage !== stages.dispatching) {
            if (record.stage === next && record.outcome === outcome) {
                finish({ changed: false, record });
                return;
            }
            if (next === stages.accepted && record.stage === next) {
                const earlier = JSON.parse(record.outcome);
                if (JSON.stringify({ ...earlier, followupRequired: false }) === JSON.stringify({ ...result, followupRequired: false })) {
                    const changed = result.followupRequired === true && earlier.followupRequired !== true;
                    if (changed) {
                        record.outcome = outcome;
                        store.put(record, address(record.key));
                    }
                    finish({ changed, record });
                    return;
                }
            }
            fail("ConflictingOutcome");
        }
        record.stage = next;
        record.outcome = outcome;
        store.put(record, address(record.key));
        finish({ changed: true, record });
    });
}

export function clearResolved(key, operationId, payload) {
    return remove(key, operationId, payload, record => record.stage === stages.accepted || record.stage === stages.rejected);
}

export function cancelPrepared(key, operationId, payload) {
    return remove(key, operationId, payload, record => record.stage === stages.prepared);
}

function remove(key, operationId, payload, allowed) {
    return change(key, operationId, payload, (record, store, finish) => {
        if (!allowed(record)) {
            finish({ changed: false, record });
            return;
        }
        store.delete(address(record.key));
        finish({ changed: true, record: null });
    });
}
