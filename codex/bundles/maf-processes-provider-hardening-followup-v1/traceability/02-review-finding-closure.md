# Review Finding Closure Matrix

| Finding | Closure subbundle | Required closure proof |
| --- | --- | --- |
| F-001 direct MAF -> Processes dependency must stay removed | SB01, SB12 | Hidden dependency scans and architecture tests. |
| F-002 Tooling project needs metadata hardening | SB02 | Descriptor metadata tests and Tooling build. |
| F-003 MAF provider composition needs provider-neutral refactor | SB03 | Source audit proves no process-specific helper names remain. |
| F-004 Processes provider registration must remain stable | SB07, SB11 | Runtime composition integration proof. |
| F-005 project-structure attach path remains in MAF | SB04 | Provider parity and source scan removing attach method or documented exception. |
| F-006 image-generation attach path remains in MAF | SB05 | Provider parity and source scan removing attach method or documented exception. |
| F-007 process provider is large | SB07 | File/class split with parity tests. |
| F-008 provider purpose not yet strong policy | SB08 | Purpose/access matrix tests. |
| F-009 branch bundle churn | SB01 | Diff classification and cleanup decision. |
