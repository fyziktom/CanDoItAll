# QA Prompt

Validate that the runtime-start performance repair preserves behavior.

Required checks:

- Start-run assignment binding still persists expected runtime signals.
- Step status progression still activates dependencies correctly.
- Process mock-agent coverage passes or a blocker is recorded.
- Independent simple .NET app build smokes pass.
- No product code now contains stack-specific process logic for .NET app creation.
