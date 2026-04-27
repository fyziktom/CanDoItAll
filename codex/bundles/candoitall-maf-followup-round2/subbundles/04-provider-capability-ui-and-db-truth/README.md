# Subbundle 04 — Provider capability UI and DB truth

## Goal

Make provider capability truth consistent across core runtime, Workspace UI, provider registry, and managed SQLite bootstrap.

## Problem

The core `ProviderFeatureMatrix` says:

- OpenAI/Azure OpenAI Responses: structured output true; approval true; hosted tools true.
- OpenAI/Azure OpenAI Chat Completions: structured output true; approval false; hosted tools false.
- Ollama: structured output false.

But Workspace UI defaults still mark local and remote Ollama structured-output capable. The workspace-backed registry still persists `SupportsStructuredOutput` using transport-only logic. Managed SQLite provider DB fields are also misleading.

## Required implementation

1. Create a shared capability-default resolver for UI/provider forms.

Suggested location:

```text
src/CanDoItAll.Modules.Workspace/Providers/ProviderCapabilityDefaults.cs
```

or reuse a core service if dependency direction allows it.

2. Derive UI defaults from the same logic as `ProviderFeatureMatrix`.

If direct dependency is inappropriate, introduce a small DTO/mapping layer that mirrors core feature-matrix rules and is covered by tests.

3. Fix `ProviderManagementPanel.razor.cs` and `SettingsPage.razor.cs` defaults:

- OpenAI: structured output true if OpenAI/Azure OpenAI with Responses or Chat Completions transport.
- Ollama local/remote: structured output false by default.

4. Fix `WorkspaceBackedAgentProviderProfileRegistry.cs`.

Do not use only:

```csharp
entity.SupportsStructuredOutput = model.Transport == ProviderTransportKind.Responses;
```

Replace it with canonical feature-matrix logic or mark the DB field as a non-authoritative display flag.

5. Reconcile managed SQLite bootstrap.

`RuntimeHostServiceCollectionExtensions.cs` currently forces the persisted `SupportsStructuredOutput` flag false while the core runtime provider uses OpenAI Chat Completions, which the core matrix says supports structured output.

Choose one of these paths:

- Preferred: store/display computed runtime capability separately and remove this persisted flag from runtime truth.
- Acceptable: set the persisted flag to match the core runtime feature matrix and document it.

6. Update UI text to distinguish:

- “Runtime structured output support” = computed, authoritative.
- “Provider metadata flag” = optional legacy/user-entered information if still retained.

## Tests to add

- `ProviderCapabilityDefaults_marks_ollama_structured_output_false`.
- `ProviderCapabilityDefaults_marks_openai_chat_completions_structured_output_true`.
- `WorkspaceBackedRegistry_persists_structured_output_consistently_with_feature_matrix`.
- `ManagedSqliteProvider_runtime_and_display_capabilities_do_not_contradict_each_other`.
- Playwright/static tests updated so test fixtures do not claim Ollama structured output unless intentionally testing legacy UI only.

## Acceptance criteria

- No UI default claims Ollama structured-output support by default.
- Core runtime, registry, and UI display do not contradict each other.
- Tests prove the capability truth for OpenAI Responses, OpenAI Chat Completions, and Ollama.
