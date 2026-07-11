# Capture feature or function boundary

Restate the request as one behavior that can be implemented and validated in a single subprocess run. Record acceptance criteria, exclusions, repository target, assumed existing scaffold, and the smallest proof that will demonstrate the behavior.

If the parent request is a repair request, treat the parent repair target as the feature boundary. Do not derive a new MVP behavior. The scope packet must preserve the failed acceptance criteria, failing command or browser metrics, upstream child/parent artifact refs, and the smallest proof that would close the defect. Exclusions must not exclude the failing requirement that triggered repair.

Read every exact `ParentRequiredArtifactRefs` entry before defining a repair boundary. A runtime-synthesized parent handoff may expose the originating child no-go or validation artifact as an additional required ref. Treat that concrete defect evidence as authoritative; do not replace it with generic solution-layout, build-only, or test-count acceptance.

When the parent scope is a simple full app or one-shot broad deliverable, do not block only because it contains several core behaviors. Preserve every explicitly named criterion required for the recognizable MVP and continue. Do not invent a later slice unless the parent context supplies an explicit remaining-slice schedule.

Without such a schedule, named interaction, typed state transitions, persistence, calculation, search, dashboard, reload-restoration, and graceful-recovery behavior must remain in this feature boundary. Exclusions may remove optional polish, alternate platforms, or clearly deferred extensions, but not a core requirement merely to make the current implementation smaller.

Use upstream facts as decisions, not questions. Preserve the named product root, app archetype, target framework, test framework, UI/no-UI classification, required controls, validation hooks, and no-go constraints. Do not invent optional behavior or substitute easier contracts.

If upstream scope lists visual target ImageAsset ids or media paths, preserve them as acceptance inputs for the visible UI behavior. Record the target asset identity and media path in the feature boundary instead of translating it into an unsupported generic style sentence.

For a generated app, the derived behavior must be the complete runnable core workflow named by the parent. A scaffold, empty app shell, starter page replacement note, static-looking mock, or build-only proof is not a valid derived behavior. Record only optional polish or explicitly scheduled later work as exclusions; keep every core named interaction and persistence/recovery criterion that makes the requested product recognizable and usable.

Block only when the upstream evidence is contradictory, when product root/app archetype/validation boundary is missing, or when selecting one behavior would require inventing requirements.
