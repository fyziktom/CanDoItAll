# Final long-content visual correction

The final screenshot inspection found an existing renderer sizing defect: a long title consumed the grid's intrinsic title track, reducing statistics to zero width. The card content also used its intrinsic long endpoint width inside a start-aligned Stack (about 963px inside a 325px card). The first browser assertion failed before the correction; raw geometry and images are retained in browser-SB02-long-red and browser-SB02-long-geometry-red.

Two renderer-owned CSS rules correct these observed defects: cap the title's intrinsic width at 20rem, and size card content to 100% of its existing owner. No new wrapper, scroll owner, application behavior or sibling change is introduced. Short normal headings remain within the cap.

Direct UI and both sandbox builds pass. SHA-256 checks prove the UI and both sandbox DLLs are byte-identical across the CSS-only correction, so the ongoing stable application-binary gate is not invalidated. Final browser-SB02-long-green passes all 29 states plus filters, tree, query, recovery and accessibility behavior in both modes. Header statistics retain at least 200px in the long case; content fits its card. The corrected Fast long-content screenshot was visually inspected. Earlier fail-closed, normal, warning and full-app details/recovery images were also inspected.

The measurement source hashes are refreshed for the two corrected CSS files before any post-extraction timing. The edit definitions, visible predicates, fixture, SDK, timing harness and accepted pre-extraction series are unchanged. This follow-up supersedes the pending long-content inspection in the initial SB02 closure.
