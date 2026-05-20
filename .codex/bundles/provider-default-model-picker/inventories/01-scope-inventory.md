# Scope Inventory

| Surface | Current model behavior | Planned action |
| --- | --- | --- |
| Agents Runtime tab | Plain text `InputText` writes `AgentEditorModel.Model`. Empty model already resolves provider default at runtime. | Replace with shared selector and clear model on provider change. |
| Agent runtime execution | `ManagedSeedProviderFallbacks.ResolveModel` maps empty agent model to `ProviderProfile.DefaultModel`. | Preserve. |
| Provider profiles | `DefaultModel` plus `SuggestedModels`; Ollama health can discover models. | Use as selector inputs. |
| Workflow canvas new LLM component | Local provider and model dropdown/text logic, concrete model save. | Review and adopt shared selector if it fits without changing workflow persistence semantics. |
| Cognitive Memory settings tab | Provider policy allow-list only; no direct per-role model picker visible in tab. | Document as reviewed unless an existing model editor is discovered. |
| Voice settings | Separate STT/TTS model text fields with provider selectors. | Follow-up candidate; do not widen unless cheap and safe. |
