# SB03 semantic invariants

- Public Agent component parameters and typed Agent callbacks remain available.
- Agent names, fallback roles/summaries, status/workload/private/history/selected badges, tags, metadata, and favorites are mapped with the existing copy.
- Agent switcher search fields remain name, role, summary, and model; tag filters exclude the Agent favorite marker; favorites remain first.
- Compact-list action labels, icon names, disabled-busy state, test ids, selection, double-click, new-chat, and history propagation remain unchanged.
- Stable card/list CSS classes and `data-testid` values remain in the rendered DOM.
- The neutral key tests use `external/source:participant-alpha` and `participant/not-a-guid`, proving no Guid assumption.
- No backend, AgentFramework, LlmChats, persistence, EF, or service-location reference exists in neutral production source.
