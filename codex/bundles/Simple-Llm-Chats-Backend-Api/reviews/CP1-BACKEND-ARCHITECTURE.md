# CP1 — backend architecture review

Status: Pass

## Required review lenses

- feature-block architecture review;
- canonical-model review;
- project dependency review;
- profile lifecycle review;
- persistence/CAS review;
- idempotency/recovery review;
- testability and partial-class review.

## Checklist

- [x] New domain project has no EF/Web/UI/agent-runtime dependency.
- [x] New persistence project owns EF and profile adapters only.
- [x] Existing generic transcript model remains canonical.
- [x] No duplicate provider/message/usage models.
- [x] Thinking-effort capability remains one provider/model truth; revision, fingerprint, dispatch, and audit preserve provider default versus explicit `None`.
- [x] Production does not globally register generic file-backed conversations.
- [x] Definition revisions are immutable and pinned.
- [x] Cross-process transcript CAS is proven.
- [x] Operation ID equals turn ID.
- [x] Profile switch prevents provider result commit, including the pre-commit race.
- [x] Idempotent replay cannot invoke twice.
- [x] Cancellation and reconciliation have direct tests.
- [x] No concrete context/deployment/UI work entered scope.
- [x] Focused tests comply with budget.

## Verdict

- [x] Pass — unlock SB07
- [ ] Reopen owning subbundle
- [ ] Stop bundle

Evidence: `proof/SB06/transcripts/04-cp1-focused-unit-union-final.txt`,
`proof/SB06/transcripts/05-composition-build.txt`,
`proof/SB06/transcripts/06-cp1-source-boundary-audit.txt`,
`proof/SB06/07-cp1-codeanalytics-review.md`, and the still-current SB03/SB05 PostgreSQL proof.
