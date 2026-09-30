# Assessment of the previous validation

Sources: R01-R08. All execution numbers here are reported by the implementer; this reviewer
inspected the maintained reports, not the original private TRX, complete host logs or images.

## Stable is valuable, but belongs to a particular checkpoint

The reported frozen Stable run executed 15,849 cases: 15,849 passed, zero failed/skipped.
Discovery listed 15,794 descriptors, with 55 additional data-driven runtime cases reconciled.
Its 19-project inventory and source manifest are not interchangeable with a runner's top-line
count. The source-manifest SHA-256 is
`abfb2b368c24e06a107047aed3f84c3d72d2f33fb6d71ed0c4239a5d3ea88594`.
That 64-character value is not the 40-character Git commit SHA.

WC-F6 initial-editor acquisition and WC-F7 sandbox markup changed after that checkpoint.
Their reported focused leaf/host/browser evidence must be kept separate. It is inaccurate to
call the old Stable run an execution of every byte of final HEAD. This closure will change
shared lifetime code and possibly the exact control-plane editor boundary, so a fresh final
broad Stable run is required. Do not pay for repeating it at every local edit.

## Browser runner outcomes versus semantic coverage

| Attempt | Runner result | Actual reported meaning |
|---|---|---|
| First complete inventory, 142 cases | 123 passed, 19 failed | 122 passed, 7 failed, 12 blocked by prerequisites, 1 not run |
| Second complete inventory, 143 cases | 129 passed, 14 failed | 128 passed, 2 failed, 12 blocked, 1 not run |
| Later focused TestLab correction | Separate passing repair | Effective disposition becomes 129 passed, 1 failed, 12 blocked, 1 not run; this is not a third clean broad run |

A gated-off Scenario04 appeared passed to the runner but was not executed. Dynamic unavailable
cases were reported as failures by the runner; they need explicit environment classification,
not artificial red tests relabelled green. The shared Collaboration log still contains the
three lifetime paths; an isolated Collaboration or TestLab pass does not disprove that trace.

Original group failures are APP-01, APP-06, APP-10, APP-15, LIVE-01, LIVE-03 and GATE-02.
They are seven failed coverage groups, not seven independent production bugs. Several groups
are detecting the same shared-shell failures. Conversely WC-C1, WC-C2 and WC-C3 are distinct
stacks, not one cancellation exception copied to three feature names.

## Live validation

The old campaign reserved 40/40 requests, counting failures and retries. No execution is
reported above its ten-request bound. LIVE-02 is actual planner and HR approval/denial proof.
The file negative control passed with original sibling content and project nodes unchanged.
The three positive file attempts used six, seven and nine requests without a full pass.
The second retained a committed workspace write and pending separate asset attachment; no
atomic rollback of the whole conversation exists or is claimed.

WC-L1 includes confirmed approval-observation harness mistakes. The last failed attempt lost
failure read-back when its fixture database was dropped by the old watchdog disposal path.
The newer StopOwnedApplicationAsync preserves the database until later cleanup, but it was
not followed by a successful live run after the budget was exhausted. Deterministic proof
of a harness fix and live completion are different results. (R08, R25-R27)

WC-L2 first rejected temperature zero. The retry used nullable temperature and retained a
150-token output cap; HTTP success was followed by Failed in the provider journal and
Workflow. Missing terminal reason/body prevents concluding why. Test a sanitized status
observer and protocol fixtures first; never accept HTTP 200 as completion or relax the
provider's status check. (R08, R28, R29)

## Missing prerequisites and attribution

The reported external lanes need combinations of shared-provider publisher/client credentials,
TLS/certificates, controlled write opt-ins, Ollama/model/image capability, history relay and
the configured generated-app Scenario04 root/URL. Inventory exact tests and current gates.
They may now be available; provision private fixtures where permitted. Do not replace an
unavailable external provider with a stub while keeping the original live claim.

Neither one missing environment nor an exhausted budget is proof of a product regression.
Neither permits a global readiness pass. Attribute a refactor regression only with a
controlled before/after comparison or an actual changed-path causal proof. Historical
source blame establishes provenance, not that older builds executed the same scenario safely.

## Readiness axes to report separately

1. Workspace render boundaries complete.
2. Identified product fixes closed and controlled negative tests passing.
3. New Stable and composed browser coverage on the repaired source pair.
4. Required environment and live groups really executed.
5. Repaired Components dependency deliverable/reproducible in the consumer workflow.

Only their conjunction supports an unqualified `ready_for_next_module=true`. A documented
blocker can permit safe additional testing, not an automatic waiver of a required proof.
