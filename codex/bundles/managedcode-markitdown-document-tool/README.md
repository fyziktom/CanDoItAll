# ManagedCode MarkItDown Document Tool

This bundle replaces the agent-visible `workspace_convert_document` implementation with a C# ManagedCode.MarkItDown-backed document converter hosted in `CanDoItAll.Tools.Documents`.

## Profile

- `feedback`

## Mission

- Add a typed C# document-to-markdown converter for PDF, Office, HTML, CSV, JSON, XML, EPUB, archives, and related document assets.
- Keep ManagedCode.MarkItDown isolated in the document tools implementation project.
- Preserve `workspace_convert_document` as the public agent tool name and receipt contract.
- Remove the Python `python -m markitdown` dependency from the agent conversion path.
- Prove the tool through focused unit tests, build/test gates, and a real 5032 project-structure floating chat validation.

## Outcome Contract

- Requested outcome: agents can convert project-structure PDF/document assets to markdown through `workspace_convert_document` without Python MarkItDown.
- Hard constraints: do not put converter logic into `MafAgentRuntime`; keep SDK dependency out of Core runtime contracts; keep behavior strongly typed; fail explicitly on missing files or conversion errors.
- Evidence required before closure: bundle self-review, CodeAnalytics/dependency evidence, focused unit tests, project build, 5032 restart, and project-structure floating chat proof.
- Known validation constraint: Financial Strategist currently has read-only project-structure mutation rights in the managed seed. Conversion can be validated with that agent; node creation requires a write-enabled project-structure agent or a separate permission-change bundle.

## Recommended Execution Order

1. `subbundles/01-managedcode-document-converter`
2. `subbundles/02-workspace-tool-wiring`
3. `subbundles/03-validation-and-e2e`

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Implemented`
- Subbundle gate review: `PassedWithExternalFinding`
- Final closure gate: `ImplementedWithFollowUp`
- Browser validation analytics: `ConversionPassedNodeCreateApprovalBlocked`

## Closure Summary

- `workspace_convert_document` is implemented through the C# `ManagedCode.MarkItDown` adapter in `CanDoItAll.Tools.Documents`.
- The previous Python `python -m markitdown` command path was removed from the workspace conversion tool.
- Builds and focused tests passed after the receipt filename repair.
- The live 5032 floating agent chat converted the quotation PDF and extracted `ZM-x5600` and `$35,000 USD`.
- The requested project-structure node creation did not complete because the existing approval continuation flow stayed pending after UI approval. That is a separate runtime/approval continuation issue, not a document conversion failure.
