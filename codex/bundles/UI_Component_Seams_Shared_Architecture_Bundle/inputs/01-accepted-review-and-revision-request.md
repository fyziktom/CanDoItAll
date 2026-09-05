# Accepted review and bundle-only revision request

## Latest owner request (verbatim, transport formatting omitted)

> those are good findings. improve the bundles.
> it sounds like larger improvements. We must be carefull to do not break any of actual functionalities.
> do not start implementation. just improve those two bundles.

## Source and authority

The owner accepted the preceding static architectural review in this conversation.
The following is a normalized finding map, not a verbatim transcript of that review.
It applies to the two named bundles only. The original owner directive and four supplied
bookmarkability source documents remain unchanged as historical input.

## Finding-to-repair closure

| ID | Accepted finding | Shared repair owner | Agents implementation-plan owner |
|---|---|---|---|
| F01 | Sandbox must not wait for production bookmarkability; measure actual iteration | architecture/04; plan/00 | SB01 baseline; SB03/SB07 downstream handoff |
| F02 | Parent injection cleanup is not subtree sandbox proof | architecture/04; assessment template | dependency inventory; SB04–SB06 |
| F03 | Public DTO declaring assemblies can retain the heavy graph | architecture/01 | contract inventory; SB01/SB04/SB07 |
| F04 | DialogService closes on location change; editor lifetime needs an explicit contract | architecture/02 | session/host contract; SB03/SB04 |
| F05 | Selection, editor target, mutable draft, version and circuit scope are different | architecture/02 | transition matrix; SB02–SB05 |
| F06 | Three-interface quota and concentrating effects in page/controller are wrong gates | architecture/03 and 06 | PSRs; SB02–SB05 |
| F07 | Aggregate query risks eager loads; existing history-host tests were omitted | architecture/03 and 05 | behavior matrix; SB01/SB02 |
| F08 | Preserve behavior and real wiring, not a fixed 46/18 test budget | architecture/05 | test inventory; all phases |
| F09 | Meeting pack was mischaracterized; navigation design remains proposed | architecture/02; plan/00 | routing analysis and decision register |

Bundle revision closure means these findings have concrete requirements, phase owners,
and planned evidence. It does not mean the application issues have been implemented or
the future behavior has passed tests.

## Original intent retained

The earlier owner requested review before implementation, application-wide component
decoupling with Agents first, no loss of current functionality, and preparation for
bookmarkability and faster dotnetwatch sandbox iteration. This follow-up authorizes
improving the plans after that review, not executing them.
