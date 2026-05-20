# Normalized Requirements

| Id | Requirement | Source | Owning subbundle | Proof |
| --- | --- | --- | --- | --- |
| `R-001` | Provide a fluent Cognitive Memory curator conversation mode separate from the existing form-heavy probe workbench. | `SRC-001` | `03-03-curator-ui-and-voice` | Browser proof on `/cognitive-memory` and component tests. |
| `R-002` | Support two runtime modes: curator as an agent and curator as a direct provider LLM call. | `SRC-001` | `02-02-curator-runtime-modes-and-memory-routing` | Unit tests for both modes and UI mode switch proof. |
| `R-003` | Use bidirectional voice so the operator can speak to the curator and hear curator responses. | `SRC-001` | `03-03-curator-ui-and-voice` | Component/browser proof for voice controls plus service tests where feasible. |
| `R-004` | Automatically extract user corrections and new knowledge from the conversation. | `SRC-001` | `01-01-curator-contracts-and-capture-pipeline` | Unit tests for extraction/capture decisions. |
| `R-005` | Persist extracted human input as high-priority, high-confidence memory-improvement artifacts with actor credit. | `SRC-001` | `01-01-curator-contracts-and-capture-pipeline` | Persistence tests asserting source/evidence/mutation/candidate metadata. |
| `R-006` | When a curator answer is corrected, preserve the recall trace and affected memory ids used to produce the answer so the wrong memory can be improved. | `SRC-001` | `01-01-curator-contracts-and-capture-pipeline` | Persistence tests asserting trace id and memory ids on correction artifacts. |
| `R-007` | Skip manual confirmations/approvals in this trusted curator mode without weakening normal probe/review flows. | `SRC-001` | `01-01-curator-contracts-and-capture-pipeline` | Tests asserting curator capture is accepted while existing probe feedback still requires review. |
| `R-008` | Feed captured improvements into the existing dreaming/consolidation substrate so clustering, connection, and aggregation can process them later. | `SRC-001` | `02-02-curator-runtime-modes-and-memory-routing` | Tests asserting created source items/candidates are discoverable by consolidation/dreaming services. |
| `R-009` | Keep the UI proper, dense, and component-based using existing CanDoItAll components. | `SRC-001` | `03-03-curator-ui-and-voice` | Browser screenshot review and component test. |
| `R-010` | Support short, medium, and long curator conversation depth modes that control reply detail and the breadth of recall/aggregation input used for memory capture. | `SRC-006` | `05-05-conversation-depth-modes` | Unit tests for depth-driven budgets/prompts/capture metadata, component selector test, EF model checks, and browser proof. |
