# Codex QA prompt — verify round 2 implementation

You are reviewing a completed MAF stabilization follow-up implementation. Be skeptical.

Use this checklist:

## Finalizer mode-aware runtime

- Does `IAgentRuntime.RunAsync(...)` receive effective finalizer mode or runtime execution policy?
- Does `RespondToPendingApprovalsAsync(...)` receive and preserve it?
- Does temperature retry preserve it?
- Does disabled mode avoid finalizer tool attachment and finalizer instructions?
- Does shadow mode avoid exact-once required instructions?
- Does required mode still require exact-one finalizer at completion?

## Instruction consistency

- Required mode instructions say finalizer is authoritative and final assistant response must be JSON matching schema.
- Required mode instructions do not say final assistant text can be prose/Markdown/display-only.
- Disabled mode appends no finalizer text.

## Tool-policy exception boundary

- Is there a dedicated policy-block exception type?
- Are broad catches for `InvalidOperationException`/`NotSupportedException` removed?
- Can a real tool failure still surface as a real tool failure?

## Provider capability truth

- Does core runtime feature matrix agree with UI defaults?
- Does registry persistence avoid transport-only structured-output logic?
- Do local/remote Ollama defaults avoid structured-output claims?
- Is managed SQLite provider capability display truthful?

## Tests

- Are there behavior tests for finalizer mode-aware runtime behavior?
- Are there behavior tests for policy exception boundary?
- Are there provider capability truth tests across core/UI/registry?
- Did `dotnet build` and `dotnet test` actually run?

Return a concise QA report with:

- Pass/fail per area.
- Evidence file paths.
- Remaining risks.
- Any claims in docs that are not backed by test/build output.
