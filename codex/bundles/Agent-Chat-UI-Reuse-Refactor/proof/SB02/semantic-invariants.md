# SB02 semantic invariants

- Opaque keys are nonblank and are compared as exact values; the neutral layer does not parse or classify them.
- Presentation records contain UI-ready values only and do not expose Agent, LlmChats, provider, persistence, or runtime entities.
- `PresentationBadgeList` maps typed source-neutral tones to BaseLib tones and renders no empty layout shell.
- The neutral project has no DI registrations, injected services, service lookup, runtime effects, persistence, routes, or product menus.
- Agent entry points and all current production rendering remain unchanged until their owning migration subbundles.
- Future extraction must add focused components/adapters, not a source-kind switch or universal conversation service.

