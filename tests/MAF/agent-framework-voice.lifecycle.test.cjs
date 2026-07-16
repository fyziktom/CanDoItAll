const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

const voiceScriptPath = path.resolve(
    __dirname,
    "../../src/MAF/Common/CanDoItAll.AgentFramework.Components/wwwroot/js/agent-framework-voice.js");
const voiceScript = fs.readFileSync(voiceScriptPath, "utf8");

function createHarness({ constructorError = null } = {}) {
    const streams = [];
    const recorders = [];
    const timers = new Map();
    let nextTimerId = 1;

    function createStream() {
        const track = {
            stopCount: 0,
            stop() {
                this.stopCount++;
            }
        };
        const stream = {
            track,
            getTracks() {
                return [track];
            }
        };
        streams.push(stream);
        return stream;
    }

    class MockMediaRecorder {
        constructor(stream) {
            if (constructorError) {
                throw constructorError;
            }

            this.stream = stream;
            this.state = "inactive";
            this.mimeType = "audio/webm";
            this.stopCount = 0;
            recorders.push(this);
        }

        start(chunkMilliseconds) {
            this.chunkMilliseconds = chunkMilliseconds;
            this.state = "recording";
        }

        stop() {
            this.stopCount++;
            this.state = "inactive";
        }
    }

    const window = {
        MediaRecorder: MockMediaRecorder,
        setTimeout(callback, milliseconds) {
            const timerId = nextTimerId++;
            timers.set(timerId, { callback, milliseconds });
            return timerId;
        },
        clearTimeout(timerId) {
            timers.delete(timerId);
        }
    };
    const context = {
        Audio: class {},
        Blob,
        console,
        FileReader: class {},
        Map,
        MediaRecorder: MockMediaRecorder,
        navigator: {
            mediaDevices: {
                async getUserMedia() {
                    return createStream();
                }
            }
        },
        Promise,
        Set,
        Uint8Array,
        URL: {
            createObjectURL() {
                return "blob:mock";
            },
            revokeObjectURL() {
            }
        },
        atob() {
            return "";
        },
        window
    };
    vm.runInNewContext(voiceScript, context, { filename: voiceScriptPath });

    return {
        api: window.CanDoItAll.agentFramework.voice,
        recorders,
        streams,
        timers,
        fireTimer(timerId) {
            const timer = timers.get(timerId);
            assert.ok(timer, `Timer ${timerId} does not exist.`);
            timers.delete(timerId);
            timer.callback();
        }
    };
}

test("releases the acquired stream when MediaRecorder construction fails", async () => {
    const harness = createHarness({ constructorError: new Error("MediaRecorder construction failed.") });

    await assert.rejects(
        harness.api.startRecordingForOwner("constructor-failure"),
        /MediaRecorder construction failed/);

    assert.equal(harness.streams.length, 1);
    assert.equal(harness.streams[0].track.stopCount, 1);
    assert.equal(harness.timers.size, 0);
});

test("watchdog stops tracks and reports the bounded recording limit", async () => {
    const harness = createHarness();

    await harness.api.startRecordingForOwner("watchdog-owner");

    assert.equal(harness.timers.size, 1);
    const [timerId, timer] = [...harness.timers.entries()][0];
    assert.equal(timer.milliseconds, 5 * 60 * 1000);

    harness.fireTimer(timerId);

    assert.equal(harness.recorders[0].stopCount, 1);
    assert.equal(harness.streams[0].track.stopCount, 1);
    assert.throws(
        () => harness.api.stopRecordingForOwner("watchdog-owner"),
        /five-minute limit/);
});

test("disposing one owner does not stop another owner's recording", async () => {
    const harness = createHarness();

    await harness.api.startRecordingForOwner("owner-a");
    await harness.api.startRecordingForOwner("owner-b");

    harness.api.disposeOwner("owner-a");

    assert.equal(harness.streams[0].track.stopCount, 1);
    assert.equal(harness.streams[1].track.stopCount, 0);
    assert.equal(harness.recorders[1].state, "recording");

    harness.api.disposeOwner("owner-b");

    assert.equal(harness.streams[1].track.stopCount, 1);
    assert.equal(harness.timers.size, 0);
});
