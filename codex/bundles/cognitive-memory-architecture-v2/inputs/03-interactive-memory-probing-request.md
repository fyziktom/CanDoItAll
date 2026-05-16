# Interactive Memory Probing Request

## User Request Summary

The user wants the current Cognitive Memory architecture bundle extended with an Interactive Memory Probing capability. The probing capability should let the user talk to the memory module like a student, challenge it, ask it why it believes something, and quickly reveal knowledge gaps, stale assumptions, context confusion, and overconfident answers.

The user also notes that the architecture now includes an Epistemic Drive layer that creates a form of knowledge desire. Interactive probing must therefore be connected to Epistemic Drive: the drive layer should generate useful probe questions, and probe results should become evidence for knowledge gaps and learning proposals.

## Design Interpretation

The probing feature is not a chat UI bolted onto RAG. It is a controlled cognitive evaluation loop:

```text
human/random/drive-generated question
  -> recall with trace
  -> answer with confidence and source explanation
  -> user confirmation/correction/challenge
  -> probe outcome classification
  -> gap/correction/review/regression-test evidence
  -> consolidation and Epistemic Drive consume evidence later
```

The probing conversation must not directly rewrite authoritative truth. It may create evidence, correction candidates, review items, regression tests, and learning proposals.

## Required Architecture Impact

- Add durable probe session and probe turn records.
- Add probe question generation from coverage maps, gaps, staleness, contradictions, active directions, and serendipitous topic walks.
- Add user correction and answer assessment records.
- Add regression test generation from failed probes.
- Extend Epistemic Drive to consume probe outcomes.
- Extend UI with a Cognitive Memory Dialogue Workbench.
- Extend workflow/MAF integration with probe executors/tools where useful.
