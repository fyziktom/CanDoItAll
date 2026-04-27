# Subbundle 07 — Tool composition approval fail-fast

## Problem

The function-call middleware prevents mutation/destructive tools from executing when approval is required but no effective approval path exists. However the runtime can still expose such tools to the model, only to block them later.

That is safe but noisy and less stable. The model can repeatedly attempt unusable tools and hit policy blocks.

## Required change

During capability/tool composition, detect mutation/destructive tools that would require approval and cannot have an effective approval path for the current provider/run.

Target behavior options:

1. Fail runtime build with a clear diagnostic, preferred for governed production runs.
2. Omit unusable mutation tools and log/trace why they were omitted, acceptable for exploratory/manual runs.

Do not silently expose unusable mutation tools.

## Considerations

- Auto-approved process automation may intentionally suppress approval requirements.
- MAF `ApprovalRequiredAIFunction` is only effective if the provider/runtime supports approval requests.
- Hosted provider-native tools may have their own approval mode and should be handled separately.
- Local MCP and workspace plugin functions need clear classification.

## Tests

- Mutation tool + no auto-approval + no provider approval support => runtime build fails or omits tool with explicit diagnostic.
- Mutation tool + auto-approval => tool remains available.
- Mutation tool + approval wrapper effective => tool remains available and middleware requires approval.
- Read-only tools remain available.
- Validation tools are handled according to explicit project policy.
