window.CanDoItAll = window.CanDoItAll || {};
window.CanDoItAll.agentFramework = window.CanDoItAll.agentFramework || {};

window.CanDoItAll.agentFramework.voice = (function () {
    let mediaRecorder = null;
    let stream = null;
    let chunks = [];

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
        mediaRecorder.start();
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
                    const blob = new Blob(chunks, { type: contentType });
                    const base64 = await blobToBase64(blob);
                    stopTracks();
                    resolve({
                        base64,
                        contentType,
                        fileName: resolveFileName(contentType)
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

    async function playAudio(base64, contentType) {
        if (!base64) {
            throw new Error("Audio payload is empty.");
        }

        const playbackContentType = normalizePlaybackContentType(contentType);
        const support = new Audio().canPlayType(playbackContentType);
        if (!support) {
            throw new Error(`Browser audio playback does not support ${playbackContentType}.`);
        }

        const blob = new Blob([base64ToBytes(base64)], { type: playbackContentType });
        const url = URL.createObjectURL(blob);
        const audio = new Audio(url);
        audio.onended = () => URL.revokeObjectURL(url);
        audio.onerror = () => URL.revokeObjectURL(url);
        try {
            await Promise.race([audio.play(), timeoutAfter(5000)]);
        } catch (error) {
            URL.revokeObjectURL(url);
            throw new Error(`Audio playback failed for ${playbackContentType}: ${error?.message || error}`);
        }
    }

    return {
        startRecording,
        stopRecording,
        playAudio
    };
})();
