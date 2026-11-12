# Resource portability delta review

The first complete untracked-inclusive scan covered 8,807 files and 37,009 findings,
without truncation. It found eleven added and four stale protected-source allowances.
All fifteen were inspected against source and the retained predecessor scan. The
post-correction scan covered 8,810 files and 37,014 findings, without truncation, and
produced the identical protected delta. The baseline refresh changed exactly eleven
allowances in and four out; unrelated policy remained identical. Final enforcement
without `--write-baseline` passed with 15,511 reviewed executable-source findings.

| Findings | Disposition |
| --- | --- |
| Generated Resource migration designer: RootPathState, RootPathSyntax, RaisesEscalation, EscalationCount | Four existing model property names copied into the canonical designer. No host path, executable or process API is introduced. Complete model review found only the Resource entity changed. |
| ResourceMetadataContracts immutable configuration dictionary | OrdinalIgnoreCase preserves the existing ConnectorConfigState/ConfigurationState key contract. It does not compare filesystem paths. |
| ResourceMetadataValues installed keys, webhook/governed connector comparisons, duplicate JSON key detection | Four uses preserve the registry's explicit culture-independent key identity. Ambiguous configuration keys are refused. No host-dependent comparison is added. |
| ResourceRegistryWorkspace configuration reconciliation | Distinct keys use the configuration owner's existing comparer; values and field versions still require their original equality. |
| ResourceBrowseOwner constructor | One added and one stale FileTools fingerprint reflect the new metadata dependency. Existing injected FileTools session/action boundaries remain; no executable discovery or shell invocation is added. |
| ResourceModels connector comparisons | Two stale fingerprints moved to the explicit ResourceMetadataValues preparation owner with the same connector-key semantics. |
| ResourcePromotionCommittedException | One stale FileTools import belongs to the deleted exception; typed committed/unconfirmed outcomes replace it. |

No genuine portability defect was found in this delta. Exact scan, before-baseline,
reviewed difference and final enforcement logs are retained in the owned
`resource-validation-preparation` evidence directory. Runtime proof remains separate.
