# Suggested Implementation Agent Prompt

You are implementing `process-dispatch-route-handler-pipeline-boundary-v1`.

Work only on `maf-processes-refactor`.

Preserve all behavior. This is a refactor/hardening bundle only.

Do not create Process Core. Do not create production driver APIs. Do not touch UI files or create mobile/browser proof.

Execute subbundles SB001-SB112 in order. Stop at each critical gate and produce the required manifest, semantic invariants, source scan and test proof. Do not collapse report rows.

Primary goal: split `ExecuteClaimedDispatchRouteAsync` into module-local route handlers while preserving exact route order and side-effect ownership.
