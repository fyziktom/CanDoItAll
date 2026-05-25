# Shared QA Prompt

Review the implemented subbundle as a skeptical process-runtime QA engineer.

Check:
- Does the fix use production runtime paths?
- Does it avoid prompt-only behavior?
- Does it work for non-software processes?
- Does it respect the Processes vs Workflows boundary?
- Does it avoid reintroducing SQLite assumptions?
- Are tests red-team enough to fail the old behavior?
- Are proof manifests complete?
