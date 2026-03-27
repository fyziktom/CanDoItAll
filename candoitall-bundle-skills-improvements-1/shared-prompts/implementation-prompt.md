# Implementation Prompt

```text
Implement the selected subbundle only.

Treat the bundle skills and validator as production artifacts. Tighten them enough to catch the issues discovered during the feedback5/feedback6 workflow run:
- validator must check exact source references and feedback execution-report scaffolding
- workflow and execution rules must force final bundle status synchronization
- mtp-hot-reload must stay optional, MTP-gated, and never count as final proof

Keep the changes generic for future bundles, then re-run the validator on this bundle and on at least one shipped feedback bundle.
```
