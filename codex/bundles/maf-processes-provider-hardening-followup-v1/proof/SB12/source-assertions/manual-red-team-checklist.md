# SB12 Manual Red-Team Checklist

| Check | Result | Evidence |
| --- | --- | --- |
| No direct MAF dependency on Processes, Projects, or Workbench product tool modules | Pass | `bundle://proof/SB12/transcripts/final-hidden-dependency-and-scope-scan.txt` |
| Removed hard-coded project-structure/image-generation attach paths did not return | Pass | `bundle://proof/SB12/transcripts/final-hidden-dependency-and-scope-scan.txt` |
| Tooling project stayed product-neutral | Pass | `bundle://proof/SB12/transcripts/final-hidden-dependency-and-scope-scan.txt` |
| Process runtime tool names and access/policy behavior stayed covered | Pass | `bundle://proof/SB12/transcripts/targeted-unit-provider-policy-tests.txt` and `bundle://proof/SB12/transcripts/targeted-integration-provider-process-tests.txt` |
| Real process runtime and subprocess artifact lineage stayed covered after SB11 fixes | Pass | `bundle://proof/SB11/transcripts/dotnet-test-integration-process.txt` and `bundle://proof/SB12/transcripts/targeted-integration-provider-process-tests.txt` |
| No process-core project or process driver-pack implementation was smuggled into this phase | Pass | `bundle://proof/SB12/transcripts/final-hidden-dependency-and-scope-scan.txt` |
| Full solution build remains clean | Pass | `bundle://proof/SB12/transcripts/final-dotnet-build-slnx.txt` |
| Whitespace and scoped anti-stub checks are clean | Pass | `bundle://proof/SB12/transcripts/git-diff-check.txt` and `bundle://proof/SB12/transcripts/anti-stub-audit.txt` |
| Rendered UI validation | N/A | No rendered UI route changed in SB12. |
