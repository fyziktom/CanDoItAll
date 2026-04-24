# Serial CLI Reference Design

## Command parser behavior

- Process input in `FirmwareApp::tick()`.
- Use a fixed-size char buffer.
- Parse lines terminated by `\n` or `\r`.
- Ignore empty lines.
- Trim whitespace.
- Lowercase command tokens for matching.
- Never allocate large dynamic strings in the hot path.

## Suggested class

```cpp
class SerialCommandShell {
 public:
  void begin(FirmwareApp* app);
  void poll();

 private:
  void processLine(const char* line);
  void printHelp();
};
```

It can also be implemented directly in `FirmwareApp` if that is less intrusive.

## Internal update functions

The CLI should call internal functions instead of mutating config everywhere:

```cpp
bool FirmwareApp::setOrientationMode(OrientationReportMode mode, bool reconfigureNow);
bool FirmwareApp::setTelemetryIntervalMs(uint16_t intervalMs);
bool FirmwareApp::setReportIntervals(uint16_t qUs, uint16_t accelUs, uint16_t gyroUs, bool reconfigureNow);
bool FirmwareApp::saveSettings();
bool FirmwareApp::restoreDefaults();
```

## Response style

Prefer compact responses that are easy to parse:

```text
[I3DM][CLI] ok orientation=game active=GAME_RV mag=0
[I3DM][CLI] ok telemetry_ms=10
[I3DM][CLI] err value-out-of-range telemetry_ms min=5 max=40
```

## JSON response

`get json` should print one single-line response. Example:

```json
{"orientation":"game","active":"GAME_RV","mag":false,"fallback":true,"q_us":5000,"accel_us":2500,"gyro_us":2500,"telemetry_ms":10,"drops":0}
```

Do not rely on a full JSON parser on firmware; formatting a JSON-like line is enough for test logs.
