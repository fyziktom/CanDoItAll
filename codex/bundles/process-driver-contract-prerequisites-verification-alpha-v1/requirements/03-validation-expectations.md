# Validation Expectations

## Required Commands

- `dotnet build CanDoItAll.slnx --no-restore`
- Full unit test project.
- Focused process integration matrix:
  - route boundary,
  - subprocess lifecycle/projection,
  - artifact projection/validation,
  - finalizer descriptor paths,
  - execution descriptor paths,
  - driver prerequisite denial tests.
- Source scans:
  - Core forbidden dependency scan,
  - production driver token scan,
  - UI/media drift scan,
  - anti-stub scan,
  - public Core API snapshot guard,
  - subbundle row collapse guard.

## Browser Validation

N/A by default. If UI/media changes occur, fail the bundle unless the change is explicitly justified and large desktop proof is added. Do not create small/medium/mobile proof for this runtime bundle.

## Proof Requirements

Every critical gate must include:
- manifest,
- semantic invariants,
- source assertions,
- passing test transcript,
- failing-first or adversarial negative proof when applicable,
- anti-stub scan.
