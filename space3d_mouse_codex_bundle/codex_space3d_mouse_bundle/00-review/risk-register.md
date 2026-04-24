# Risk Register

| Risk | Area | Severity | Mitigation |
|---|---|---:|---|
| Changing telemetry frame layout breaks browser parsing | Firmware/protocol/host | High | Keep v1.03 parser. Add v1.04 parser only as additive. Do not change v1.03 field order. |
| BNO085 library constant names differ from expected names | Firmware | Medium | Inspect installed `sh2_SensorId_t` definitions before coding. Support only constants that exist. |
| Multiple orientation reports remain enabled and produce mixed sources | Firmware | High | Configure exactly one orientation report after reset/reconfigure. Track active report ID and ignore other orientation reports. |
| Serial CLI blocks main loop or BLE | Firmware | Medium | Implement non-blocking line buffer. Parse in `FirmwareApp::tick()`. |
| Pan/zoom feel changes too much from current behavior | Host | Medium | Add a `current-like` profile and `smooth-default` profile. Let UI switch profiles. |
| Over-filtering causes latency | Host | Medium | Keep tau settings adjustable. Use smaller tau for gyro than acceleration. Add precision mode instead of globally heavy smoothing. |
| UI changes make lab cluttered | Host UI | Low | Put advanced settings in collapsible panels. Keep current summary visible. |
| Browser BLE setting write cannot be validated by Codex | Browser/firmware | Medium | Make serial settings required. Make BLE write optional and add manual checklist for the user. |
| Saved firmware settings become invalid after schema changes | Firmware persistence | Medium | Bump config schema version and reset invalid values through `validateConfig()`. |
| No local toolchain available in execution environment | Validation | Medium | Codex must report exact commands attempted and toolchain errors. Arduino MCP serial validation can still be used if available. |
