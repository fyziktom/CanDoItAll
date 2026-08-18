# Presentation contracts

The names below are design intent. Codex may refine names while preserving ownership and behavior.

## Core primitives

```text
ConversationPresentationKey
  - opaque stable string value
  - nonblank
  - no Guid or Agent assumption

PresentationBadge
  - text
  - tone
  - optional icon
  - optional accessible description

PresentationMetaItem
  - label/value or display text
  - optional tooltip
```

## Participant

```text
ConversationParticipantPresentation
  - key
  - display name
  - optional subtitle/role
  - summary
  - avatar image URL
  - avatar seed/fallback text
  - source-neutral kind label/icon
  - badges
  - tags
  - metadata
  - selected/busy/disabled presentation state
```

Agent adapters calculate workload, status, private-provider, chat-history, favorite, capability count, and managed-agent badges. The neutral record does not import those enums.

## Thread

```text
ConversationThreadPresentation
  - key
  - title
  - optional preview
  - updated timestamp
  - selected/busy/disabled state
  - badges
  - metadata
  - optional accessibility description
```

Pending approvals and auto-approval state are adapter-provided badges/adornments.

## Message

```text
ConversationMessagePresentation
  - key
  - source-neutral author role
  - author label
  - visible markdown/text
  - optional explicit context summary/detail
  - created timestamp
  - optional token/meta text
  - copyable content
  - visual state needed by current behavior
```

Do not put Agent execution records, LlmMessage, ChatMessageRecord, or provider SDK chunks in this contract.

## Workspace

The workspace composes focused components and slots:

- header identity;
- header actions;
- status badges;
- transcript;
- before/after transcript adornments;
- current activity/execution slot;
- approval slot;
- composer;
- attachment slot;
- voice slot;
- prompt-gallery action;
- runtime-detail action;
- status/error region.

Slots prevent a broad parameter matrix and keep Agent-only behavior out of the neutral project.

## Settings

Use neutral editor/view models:

- configurable identity field labels;
- name, subtitle/role optionality, summary, instructions/system-prompt value;
- avatar presentation and action callbacks/fragments;
- provider options represented by source-neutral ids/names/default model/model options;
- model value and override state;
- optional temperature;
- advanced settings fragment.

Do not copy `ProviderProfile`, `LlmModelSettings`, or Agent policy models into the neutral layer.
