# Score Geometry Review Request

## Raw Request

The user asked for another architecture review focused on the scoring model. Original design used simplified add/subtract sub-scores. A later architecture pass moved toward advanced score definitions using vectors or shapes in space. The user asked to analyze whether scores in different situations/functions really use the advanced system, to consider it as a generic driver reused across situations, and to improve the v2 bundle.

## Required Outcome

- Review every scoring, confidence, activation, salience, ranking, priority, belief, attention, and selection surface.
- Replace scalar-only decision models with reusable score-space, vector, and shape contracts.
- Preserve scalar display scores only as derived UI/sorting projections.
- Add a logical phase for the generic driver before dependent memory behavior.
- Update validations so implementations cannot regress to add/subtract scoring.

