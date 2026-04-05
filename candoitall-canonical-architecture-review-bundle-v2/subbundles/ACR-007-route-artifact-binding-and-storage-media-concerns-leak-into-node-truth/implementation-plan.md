# Implementation plan

## Remediation goal

Create typed artifact bindings and attachment/media models; compute routes through resolvers instead of persisting them as canonical node truth.

## Ordered steps

- Move route/navigation hints to DTO/projection space unless a route is truly business meaning.
- Split artifact identity from storage placement and media metadata into dedicated binding records.
- Keep the node carrier free of filesystem/IPFS/media transport details except where the node genuinely represents a file-like artifact.
- Refactor upload/binding flows to write the dedicated binding owners and then project them back into UI DTOs.

## Guardrails

- Do not break existing open/download routes without compatibility shims.
- Do not move storage concerns into UI components.

## Acceptance criteria

- Routes are derived or resolved, not rewritten as canonical truth on project moves.
- Attachment and storage bindings are typed and separately owned.
