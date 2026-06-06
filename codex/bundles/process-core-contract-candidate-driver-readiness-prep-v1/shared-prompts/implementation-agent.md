# Implementation Agent Prompt

You are implementing this bundle on branch `maf-processes-refactor`.
Do not create Process Core. Do not add production driver APIs. Preserve behavior.

Work subbundle by subbundle. Every critical gate must pass before continuing.
Do not collapse execution report rows. If a subbundle is skipped, mark it explicitly with reason and reopen impact.

For every moved behavior:
1. Preserve route order and side-effect order.
2. Keep adapters at explicit edges only.
3. Add or update focused tests before declaring parity.
4. Run source scans for forbidden Core/driver/UI/stub tokens.
5. Record proof transcripts under the owning subbundle.
