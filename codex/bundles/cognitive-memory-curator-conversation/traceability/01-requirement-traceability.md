# Requirement Traceability

| Requirement | Inputs | Analysis | Architecture | Plan | Subbundle | Proof |
| --- | --- | --- | --- | --- | --- | --- |
| `R-001` | `SRC-001` | Existing probe is not fluent conversation. | UI strategy. | Gate 3. | `03-03-curator-ui-and-voice` | Browser and component tests. |
| `R-002` | `SRC-001` | Agent/provider APIs exist. | Runtime modes. | Gate 2. | `02-02-curator-runtime-modes-and-memory-routing` | Unit tests for both modes. |
| `R-003` | `SRC-001` | Voice service/JS bridge exist. | UI strategy. | Gate 3. | `03-03-curator-ui-and-voice` | Browser/component proof. |
| `R-004` | `SRC-001` | Probe feedback has precedent. | Memory improvement path. | Gate 1. | `01-01-curator-contracts-and-capture-pipeline` | Unit tests. |
| `R-005` | `SRC-001` | Probe repair candidate is review-gated. | Memory improvement path. | Gate 1. | `01-01-curator-contracts-and-capture-pipeline` | Persistence assertions. |
| `R-006` | `SRC-001` | Recall trace persists selected refs. | Memory improvement path. | Gate 1. | `01-01-curator-contracts-and-capture-pipeline` | Persistence assertions. |
| `R-007` | `SRC-001` | Normal probe review remains approval-oriented. | Memory improvement path. | Gate 1. | `01-01-curator-contracts-and-capture-pipeline` | Unit tests for curator vs probe behavior. |
| `R-008` | `SRC-001` | Consolidation/dreaming read source-backed candidates. | Memory improvement path. | Gate 2. | `02-02-curator-runtime-modes-and-memory-routing` | Unit/integration assertions. |
| `R-009` | `SRC-001` | Existing page uses BaseLib tabs/cards. | UI strategy. | Gate 3. | `03-03-curator-ui-and-voice` | Browser screenshot review. |
| `R-010` | `SRC-006` | Existing curator implementation used fixed response/recall depth. | Depth profile in curator service plus UI selector. | Gate 4. | `05-05-conversation-depth-modes` | Unit/component/integration tests, EF pending-model checks, and browser screenshot review. |
