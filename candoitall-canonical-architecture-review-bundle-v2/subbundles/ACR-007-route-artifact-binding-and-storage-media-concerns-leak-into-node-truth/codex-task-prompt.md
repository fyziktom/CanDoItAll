# Codex task prompt — ACR-007

Implement finding `ACR-007` from this subbundle.

## Required stance

- follow the bundle architecture
- do not solve this by introducing a new parallel truth
- keep changes aligned with `Phase 3`
- preserve node-as-carrier and canonical spatial semantics where relevant
- add required positive and negative tests
- run the validation commands
- produce evidence for QA

## Finding summary

Route strings are rewritten during project moves and storage/media references live inside the main node record, making navigation and attachment concerns look canonical even though they are transport/integration concerns.

## Ordered implementation steps

- Move route/navigation hints to DTO/projection space unless a route is truly business meaning.
- Split artifact identity from storage placement and media metadata into dedicated binding records.
- Keep the node carrier free of filesystem/IPFS/media transport details except where the node genuinely represents a file-like artifact.
- Refactor upload/binding flows to write the dedicated binding owners and then project them back into UI DTOs.

## Guardrails

- Do not break existing open/download routes without compatibility shims.
- Do not move storage concerns into UI components.

## Done means

- Routes are derived or resolved, not rewritten as canonical truth on project moves.
- Attachment and storage bindings are typed and separately owned.
