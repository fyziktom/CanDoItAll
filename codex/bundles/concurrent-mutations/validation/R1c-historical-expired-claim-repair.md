# Historical expired-claim fixture repair

The first historical candidate executed 133 ordinary cases: 121 passed and 12 failed. Its results remain retained under `.artifacts/concurrent-mutations/validation/r1c-historical-candidate-validation`; this note records a successor, not a passing result.

The Process four-receipt Fact changed the original claim expiry to one hour before the current time. That also placed expiry before execution creation, invalidating the original evidence interval. The production authority reader correctly refused to reconstruct that evidence, so the test never reached its intended expired-but-original receipt-read assertion.

The repaired Fact preserves the persisted claim and advances its existing controlled clock beyond the claim's actual expiry. Its scoped production `EfProcessExecutionAuthorityQuery` receives the canonical Process context factory and that clock. Before expiry it asserts a valid original snapshot with current dispatch; after expiry and write revocation it asserts that the same original snapshot remains available without current dispatch. Historical pages must still enforce current read authority, preserve receipts and files, and use the expected two or three catalog decodes. Each shared/separate executor arrangement resets the clock before its own admission.

No production policy or test case count changes. The preceding source SHA256 is `CC77D01DE672F28DCAEA7B68616492A7A4D337F21BBEC24DEFF26B682FEDEAAC`, preserved in the owned repair output's `predecessors` tree. The separate current-schema prelaunch seed repair is recorded in [its proof](R1c-historical-process-authority-repair-source.md). Both require rebuilt, discovered and executed focused tests before closure.
