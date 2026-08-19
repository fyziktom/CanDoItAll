# Future case matrix

The hardening must avoid closing these future paths while not implementing them now.

| Future use | Foundation needed now | Deferred owner |
|---|---|---|
| Main application Simple Chat UI | stable definition/conversation/query/operation/SSE contracts | UI integration bundle |
| Floating catalog with agents/chats | stable chat identity/status/capabilities projection | shared-component + UI bundles |
| Project Structure context | turn accepts future typed context references without agent authority | context bundle |
| Attachments/images | operation/event bounds and extensible turn input | attachment/context bundle |
| Voice | request-independent operation and streaming output | UI/voice bundle |
| Internal enterprise chatbot | durable operations, auth scopes, external-client API | deployment bundle |
| Public embedded chatbot | deployment-pinned revision, participant/channel model | deployment bundle |
| Multi-tenant/organization scoping | profile/organization ownership and auth seams | deployment/security bundle |
| Human handoff | terminal/nonterminal operation/event model | deployment/collaboration bundle |
| RAG/memory | explicit context source separate from transcript truth | context/memory bundle |
| Streaming structured output | typed stream/final validation boundary | future provider/API bundle |
