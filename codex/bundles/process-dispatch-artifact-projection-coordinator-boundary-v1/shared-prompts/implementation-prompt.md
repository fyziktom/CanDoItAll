# Implementation Prompt

You are implementing the `process-dispatch-artifact-projection-coordinator-boundary-v1` bundle on branch `maf-processes-refactor`.

Rules:

- Execute subbundles in order.
- Do not create Process Core.
- Do not add production process driver APIs.
- Do not touch UI files.
- Do not create small/medium/mobile proof.
- Preserve all projection source families and source order.
- Keep planners side-effect-free and coordinators explicitly side-effectful.
- Add focused tests before or with source movement.
- Run gates and repair failures before continuing.
- Comments in source code must be in English.
