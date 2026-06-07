# Driver Verification Read-Model Proposal

This bundle does not introduce a production process-helper-driver API.

## Verification-Only Read Models
- Route helper evidence: route eligibility and planner decisions from `CanDoItAll.Processes.Core.Routing`.
- Subprocess helper evidence: parent status/reason facts and artifact mapping decisions from `CanDoItAll.Processes.Core.Subprocess` and `CanDoItAll.Processes.Core.Artifacts`.
- Artifact helper evidence: expectation snapshots, strong-match decisions, and recorded-satisfaction descriptors from `CanDoItAll.Processes.Core.Artifacts`.

## Future Driver Lane
- A future driver bundle may define permission and evidence vocabulary around these read models.
- The future bundle must still prove absence of production registration until the driver runtime contract is explicitly approved.
- Driver permissions must be modeled separately from runtime execution, persistence, and finalizer application.

## Current Denials
- No `IProcessDriverPack`.
- No `IProcessDriverRegistry`.
- No `ProcessDriverRegistry`.
- No `IProcessHelperDriver`.
- No `MapProcessDriver`.
- No DI registration or manager command.
