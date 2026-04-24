# Codex Master Prompt

You are implementing improvements to a BNO085-based Space3D mouse project. Read the bundle README first, then implement the sub-bundles in order.

Important constraints:

- Source-code comments must be in English.
- Do not remove the existing BLE telemetry path.
- Preserve v1.03 telemetry parser compatibility.
- Browser BLE testing will be performed manually by the user; you should still build and test everything possible without browser pairing.
- Use Arduino MCP and serial communication for firmware settings validation if available.
- Add a final `VALIDATION_REPORT.md` documenting commands run, results, limitations, and manual BLE steps still required.

Implementation order:

1. Host motion filter and frame-rate-independent pan/zoom.
2. Host settings and diagnostics UI.
3. Firmware exact orientation mode selection and serial CLI.
4. Protocol extension only if needed.
5. Tests and validation report.

Do not proceed to later phases if an earlier phase fails to build, except to make minimal fixes needed for build/test success.
