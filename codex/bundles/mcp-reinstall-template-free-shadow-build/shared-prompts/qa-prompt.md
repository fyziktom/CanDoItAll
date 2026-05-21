# QA Prompt

Validate the implemented bundle against the raw notes and requirements. Do not accept a targeted build alone: the full `tools\Reinstall-CanDoItAllMcps.ps1` path must pass, MCP artifact outputs must not contain copied `Templates`, and the proof manifest must cite existing transcripts and source assertions. Verify skills sync remains present in source and in the install manifest. No browser proof is required for this host/build-script-only change.
