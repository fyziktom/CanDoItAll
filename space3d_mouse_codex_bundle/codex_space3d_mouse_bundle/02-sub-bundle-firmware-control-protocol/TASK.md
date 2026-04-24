# Sub-bundle 02 — Firmware Control Protocol + Protocol Documentation

## Goal

Keep telemetry backward compatible while adding enough protocol support to expose extended orientation source/status and, optionally, allow settings to be changed over BLE SysEx. Serial CLI support from sub-bundle 01 is required; BLE settings are optional but recommended if time allows.

## Required implementation tasks

### 1. Decide whether telemetry v1.04 is necessary

Current protocol v1.03 encodes orientation source using two bits split across `flags` and `status`. That supports only four values. Once firmware supports more than four sources, v1.03 cannot represent them cleanly.

Options:

#### Option A — Keep v1.03 telemetry and compress source labels

Use this only as a short-term compatibility fallback. Example:

- `RotationVectorMag` -> existing `RotationVector`
- `GeomagneticNoGyro` -> existing `Geomagnetic`
- `ArvrStabilizedMag` -> existing `ArvrStabilized`
- `GameRotationVectorNoMag` -> existing `ArvrStabilized` or `Unknown`

This is not ideal because the host cannot distinguish no-magnetometer mode.

#### Option B — Add telemetry v1.04 with an explicit source byte

Recommended.

Add one 7-bit byte after the current `status` byte or after current ADC event fields. The exact position must be documented. Keep the parser able to read v1.03 and v1.04.

Suggested v1.04 source enum:

| Value | Meaning |
|---:|---|
| 0 | Unknown |
| 1 | RotationVectorMag |
| 2 | GameRotationVectorNoMag |
| 3 | GeomagneticNoGyro |
| 4 | ArvrStabilizedMag |
| 5 | ArvrStabilizedGameNoMag |

Add another optional 7-bit status byte only if needed:

- bit 0: source uses magnetometer
- bit 1: source fallback was used
- bit 2: reports were reconfigured since boot
- bit 3: settings are dirty/not saved
- bit 4: firmware runtime settings valid

If the extra byte is added, bump protocol minor to 4. Do not bump major.

### 2. Keep host parsing backward compatible

File: `CanDoItAll.Space3D.Mouse.Driver/Protocol/Space3DMouseProtocol.cs`

Required behavior:

- v1.01/v1.02/v1.03 frames parse exactly as before.
- v1.04 frames parse with the new source byte/status byte.
- Unknown future minor versions should be rejected or parsed only if safe. Prefer explicit support for `minor <= 4`.
- Add unit tests with fixture frames for v1.03 and v1.04.

### 3. Update protocol docs

Update firmware `PROTOCOL.md` with:

- v1.03 frame layout retained.
- v1.04 frame layout if added.
- Source enum table.
- Settings/control messages if implemented.
- CRC rules.
- Examples of valid frames.

### 4. Optional BLE settings SysEx command messages

Serial CLI is enough for Codex validation, but BLE settings are useful in the browser later.

If implemented, add a settings command message family. Suggested message types:

| Message type | Direction | Meaning |
|---:|---|---|
| `0x31` | firmware -> host | Mouse state telemetry, existing |
| `0x41` | host -> firmware | Set setting |
| `0x42` | host -> firmware | Get settings |
| `0x43` | firmware -> host | Settings snapshot |
| `0x44` | host -> firmware | Save settings |
| `0x45` | host -> firmware | Reset runtime/defaults |

Payload must remain MIDI SysEx-safe: every payload byte must be 0..127. Use 7-bit enum values and unsigned14/signed14 encoding where needed.

Suggested setting IDs:

| ID | Setting | Encoding |
|---:|---|---|
| 1 | orientation mode | 7-bit enum |
| 2 | orientation fallback | 0/1 |
| 3 | quaternion interval us | unsigned14 |
| 4 | linear accel interval us | unsigned14 |
| 5 | gyro interval us | unsigned14 |
| 6 | telemetry interval ms | unsigned14 |
| 7 | save requested | 0/1 |
| 8 | runtime reset requested | 0/1 |

Keep the serial CLI and BLE settings code paths calling the same internal setting-update functions.

### 5. Optional browser write path

If BLE settings are implemented, add JS support in `space3dMouseBle.js`:

- Store the writable characteristic.
- Add a function like `sendSysExFrame(frameBytes)`.
- Split into BLE-MIDI packets correctly if needed.
- Expose .NET interop method to send settings.

Do not make browser settings write path a blocker for firmware and host filter work. The user will perform browser BLE validation manually.

## Acceptance criteria

Required:

- Existing v1.03 telemetry still parses in host tests.
- Protocol documentation matches implementation.
- If v1.04 is added, v1.04 test frames parse and expose exact source mode.
- Firmware still sends v1.03 or v1.04 frames consistently; no mixed layout within a run unless explicitly documented.

Optional BLE settings:

- Host can build a settings SysEx frame.
- Firmware can receive and apply one settings command.
- Serial CLI and BLE settings update the same config fields.

## Validation

Run or add tests for:

- CRC rejection.
- Unknown message type rejection.
- v1.03 source decoding.
- v1.04 source decoding.
- Signed14/unsigned14 boundaries.
- Corrupted SysEx terminator rejection.

## Non-goals

- Do not add streaming calibration blobs.
- Do not add large JSON over BLE SysEx.
- Do not change BLE MIDI service UUIDs.
