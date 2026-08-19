# Compatibility and DOM contract

## Stable public entry points

Keep these names callable until migration and proof complete:

- `AgentSelectionCard`
- `AgentCompactList`
- `AgentCompactListItem`
- `AgentSwitchDialog`
- `ProviderModelSelector`
- `ChatPromptTextArea`
- `ChatWorkspacePanel`
- current Agent Chat panels/dialogs/hosts

They may delegate internally to neutral owners.

## Stable observed behavior

Record and preserve:

- `data-testid` values used by owner tests;
- accessible names and roles;
- visible copy;
- selected/busy/disabled states;
- action ordering;
- tooltip content and trigger behavior;
- keyboard input/send behavior;
- dialog close/focus behavior;
- scroll owners;
- first-viewport hierarchy;
- overlay placement/layering;
- CSS appearance.

## CSS isolation rule

Moving Razor markup changes generated CSS scope ownership. Before moving a component:

1. inventory its `.razor.css` selectors and referenced classes;
2. decide which project owns the resulting CSS;
3. keep compatibility classes or update CSS and tests in the same subbundle;
4. inspect rendered output;
5. do not leave duplicate conflicting isolated styles.

Internal class names are not sacred when unobserved, but any change must preserve visual and interaction behavior and must not break automation selectors.
