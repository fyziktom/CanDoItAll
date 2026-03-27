
# Master validation prompt

Validate the implementation as a skeptical senior QA inspector.

## Validation order

1. Confirm the implemented item matches its `SPECIFICATION.md`.
2. Confirm all acceptance criteria are met exactly.
3. Run the listed tests and inspect failures, not only pass counts.
4. Inspect all screenshot evidence semantically.
5. Confirm traceability still exists for every covered note.
6. Reject the item if any required screenshot or proof is missing.

## What to look for in screenshots

- Is the intended UI actually visible?
- Is the layout floating, pinned, searchable, scrollable, or side-aware exactly as requested?
- Is the visual hierarchy clear enough for a real user?
- Does the screenshot prove the claimed behavior, or merely show a static panel?

## Special handling

### LLM-backed items
Reject if any LLM action fires without explicit confirmation and provider selection.

### Execution-related items
Reject if the design still assumes impossible browser-native terminal launching.

### Prompt Factory duplicate-add bug
Reject if the author cannot explain the root cause and show regression evidence.

## Output format

For each validated item, require:
- implemented scope summary
- test evidence summary
- screenshot evidence summary
- known risks or follow-up notes
- final verdict: pass or fail
