# SB01 Proof Manifest

## Subbundle

- ID: SB01
- Title: Baseline coupling inventory and proof plan
- Status: Completed
- Critical foundation: Yes
- Owned requirements: RQ-010, RQ-014
- Raw notes: "po mensich krocich"; "nesmi se ztratit"; "nesmi veci zjednodusit nebo neco vynechat"
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`

## Changed Files With Hashes

| File | SHA-256 | Reason |
| --- | --- | --- |
| `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | `B97C997D39070A9ECB42B734604EF3AA5446BDBFF52478B748451E0697EAABA3` | Baseline direct project reference source. |
| `src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProcessTools.cs` | `7475176625B7792FAA861C2FECE9958F088E88BFF8E7C43D0F2CD2D0137C2E4F` | Baseline process tool builder source, now deleted by SB05. |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs` | `8AD6B2EAC5A813FA65DB394E5F24464F5E6857F7BA3FB6CF68BE4806ACF860BE` | Baseline hard-coded process attachment source. |
| `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs` | `9763AF85602857A188CC8AD6BCA9B6FDFDACC8D0CCC07B85E26B0403C8CC8092` | Baseline process tool policy constants. |
| `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolContractCatalog.cs` | `29035F14F5CF96D21B2C0E4339185CA54B97FABE950685511C0A39C2A9BD95B9` | Baseline known-tool catalog source. |
| `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolCapabilityRegistry.cs` | `24E1CF07F33B014597C824DB7D3C15F0F1BF0BB7F4B4BABE29CDC796C932C3E6` | Baseline read/mutation policy classification source. |
| `repo://codex/bundles/maf-processes-decoupling-bundle-v1/inventories/01-process-tool-parity-inventory.md` | `961BFDFB7EE2271ABAB2BA9296A8C34D10E0F2ABF1C7A81990F35D76A90A4BDF` | Baseline exact process tool inventory. |
| `repo://codex/bundles/maf-processes-decoupling-bundle-v1/inventories/02-source-impact-inventory.md` | `5140743EFB698ED5815AE4956324EB0CF32454F3583C33C54D76B5244ACD84D2` | Baseline source impact inventory. |
| `repo://codex/bundles/maf-processes-decoupling-bundle-v1/inventories/03-test-impact-inventory.md` | `A59BFA1735E94F369152FD7B659BCD19C7194D80650E50A92F4F34ED5ECCA54D` | Baseline test impact inventory. |

## Commands

| Command | Transcript path | Exit code | Purpose |
| --- | --- | ---: | --- |
| `rg -n "CanDoItAll\.Modules\.Processes" src/CanDoItAll.AgentFramework.Maf` | `bundle://proof/SB01/transcripts/source-coupling-grep.txt` | 0 | Baseline MAF -> Processes coupling locations. |
| `rg -n "CreateProcessToolBuilder\|ProcessToolBuilder\|AttachInternalProcessToolsAsync" src/CanDoItAll.AgentFramework.Maf tests -g !bin -g !obj` | `bundle://proof/SB01/transcripts/process-builder-grep.txt` | 0 | Baseline hard-coded process builder attachment locations. |
| PowerShell regex extraction with policy-constant resolution | `bundle://proof/SB01/transcripts/process-tool-name-extract.txt` | 0 | Exact 23-tool source inventory and comparison to bundle inventory. |
| Dispatcher partial inventory with line counts | `bundle://proof/SB01/transcripts/dispatcher-partial-inventory.txt` | 0 | Confirms dispatcher is large and remains out of scope. |
| `dotnet build src\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj --no-restore -v:minimal` | `bundle://proof/SB01/transcripts/maf-project-build-baseline.txt` | 0 | Baseline MAF project build proof. |
| `Get-Process -Id 48648` | `bundle://proof/SB01/transcripts/baseline-web-lock-process.txt` | 0 | Documents unrelated running Web process that locks normal Web output. |
| `dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore -v:minimal -p:CopyRetryCount=0` | `bundle://proof/SB01/transcripts/web-lock-build-baseline.txt` | 1 | Reproduces unrelated Web output lock baseline blocker. |

## Validator Proof Citations

- Adversarial negative proof: N/A process/non-production baseline inventory; SB01 records current coupling and exact process tool inventory before implementation starts.
- Passing transcript: `bundle://proof/SB01/transcripts/process-tool-name-extract.txt`.
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`.

## Source Assertions

| Assertion | Source path | Result |
| --- | --- | --- |
| MAF currently references Processes directly. | `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`; `bundle://proof/SB01/transcripts/source-coupling-grep.txt` | Found direct project reference. |
| MAF currently imports Processes namespace. | `src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProcessTools.cs`; `bundle://proof/SB01/transcripts/source-coupling-grep.txt` | Found `using CanDoItAll.Modules.Processes;` in the SB01 baseline before SB05 deleted the file. |
| Current process tool source surface has exactly the inventory's 23 names. | `bundle://proof/SB01/transcripts/process-tool-name-extract.txt` | 23 source names, 23 inventory names, no missing source/inventory entries. |
| Dispatcher migration is out of scope. | `bundle://proof/SB01/transcripts/dispatcher-partial-inventory.txt` | 33 dispatcher partial files inventoried; no dispatcher source changed in SB01. |
| Baseline MAF build succeeds. | `bundle://proof/SB01/transcripts/maf-project-build-baseline.txt` | Exit code 0. |
| Full normal Web build is blocked by a running Web process, not by SB01 source changes. | `bundle://proof/SB01/transcripts/baseline-web-lock-process.txt`; `bundle://proof/SB01/transcripts/web-lock-build-baseline.txt` | `CanDoItAll.Web` process 48648 locks copied output DLLs; Web build reproduces MSB3021 lock failures. |
| Anti-stub audit found no new stubs from SB01. | `bundle://proof/SB01/source-assertions/anti-stub-audit.txt` | Matches are expected baseline Processes references and inventory text, not production `TODO` or `NotImplemented` additions. |

## Semantic Adequacy Gate

| Label | Evidence |
| --- | --- |
| Raw note owned | Small steps must not get lost, and no process tools may be simplified or omitted. |
| Shipped behavior | SB01 shipped a source-grounded baseline only: current coupling points, exact tool names, dispatcher scope, and guardrail proof plan are durable before implementation starts. |
| Source proof | `bundle://proof/SB01/transcripts/source-coupling-grep.txt`, `bundle://proof/SB01/transcripts/process-builder-grep.txt`, and `bundle://proof/SB01/transcripts/process-tool-name-extract.txt`. |
| Test proof | `bundle://proof/SB01/transcripts/maf-project-build-baseline.txt`; full Web build blocker documented in `bundle://proof/SB01/transcripts/web-lock-build-baseline.txt`. |
| Shallow-pass trap | Counting process tools or checking only one string would miss policy-constant tool names and allow a later migration to drop tools. |
| Adversarial negative proof | Tool extraction resolves both string literals and `AgentToolInvocationPolicyMetadata` constants, catching the two process template tools and role-add tool that a string-only scan would undercount. |
| Semantic positive proof | Source extraction and inventory comparison both report 23 exact names with no missing source or inventory entries. |
| Anti-stub audit | `bundle://proof/SB01/source-assertions/anti-stub-audit.txt`. |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative test |
| --- | --- | --- | --- | --- |
| N/A | SB01 introduces no production signal, state, record, or event. | N/A | N/A | N/A |
