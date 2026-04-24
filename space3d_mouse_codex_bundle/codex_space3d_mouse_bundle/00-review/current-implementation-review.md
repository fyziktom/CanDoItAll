# Current Implementation Review

## Executive summary

The current implementation is already structurally good: firmware collects BNO085 orientation/gyro/linear acceleration, packs a compact BLE MIDI SysEx frame, the browser parses it, and the Blazor workbench maps button holds to pan/rotate/zoom actions. The main control-feel problems are not caused by BLE itself. They are mostly caused by frame-rate-dependent pan/zoom, raw acceleration usage, fixed hardcoded thresholds, and missing runtime settings.

The largest firmware gap is orientation source selection. The current `StabilizedFirst` option is not equivalent to magnetometer-off operation. It currently tries `SH2_ARVR_STABILIZED_RV`, `SH2_GEOMAGNETIC_ROTATION_VECTOR`, and `SH2_ROTATION_VECTOR`; it does not request `SH2_GAME_ROTATION_VECTOR` or an AR/VR-stabilized game rotation vector. Therefore the host cannot currently compare magnetometer-on vs magnetometer-off behavior cleanly.

## Firmware findings

### 1. Orientation report selection is preference-based and does not include true game/no-magnetometer mode

File: `src/fw/CanDoItAll.Space3D.Mouse.BNO085/src/BnoUartSensor.cpp`

Relevant lines from the reviewed source:

- `configureOrientationReport()` chooses from two hardcoded candidate arrays around lines 85-112.
- `kAbsoluteFirst`: `SH2_ROTATION_VECTOR`, `SH2_GEOMAGNETIC_ROTATION_VECTOR`, `SH2_ARVR_STABILIZED_RV`.
- `kStabilizedFirst`: `SH2_ARVR_STABILIZED_RV`, `SH2_GEOMAGNETIC_ROTATION_VECTOR`, `SH2_ROTATION_VECTOR`.
- `sourceFromReportId()` maps only `SH2_GEOMAGNETIC_ROTATION_VECTOR`, `SH2_ROTATION_VECTOR`, and `SH2_ARVR_STABILIZED_RV` around lines 22-33.

Impact:

- There is no clean setting for `GameRotationVectorNoMag`.
- Magnetometer influence cannot be switched off explicitly.
- The UI may label a source as stabilized while the underlying SH-2 report may still be magnetometer-assisted.

Required fix:

- Replace `OrientationPreference` with an exact runtime setting like `OrientationReportMode`.
- Add `GameRotationVectorNoMag` and, if supported by the installed SH-2/Adafruit headers, `ArvrStabilizedGameNoMag`.
- Keep optional fallback behavior separate from the selected mode.

### 2. Config has report rates but no runtime control surface

File: `src/fw/CanDoItAll.Space3D.Mouse.BNO085/src/AppConfig.h`

Current defaults:

- Quaternion interval: 5000 us.
- Linear acceleration interval: 2500 us.
- Gyro interval: 2500 us.
- Telemetry interval: 12 ms.
- UART baud field exists.

Impact:

- Testing sensitivity/smoothing requires recompilation or code edits.
- Codex/Arduino MCP cannot easily run A/B tests by sending serial commands.

Required fix:

- Add serial CLI commands for orientation source, fallback, report intervals, telemetry interval, and reset/defaults/save.
- Keep runtime settings validated by `validateConfig()`.

### 3. UART baud setting appears unused by the current `begin_UART()` call

File: `src/fw/CanDoItAll.Space3D.Mouse.BNO085/src/BnoUartSensor.cpp`

Relevant lines:

- `serial_.setPins(...)` and `serial_.setRxBufferSize(2048)` around lines 173-175.
- `bno_.begin_UART(&serial_)` around line 196.

Impact:

- `SensorConfig::uartBaud` exists but may not actually be applied depending on the library signature.

Required fix:

- Inspect the installed `Adafruit_BNO08x` library signature.
- If a baud argument is supported, pass `config_.uartBaud`.
- If not supported, explicitly initialize the serial port before `begin_UART()` only if compatible with the library; otherwise document that the setting is currently informational.

### 4. Firmware motion model computes filtered acceleration/gyro but telemetry sends raw values

Files:

- `MouseMotionModel.cpp`
- `MouseTelemetryProtocol.cpp`

Relevant lines:

- `MouseMotionModel.cpp` computes `filteredLinearAccelG` and `filteredGyroDps` around lines 85-95.
- It then stores raw `linearAccelG` and raw `gyroDps` around lines 104-106.
- `MouseTelemetryProtocol.cpp` sends `motion.gyroDps` and `motion.linearAccelG` around lines 129-131.

Impact:

- The browser receives raw acceleration and raw gyro.
- Pan/zoom depend directly on noisy acceleration.
- Firmware-filtered values only affect diagnostics, not navigation.

Required fix:

- Prefer host-side filtering for navigation so settings can be changed live.
- Keep firmware raw telemetry available.
- Optionally add protocol v1.04 fields for filtered values only if there is a strong reason. Avoid enlarging telemetry unless needed.

### 5. Existing BLE receive path exists but is not connected to app-level settings

File: `BleMidiBackendEsp32BleMidi.*` and `FirmwareApp.*`

Impact:

- The firmware can likely receive SysEx, but no app callback currently applies settings.
- Browser JS has no write path for settings either.

Required fix:

- Required: add a serial CLI for settings because Arduino MCP can validate it.
- Optional: add BLE settings SysEx later and browser write support.

## Host/browser findings

### 1. Pan is frame-rate dependent

File: `CanDoItAll.Space3D.Mouse.Sandbox/Components/Pages/Space3DProcessWorkbench.razor`

Relevant lines:

- `deltaSeconds` is computed at line 364.
- Pan uses acceleration directly at lines 367-372:
  - `panX = ApplyDeadzone(accel.X, AccelDeadzoneG) * 155d`
  - `panY = ApplyDeadzone(accel.Z + accel.Y * 0.35d, AccelDeadzoneG) * 155d`
  - `PanAsync(panX, panY)`

Impact:

- A 100 Hz telemetry stream pans roughly twice as far per second as a 50 Hz stream.
- This makes sensitivity inconsistent and can feel twitchy.

Required fix:

- Convert acceleration to a pan rate and multiply by `deltaSeconds`.
- Add max-rate clamp.
- Use filtered acceleration, not raw acceleration.

### 2. Zoom is frame-rate dependent

Same file, relevant lines 387-395:

- `factor = 1d + Math.Clamp(zoomSignal * 0.18d, -0.12d, 0.12d)`
- `ZoomAsync(factor)`

Impact:

- The zoom factor is applied per received telemetry frame, so total zoom rate changes with frame rate.
- Multiplicative zoom should use elapsed time.

Required fix:

- Convert input to log-zoom rate per second.
- Apply `factor = Math.Exp(zoomRatePerSecond * deltaSeconds)`.
- Use filtered acceleration and a soft deadzone.

### 3. Rotation is time-scaled but still raw and hardcoded

Same file, relevant lines 376-384:

- Rotation uses `gyro.Z` and `gyro.X` with `deltaSeconds`.
- This is the right basic structure, but it uses raw gyro and fixed constants.

Impact:

- Rotation can be smoother with host-side filtered gyro, soft deadzone, sensitivity settings, and optional precision mode.

Required fix:

- Use a host control filter.
- Make gyro deadzone, smoothing, sensitivity, and exponent configurable.

### 4. Pose transform has hardcoded pointer smoothing/deadzones

File: `CanDoItAll.Space3D.Mouse.Driver/Scene/MouseLabPoseTransform.cs`

Relevant lines:

- `PointerYawFullScaleDeg = 55`
- `PointerPitchFullScaleDeg = 55`
- `PointerDeadzoneDeg = 2.25`
- `PointerSnapMagnitude = 0.035`
- `PointerSmoothingTauMs = 42`

Impact:

- Pointer feel cannot be adjusted from UI.
- It is hard to test whether jitter comes from orientation, pointer mapping, or host UI throttling.

Required fix:

- Move these constants into settings or add a settings object consumed by `MouseLabPoseTransform`.

### 5. MouseLab UI throttles visual updates to 20 ms

File: `CanDoItAll.Space3D.Mouse.Components/Components/MouseLab.razor`

Relevant line:

- `MinUiRefreshInterval = TimeSpan.FromMilliseconds(20d)` around line 511.

Impact:

- UI visualization is limited to about 50 Hz even if BLE telemetry is higher.
- This is not necessarily wrong, but motion commands should not be tied to this UI redraw throttle.

Required fix:

- Keep command processing on telemetry events.
- Consider making UI throttle configurable or set to 16 ms for 60 Hz diagnostics.
- Do not over-render WebGL; prefer stable command processing over visual refresh frequency.

### 6. Host lacks a navigation abstraction

Current workbench logic directly maps button holds to `PanAsync`, `OrbitAsync`, and `ZoomAsync` in the Razor component.

Impact:

- Hard to unit test.
- Hard to validate frame-rate invariance.
- Hard to expose settings cleanly.

Required fix:

- Add a driver-layer `Space3DMouseControlFilter` / `NavigationMapper` class.
- Unit test it with synthetic telemetry sequences.
- Keep Razor as a thin coordinator.

## Recommended design direction

Use BNO085 as:

- orientation source for pointer/object orientation,
- gyro source for orbit/rotation,
- linear acceleration source for pan/zoom intent and gestures.

Do not integrate acceleration to position. For navigation, map filtered acceleration to rates. Rates must be time-scaled before applying to the scene.

## Minimum acceptance criteria

- Pan/zoom results are approximately independent of telemetry rate.
- Rotation is smoother under moderate hand tremor.
- Magnetometer-on and magnetometer-off modes can be selected without recompiling.
- Settings can be changed during testing, at least over serial.
- Existing BLE telemetry still parses in the browser.
