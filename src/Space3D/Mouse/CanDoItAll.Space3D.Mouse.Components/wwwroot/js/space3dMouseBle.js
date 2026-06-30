const BLE_MIDI_SERVICE_UUID = "03b80e5a-ede8-4b33-a751-6ce34ec4c700";
const BLE_MIDI_CHARACTERISTIC_UUID = "7772e5db-3868-4112-a1a9-f2669d106bf3";

let dotNetRef = null;
let bleDevice = null;
let bleServer = null;
let bleCharacteristic = null;
let bleNotificationsAttached = false;
let bleSysExBuffer = null;
let pendingOperation = Promise.resolve();
let lastError = "";
let frameCount = 0;
let rawPacketCount = 0;
let debugCaptureEnabled = true;
let debugPacketCaptureLimit = 96;
let debugPacketCaptureCount = 0;
let debugLog = [];
let lastDebugNotifyAt = 0;
const MAX_SYSEX_FRAME_BYTES = 96;
const DEBUG_LOG_LIMIT = 800;
const DEBUG_NOTIFY_INTERVAL_MS = 250;

function queueOperation(operation) {
    pendingOperation = pendingOperation
        .catch(() => undefined)
        .then(operation);
    return pendingOperation;
}

function webBluetoothSupported() {
    return !!navigator &&
        !!navigator.bluetooth &&
        typeof navigator.bluetooth.requestDevice === "function";
}

function hasActiveUserGesture() {
    return !!navigator &&
        !!navigator.userActivation &&
        navigator.userActivation.isActive;
}

function toErrorMessage(error) {
    if (!error) {
        return "Unknown BLE error.";
    }

    if (typeof error === "string") {
        return error;
    }

    const name = error.name ? String(error.name) : "";
    const message = error.message ? String(error.message) : String(error);
    return name.length > 0 ? `${name}: ${message}` : message;
}

async function tryGetRememberedBleDevice() {
    if (!webBluetoothSupported() ||
        !navigator.bluetooth ||
        typeof navigator.bluetooth.getDevices !== "function") {
        return null;
    }

    let devices = [];
    try {
        devices = await navigator.bluetooth.getDevices();
    } catch {
        return null;
    }

    if (!Array.isArray(devices) || devices.length === 0) {
        return null;
    }

    if (bleDevice && bleDevice.id) {
        const same = devices.find(device => device && device.id === bleDevice.id);
        if (same) {
            return same;
        }
    }

    return devices.find(device =>
        device &&
        typeof device.name === "string" &&
        (device.name.startsWith("I3DM-") || device.name.startsWith("IBNO-"))) || null;
}

function attachBleDevice(device) {
    if (!device) {
        return;
    }

    if (bleDevice) {
        try {
            bleDevice.removeEventListener("gattserverdisconnected", onBleDisconnected);
        } catch {
        }
    }

    bleDevice = device;
    bleDevice.addEventListener("gattserverdisconnected", onBleDisconnected);
}

function buildDiagnostics() {
    return {
        supported: webBluetoothSupported(),
        state: !webBluetoothSupported()
            ? "unsupported"
            : bleNotificationsAttached
                ? "streaming"
                : bleDevice && bleDevice.gatt && bleDevice.gatt.connected
                    ? "connected"
                    : bleDevice
                        ? "selected"
                        : "disconnected",
        selectedDeviceId: bleDevice && bleDevice.id ? String(bleDevice.id) : "",
        selectedDeviceName: bleDevice && bleDevice.name ? String(bleDevice.name) : "",
        hasRememberedDevice: false,
        gattConnected: !!(bleDevice && bleDevice.gatt && bleDevice.gatt.connected),
        notificationsActive: !!bleNotificationsAttached,
        lastError,
        frameCount,
        rawPacketCount,
        debugCaptureEnabled,
        debugPacketCaptureCount
    };
}

async function notifyStatusChanged() {
    if (!dotNetRef) {
        return;
    }

    const diagnostics = buildDiagnostics();
    diagnostics.hasRememberedDevice = !!(await tryGetRememberedBleDevice());
    await dotNetRef.invokeMethodAsync("OnBleStatusChanged", diagnostics);
}

function resetConnectionArtifacts() {
    bleServer = null;
    bleCharacteristic = null;
    bleNotificationsAttached = false;
    bleSysExBuffer = null;
}

function onBleDisconnected() {
    if (bleCharacteristic && bleNotificationsAttached) {
        try {
            bleCharacteristic.removeEventListener("characteristicvaluechanged", handleCharacteristicValueChanged);
        } catch {
        }
    }

    resetConnectionArtifacts();
    void notifyStatusChanged();
}

function bytesToHex(bytes) {
    if (!bytes || typeof bytes.length !== "number") {
        return "";
    }

    const parts = [];
    for (let index = 0; index < bytes.length; index++) {
        parts.push((Number(bytes[index]) & 0xff).toString(16).padStart(2, "0").toUpperCase());
    }

    return parts.join(" ");
}

function appendDebugLog(line) {
    const elapsed = Math.round(performance.now());
    debugLog.push(`${elapsed}ms ${line}`);
    if (debugLog.length > DEBUG_LOG_LIMIT) {
        debugLog.splice(0, debugLog.length - DEBUG_LOG_LIMIT);
    }
}

async function notifyDebugLog(force) {
    if (!dotNetRef) {
        return;
    }

    const now = performance.now();
    if (!force && (now - lastDebugNotifyAt) < DEBUG_NOTIFY_INTERVAL_MS) {
        return;
    }

    lastDebugNotifyAt = now;
    await dotNetRef.invokeMethodAsync(
        "OnBleDebugLog",
        debugLog.slice(),
        rawPacketCount,
        debugCaptureEnabled,
        debugPacketCaptureCount);
}

function traceParser(packetNumber, message) {
    if (packetNumber <= 0) {
        return;
    }

    appendDebugLog(`P#${packetNumber} ${message}`);
}

function isLikelyBleMidiTimestamp(packetBytes, index, insideSysEx) {
    const value = Number(packetBytes[index]) & 0xff;
    if (value < 0x80) {
        return false;
    }

    const next = index + 1 < packetBytes.length
        ? Number(packetBytes[index + 1]) & 0xff
        : -1;

    if (!insideSysEx && next === 0xf0) {
        return true;
    }

    if (insideSysEx && next === 0xf7) {
        return true;
    }

    return value !== 0xf0 && value !== 0xf7;
}

function extractSysExFramesFromBlePacket(packetBytes, tracePacketNumber) {
    const frames = [];
    if (!(packetBytes instanceof Uint8Array) || packetBytes.length === 0) {
        return frames;
    }

    const startIndex = (packetBytes[0] & 0x80) ? 1 : 0;
    traceParser(
        tracePacketNumber,
        `scan start=${startIndex} header=${packetBytes.length > 0 ? (packetBytes[0] & 0xff).toString(16).padStart(2, "0").toUpperCase() : "--"} buf=${bleSysExBuffer ? bleSysExBuffer.length : 0}`);

    for (let index = startIndex; index < packetBytes.length; index++) {
        const value = Number(packetBytes[index]) & 0xff;
        const insideSysEx = bleSysExBuffer !== null;
        if (isLikelyBleMidiTimestamp(packetBytes, index, insideSysEx)) {
            traceParser(tracePacketNumber, `skip timestamp i=${index} v=${value.toString(16).padStart(2, "0").toUpperCase()} inSysEx=${insideSysEx ? 1 : 0}`);
            continue;
        }

        if (value === 0xf0) {
            bleSysExBuffer = [0xf0];
            traceParser(tracePacketNumber, `start sysex i=${index}`);
            continue;
        }

        if (bleSysExBuffer === null) {
            if (value >= 0x80) {
                traceParser(tracePacketNumber, `skip status/no-sysex i=${index} v=${value.toString(16).padStart(2, "0").toUpperCase()}`);
            }
            continue;
        }

        if (value === 0xf7) {
            bleSysExBuffer.push(0xf7);
            frames.push(new Uint8Array(bleSysExBuffer));
            traceParser(tracePacketNumber, `end sysex i=${index} len=${bleSysExBuffer.length} ${bytesToHex(bleSysExBuffer)}`);
            bleSysExBuffer = null;
            continue;
        }

        if (value < 0x80) {
            bleSysExBuffer.push(value);
            if (bleSysExBuffer.length > MAX_SYSEX_FRAME_BYTES) {
                traceParser(tracePacketNumber, `drop sysex too long len=${bleSysExBuffer.length}`);
                bleSysExBuffer = null;
            }
        } else {
            traceParser(tracePacketNumber, `skip high-byte i=${index} v=${value.toString(16).padStart(2, "0").toUpperCase()} buf=${bleSysExBuffer.length}`);
        }
    }

    if (frames.length === 0) {
        traceParser(tracePacketNumber, `no complete frame buf=${bleSysExBuffer ? bleSysExBuffer.length : 0}`);
    }

    return frames;
}

function handleCharacteristicValueChanged(event) {
    if (!dotNetRef) {
        return;
    }

    const source = event && event.target ? event.target.value : null;
    if (!source) {
        return;
    }

    const bytes = new Uint8Array(source.buffer, source.byteOffset, source.byteLength);
    rawPacketCount++;

    const shouldCapture = debugCaptureEnabled && debugPacketCaptureCount < debugPacketCaptureLimit;
    const tracePacketNumber = shouldCapture ? rawPacketCount : 0;
    if (shouldCapture) {
        debugPacketCaptureCount++;
        appendDebugLog(`RAW #${rawPacketCount} len=${bytes.length} ${bytesToHex(bytes)}`);
    }

    const frames = extractSysExFramesFromBlePacket(bytes, tracePacketNumber);
    if (shouldCapture && debugPacketCaptureCount >= debugPacketCaptureLimit) {
        debugCaptureEnabled = false;
        appendDebugLog(`CAPTURE stopped after ${debugPacketCaptureCount} raw packets`);
    }

    if (shouldCapture) {
        void notifyDebugLog(frames.length > 0 || !debugCaptureEnabled);
    }

    if (frames.length === 0) {
        return;
    }

    for (const frame of frames) {
        frameCount++;
        if (shouldCapture) {
            appendDebugLog(`FRAME #${frameCount} len=${frame.length} ${bytesToHex(frame)}`);
            void notifyDebugLog(true);
        }

        dotNetRef.invokeMethodAsync("OnBleSysExFrame", Array.from(frame));
    }
}

async function requestBleDeviceFromUserGesture() {
    if (!hasActiveUserGesture()) {
        throw new Error("A direct user gesture is required to request a BLE device.");
    }

    const selected = await navigator.bluetooth.requestDevice({
        filters: [{ services: [BLE_MIDI_SERVICE_UUID] }],
        optionalServices: [BLE_MIDI_SERVICE_UUID]
    });

    attachBleDevice(selected);
    resetConnectionArtifacts();
}

async function ensureBleConnectionCore() {
    if (!webBluetoothSupported()) {
        throw new Error("Web Bluetooth is not supported in this browser.");
    }

    if (!bleDevice) {
        const remembered = await tryGetRememberedBleDevice();
        if (remembered) {
            attachBleDevice(remembered);
        }
    }

    if (!bleDevice) {
        await requestBleDeviceFromUserGesture();
    }

    if (!bleDevice || !bleDevice.gatt) {
        throw new Error("The selected device does not expose a GATT server.");
    }

    if (!bleDevice.gatt.connected) {
        bleServer = await bleDevice.gatt.connect();
        bleCharacteristic = null;
        bleNotificationsAttached = false;
    } else if (!bleServer) {
        bleServer = bleDevice.gatt;
    }

    if (!bleCharacteristic) {
        const service = await bleServer.getPrimaryService(BLE_MIDI_SERVICE_UUID);
        bleCharacteristic = await service.getCharacteristic(BLE_MIDI_CHARACTERISTIC_UUID);
    }
}

async function ensureBleNotificationsCore() {
    await ensureBleConnectionCore();
    if (!bleCharacteristic) {
        throw new Error("BLE MIDI characteristic is unavailable.");
    }

    if (!bleNotificationsAttached) {
        await bleCharacteristic.startNotifications();
        try {
            bleCharacteristic.removeEventListener("characteristicvaluechanged", handleCharacteristicValueChanged);
        } catch {
        }

        bleCharacteristic.addEventListener("characteristicvaluechanged", handleCharacteristicValueChanged);
        bleNotificationsAttached = true;
    }
}

export async function initialize(dotNetRefArg) {
    dotNetRef = dotNetRefArg || null;
    const diagnostics = buildDiagnostics();
    diagnostics.hasRememberedDevice = !!(await tryGetRememberedBleDevice());
    return diagnostics;
}

export async function connect() {
    return queueOperation(async () => {
        try {
            lastError = "";
            await ensureBleNotificationsCore();
            await notifyStatusChanged();
            return buildDiagnostics();
        } catch (error) {
            lastError = toErrorMessage(error);
            if (dotNetRef) {
                await dotNetRef.invokeMethodAsync("OnBleError", lastError);
            }
            await notifyStatusChanged();
            throw error;
        }
    });
}

export async function startDebugCapture(maxPackets) {
    debugLog = [];
    debugPacketCaptureLimit = Number.isFinite(maxPackets) && maxPackets > 0
        ? Math.max(1, Math.min(512, Math.floor(maxPackets)))
        : 96;
    debugPacketCaptureCount = 0;
    debugCaptureEnabled = true;
    appendDebugLog(`CAPTURE started limit=${debugPacketCaptureLimit}`);
    await notifyDebugLog(true);
    return buildDiagnostics();
}

export async function stopDebugCapture() {
    debugCaptureEnabled = false;
    appendDebugLog(`CAPTURE stopped manually at raw=${rawPacketCount}`);
    await notifyDebugLog(true);
    return buildDiagnostics();
}

export async function clearDebugLog() {
    debugLog = [];
    debugPacketCaptureCount = 0;
    await notifyDebugLog(true);
    return buildDiagnostics();
}

export async function copyDebugLog() {
    const text = debugLog.join("\n");
    if (navigator && navigator.clipboard && typeof navigator.clipboard.writeText === "function") {
        await navigator.clipboard.writeText(text);
    }

    return text;
}

export async function disconnect() {
    return queueOperation(async () => {
        if (bleDevice && bleDevice.gatt && bleDevice.gatt.connected) {
            bleDevice.gatt.disconnect();
        } else {
            resetConnectionArtifacts();
        }

        await notifyStatusChanged();
        return buildDiagnostics();
    });
}

export async function dispose() {
    dotNetRef = null;
}
