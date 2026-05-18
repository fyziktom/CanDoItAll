window.CanDoItAll = window.CanDoItAll || {};
window.CanDoItAll.agentFramework = window.CanDoItAll.agentFramework || {};

window.CanDoItAll.agentFramework.voice = (function () {
    let mediaRecorder = null;
    let stream = null;
    let chunks = [];
    let playbackQueue = Promise.resolve();
    let playbackGeneration = 0;
    let currentAudio = null;
    let currentAudioUrl = null;
    let stopCurrentPlayback = null;
    const queuedAudioPayloads = new Set();

    const recordingChunkMilliseconds = 25000;

    function ensureSupported() {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia || !window.MediaRecorder) {
            throw new Error("Browser audio recording is not available.");
        }
    }

    async function startRecording() {
        ensureSupported();
        if (mediaRecorder && mediaRecorder.state === "recording") {
            throw new Error("Audio recording is already active.");
        }

        stream = await navigator.mediaDevices.getUserMedia({ audio: true });
        chunks = [];
        mediaRecorder = new MediaRecorder(stream);
        mediaRecorder.ondataavailable = event => {
            if (event.data && event.data.size > 0) {
                chunks.push(event.data);
            }
        };
        mediaRecorder.start(recordingChunkMilliseconds);
    }

    function stopRecording() {
        if (!mediaRecorder || mediaRecorder.state !== "recording") {
            throw new Error("Audio recording is not active.");
        }

        return new Promise((resolve, reject) => {
            mediaRecorder.onerror = event => reject(event.error || new Error("Audio recording failed."));
            mediaRecorder.onstop = async () => {
                try {
                    const contentType = mediaRecorder.mimeType || "audio/webm";
                    const recordedChunks = chunks.slice();
                    const recordingChunks = await Promise.all(recordedChunks.map(async (chunk, index) => {
                        const chunkContentType = chunk.type || contentType;
                        return {
                            base64: await blobToBase64(chunk),
                            contentType: chunkContentType,
                            fileName: resolveChunkFileName(chunkContentType, index)
                        };
                    }));
                    const base64 = recordingChunks.length === 1 ? recordingChunks[0].base64 : "";
                    stopTracks();
                    resolve({
                        base64,
                        contentType,
                        fileName: resolveFileName(contentType),
                        chunks: recordingChunks
                    });
                } catch (error) {
                    stopTracks();
                    reject(error);
                }
            };
            mediaRecorder.stop();
        });
    }

    function stopTracks() {
        if (stream) {
            for (const track of stream.getTracks()) {
                track.stop();
            }
        }

        stream = null;
        mediaRecorder = null;
        chunks = [];
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

    function createAudioPayload(base64, contentType) {
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
            url: URL.createObjectURL(blob)
        };
        queuedAudioPayloads.add(payload);
        return payload;
    }

    async function playAudioPayload(payload, generation) {
        if (generation !== playbackGeneration) {
            queuedAudioPayloads.delete(payload);
            URL.revokeObjectURL(payload.url);
            return;
        }

        const audio = new Audio(payload.url);
        currentAudio = audio;
        currentAudioUrl = payload.url;

        return new Promise(async (resolve, reject) => {
            let completed = false;
            const cleanup = () => {
                if (completed) {
                    return;
                }

                completed = true;
                audio.onended = null;
                audio.onerror = null;
                if (currentAudio === audio) {
                    currentAudio = null;
                }

                if (currentAudioUrl === payload.url) {
                    currentAudioUrl = null;
                }

                if (stopCurrentPlayback === stop) {
                    stopCurrentPlayback = null;
                }

                URL.revokeObjectURL(payload.url);
                queuedAudioPayloads.delete(payload);
            };
            const stop = () => {
                audio.pause();
                cleanup();
                resolve();
            };
            stopCurrentPlayback = stop;
            audio.onended = () => {
                cleanup();
                resolve();
            };
            audio.onerror = () => {
                cleanup();
                reject(new Error(`Audio playback failed for ${payload.contentType}.`));
            };

            try {
                await Promise.race([audio.play(), timeoutAfter(5000)]);
            } catch (error) {
                cleanup();
                reject(new Error(`Audio playback failed for ${payload.contentType}: ${error?.message || error}`));
            }
        });
    }

    function clearAudioQueue() {
        playbackGeneration++;
        playbackQueue = Promise.resolve();
        if (stopCurrentPlayback) {
            stopCurrentPlayback();
        }

        if (currentAudio) {
            currentAudio.pause();
            currentAudio = null;
        }

        if (currentAudioUrl) {
            URL.revokeObjectURL(currentAudioUrl);
            currentAudioUrl = null;
        }

        for (const payload of queuedAudioPayloads) {
            URL.revokeObjectURL(payload.url);
        }

        queuedAudioPayloads.clear();
    }

    async function enqueueAudio(base64, contentType) {
        const payload = createAudioPayload(base64, contentType);
        const generation = playbackGeneration;
        playbackQueue = playbackQueue.then(
            () => playAudioPayload(payload, generation).catch(error => console.error(error)),
            () => playAudioPayload(payload, generation).catch(error => console.error(error)));
    }

    async function playAudio(base64, contentType) {
        clearAudioQueue();
        const payload = createAudioPayload(base64, contentType);
        const generation = playbackGeneration;
        try {
            await playAudioPayload(payload, generation);
        } catch (error) {
            throw new Error(error?.message || error);
        }
    }

    return {
        clearAudioQueue,
        enqueueAudio,
        startRecording,
        stopRecording,
        playAudio
    };
})();
