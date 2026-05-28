# SB01 Manifest

## Summary

SB01 repaired the bundle contract and classified prior proof debt without production-code changes. Because prior process/preflight bundles are not present locally, their claims are treated as reviewed-state inputs until later subbundles produce current transcript-backed proof.

## Changed File Hashes

- bundle://README.md SHA-256 b7c86743366df40244f9a8bae57f2e5699656f0d88f0e54d6e1377a8623e3784
- bundle://inputs/00-original-request.md SHA-256 665b2008b196103d7f4f4325d2381ee65bd0dc3fa6a9bbf9315176c5a21c54ab
- bundle://inputs/01-source-artifacts.md SHA-256 1d5531f0cd231116b3e352965393e1948498328cc8637cb22f1adab0fead0bfd
- bundle://inputs/02-structured-input.md SHA-256 f35c13bcd5698951860a9af5221ea47e52a762680a8fe2b22a20bd3ed085b199
- bundle://analysis/01-current-state.md SHA-256 fe557dfa5c1450ddd79d7fd7fe3a664d7141a69d39812805f3e62b26e406b0f2
- bundle://analysis/02-assumptions-and-risks.md SHA-256 df2a799bdf29c42c49c45dcafaca21385123f579b3811c8c8e9761509332c7e9
- bundle://plan/01-phase-plan.md SHA-256 4c561a3c027147600dbfb67486e841d0d01bb6dc8853d46d034950b4a00a91e0
- bundle://subbundles/01-post-live-run-evidence-and-proof-debt-audit/README.md SHA-256 56d85966d80139c2f5d1d79d8fcc92dcd28c009b203b125cded974f999eb3f92
- bundle://reviews/01-execution-report.md SHA-256 59290d398bc9cc8b61871d180514a590ce6131ef47e4186e093960fe4cf3c412

## Artifact References

- Proof debt audit: bundle://proof/SB01/proof-debt-audit.md
- Semantic invariant contract: bundle://proof/SB01/semantic-invariants.md
- Source assertions transcript: bundle://proof/SB01/transcripts/sb01-source-assertions.txt
- Local bundle inventory transcript: bundle://proof/SB01/transcripts/sb01-local-bundle-inventory.txt
- Anti-stub audit transcript: bundle://proof/SB01/transcripts/sb01-anti-stub-audit.txt

## Semantic Evidence

- Raw note owned: RN01
- Shipped behavior: bundle now has an executable prepared-stage contract and a proof-debt table that does not claim absent prior bundles as local proof.
- Source proof: bundle://proof/SB01/transcripts/sb01-source-assertions.txt
- Test proof: N/A - process audit, no production behavior change.
- Shallow-pass trap: marking every prior blocker closed because the successful live run reportedly completed.
- Adversarial negative proof: N/A - process/non-production audit; local inventory transcript proves prior process/preflight bundles are absent and cannot be used as artifact-backed closure proof.
- Semantic positive proof: bundle://proof/SB01/proof-debt-audit.md classifies each blocker and assigns downstream owners.
- Passing transcript: bundle://proof/SB01/transcripts/sb01-source-assertions.txt
- Anti-stub audit: bundle://proof/SB01/transcripts/sb01-anti-stub-audit.txt

## Closure Decision

- Entry gate: Passed.
- Closure gate: Passed for audit scope.
- Downstream dependency check: SB02 may proceed; runtime proof debts remain assigned to SB03-SB18 instead of hidden in SB01 prose.
