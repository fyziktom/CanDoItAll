# No-Core Cutline

- This bundle intentionally stops at module-local internal classes under repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch.
- No repo://src/CanDoItAll.Processes.Core project or repo://src/CanDoItAll.Modules.Processes.Core project was created.
- No production process-driver API was introduced.
- `IProcessArtifactProjectionHost` is an internal dependency surface, not a public driver contract.
- Driver readiness is documentation-only in repo://codex/bundles/process-dispatch-artifact-projection-split-dependency-boundary-v1/architecture/03-driver-readiness-map.md.
- Proof: bundle://proof/shared/transcripts/source-scans.txt.
