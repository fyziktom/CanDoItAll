# QA Prompt

Review the executed subbundle as a senior C# architect and QA gatekeeper.

Check:

- Does the subbundle satisfy its objective and nothing outside its scope?
- Were prerequisites and dependency gates respected?
- Are package decisions supported by current NuGet CLI evidence?
- Are compile fixes limited to adapter seams?
- Did approvals, finalizers, structured output, provider gates, telemetry, context manifests, and session compatibility remain intact?
- Are tests behavior-focused rather than non-null/count-only checks?
- Did source scans prove no direct process runtime tool provider or process route expansion?
- Are command transcripts, changed-file hashes, source assertions, and anti-stub audit artifacts recorded for critical subbundles?
- If UI or host-visible behavior was validated, are route/window, viewport, actions, assertions, screenshot paths, and result recorded?
- Are skipped tests tied to concrete environment reasons and not used to hide risk?

Reject closure if:

- evidence is prose-only for a critical gate;
- package updates are broader than the matrix allows;
- a new abstraction has only the old runtime as trivial implementation;
- a new helper cannot be unit-tested without constructing the old runtime;
- implementation added broad fallback behavior that silently hides errors.
