# Architecture change record

- Subbundle: SB02
- Before owner: no application-owned neutral conversation presentation boundary
- After owner: `CanDoItAll.Conversations.Components`
- Responsibility moved: none from production consumers in this subbundle; a new neutral owner now defines opaque presentation primitives and renders typed badge collections.
- Contracts moved: none; new `ConversationPresentationKey`, `PresentationBadge`, `PresentationMetaItem`, and `PresentationTone` contracts were introduced without backend assumptions.
- Adapter: none yet; SB03 owns the first Agent adapter/migration.
- New project references: AgentFramework.Components -> Conversations.Components; component tests -> Conversations.Components.
- Removed project references: none.
- Before snapshot/dependency evidence: `snap-20260816102508-c82f9e5f`.
- After snapshot/dependency evidence: `snap-20260816110147-d3f1a4be`.
- Cycle result: no project cycle; pre-existing intra-project findings unchanged.
- Independent tests: seven direct primitive/component cases instantiate the neutral owner with BaseLib only; no Agent runtime is constructed.
- Old owner responsibility reduction: intentionally zero at CP1 because production migration is forbidden; the new project is nevertheless executable UI, not a type bucket.
- Partial-class result: no partial files added.
- Service-location result: no DI/runtime service injection or lookup.
- Source-switch/boolean-matrix result: no source-kind switch and no capability boolean matrix.
- Rejected simpler option: placing records in AgentFramework.Components would not create the required dependency boundary or independent validation surface.
- Review decision: CP1 passes to SB03, with the CodeAnalytics broad-suite promotion scheduled for the single SB09 gate.

