# PP1 · Complete provider catalog and local editor

## Included

The actual `AgentProviderProfilesPanel` list/detail workspace: initial readiness, errors/retry, provider tree and expansion, search/reset/counts, selected versus new target, headline/status, metadata warnings, pending/unknown/verified-retry notices, and the six-section shell.

Extract the complete Connection, Prices, Runtime and Thinking sections, including the actual `ProviderProfileEditorForm`, pricing table/actions/summary and Thinking model table/search/edit dialog. Connection covers name, default model, kind, purpose, endpoint, metadata-only secret choice and tags. Runtime covers transport, suggested models, enabled/streaming/tools/history/background flags, raw configuration JSON and notes. Prices covers every currently represented rate, threshold and private-provider rule; Thinking covers automatic/discovered/configured metadata, control modes, allowed values and defaults. [R12, R19–R21]

Keep six tab identities/order and actual entry points: Connection, Prices, Runtime, Thinking, Sharing and History. Sharing and History are **retained production integrations**, as is the Shared provider connections dialog and the source-refresh control used from Thinking. They remain operational through narrowly typed slots/intents. PP1 is not their independent rendering closure. [R12, R25]

## Preferred placement

```text
src/UI/CanDoItAll.AgentFramework.Providers.UI
src/Sandboxes/CanDoItAll.AgentFramework.Providers.UiSandbox
```

Use equivalent established projects if the checkout already supplies the seam. A separate product contracts/presentation assembly is optional only for a real shared boundary. Do not create one project per tab, a new general provider facade, or a competing persisted ProviderProfileEditorModel.

The production AgentFramework module owns route/activation, native reads and writes, current profile, secret metadata adapter, source notifications, authoritative outcomes, actual dialogs and composition. The feature leaf owns reusable markup, neutral presentation, local editing behavior and typed intents. A cohesive view contract or presentation-plus-intents is allowed. Keep a single writable provider draft and EditContext.

```text
Web/composition -> AgentFramework host -> Providers.UI -> required Models/BaseLib/neutral UI
                  |                       ^
                  -> current owners       |
Independent Providers.UiSandbox -----------+
```

Never point Models, Core, ProviderManagement, Infrastructure or neutral shared libraries back at the new leaf. Do not turn the existing owner service graph into the sandbox's substitute runtime. Sharing/History children are supplied by the production host, not referenced from Providers.UI. Agent Editor, existing catalog/Overview and Workspace/Projects roots do not acquire the new provider UI as a hidden dependency.

## Native boundaries to reuse

`ProviderProfilesSession`, `ProviderEditorOperations`, `ProviderEditorSubmission`, `ProviderEditorRecovery`, reads and commands already implement meaningful selection and outcome protocols. Keep those responsibilities, simplifying only where the selected seam requires it. To share state with the sandbox, move genuinely presentation-only parts after separating implementation-bound read results; do not import `SecretListItem`, production services or a hidden IServiceProvider into the leaf. [R13–R18]

Reusing Models types is allowed after checking their actual transitive graph and payload. Catalog display should carry only the necessary safe values. A saved secret reference is not a resolved credential. Imported source IDs/model constraints must not become display-name authority. [R18, R23]

## Excluded

No API-only migration; no runtime/driver redesign; no schema/migration; no new provider kinds, models or automatic discovery; no new history purge/export behavior; no source-token issuance/publication/reconciliation redesign; no capability/team authoring; no Workflow canvas or Workbench/Process extraction. Provider test-chat/model-maintenance surfaces outside this four-tab host remain deferred, not silently attached as another task.

The execution must finish a renderer/asset/caller census. Classify each retained Razor as an owner/effect host, retained integration or unextracted renderer. “All provider administration is done” is not an acceptable PP1 completion claim.
