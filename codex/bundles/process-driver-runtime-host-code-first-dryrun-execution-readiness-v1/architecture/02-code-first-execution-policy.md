# Code-First Execution Policy

## Why this policy exists

The previous bundle produced a large amount of bundle/proof churn compared with a small source-code delta. This bundle must invert that.

## Implementation ratio gate

At SB003, SB015, and SB030 Codex must record:

```powershell
git diff --stat <bundle-start-sha> HEAD -- src tests docs
git diff --stat <bundle-start-sha> HEAD -- codex/bundles
```

The gate fails if:
- bundle/proof changes dominate and source/test changes do not materially implement the target behavior,
- the implementation creates many subbundle/proof files without corresponding production/test code,
- the execution report claims completion by repeating generic proof language.

## Subbundle style

Use 30 subbundles total. Each subbundle must be larger and implementation-oriented.

Do not create:
- 60+ repeated subbundle READMEs,
- per-subundle manifest forests,
- duplicated semantic boilerplate,
- proof transcripts with no new command or source behavior.

Critical gates only: SB003, SB006, SB009, SB012, SB015, SB018, SB021, SB024, SB027, SB030.
