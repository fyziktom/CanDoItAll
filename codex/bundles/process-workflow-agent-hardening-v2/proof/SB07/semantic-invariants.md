# SB07 Semantic Invariants

## Invariants

- `SB07-INV-001`: Governed software-delivery templates must publish with strict contract mode.
- `SB07-INV-002`: Every software-delivery step must define `AllowedOperations` and `OperationTargetScope`.
- `SB07-INV-003`: Skills that claim production process E2E proof must require execution runs, tool receipts, lineage, and provider usage observations.
- `SB07-INV-004`: Active skill-root copies must hash-match repo skill copies after edits.
- `SB07-INV-005`: Templates, agents, skills, and seed assets must not hardcode SB04 scenario keys.

## Evidence

- `bundle://proof/SB07/transcripts/template-contract-and-scenario-scan.txt`
- `bundle://proof/SB07/active-skill-sync-hashes.json`
- `bundle://proof/SB04/scenarios/recipe-pantry-planner/process-run-detail.json`

## Residual Risk

Skill root synchronization is machine-local. The hash proof captures this workstation state; another machine must rerun the sync step after pulling these repo changes.
