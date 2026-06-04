# Next Phase Strategy

Do not start Process Core.

The safest next sequence is:

1. Harden write coordinator contract.
2. Migrate storage-backed write side effects one path at a time.
3. Add a separate record-only helper for completed decision artifacts.
4. Add refactor gates and line-count/source-scan proof.
5. Leave source discovery, file path resolution, and candidate state updates in the dispatcher unless proven safe to isolate later.

After this bundle, a later bundle can target either `ArtifactValidation.cs` rule extraction or `ToolValidation.cs` required-tool boundary extraction.
