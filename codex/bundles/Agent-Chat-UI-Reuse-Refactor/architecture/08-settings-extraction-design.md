# Settings extraction design

## Reusable identity fields

- name;
- optional subtitle/role;
- summary;
- instructions text with configurable label and description;
- avatar display;
- choose/default/generate action slots supplied by the owner;
- validation messages supplied by the owner.

The Agent adapter uses the label `Instructions`. A future Simple Chat adapter may use `System prompt`.

## Reusable runtime binding

Define neutral provider options:

```text
ProviderOption
  - opaque id
  - name
  - enabled state
  - default model
  - suggested model names
  - optional badge/description
```

A neutral model selector owns dropdown/override behavior. Existing `ProviderModelSelector` may become an Agent-facing facade mapping `ProviderProfile`.

## Model settings

Phase 1 may extract:

- temperature as an optional source-neutral numeric field;
- an advanced-settings render fragment;
- generic disabled/loading/error presentation.

Keep Agent reasoning effort and provider-parameter policy in an Agent adapter unless represented through neutral option records without importing Agent enums.

## Agent-only tabs remain

- status;
- workload;
- chat history;
- approval policy;
- Memory;
- Images;
- capabilities;
- tools;
- skills;
- governance;
- project structure access;
- deletion and technical identity behavior;
- save/version/concurrency semantics.

## Floating settings

Extract only active-chat lifecycle presentation:

- hidden chat retention;
- maximum active chats;
- validation/status.

Keep prepared-agent metadata stock, adaptive preparation, and prepared retention in the Agent settings owner.
