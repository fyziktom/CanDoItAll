# Normalized Requirements

| ID | Requirement | Owning subbundles |
| --- | --- | --- |
| RQ-001 | Verify previous subprocess boundary closure before starting production movement. | SB01 |
| RQ-002 | Keep all changes module-local; do not create Process Core. | All, gates |
| RQ-003 | Do not create production driver APIs or driver packs. | All, gates |
| RQ-004 | Build an implementation-proof source inventory with exact consumers and side effects. | SB02 |
| RQ-005 | Extract implementation contract/evidence text snapshots without behavior change. | SB05 |
| RQ-006 | Extract stack detection rules for .NET, JS, negated .NET, and explicit tests. | SB06-SB08 |
| RQ-007 | Extract receipt timeline and concrete product path facts. | SB09-SB12 |
| RQ-008 | Extract concrete implementation proof summary rules while preserving exact summaries. | SB13-SB14 |
| RQ-009 | Extract runnable application proof and dotnet host path/shape helpers. | SB15-SB18 |
| RQ-010 | Extract carried/historical implementation proof state helpers. | SB19-SB21 |
| RQ-011 | Preserve process mock proof satisfaction and implementation artifact write satisfaction. | SB22-SB23 |
| RQ-012 | Wire helpers into ToolValidation, Execution, RecoveryPackets, and completion blocker paths through existing wrappers. | SB24-SB25 |
| RQ-013 | Keep driver readiness documentation-only. | SB26 |
| RQ-014 | Run focused tests, source scans, full build, anti-stub audit, no-core/no-driver/no-UI scans. | Gates |
| RQ-015 | No small/medium/mobile proof artifacts. | All |
