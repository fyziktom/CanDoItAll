# CDA-UI-SEAMS-PROVIDER-REQUEST-HISTORY-01

Status: implementation in progress, authorized after the Diagnostics focused/browser/extraction gate passed.

This single bounded bundle preserves explicit Search, draft/applied query separation, fixed scope, validation, 32-cursor paging, cancellation, coverage and lazy authorized metadata/content reads. It does not change history backend policy, routing or provider mutations.

ProviderHistorySearchState remains the search owner; the Module panel owns mutable filters, authorization/profile lifecycle and selection. The UI assembly receives immutable results and typed navigation/selection intents. Pure metadata/content views move once; the details host retains authorization-bound reads and completion-owned cancellation. No raw exception messages are public. Captured content remains exactly as authorized by the backend, with existing flags, byte counts and expiry.

Sequence: strengthen existing public behavior tests; extract the pure closure with a direct reference only to ProviderHistory.Abstractions; exercise eight sandbox scenarios and real Web reads; freeze source; run affected predecessor and feature selections, one final broad stable gate and static gates. Watch validation is one representative Razor/C#/owned CSS edit for each new Surface on Web, Parity and Fast. No cold-start campaign.

Evidence shares the Diagnostics budget: ten files, four MiB, two screenshots. Captured content never appears in retained screenshots. Governance and sibling source remain unchanged. Future Simple Chats/Workflows work is report-only.
