# Sub-bundle 01 — Firmware Sensor Source + Runtime Serial Settings

## Goal

Make BNO085 orientation source selection explicit and testable. Add true magnetometer-off orientation mode, expose runtime settings through a non-blocking serial CLI, and keep current telemetry working.

## Why this matters

The existing `OrientationPreference` values (`AbsoluteFirst`, `StabilizedFirst`) do not allow clean A/B testing of magnetometer influence. For a 3D mouse near a laptop, a magnetometer can produce yaw jumps. The default should be a game/no-magnetometer orientation source for smooth control, with magnetometer-assisted modes available for testing.

## Required implementation tasks

### 1. Replace preference-based orientation selection with exact mode selection

Update firmware config types in `AppConfig.h`.

Add an enum similar to:

```cpp
// Comments in source code must remain in English.
enum class OrientationReportMode : uint8_t {
  RotationVectorMag = 0,
  GameRotationVectorNoMag = 1,
  GeomagneticNoGyro = 2,
  ArvrStabilizedMag = 3,
  ArvrStabilizedGameNoMag = 4,
};
```

Also add:

```cpp
enum class OrientationFallbackMode : uint8_t {
  Disabled = 0,
  SafeFallback = 1,
};
```

Update `SensorConfig`:

- Replace `OrientationPreference orientationPreference` with `OrientationReportMode orientationMode`.
- Add `OrientationFallbackMode orientationFallback`.
- Keep `quaternionIntervalUs`, `linearAccelIntervalUs`, `gyroIntervalUs`, `uartBaud`, and queue settings.

Default recommendation:

- `orientationMode = OrientationReportMode::GameRotationVectorNoMag`
- `orientationFallback = OrientationFallbackMode::SafeFallback`
- `quaternionIntervalUs = 5000`
- `linearAccelIntervalUs = 2500`
- `gyroIntervalUs = 2500`
- `TelemetryConfig::intervalMs = 10` or keep 12 if BLE bandwidth is tight. Make it configurable.

### 2. Extend source enum and source names

Update `BnoUartSensor.h`:

- Replace or extend `BnoOrientationSource` to represent at least:
  - `Unknown`
  - `RotationVectorMag`
  - `GameRotationVectorNoMag`
  - `GeomagneticNoGyro`
  - `ArvrStabilizedMag`
  - `ArvrStabilizedGameNoMag`

Add helper functions:

- `const char* orientationReportModeName(OrientationReportMode mode)`
- `const char* bnoOrientationSourceName(BnoOrientationSource source)`
- `bool orientationSourceUsesMagnetometer(BnoOrientationSource source)`

### 3. Inspect installed library constants before coding the final switch

Before implementing, inspect the local Adafruit/SH-2 headers for exact names. Search for:

- `SH2_GAME_ROTATION_VECTOR`
- `SH2_ARVR_STABILIZED_RV`
- any constant that looks like AR/VR-stabilized game rotation vector, for example `SH2_ARVR_STABILIZED_GRV`, `SH2_ARVR_STABILIZED_GAME_RV`, or similar.

Do not assume the AR/VR game constant name. If it is not available in the installed library, keep `ArvrStabilizedGameNoMag` disabled with a clear serial error message and a `#if defined(...)` guard.

### 4. Update BNO report configuration

Refactor `BnoUartSensor::configureOrientationReport()` so it maps the selected exact mode to exactly one desired SH-2 report ID.

Desired mapping:

| OrientationReportMode | Required SH-2 report | Magnetometer |
|---|---|---|
| `RotationVectorMag` | `SH2_ROTATION_VECTOR` | Yes |
| `GameRotationVectorNoMag` | `SH2_GAME_ROTATION_VECTOR` | No |
| `GeomagneticNoGyro` | `SH2_GEOMAGNETIC_ROTATION_VECTOR` | Yes, no gyro |
| `ArvrStabilizedMag` | `SH2_ARVR_STABILIZED_RV` | Yes |
| `ArvrStabilizedGameNoMag` | library-specific AR/VR game vector constant | No |

If `orientationFallback == Disabled`, fail report configuration when the requested report is unavailable.

If `orientationFallback == SafeFallback`, try a deterministic fallback order and log the fallback. Suggested fallback order:

1. Requested exact mode.
2. `GameRotationVectorNoMag`.
3. `RotationVectorMag`.
4. `ArvrStabilizedMag`.
5. `GeomagneticNoGyro`.

Keep fallback logs visible in serial health output.

### 5. Decode quaternion for game rotation vector

Current code has `quaternionFromVectorWAcc(const sh2_RotationVectorWAcc_t&)` for rotation-vector-like reports.

Add decoding support for game rotation vector payload type. Inspect `sh2_SensorValue_t` union field names in headers. It is often shaped like `event.un.gameRotationVector.real`, `i`, `j`, `k`, but verify.

Add helper functions like:

```cpp
Quaternion quaternionFromRotationVectorWAcc(const sh2_RotationVectorWAcc_t& vector);
Quaternion quaternionFromGameRotationVector(const sh2_GameRotationVector_t& vector);
```

Then update the event switch in `taskLoop()` to accept and process all supported orientation report IDs.

### 6. Runtime reconfigure support

Add a safe reconfiguration path in `BnoUartSensor`:

- `void requestReconfigure(const SensorConfig& config);`
- Or `bool updateConfig(const SensorConfig& config);`

Requirements:

- Do not destroy the FreeRTOS queue.
- Stop using stale orientation source labels after reconfigure.
- Clear `haveOrientation_`, `haveLinearAccel_`, and `haveGyro_` when reconfiguring.
- Increment a reconfiguration counter in the sensor snapshot.
- Prefer reconfigure without reboot. If the library cannot disable reports cleanly, document and implement a safe sensor-task reset/restart path.

### 7. Add non-blocking serial CLI

Add a small command shell, either as new files or inside `FirmwareApp` if that keeps the patch smaller.

Required behavior:

- Accumulate serial characters in a fixed-size buffer, max 128 or 160 bytes.
- Parse on `\n` or `\r`.
- Never block waiting for input.
- Print concise machine-readable responses.

Required commands:

```text
help
get
get json
set orientation rotation
set orientation game
set orientation geomag
set orientation arvr
set orientation arvr-game
set fallback on
set fallback off
set q_us 5000
set accel_us 2500
set gyro_us 2500
set telemetry_ms 10
save
defaults
runtime-reset
sensor-reconfigure
cal capture
cal clear
```

Suggested responses:

```text
[I3DM][CLI] ok orientation=game source=GAME_RV mag=0
[I3DM][CLI] ok telemetry_ms=10
[I3DM][CLI] err unsupported-orientation arvr-game
[I3DM][CLI] settings {"orientation":"game","fallback":true,"q_us":5000,"accel_us":2500,"gyro_us":2500,"telemetry_ms":10}
```

`cal capture` may use the latest valid sample. If no valid sample is available, return an error.

### 8. Persist settings carefully

If the existing persistence layer stores `FirmwareConfig`, bump the config version and make old values migrate safely.

Requirements:

- Old config should not crash or produce invalid orientation mode.
- Invalid saved mode should reset to `GameRotationVectorNoMag`.
- `validateConfig()` must clamp intervals and telemetry rate.

Suggested clamps:

- `quaternionIntervalUs`: 2500 to 20000.
- `linearAccelIntervalUs`: 2500 to 20000.
- `gyroIntervalUs`: 2500 to 20000.
- `telemetry.intervalMs`: 5 to 40.
- `queueDepth`: 32 to 256.

### 9. Health/logging updates

Update logs to include:

- requested orientation mode,
- active source,
- whether the source uses magnetometer,
- report intervals,
- telemetry interval,
- reconfigure count,
- queue depth/drops.

Do not print on every telemetry frame.

## Acceptance criteria

- Firmware compiles in default build.
- Firmware compiles with `ID3M_GPIO36_AS_ADC` build flag.
- Serial CLI `get json` prints the active settings.
- Serial CLI can switch between `game` and `rotation` without reflashing.
- `game` mode reports an active source that is marked `mag=0`.
- `rotation` mode reports an active source that is marked `mag=1`.
- Queue drops remain near zero during a 5-minute idle run at the default report rates.
- BLE mouse telemetry continues to send frames after orientation mode changes.

## Arduino MCP validation procedure

1. Compile firmware default build.
2. Compile firmware with ADC flag.
3. Upload firmware.
4. Open serial at the board's normal monitor baud.
5. Send:

```text
get json
set orientation game
sensor-reconfigure
get json
set orientation rotation
sensor-reconfigure
get json
set telemetry_ms 10
set q_us 5000
save
```

6. Capture at least 30 seconds of logs.
7. Confirm active source and magnetometer flag change as expected.
8. Confirm no watchdog resets and no report configuration loop.

## Notes for Codex

- If the installed library does not support AR/VR game rotation vector, do not invent a constant. Keep `arvr-game` as an unsupported option and return a clear error.
- Keep the old UI/host able to parse frames even if the extended source enum cannot be fully represented in v1.03. Sub-bundle 02 covers source encoding if needed.
