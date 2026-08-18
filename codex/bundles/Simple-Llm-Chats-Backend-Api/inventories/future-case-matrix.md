# Case coverage and future-readiness matrix

| Case | This bundle | Later bundle |
|---|---|---|
| reusable named chat definition | implement | UI editor |
| avatar metadata | implement | upload/generation UI |
| system prompt | implement | UI editor/templates |
| provider/model/temperature/settings | implement and validate | UI controls |
| per-model thinking-effort availability and override | implement typed provider-default/explicit selection, API projection, validation, dispatch, and audit | UI selector reuses API options |
| multiple durable threads | implement | thread UI |
| API conversation | implement | client SDK/skills |
| concurrent send | reject typed | UI state |
| idempotent retry | implement | client helper |
| cancellation | implement | UI cancel button |
| crash recovery | implement exact operation/turn flow | operator UI |
| definition update | immutable revision | UI revision display |
| suspend/kill switch | implement | admin UI |
| usage audit | implement | analytics UI |
| Project Structure whole project | documented stable boundary only | context bundle |
| selected node/subtree | documented stable boundary only | context bundle |
| files/images | documented boundary only | attachment bundle |
| streaming | operation contract compatible | streaming bundle |
| voice | no | UI/voice bundle |
| floating All/Agents/Chats | summary contract only | UI bundle |
| public web chatbot | transcript/operation model compatible; no dormant adapter | deployment bundle |
| anonymous visitors | no local-user mapping | deployment bundle |
| moderation/PII/rate limits | documented policy boundary only | deployment bundle |
| human handoff | no | deployment/collaboration bundle |
| tenant/data residency | scope model must not block it | deployment/policy bundle |
| enterprise SSO/external identity | no local-user invention | deployment/identity bundle |
| legal hold/eDiscovery/export | audit/immutable revision compatible | governance bundle |
| deployment revision rollout | immutable definition revision supports it | deployment bundle |
| RAG/Memory | no implicit dependency | dedicated capability |
