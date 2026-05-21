# SB05 Semantic Invariants

## Invariant SB05-DIACRITIC-INSENSITIVE-CAPTURE-01

- Invariant ID: `SB05-DIACRITIC-INSENSITIVE-CAPTURE-01`
- Source raw note: Natural Czech and Q&A professor teaching must create structured temporary anchors.
- Expected behavior: Czech phrases with or without diacritics match teaching, scope, example, and counterexample signals.
- Disallowed shallow implementation: Requiring explicit remember/learn commands or ASCII-only Czech matching.
- Failing-first test: `SemanticInvariant_CuratorCaptureCzechDiacriticsAndNaturalScopeCreatesProfessorAnchor` failed in the SB02 baseline.
- Passing test: `SemanticInvariant_CuratorCaptureCzechDiacriticsAndNaturalScopeCreatesProfessorAnchor`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorExtraction.cs`.
- Production assertions: Search matching uses `NormalizeForSearch`; stored messages still come from `NormalizeText`.
- Red-team negative case: `LooksLikeQuestionOnly` still blocks pure questions without teaching context.
- Downstream dependency check: Curator service can now create anchors from natural Czech professor guidance.

## Invariant SB05-PRESERVE-STORED-TEXT-02

- Invariant ID: `SB05-PRESERVE-STORED-TEXT-02`
- Source raw note: Do not lower-case or strip diacritics in stored professor text.
- Expected behavior: The persisted capture summary includes original source utterances with Czech diacritics.
- Disallowed shallow implementation: Storing normalized search text as memory content.
- Failing-first test: SB02 red baseline had no capture for the Czech flow.
- Passing test: The Czech capture test asserts `Příklad`, `Protipříklad`, and `špatný rozsah` in the capture summary.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorExtraction.cs`.
- Production assertions: `SourceUtterances` are built from original normalized display text, not the search-normalized form.
- Red-team negative case: Search normalization is private and only used by comparisons.
- Downstream dependency check: Final proof can validate readable professor provenance.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Czech professor anchor capture | `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorExtraction.cs` | `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs` | `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` behavior test | `bundle://proof/SB05/transcripts/failing-first.txt` |
