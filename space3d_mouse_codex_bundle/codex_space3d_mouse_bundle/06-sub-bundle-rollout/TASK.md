# Sub-bundle 06 — Rollout and Integration Order

## Goal

Integrate changes safely without breaking the currently working 3D mouse path.

## Recommended order

### Phase 1 — Host-only improvements

1. Add `Space3DMouseControlSettings`.
2. Add `Space3DMouseControlFilter`.
3. Move workbench navigation mapping out of Razor.
4. Fix pan/zoom time scaling.
5. Add tests for frame-rate invariance.

This phase can be validated without firmware changes by using demo frames/synthetic telemetry.

### Phase 2 — UI tuning controls

1. Add profile settings UI.
2. Add raw/filtered diagnostics.
3. Add local persistence for host settings.
4. Keep old defaults available as `Current-like`.

### Phase 3 — Firmware exact orientation modes

1. Add exact orientation mode enum.
2. Add game/no-magnetometer report support.
3. Add serial CLI.
4. Validate with Arduino MCP serial commands.

### Phase 4 — Protocol extension if needed

1. Add v1.04 source byte.
2. Update host parser and tests.
3. Update docs.
4. Keep v1.03 compatibility.

### Phase 5 — Optional BLE settings write

1. Add firmware settings SysEx handlers.
2. Add JS write path.
3. Add UI controls that send settings.
4. Leave manual browser BLE validation for the user.

## Rollback plan

- Keep a `Current-like` host profile that approximates the old constants.
- Keep firmware default report intervals close to current values.
- If v1.04 causes issues, allow firmware to compile/send v1.03 telemetry with source compression until host parser is fixed.
- If game/no-magnetometer report is unsupported by a specific library build, fallback to rotation vector and clearly report it over serial.

## Done definition

The work is done when:

- Host builds and tests pass.
- Firmware compiles in default and ADC modes.
- Serial CLI can switch orientation source and report settings.
- Pan/zoom are time-scaled and tested.
- Rotation uses filtered gyro and configurable settings.
- UI exposes tuning controls and diagnostics.
- `VALIDATION_REPORT.md` exists and is honest about what was and was not validated.
