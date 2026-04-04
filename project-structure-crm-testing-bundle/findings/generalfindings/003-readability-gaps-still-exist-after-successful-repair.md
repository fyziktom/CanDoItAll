# General Finding 003: The Repaired Canvas Is Usable, But Some Readability Gaps Still Need Attention

## What Still Felt Weak

- Long titles are truncated aggressively at the default zoom after recomposition.
- The floating selection panel can obscure important neighboring nodes while the user is trying to judge the graph shape.
- Opening a subproject route can initially select the related parent project instead of the local project, which increases confusion during review.

## Impact

- These issues do not block plan creation or continued management use.
- They do slow down first-pass comprehension, especially when the plan was backfilled from a larger bundle.

## Recommendation

- Preserve the recomposed layouts because they are already a major improvement.
- Follow up with UI work on label width, panel placement behavior, and selection defaults so imported plans need less manual camera and chrome management before they can be read comfortably.
