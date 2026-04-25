# Serial Test Plan

## Setup

- Upload the new firmware.
- Open the serial monitor at the firmware's configured debug baud.
- Use newline-terminated commands.
- Keep a transcript of all commands and responses.

## Basic settings test

Send:

```text
help
get
get json
```

Expected:

- `help` lists available commands.
- `get` prints readable settings.
- `get json` prints a compact settings JSON line.

## Orientation mode test

Send:

```text
set orientation game
sensor-reconfigure
get json
set orientation rotation
sensor-reconfigure
get json
set orientation geomag
sensor-reconfigure
get json
set orientation arvr
sensor-reconfigure
get json
set orientation arvr-game
sensor-reconfigure
get json
```

Expected:

- Supported modes return `ok`.
- Unsupported AR/VR game mode returns `err unsupported-orientation arvr-game` or equivalent and keeps the previous valid mode.
- `game` mode reports magnetometer disabled.
- `rotation` and `arvr` modes report magnetometer enabled.

## Report interval test

Send:

```text
set q_us 5000
set accel_us 2500
set gyro_us 2500
set telemetry_ms 10
sensor-reconfigure
get json
```

Expected:

- Values are clamped only if outside valid range.
- Telemetry continues after reconfigure.

## Persistence test

Send:

```text
set orientation game
set telemetry_ms 10
save
```

Then reset the board. After reboot:

```text
get json
```

Expected:

- Saved settings are restored.
- Invalid settings are not restored; they are corrected by validation.

## Runtime health test

Let the board run for at least 5 minutes at default report rates.

Expected:

- No watchdog resets.
- No repeated report configuration failures.
- Queue drops remain near zero.
- BLE telemetry continues if a browser is connected.
