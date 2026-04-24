# Sub-bundle 05 — Validation Plan

## Goal

Prove that the implementation builds, stays backward compatible, and improves control smoothness in measurable ways.

## Required validation categories

### 1. Static and build validation

Firmware:

- Compile default firmware.
- Compile firmware with `ID3M_GPIO36_AS_ADC`.
- Confirm no source-code comments were added in Czech.

Host:

- Restore packages.
- Build all projects.
- Run tests.

If no solution file exists, build each project directly.

Suggested commands:

```bash
dotnet restore
find . -name "*.csproj" -maxdepth 3 -print
for p in $(find . -name "*.csproj" -maxdepth 3); do dotnet build "$p" --configuration Release; done
dotnet test --configuration Release
```

Firmware compile commands must follow the existing README/VALIDATION docs. If `arduino-cli` is available, use it. If Arduino MCP is available, use its compile/upload operations and record the output.

### 2. Protocol validation

Required tests:

- Valid v1.03 frame parses.
- Valid v1.04 frame parses if v1.04 is added.
- Corrupted CRC rejects.
- Corrupted terminator rejects.
- Unknown message type rejects.
- Signed14 decoding handles negative values.
- Orientation quaternion is normalized or rejected safely.

### 3. Motion mapper validation

Required tests:

- Pan is frame-rate invariant at 50 Hz vs 100 Hz.
- Zoom is frame-rate invariant at 50 Hz vs 100 Hz.
- Gyro rotation is frame-rate invariant at 50 Hz vs 100 Hz.
- Idle jitter below deadzone does not produce meaningful navigation output.
- Soft deadzone output is continuous and monotonic.
- Profile reset restores default values.

### 4. Firmware serial validation with Arduino MCP

Use `validation/serial-test-plan.md`.

Minimum serial test commands:

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
set accel_us 2500
set gyro_us 2500
runtime-reset
get json
```

Expected:

- All supported settings return `ok`.
- Unsupported orientation modes return a clear `err` but do not crash.
- Active source changes after reconfigure.
- No watchdog reset.
- Health log shows queue drops near zero.

### 5. Manual browser BLE validation

The user will do this. Codex must provide a checklist and keep the UI/build ready.

Checklist is in `validation/manual-ble-checklist.md`.

### 6. Acceptance metrics

Use `validation/acceptance-metrics.md`.

The main measurable targets:

- 50 Hz vs 100 Hz pan total delta differs by less than 10% for the same synthetic input.
- 50 Hz vs 100 Hz zoom final factor differs by less than 10%.
- Idle jitter test produces less than 1 px/sec equivalent pan and no significant zoom.
- Serial orientation switch completes without reboot unless reboot is explicitly documented.

## Validation report required from Codex

Create a final `VALIDATION_REPORT.md` in the repo with:

- Commit/branch identifier if available.
- Firmware build commands and results.
- Host build/test commands and results.
- Serial command transcript.
- Any unsupported library constants discovered.
- Any manual BLE steps left for the user.
- Known remaining issues.
