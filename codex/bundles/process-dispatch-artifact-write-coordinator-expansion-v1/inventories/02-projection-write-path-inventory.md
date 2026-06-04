# Projection Write Path Inventory

| Path | Storage-backed? | Current migration state | Next action |
| --- | --- | --- | --- |
| Execution artifact | Yes | Uses write coordinator | Harden with structured outcome and parity proof |
| Process mock artifact | Yes | Uses source adapter, still writes directly | Migrate through coordinator, preserve hard failure behavior |
| Workspace-written artifact | Yes | Uses source adapter, still writes directly | Migrate through coordinator, preserve matching/path rules |
| Existing managed artifact | Yes | Uses source adapter, still writes directly | Migrate through coordinator, preserve duplicate detection |
| Response text artifact | Yes, after writing text file | Uses source adapter, still writes/places/records directly | Migrate storage/recording through coordinator; keep file creation/path safety outside coordinator |
| Provider-native browser artifact | Yes | Uses source adapter, still writes directly | Migrate expected and discovered paths; preserve modes |
| Completed decision artifact | No | Record-only direct `RecordArtifactAsync` | Add record-only helper, no storage placement |
