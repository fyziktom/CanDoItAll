window.CanDoItAll = window.CanDoItAll || {};
window.CanDoItAll.agentFramework = window.CanDoItAll.agentFramework || {};

window.CanDoItAll.agentFramework.voice = (function () {
    const legacyOwnerId = "__legacy__";
    const ownerStates = new Map();
    const recordingChunkMilliseconds = 25000;
    const maximumRecordingMilliseconds = 5 * 60 * 1000;

    function normalizeOwnerId(ownerId) {
        if (typeof ownerId !== "string" || !ownerId.trim()) {
            throw new Error("Audio owner id is required.");
        }

        return ownerId.trim();
    }

    function createOwnerState(ownerId) {
        return {
            ownerId,
            disposed: false,
            recordingGeneration: 0,
            mediaRecorder: null,
            stream: null,
            chunks: [],
            pendingRecordingStop: null,
            recordingWatchdogId: null,
            recordingFailure: null,
            playbackQueue: Promise.resolve(),
            playbackGeneration: 0,
            currentAudio: null,
            currentAudioUrl: null,
            stopCurrentPlayback: null,
            queuedAudioPayloads: new Set()
        };
    }

    function getOrCreateOwnerState(ownerId) {
        const normalizedOwnerId = normalizeOwnerId(ownerId);
        let state = ownerStates.get(normalizedOwnerId);
        if (!state) {
            state = createOwnerState(normalizedOwnerId);
            ownerStates.set(normalizedOwnerId, state);
        }

        return state;
    }

    function getOwnerState(ownerId) {
        return ownerStates.get(normalizeOwnerId(ownerId)) || null;
    }

    function ensureSupported() {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia || !window.MediaRecorder) {
            throw new Error("Browser audio recording is not available.");
        }
    }

    async function startRecordingForOwner(ownerId) {
        ensureSupported();
        const state = getOrCreateOwnerState(ownerId);
        if (state.pendingRecordingStop) {
            throw new Error("Audio recording is still being processed for this owner.");
        }

        if (state.mediaRecorder && state.mediaRecorder.state !== "inactive") {
            throw new Error("Audio recording is already active for this owner.");
        }

        if (state.mediaRecorder) {
            releaseRecording(state, state.mediaRecorder);
        }

        state.recordingFailure = null;
        const generation = ++state.recordingGeneration;
        const acquiredStream = await navigator.mediaDevices.getUserMedia({ audio: true });
        if (state.disposed ||
            state.recordingGeneration !== generation ||
            ownerStates.get(state.ownerId) !== state) {
            stopMediaStream(acquiredStream);
            throw new Error("Audio recording owner was disposed.");
        }

        let recorder;
        try {
            recorder = new MediaRecorder(acquiredStream);
        } catch (error) {
            stopMediaStream(acquiredStream);
            throw error;
        }

        state.stream = acquiredStream;
        state.chunks = [];
        state.mediaRecorder = recorder;
        recorder.ondataavailable = event => {
            if (state.mediaRecorder === recorder && event.data && event.data.size > 0) {
                state.chunks.push(event.data);
            }
        };

        try {
            recorder.start(recordingChunkMilliseconds);
            armRecordingWatchdog(state, recorder);
        } catch (error) {
            releaseRecording(state, recorder);
            throw error;
        }
    }

    function stopRecordingForOwner(ownerId) {
        const state = getOwnerState(ownerId);
        if (state?.recordingFailure) {
            const recordingFailure = state.recordingFailure;
            state.recordingFailure = null;
            throw recordingFailure;
        }

        const recorder = state?.mediaRecorder;
        if (!state || !recorder || recorder.state !== "recording") {
            throw new Error("Audio recording is not active for this owner.");
        }

        return new Promise((resolve, reject) => {
            let settled = false;
            const complete = action => {
                if (settled) {
                    return;
                }

                settled = true;
                if (state.pendingRecordingStop?.recorder === recorder) {
                    state.pendingRecordingStop = null;
                }

                releaseRecording(state, recorder);
                action();
            };

            state.pendingRecordingStop = {
                recorder,
                cancel: error => complete(() => reject(error))
            };
            recorder.onerror = event => complete(
                () => reject(event.error || new Error("Audio recording failed.")));
            recorder.onstop = async () => {
                const contentType = recorder.mimeType || "audio/webm";
                const recordedChunks = state.chunks.slice();
                releaseRecording(state, recorder);
                try {
                    const recordingChunks = await Promise.all(recordedChunks.map(async (chunk, index) => {
                        const chunkContentType = chunk.type || contentType;
                        return {
                            base64: await blobToBase64(chunk),
                            contentType: chunkContentType,
                            fileName: resolveChunkFileName(chunkContentType, index)
                        };
                    }));
                    const base64 = recordingChunks.length === 1 ? recordingChunks[0].base64 : "";
                    complete(() => resolve({
                        base64,
                        contentType,
                        fileName: resolveFileName(contentType),
                        chunks: recordingChunks
                    }));
                } catch (error) {
                    complete(() => reject(error));
                }
            };

            try {
                recorder.stop();
            } catch (error) {
                complete(() => reject(error));
            }
        });
    }

    function releaseRecording(state, recorder) {
        if (recorder) {
            recorder.ondataavailable = null;
            recorder.onerror = null;
            recorder.onstop = null;
            if (recorder.state !== "inactive") {
                try {
                    recorder.stop();
                } catch {
                }
            }
        }

        if (state.mediaRecorder !== recorder) {
            return;
        }

        clearRecordingWatchdog(state);
        stopMediaStream(state.stream);
        state.stream = null;
        state.mediaRecorder = null;
        state.chunks = [];
    }

    function armRecordingWatchdog(state, recorder) {
        clearRecordingWatchdog(state);
        state.recordingWatchdogId = window.setTimeout(() => {
            state.recordingWatchdogId = null;
            if (state.disposed ||
                state.mediaRecorder !== recorder ||
                recorder.state === "inactive") {
                return;
            }

            const error = new Error("Audio recording reached the five-minute limit and was stopped.");
            if (state.pendingRecordingStop?.recorder === recorder) {
                state.pendingRecordingStop.cancel(error);
                return;
            }

            state.recordingFailure = error;
            releaseRecording(state, recorder);
        }, maximumRecordingMilliseconds);
    }

    function clearRecordingWatchdog(state) {
        if (state.recordingWatchdogId === null) {
            return;
        }

        window.clearTimeout(state.recordingWatchdogId);
        state.recordingWatchdogId = null;
    }

    function stopMediaStream(mediaStream) {
        if (!mediaStream) {
            return;
        }

        for (const track of mediaStream.getTracks()) {
            track.stop();
        }
    }

    function blobToBase64(blob) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onloadend = () => {
                const value = reader.result || "";
                const text = value.toString();
                const commaIndex = text.indexOf(",");
                resolve(commaIndex >= 0 ? text.substring(commaIndex + 1) : text);
            };
            reader.onerror = () => reject(reader.error || new Error("Audio recording could not be read."));
            reader.readAsDataURL(blob);
        });
    }

    function resolveFileName(contentType) {
        if (contentType.includes("wav")) {
            return "voice-input.wav";
        }

        if (contentType.includes("mp4")) {
            return "voice-input.mp4";
        }

        return "voice-input.webm";
    }

    function resolveChunkFileName(contentType, index) {
        const fileName = resolveFileName(contentType);
        const extensionIndex = fileName.lastIndexOf(".");
        const suffix = `-${index + 1}`;
        if (extensionIndex < 0) {
            return `${fileName}${suffix}`;
        }

        return `${fileName.substring(0, extensionIndex)}${suffix}${fileName.substring(extensionIndex)}`;
    }

    function normalizePlaybackContentType(contentType) {
        const normalized = (contentType || "audio/mpeg").trim().toLowerCase();
        if (normalized === "audio/opus") {
            return "audio/ogg; codecs=opus";
        }

        if (normalized === "audio/pcm") {
            return "audio/wav";
        }

        return normalized;
    }

    function base64ToBytes(base64) {
        const binary = atob(base64.replace(/\s/g, ""));
        const bytes = new Uint8Array(binary.length);
        for (let index = 0; index < binary.length; index++) {
            bytes[index] = binary.charCodeAt(index);
        }

        return bytes;
    }

    function timeoutAfter(milliseconds) {
        return new Promise((_, reject) => {
            window.setTimeout(() => reject(new Error("Audio playback did not start in time.")), milliseconds);
        });
    }

    function createAudioPayload(state, base64, contentType) {
        if (!base64) {
            throw new Error("Audio payload is empty.");
        }

        const playbackContentType = normalizePlaybackContentType(contentType);
        const support = new Audio().canPlayType(playbackContentType);
        if (!support) {
            throw new Error(`Browser audio playback does not support ${playbackContentType}.`);
        }

        const blob = new Blob([base64ToBytes(base64)], { type: playbackContentType });
        const payload = {
            contentType: playbackContentType,
            url: URL.createObjectURL(blob),
            released: false
        };
        state.queuedAudioPayloads.add(payload);
        return payload;
    }

    function releaseAudioPayload(state, payload) {
        if (payload.released) {
            return;
        }

        payload.released = true;
        state.queuedAudioPayloads.delete(payload);
        URL.revokeObjectURL(payload.url);
    }

    async function playAudioPayload(state, payload, generation) {
        if (state.disposed || generation !== state.playbackGeneration) {
            releaseAudioPayload(state, payload);
            return;
        }

        let audio;
        try {
            audio = new Audio(payload.url);
        } catch (error) {
            releaseAudioPayload(state, payload);
            throw error;
        }

        state.currentAudio = audio;
        state.currentAudioUrl = payload.url;

        return new Promise((resolve, reject) => {
            let completed = false;
            const cleanup = () => {
                if (completed) {
                    return;
                }

                completed = true;
                audio.onended = null;
                audio.onerror = null;
                if (state.currentAudio === audio) {
                    state.currentAudio = null;
                }

                if (state.currentAudioUrl === payload.url) {
                    state.currentAudioUrl = null;
                }

                if (state.stopCurrentPlayback === stop) {
                    state.stopCurrentPlayback = null;
                }

                releaseAudioPayload(state, payload);
            };
            const stop = () => {
                audio.pause();
                cleanup();
                resolve();
            };
            state.stopCurrentPlayback = stop;
            audio.onended = () => {
                cleanup();
                resolve();
            };
            audio.onerror = () => {
                cleanup();
                reject(new Error(`Audio playback failed for ${payload.contentType}.`));
            };

            const rejectPlayback = error => {
                cleanup();
                reject(new Error(`Audio playback failed for ${payload.contentType}: ${error?.message || error}`));
            };
            try {
                Promise.race([audio.play(), timeoutAfter(5000)]).catch(rejectPlayback);
            } catch (error) {
                rejectPlayback(error);
            }
        });
    }

    function clearAudioQueueForState(state) {
        state.playbackGeneration++;
        state.playbackQueue = Promise.resolve();
        if (state.stopCurrentPlayback) {
            state.stopCurrentPlayback();
        }

        if (state.currentAudio) {
            state.currentAudio.pause();
            state.currentAudio = null;
        }

        if (state.currentAudioUrl) {
            URL.revokeObjectURL(state.currentAudioUrl);
            state.currentAudioUrl = null;
        }

        for (const payload of [...state.queuedAudioPayloads]) {
            releaseAudioPayload(state, payload);
        }
    }

    function clearAudioQueueForOwner(ownerId) {
        clearAudioQueueForState(getOrCreateOwnerState(ownerId));
    }

    async function enqueueAudioForOwner(ownerId, base64, contentType) {
        const state = getOrCreateOwnerState(ownerId);
        const payload = createAudioPayload(state, base64, contentType);
        const generation = state.playbackGeneration;
        state.playbackQueue = state.playbackQueue.then(
            () => playAudioPayload(state, payload, generation).catch(error => console.error(error)),
            () => playAudioPayload(state, payload, generation).catch(error => console.error(error)));
    }

    async function playAudioForOwner(ownerId, base64, contentType) {
        const state = getOrCreateOwnerState(ownerId);
        clearAudioQueueForState(state);
        const payload = createAudioPayload(state, base64, contentType);
        const generation = state.playbackGeneration;
        try {
            await playAudioPayload(state, payload, generation);
        } catch (error) {
            throw new Error(error?.message || error);
        }
    }

    function disposeOwner(ownerId) {
        const normalizedOwnerId = normalizeOwnerId(ownerId);
        const state = ownerStates.get(normalizedOwnerId);
        if (!state) {
            return;
        }

        state.disposed = true;
        state.recordingGeneration++;
        if (state.pendingRecordingStop) {
            state.pendingRecordingStop.cancel(new Error("Audio recording owner was disposed."));
        } else {
            releaseRecording(state, state.mediaRecorder);
        }

        state.pendingRecordingStop = null;
        state.recordingFailure = null;
        clearRecordingWatchdog(state);
        clearAudioQueueForState(state);
        ownerStates.delete(normalizedOwnerId);
    }

    function startRecording() {
        return startRecordingForOwner(legacyOwnerId);
    }

    function stopRecording() {
        return stopRecordingForOwner(legacyOwnerId);
    }

    function clearAudioQueue() {
        clearAudioQueueForOwner(legacyOwnerId);
    }

    function enqueueAudio(base64, contentType) {
        return enqueueAudioForOwner(legacyOwnerId, base64, contentType);
    }

    function playAudio(base64, contentType) {
        return playAudioForOwner(legacyOwnerId, base64, contentType);
    }

    return {
        clearAudioQueue,
        clearAudioQueueForOwner,
        disposeOwner,
        enqueueAudio,
        enqueueAudioForOwner,
        playAudio,
        playAudioForOwner,
        startRecording,
        startRecordingForOwner,
        stopRecording,
        stopRecordingForOwner
    };
})();
