# Sample Data

This folder contains the staged demo corpus for the multi-cycle Cognitive Memory validation.

## Files

- `source-manifest.json`: machine-readable source register.
- `trackers/cognitive-memory-demo-source-tracker.xlsx`: human/audit tracker used to map source files to expected memories, chat probes, analysis rows, and repair logs.
- `staged-sources/stage-01-baseline-detail`: baseline project context.
- `staged-sources/stage-02-operational-updates`: updates that should merge with or extend existing memories.
- `staged-sources/stage-03-contradictions-and-decisions`: contradictions and accepted decisions.
- `staged-sources/stage-04-email-and-instructions`: email-style Markdown assets and operating instructions.

## Loader Rule

Execution must load these files through APIs. Do not copy this data into automated test code and do not insert it directly into Cognitive Memory tables.
