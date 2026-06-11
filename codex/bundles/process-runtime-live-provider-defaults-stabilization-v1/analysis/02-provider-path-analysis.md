# Provider Path Analysis

## Required provider behavior
Processes must continue to execute agents through:
- CRM-HR / AI party assignment.
- Agent profile with `ProviderProfileId`.
- `IAgentFrameworkWorkspaceService`.
- Managed provider profile (`OpenAI default` for OpenAI live smoke).
- MAF / AgentFramework runtime.
- Process dispatch `IProcessRunAutomationDispatchService`.
- Finalizer tool contract.

## Disallowed behavior
- Direct `OpenAIClient` or raw HTTP calls from process tests/services.
- Bypassing provider profiles by constructing provider config outside the workspace provider model.
- Replacing MAF provider selection with hard-coded test-only model routing.
- Treating an arbitrary env model override as repository default.

## Current issue
The live smoke correctly uses the managed provider object, but it overwrites `DefaultModel` with the explicit env value. Because the user-provided value was `5.4-mini`, provider execution reached OpenAI but failed with `model_not_found`.

## Required fix
Add a provider-model resolution policy:
1. If `CANDOITALL_LIVE_PROCESS_RUN_OPENAI_MODEL` is set, use it as an explicit override and record it.
2. If it is absent, use the current managed provider `DefaultModel`.
3. If provider `DefaultModel` is empty, fall back to the first suggested model from the provider profile.
4. If none is available, fail as `provider-default-missing`, not as skipped proof.
5. Never print `OPENAI_API_KEY`.
6. Record provider id/name/kind/transport/purpose/model source in the live transcript.
