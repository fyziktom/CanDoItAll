# Explicit initial inherited source admission — 2026-10-20

Status: focused architecture and behavior gate passed; product Process source activation remains next.

The existing background-source interface now has an explicit initial inherited observation,
unsupported by default. Core carries the bounded original parent through workspace and run
admission, validates the original input before observation, requires the callback to retain
that exact parent, constructs schema 7 directly from the authorized session, and rechecks
the completed source. Retained workspace requests must match their saved parent.
Ordinary background admission keeps its original path. No project, interface, runtime
partial or product producer was added.

The initial candidate executed 244 cases: 38 passed and 206 failed. It incorrectly built
a temporary schema 2 journal with inherited-only support; the model correctly rejected it.
The repair returns only an authorized session from the common initializer and constructs
the correct journal at each caller. No validator was weakened. Three additional input cases
cover missing, blank and UTF-8 oversized input before owner observation or binding.
The entire selection was rebuilt and repeated; there is no production bridge claim.

Final exact discovery and execution: **247/247 passing** in 739.918 s, including 16 new cases,
196 inherited/task regressions and 35 ordinary-background regressions. Tests cover explicit
opt-in, denial and cancellation before binding, unchanged original parent, final source
revalidation, retained workspace identity, actual publication/history and saved-effect paths.
Standard Release direct builds pass: Core 8.820 s and Integration 75.728 s.
Full portability enforcement passes with all 15,647 reviewed findings unchanged, alongside
baseline/secret-tool, rendering, documentation and diff checks.

Scoped Core architecture: snapshot `snap-20261020104433-f16f9706` to
`snap-20261020112650-f16f9706`; 777 types, 5763→5768 members, unchanged 221 findings,
189 diagnostics and existing type cycles. No Process dependency was introduced.

Frozen source: 9,637 inputs,
`DF5B26C6CABEE47E411609AAD3E2C9BB7CB7D4D69EB74955076D14F1EE5F5F36`.
Runtime: 1,449 files,
`01A5A1135CB68F58999097537E5D42D5571355CF0B509F24B70EF7F025F1C636`.
Final verification confirms both unchanged. The 752.415-second observer covers the complete
command. Both candidate roots are Released, with zero remaining observed processes or
database-name delta and five unowned TEMP paths preserved per run. Shared PG18 remains
needed; existing cleanup denials, including root-sequence-probe outputs, remain unresolved.

Retained, secret-reviewed proofs:

- [Initial failed candidate](../proof/2026-10-19-planning-authority/c4-inherited-initial-source.json):
  `69ED44522831D802F43A194B57B18844C0B13010871747914E40901CDE39EEE9`.
- [Final passing candidate](../proof/2026-10-19-planning-authority/c4-inherited-initial-source-final.json):
  `8196319E3F2FA0861328DA20CD40836879E6057655669B145C87ABA4D3EE8866`.

Next is the registered Process adapter with exact complete parent-policy comparison,
current profile fences and shared live/history binding interpretation. Producer/provider,
manager approval, atomic Process outcomes, Workflow and sustained capacity remain open.
SPA stays on its dedicated branch.
