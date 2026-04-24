# Sub-bundle 04 — Host UI Settings + Diagnostics

## Goal

Expose the new control settings and diagnostics so the user can tune BNO085 behavior without recompiling. Keep the UI compact by placing advanced settings in collapsible panels.

## Required implementation tasks

### 1. Add a settings panel in MouseLab

File: `CanDoItAll.Space3D.Mouse.Components/Components/MouseLab.razor`

Add a panel with:

- Profile selector:
  - `Current-like`
  - `Smooth default`
  - `Precision`
  - `Fast orbit`
- Buttons:
  - Reset profile
  - Export JSON
  - Import JSON text
- Numeric inputs/sliders:
  - Accel smoothing tau ms
  - Gyro smoothing tau ms
  - Accel deadzone G
  - Gyro deadzone dps
  - Pan sensitivity
  - Zoom sensitivity
  - Orbit sensitivity
  - Pan exponent
  - Zoom exponent
  - Orbit exponent
  - Precision multiplier

Do not overload the main visual stage. Use `<details>` for advanced controls.

### 2. Add live diagnostics

Show raw and filtered values side by side:

- raw scene acceleration,
- filtered scene acceleration,
- estimated acceleration bias,
- raw scene gyro,
- filtered scene gyro,
- stillness state,
- active button action,
- last navigation command,
- orientation source,
- source uses magnetometer if available,
- telemetry frame interval estimate.

This helps the user tune deadzones and smoothing.

### 3. Add settings to the real workbench page

File: `CanDoItAll.Space3D.Mouse.Sandbox/Components/Pages/Space3DProcessWorkbench.razor`

Add a compact settings area or reuse a shared component. It should allow at least:

- profile selection,
- pan sensitivity,
- zoom sensitivity,
- rotation sensitivity,
- accel deadzone,
- gyro deadzone,
- smoothing preset.

The workbench should use the same settings model as MouseLab.

### 4. Add optional firmware settings controls

If sub-bundle 02 implements BLE settings write, add controls for:

- orientation mode:
  - game/no magnetometer,
  - rotation vector/magnetometer,
  - geomagnetic,
  - AR/VR stabilized,
  - AR/VR game if supported,
- report intervals,
- telemetry interval,
- save settings.

If BLE settings write is not implemented, display the equivalent serial CLI command next to each firmware setting. Example:

```text
set orientation game
sensor-reconfigure
```

This allows the user to copy commands into serial while still tuning host-side settings in the browser.

### 5. Persist host-side settings locally

Use browser local storage or the existing app settings pattern if one exists.

Suggested local storage key:

```text
space3d.mouse.control.settings.v1
```

Requirements:

- Settings load on page open.
- Invalid or missing settings fall back to defaults.
- Exported JSON can be re-imported.
- Settings changes reset the filter state where appropriate.

### 6. Keep UI refresh separate from control processing

The current lab has a 20 ms UI throttle. It is okay to keep or reduce to 16 ms for visualization, but control command generation must be based on telemetry timestamps and not on UI redraw frequency.

## Acceptance criteria

- User can switch between at least three host control profiles.
- User can adjust pan/zoom/orbit sensitivity without recompiling.
- User can see raw vs filtered accel and gyro.
- Settings survive page reload.
- The UI clearly indicates whether firmware settings are serial-only or BLE-write-capable.

## Manual UI smoke test

1. Open MouseLab.
2. Load demo frame or connect BLE.
3. Change accel deadzone and observe diagnostics.
4. Change pan sensitivity and verify workbench pan changes.
5. Export settings JSON, reset profile, import JSON, verify values return.
6. Toggle precision profile and verify output deltas shrink.
