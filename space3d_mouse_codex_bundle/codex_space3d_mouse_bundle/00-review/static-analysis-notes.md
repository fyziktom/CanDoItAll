# Static Analysis Notes

This bundle was created from a static review of the supplied source archives. The analysis focused on control feel, BNO085 report selection, BLE protocol compatibility, and testability.

## Most likely causes of current control feel issues

1. Pan is not multiplied by elapsed time.
2. Zoom applies a per-frame multiplicative factor instead of a per-second log-rate.
3. Acceleration is used raw on the host.
4. Firmware calculates filtered acceleration/gyro but sends raw values in the telemetry packet.
5. Rotation uses elapsed time but still uses raw gyro and fixed constants.
6. Host settings are hardcoded, which makes A/B tuning slow.
7. Firmware lacks a true game/no-magnetometer orientation mode.

## Tooling note

If the local Codex environment lacks `dotnet`, `arduino-cli`, or board access, Codex must still create the implementation and report the missing tools honestly in `VALIDATION_REPORT.md`. When Arduino MCP is available, use it for compile/upload/serial validation.
