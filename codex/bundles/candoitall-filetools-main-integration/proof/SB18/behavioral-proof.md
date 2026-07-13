# SB18 Behavioral Proof

## Final Outcomes

- The final published candidate starts healthy through the managed runtime after strict placement decoration moved to the two application composition roots.
- Projects visibly retains the portfolio hierarchy tree. Four current roots render in live data; direct component coverage constructs parent, child, grandchild, and unrelated projects and proves selecting the parent keeps exactly the recursive subtree.
- A real 1.7 MB Project Structure PDF was opened from the authorized file collection. The final surface is visible at both desktop viewports, uses `application/pdf` plus a `blob:` URL, has non-zero 715x470 geometry, and displays the document.
- The PDF handoff has one FileInteraction root, zero FileBrowser roots, no `/storage/objects/preview` request, no horizontal overflow, and no console warning/error, failed request, or HTTP response at or above 400.
- Projects, ready Project Structure, Processes, and Resources routes render at both required viewports with zero console/network problems in the clean final run.

## Repaired Behavior

1. Reusable runtime-module registration no longer assumes the Infrastructure concrete placement service exists. Production and test application roots apply the strict decorator explicitly after Infrastructure registration.
2. The PDF viewer no longer hides the successfully bound `<object>` while waiting for an unreliable `load` event. Images retain their decode/load readiness path; PDF errors still transition to the inert fallback.

## Negative Outcomes

- Forged unsigned `/storage/objects/preview?token=forged` returns 401.
- No legacy preview resource appears in the final browser performance entries.
- No silent fallback, unsigned reference authority, runtime service locator, new partial class, or browser construction for the known-file handoff was added.

