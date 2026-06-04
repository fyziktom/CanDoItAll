# Implementation Prompt

You are implementing `maf-processes-provider-hardening-followup-v1` in `fyziktom/CanDoItAll` on top of branch `maf-processes-refactor`.

Rules:

- Execute subbundles in order.
- Stop after SB03, SB06, and SB09 for the forced refactor checkpoints.
- Do not extract process core or process contracts in this bundle.
- Do not introduce process driver packs.
- Preserve exact process tool names and approval/access behavior unless a subbundle explicitly changes them and proves the change.
- Do not remove MAF product-module references until source scans prove they are unused or a replacement provider is registered and tested.
- Keep code comments in English.
- Record proof under each subbundle's proof folder and update the execution report.

Start with `README.md`, then `plan/01-phase-plan.md`, then the current subbundle README.
