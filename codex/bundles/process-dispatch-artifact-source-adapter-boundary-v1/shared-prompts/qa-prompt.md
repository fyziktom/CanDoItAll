# QA Prompt

Review whether the artifact projection source-adapter boundary preserves behavior.

Required questions:

1. Did every migrated projection source preserve external reference key format?
2. Did duplicate-skip behavior remain unchanged?
3. Did artifact trust status and required-artifact satisfaction remain unchanged?
4. Did recovery lineage keep compact keys and source lineage fields?
5. Did storage/DB side effects stay out of pure source adapters?
6. Did write coordinator migration affect only the intended execution-artifact write path?
7. Are Process Core and driver-pack projects still absent?
8. Are small/medium/mobile proof artifacts absent?

Browser validation is N/A unless UI files changed; if UI files changed, flag as scope drift.
