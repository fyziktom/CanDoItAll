# Input Coverage Matrix

| Raw note | Requirement ids | Owning subbundles | Closure proof |
| --- | --- | --- | --- |
| `N001` implementation added from source bundle | `R001`, `R012`, `R024` | `SB01`, `SB08` | Audit inventory and closure review compare source bundle intent with current code. |
| `N002` find weak points | `R001`-`R024` | `SB01`, all follow-on subbundles | Weak-point inventory is mapped to architecture and subbundle gates. |
| `N003` prepare new bundle only | `R024` | `SB08` | Prepared-stage validator passes; no product implementation changes. |
| `N004` performance analysis | `R015`, `R021`, `R022`, `R023` | `SB07`, `SB08` | Performance checklist, output cap tests, and artifact-vs-EF proof. |
| `N005` EF Core query analysis | `R020`, `R021`, `R022` | `SB07` | EF projection/index/paging tests and review notes. |
| `N006` Docker plugin behavior | `R006`-`R011`, `R014`, `R015` | `SB03`, `SB06` | Docker recipe unit tests, optional CLI smoke, and sample workflow proof. |
| `N007` LLM summary from Docker logs | `R014`, `R015`, `R023` | `SB05`, `SB06` | Workflow run shows Docker logs node feeding a separate LLM node. |
| `N008` plugins remain generic | `R002`, `R007`, `R012`, `R014` | `SB02`, `SB03`, `SB05`, `SB06` | Docker-specific code is limited to sample plugin/recipe implementations. |
| `N009` explicit user control over files/PowerShell | `R001`-`R011`, `R016`, `R018` | `SB02`, `SB03`, `SB04` | UI/API and runtime tests prove denied-by-default and explicit grant changes. |
