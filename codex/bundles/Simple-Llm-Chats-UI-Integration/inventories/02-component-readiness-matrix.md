# Component Readiness Matrix

| Surface | Current readiness | Required before Simple Chat consumer |
|---|---|---|
| Participant card | Ready with minor value hardening | Defensive collections/key bounds |
| Compact list/picker | Ready | Generic kind filter stays outside component; defensive collections |
| Thread rail/history | Ready | Simple Chat mapper and paging owner |
| Workspace/header | Ready | Product-owned slots and statuses |
| Transcript | Almost ready | Multiple transient messages and Assistant streaming state |
| Markdown | Almost ready | Safe URI scheme policy |
| Composer | Ready | Use existing ContextActions and Primary/Secondary action slots; no context button yet |
| Identity editor | Ready | Simple Chat labels and mapper |
| Provider/model selector | Ready | Use ILlmChatProviderResolver options |
| Temperature field | Ready | Product capability validation |
| Definition editor shell | Ready | Simple Chat status/revision/advanced sections stay product-owned |
| Floating catalog/window | Presentation-ready | App-level contributor shell and source-specific coordinators |
| Active list | Needs hardening | Generic declared actions, no hard-coded Stop |
