# Space3D Mouse BNO085 Smoothing + Settings Codex Bundle

## Mission

Improve the current Space3D mouse implementation so that BNO085-based navigation is smoother, more configurable, and easier to validate. The current implementation already works, but pan/zoom sensitivity is frame-rate dependent, acceleration is used too directly, rotation could be smoother, and the firmware does not yet provide a true magnetometer-off orientation mode.

This bundle is intentionally split into sub-bundles. Implement them in order and do not skip validation checkpoints. Keep the old BLE telemetry path working throughout the work.

## Hard constraints

- Source-code comments must be in English.
- Do not remove the existing BLE telemetry flow.
- Preserve protocol v1.03 telemetry parsing on the host. Add v1.04 support only if needed for extended orientation source/status data.
- Browser BLE connect/reconnect/manual test will be performed by the user. Codex should still keep the code buildable and add diagnostics/checklists.
- Codex may use Arduino MCP and a serial connection to the device. Prefer serial CLI validation for firmware settings because it can be tested without browser BLE.
- Avoid blocking loops on the ESP32 main loop. Serial command handling must be non-blocking.
- Runtime settings must have safe defaults that keep the controller usable even if no saved profile exists.

## Repository areas to inspect first

Firmware:

- `src/fw/CanDoItAll.Space3D.Mouse.BNO085/src/AppConfig.h`
- `src/fw/CanDoItAll.Space3D.Mouse.BNO085/src/BnoUartSensor.h`
- `src/fw/CanDoItAll.Space3D.Mouse.BNO085/src/BnoUartSensor.cpp`
- `src/fw/CanDoItAll.Space3D.Mouse.BNO085/src/MouseMotionModel.h`
- `src/fw/CanDoItAll.Space3D.Mouse.BNO085/src/MouseMotionModel.cpp`
- `src/fw/CanDoItAll.Space3D.Mouse.BNO085/src/MouseTelemetryProtocol.*`
- `src/fw/CanDoItAll.Space3D.Mouse.BNO085/src/FirmwareApp.*`
- `src/fw/CanDoItAll.Space3D.Mouse.BNO085/PROTOCOL.md`
- `src/fw/CanDoItAll.Space3D.Mouse.BNO085/VALIDATION.md`

Host/browser:

- `CanDoItAll.Space3D.Mouse.Driver/Protocol/Space3DMouseProtocol.cs`
- `CanDoItAll.Space3D.Mouse.Driver/Protocol/Space3DMouseButtons.cs`
- `CanDoItAll.Space3D.Mouse.Driver/Scene/MouseLabPoseTransform.cs`
- `CanDoItAll.Space3D.Mouse.Components/Components/MouseLab.razor`
- `CanDoItAll.Space3D.Mouse.Components/wwwroot/js/space3dMouseBle.js`
- `CanDoItAll.Space3D.Mouse.Sandbox/Components/Pages/Space3DProcessWorkbench.razor`
- `CanDoItAll.Space3D.Mouse.Sandbox/Space3DProcessWorkbenchSession.cs`

## Sub-bundles

1. `01-sub-bundle-firmware-sensor-source` — make orientation source selection explicit, add true magnetometer-off modes, and expose serial settings.
2. `02-sub-bundle-firmware-control-protocol` — optional BLE/settings protocol extension and protocol docs/tests.
3. `03-sub-bundle-host-motion-filter-settings` — fix pan/zoom frame-rate dependence, add host-side filters/settings, smooth rotation.
4. `04-sub-bundle-host-ui-diagnostics` — expose settings, diagnostics, and profile import/export in the lab/workbench UI.
5. `05-sub-bundle-validation` — build, unit-test, serial-test, and manual BLE validation plan.
6. `06-sub-bundle-rollout` — safe integration order and rollback plan.

## Current highest-impact fixes

Implement these first if time is limited:

1. Fix pan and zoom so they are scaled by elapsed time, not by telemetry frame count.
2. Add host-side filtered acceleration and filtered gyro before navigation mapping.
3. Add configurable soft deadzones and non-linear response curves.
4. Change firmware orientation selection from `AbsoluteFirst/StabilizedFirst` to exact modes, including `GameRotationVectorNoMag`.
5. Add a serial CLI to switch orientation modes and report intervals during testing.

## Suggested default behavior after this bundle

- Firmware orientation mode: `GameRotationVectorNoMag` for normal 3D-mouse use.
- Optional test mode: `RotationVectorMag` to compare magnetometer-assisted yaw behavior.
- Host pan: filtered acceleration, soft deadzone, time-scaled pixel delta.
- Host zoom: filtered forward acceleration converted to an exponential zoom factor using elapsed time.
- Host orbit/rotation: filtered gyro, soft deadzone, time-scaled angular delta.
- UI: settings visible and adjustable without recompilation.

## Deliverables expected from Codex

- Updated firmware source.
- Updated host source.
- Updated protocol documentation.
- Tests for protocol parsing, deadzone curves, smoothing, and frame-rate invariance.
- A validation report containing build output, serial command output, and any manual-browser-test notes that still need user execution.
