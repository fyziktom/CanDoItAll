const databaseName = "candoitall.projects.pending-submissions";
const databaseVersion = 1;
const submissionsStore = "submissions";
const metadataStore = "metadata";
const usageKey = "usage";
const recordSchema = 1;
const encoder = new TextEncoder();
const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const emptyUuid = "00000000-0000-0000-0000-000000000000";
const bindingPattern = /^pc1:[0-9a-f]{64}$/;
const epochPattern = /^[1-9][0-9]{0,18}$/;
const maximumEpoch = "9223372036854775807";
const stage = Object.freeze({
    prepared: "prepared",
    dispatching: "dispatching",
    unconfirmed: "unconfirmed",
    accepted: "accepted",
    rejected: "rejected",
    closedAbsent: "closedAbsent"
});
let database;
let opening;
let limits;
let generation = 0;

function fail(code) {
    const error = new Error(`Projects pending submission: ${code}.`);
    error.name = "ProjectPendingSubmissionError";
    error.code = code;
    throw error;
}

function requireInteger(value, minimum = 1) {
    if (!Number.isSafeInteger(value) || value < minimum) {
        fail("InvalidRequest");
    }
    return value;
}

function requireUuid(value) {
    if (typeof value !== "string" || !uuidPattern.test(value) || value === emptyUuid) {
        fail("InvalidRequest");
    }
    return value;
}

function captureScope(value) {
    if (!value || typeof value.callerBinding !== "string" || !bindingPattern.test(value.callerBinding)) {
        fail("InvalidRequest");
    }
    return { namespace: requireUuid(value.namespace), callerBinding: value.callerBinding };
}

function captureKey(value) {
    const scope = captureScope(value);
    if (typeof value.epoch !== "string" || !epochPattern.test(value.epoch) ||
        value.epoch.length === maximumEpoch.length && value.epoch > maximumEpoch) {
        fail("InvalidRequest");
    }
    return { ...scope, epoch: value.epoch, id: requireUuid(value.id) };
}

function address(key) {
    return [key.namespace, key.callerBinding, key.epoch, key.id];
}

function requireText(value, maximumBytes) {
    if (typeof value !== "string" || value.length === 0 || value.length > maximumBytes ||
        encoder.encode(value).byteLength > maximumBytes) {
        fail("PayloadLimit");
    }
    return value;
}

function captureLimits(value) {
    if (!value) {
        fail("InvalidRequest");
    }
    return {
        maximumRecordCount: requireInteger(value.maximumRecordCount),
        maximumTotalBytes: requireInteger(value.maximumTotalBytes),
        maximumFrozenRequestBytes: requireInteger(value.maximumFrozenRequestBytes),
        maximumAcceptedBytes: requireInteger(value.maximumAcceptedBytes),
        maximumPageSize: requireInteger(value.maximumPageSize)
    };
}

function requireReady() {
    if (!database || !limits) {
        fail("StorageUnavailable");
    }
}

function validateRecord(record) {
    if (!record) {
        return null;
    }
    const key = captureKey(record.key);
    if (record.schema !== recordSchema || indexedDB.cmp(address(key), record.address) !== 0 ||
        !Object.values(stage).includes(record.stage) || typeof record.hadUnconfirmedDispatch !== "boolean" ||
        typeof record.hadStarterPlan !== "boolean") {
        fail("CorruptRecord");
    }
    requireInteger(record.revision);
    requireInteger(record.payloadBytes);
    requireText(record.frozenRequest, limits.maximumFrozenRequestBytes);
    if (record.targetProjectId !== null) {
        requireUuid(record.targetProjectId);
    }
    if (record.attemptId !== null) {
        requireUuid(record.attemptId);
    }
    if (record.acceptedPayload !== null) {
        requireText(record.acceptedPayload, limits.maximumAcceptedBytes);
    }
    if (record.absenceProof !== null) {
        requireText(record.absenceProof, limits.maximumAcceptedBytes);
    }
    if ((record.stage === stage.accepted) !== (record.acceptedPayload !== null) ||
        (record.stage === stage.closedAbsent) !== (record.absenceProof !== null) ||
        record.stage === stage.prepared && record.attemptId !== null ||
        record.stage !== stage.prepared && record.attemptId === null ||
        record.stage === stage.unconfirmed && !record.hadUnconfirmedDispatch ||
        record.payloadBytes !== measure(record)) {
        fail("CorruptRecord");
    }
    return record;
}

function measure(record) {
    const { frozenRequest, acceptedPayload, absenceProof, payloadBytes, ...metadata } = record;
    return encoder.encode(JSON.stringify(metadata)).byteLength + encoder.encode(frozenRequest).byteLength +
        encoder.encode(acceptedPayload ?? "").byteLength + encoder.encode(absenceProof ?? "").byteLength;
}

function transaction(mode, work) {
    requireReady();
    const startedGeneration = generation;
    return new Promise((resolve, reject) => {
        let result;
        let failure;
        let tx;
        try {
            tx = database.transaction([submissionsStore, metadataStore], mode, { durability: "strict" });
            tx.oncomplete = () => {
                if (generation !== startedGeneration) {
                    reject(new Error("Projects pending submission: StorageRetired."));
                    return;
                }
                resolve(result);
            };
            tx.onabort = () => reject(failure ?? tx.error ?? new DOMException("Pending submission transaction aborted.", "AbortError"));
            tx.onerror = () => {};
            if (mode === "readwrite" && tx.durability !== "strict") {
                fail("UnsupportedDurability");
            }
            const guard = action => {
                try {
                    if (generation !== startedGeneration) {
                        fail("StorageRetired");
                    }
                    action();
                } catch (error) {
                    failure = error;
                    tx.abort();
                }
            };
            work(tx.objectStore(submissionsStore), tx.objectStore(metadataStore), value => { result = value; }, guard);
        } catch (error) {
            failure = error;
            if (tx) {
                tx.abort();
            } else {
                reject(error);
            }
        }
    });
}

function storeRecord(records, metadata, previous, next, complete, guard) {
    const request = metadata.get(usageKey);
    request.onsuccess = () => guard(() => {
        const usage = request.result;
        if (!usage || !Number.isSafeInteger(usage.count) || usage.count < 0 ||
            !Number.isSafeInteger(usage.bytes) || usage.bytes < 0 ||
            !Number.isSafeInteger(usage.revision) || usage.revision < (previous?.revision ?? 0)) {
            fail("CorruptUsage");
        }
        if (next) {
            if (usage.revision === Number.MAX_SAFE_INTEGER) {
                fail("RecordRevisionExhausted");
            }
            next.revision = usage.revision + 1;
            next.payloadBytes = measure(next);
        }
        const count = usage.count + (next ? 1 : 0) - (previous ? 1 : 0);
        const bytes = usage.bytes + (next?.payloadBytes ?? 0) - (previous?.payloadBytes ?? 0);
        if (!Number.isSafeInteger(bytes) || bytes < 0 || count < 0) {
            fail("CorruptUsage");
        }
        if (next && (count > limits.maximumRecordCount || bytes > limits.maximumTotalBytes)) {
            fail("CapacityLimit");
        }
        if (next) {
            records.put(next);
        } else {
            records.delete(previous.address);
        }
        metadata.put({ id: usageKey, count, bytes, revision: next?.revision ?? usage.revision });
        complete({ status: "applied", record: next });
    });
}

function mutate(key, change) {
    requireReady();
    const captured = captureKey(key);
    return transaction("readwrite", (records, metadata, complete, guard) => {
        const request = records.get(address(captured));
        request.onsuccess = () => guard(() => {
            const current = validateRecord(request.result);
            const decision = change(current, captured);
            if (decision.status !== "apply") {
                complete({ status: decision.status, record: current });
                return;
            }
            storeRecord(records, metadata, current, decision.record, complete, guard);
        });
    });
}

export async function initialize(options) {
    const startedLifetime = generation;
    const captured = captureLimits(options);
    if (limits && JSON.stringify(limits) !== JSON.stringify(captured)) {
        fail("LimitsChanged");
    }
    limits = captured;
    if (!globalThis.indexedDB) {
        fail("StorageUnavailable");
    }
    if (!opening) {
        const startedGeneration = generation;
        opening = new Promise((resolve, reject) => {
            let rejected = false;
            const request = indexedDB.open(databaseName, databaseVersion);
            request.onupgradeneeded = () => {
                const db = request.result;
                db.createObjectStore(submissionsStore, { keyPath: "address" });
                const metadata = db.createObjectStore(metadataStore, { keyPath: "id" });
                metadata.put({ id: usageKey, count: 0, bytes: 0, revision: 0 });
            };
            request.onblocked = () => {
                rejected = true;
                reject(new Error("Projects pending submission: UpgradeBlocked."));
            };
            request.onerror = () => reject(request.error);
            request.onsuccess = () => {
                if (rejected || generation !== startedGeneration) {
                    request.result.close();
                    reject(new Error("Projects pending submission: StorageRetired."));
                    return;
                }
                database = request.result;
                const connection = database;
                connection.onversionchange = () => {
                    if (database === connection) {
                        dispose();
                    }
                };
                connection.onclose = () => {
                    if (database === connection) {
                        dispose();
                    }
                };
                resolve();
            };
        });
    }
    try {
        await opening;
        if (generation !== startedLifetime) {
            fail("StorageRetired");
        }
        await transaction("readwrite", (_records, _metadata, complete) => complete(null));
        return { schema: recordSchema };
    } catch (error) {
        if (generation === startedLifetime) {
            dispose();
        }
        throw error;
    }
}

export function prepare(input) {
    requireReady();
    if (!input || typeof input.hadStarterPlan !== "boolean") {
        fail("InvalidRequest");
    }
    const frozenRequest = requireText(input.frozenRequest, limits.maximumFrozenRequestBytes);
    const targetProjectId = input.targetProjectId === null ? null : requireUuid(input.targetProjectId);
    const hadStarterPlan = input.hadStarterPlan;
    return mutate(input.key, (current, key) => {
        if (current) {
            if (current.frozenRequest !== frozenRequest || current.targetProjectId !== targetProjectId ||
                current.hadStarterPlan !== hadStarterPlan) {
                fail("PreparationMismatch");
            }
            return { status: "unchanged" };
        }
        return { status: "apply", record: {
            schema: recordSchema, key, address: address(key), revision: 0, stage: stage.prepared,
            frozenRequest, targetProjectId, hadStarterPlan, attemptId: null, hadUnconfirmedDispatch: false,
            acceptedPayload: null, absenceProof: null, payloadBytes: 0
        } };
    });
}

export function beginDispatch(key, expectedRevision, attemptId, explicitRetry) {
    requireInteger(expectedRevision);
    const capturedAttempt = requireUuid(attemptId);
    if (typeof explicitRetry !== "boolean") {
        fail("InvalidRequest");
    }
    return mutate(key, current => {
        if (!current) {
            return { status: "missing" };
        }
        if (current.revision !== expectedRevision) {
            return { status: "changed" };
        }
        if (current.attemptId === capturedAttempt) {
            return { status: "unchanged" };
        }
        if (current.stage !== stage.prepared &&
            !(explicitRetry && (current.stage === stage.dispatching || current.stage === stage.unconfirmed))) {
            fail("DispatchNotAllowed");
        }
        return { status: "apply", record: {
            ...current,
            stage: stage.dispatching, attemptId: capturedAttempt,
            hadUnconfirmedDispatch: current.hadUnconfirmedDispatch || current.stage !== stage.prepared
        } };
    });
}

export function observeAccepted(key, acceptedPayload) {
    requireReady();
    const captured = requireText(acceptedPayload, limits.maximumAcceptedBytes);
    return mutate(key, current => {
        if (!current) {
            return { status: "missing" };
        }
        if (current.stage === stage.accepted) {
            if (current.acceptedPayload !== captured) {
                fail("ContradictoryOutcome");
            }
            return { status: "unchanged" };
        }
        if (current.stage !== stage.dispatching && current.stage !== stage.unconfirmed) {
            fail("ContradictoryOutcome");
        }
        return { status: "apply", record: { ...current, stage: stage.accepted, acceptedPayload: captured } };
    });
}

export function observeAttempt(key, attemptId, outcome) {
    const capturedAttempt = requireUuid(attemptId);
    if (outcome !== stage.rejected && outcome !== stage.unconfirmed) {
        fail("InvalidRequest");
    }
    return mutate(key, current => {
        if (!current) {
            return { status: "missing" };
        }
        if (current.stage !== stage.dispatching || current.attemptId !== capturedAttempt) {
            return { status: "unchanged" };
        }
        const unconfirmed = current.hadUnconfirmedDispatch || outcome === stage.unconfirmed;
        return { status: "apply", record: {
            ...current,
            stage: unconfirmed ? stage.unconfirmed : stage.rejected,
            hadUnconfirmedDispatch: unconfirmed
        } };
    });
}

export function observeClosedAbsent(key, absenceProof) {
    requireReady();
    const captured = requireText(absenceProof, limits.maximumAcceptedBytes);
    return mutate(key, current => {
        if (!current) {
            return { status: "missing" };
        }
        if (current.stage === stage.closedAbsent) {
            return { status: "unchanged" };
        }
        if (current.stage !== stage.dispatching && current.stage !== stage.unconfirmed) {
            fail("ContradictoryOutcome");
        }
        return { status: "apply", record: { ...current, stage: stage.closedAbsent, absenceProof: captured } };
    });
}

function removeAtRevision(key, expectedRevision, allowed) {
    requireInteger(expectedRevision);
    return mutate(key, current => {
        if (!current) {
            return { status: "missing" };
        }
        if (current.revision !== expectedRevision) {
            return { status: "changed" };
        }
        if (!allowed.includes(current.stage)) {
            fail("RemovalNotAllowed");
        }
        return { status: "apply", record: null };
    });
}

export function cancelPrepared(key, expectedRevision) {
    return removeAtRevision(key, expectedRevision, [stage.prepared]);
}

export function clearResolved(key, expectedRevision) {
    return removeAtRevision(key, expectedRevision, [stage.accepted, stage.rejected, stage.closedAbsent]);
}

export function read(key) {
    const captured = captureKey(key);
    return transaction("readonly", (records, _metadata, complete, guard) => {
        const request = records.get(address(captured));
        request.onsuccess = () => guard(() => complete(validateRecord(request.result)));
    });
}

export function list(scope, take, afterKey = null) {
    requireReady();
    const captured = captureScope(scope);
    requireInteger(take);
    if (take > limits.maximumPageSize) {
        fail("PageLimit");
    }
    const after = afterKey === null ? null : captureKey(afterKey);
    if (after && (after.namespace !== captured.namespace || after.callerBinding !== captured.callerBinding)) {
        fail("InvalidRequest");
    }
    const prefix = [captured.namespace, captured.callerBinding];
    const range = IDBKeyRange.bound(after ? address(after) : prefix, [...prefix, []], after !== null, false);
    return transaction("readonly", (records, _metadata, complete, guard) => {
        const items = [];
        const request = records.openCursor(range);
        request.onsuccess = () => guard(() => {
            const cursor = request.result;
            if (!cursor || items.length === take) {
                complete({ items, nextKey: cursor ? items.at(-1).key : null });
                return;
            }
            const record = validateRecord(cursor.value);
            items.push({
                key: record.key, revision: record.revision, stage: record.stage,
                targetProjectId: record.targetProjectId, hadStarterPlan: record.hadStarterPlan,
                hadUnconfirmedDispatch: record.hadUnconfirmedDispatch
            });
            cursor.continue();
        });
    });
}

export function dispose() {
    generation++;
    database?.close();
    database = undefined;
    opening = undefined;
    limits = undefined;
}

function reference(record) {
    const key = captureKey(record.key);
    if (record.schema !== recordSchema || indexedDB.cmp(address(key), record.address) !== 0 ||
        !Object.values(stage).includes(record.stage) || typeof record.hadUnconfirmedDispatch !== "boolean" ||
        typeof record.hadStarterPlan !== "boolean") {
        fail("CorruptReference");
    }
    requireInteger(record.revision);
    requireInteger(record.payloadBytes);
    if (record.targetProjectId !== null) {
        requireUuid(record.targetProjectId);
    }
    return { key, revision: record.revision, stage: record.stage, targetProjectId: record.targetProjectId,
        hadStarterPlan: record.hadStarterPlan, hadUnconfirmedDispatch: record.hadUnconfirmedDispatch, payloadBytes: record.payloadBytes };
}

export function listReferences(take, afterKey = null) {
    requireReady();
    requireInteger(take);
    if (take > limits.maximumPageSize) {
        fail("PageLimit");
    }
    const after = afterKey === null ? null : address(captureKey(afterKey));
    return transaction("readonly", (records, metadata, complete, guard) => {
        const usageRequest = metadata.get(usageKey);
        usageRequest.onsuccess = () => guard(() => {
            const usage = usageRequest.result;
            if (!usage) {
                fail("CorruptUsage");
            }
            requireInteger(usage.count, 0);
            requireInteger(usage.bytes, 0);
            requireInteger(usage.revision, 0);
            const items = [];
            const request = records.openCursor(after ? IDBKeyRange.lowerBound(after, true) : null);
            request.onsuccess = () => guard(() => {
                const cursor = request.result;
                if (!cursor || items.length === take) {
                    complete({ items, nextKey: cursor ? items.at(-1).key : null, count: usage.count, bytes: usage.bytes });
                    return;
                }
                items.push(reference(cursor.value));
                cursor.continue();
            });
        });
    });
}

export function discardEvidence(key, expectedRevision) {
    requireReady();
    const captured = captureKey(key);
    requireInteger(expectedRevision);
    return transaction("readwrite", (records, metadata, complete, guard) => {
        const request = records.get(address(captured));
        request.onsuccess = () => guard(() => {
            const record = request.result;
            if (!record) {
                complete({ status: "missing", reference: null });
                return;
            }
            const current = reference(record);
            if (current.revision !== expectedRevision) {
                complete({ status: "changed", reference: current });
                return;
            }
            if (typeof record.frozenRequest !== "string" ||
                record.acceptedPayload !== null && typeof record.acceptedPayload !== "string" ||
                record.absenceProof !== null && typeof record.absenceProof !== "string" || record.payloadBytes !== measure(record)) {
                fail("CorruptUsage");
            }
            storeRecord(records, metadata, record, null, () => complete({ status: "applied", reference: null }), guard);
        });
    });
}
