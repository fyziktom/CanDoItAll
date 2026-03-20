# ASCII UI Layout Sketches

These are deliberately rough but implementation-oriented.
Codex should follow the existing workstation styling language, not the ASCII aesthetics.

## 1. Dashboard maintenance section

```text
+----------------------------------------------------------------------------------+
| Maintenance                                                                      |
| Use these when you want a targeted grouping pass without reindexing everything.  |
|                                                                                  |
| [Build profiles] [Build missing embeddings] [Grouping dry run] [Apply run]       |
| [Refresh selected scope] [Refresh]                                               |
|                                                                                  |
| Latest grouping run: DryRun #42 | Completed | 18,420 scores | 1,982 review cases |
| Profiles stale: 312 | Embeddings missing: 927 | Suspicious groups: 14            |
+----------------------------------------------------------------------------------+
```

## 2. Catalog row badges

```text
+------------------------------------------------------------------------------------------------------+
| Title                                   | Status                                                     |
|-----------------------------------------+------------------------------------------------------------|
| Nocturne in D-flat major, Op. 27 No. 2  | Selected                                                   |
| Frédéric Chopin                         | Primary group: CHOPIN-NOCTURNE-OP27-NO2                    |
|                                         | +1 related group | Confidence: High | Manual lock: No      |
+------------------------------------------------------------------------------------------------------+
```

## 3. Score detail grouping card

```text
+------------------------------------------------------------------------------------------------------+
| Grouping                                                                                             |
|------------------------------------------------------------------------------------------------------|
| Primary group                                                                                        |
| [ExactWork]  CHOPIN-NOCTURNE-OP27-NO2                                                                |
| Display: Nocturne in D-flat major, Op. 27, No. 2                                                     |
| Composer: Frédéric Chopin                                                                            |
| Confidence: Definite                                                                                 |
| Why: Exact composer + opus/number match + title similarity + high embedding similarity               |
| [Open group] [Set as manual primary] [Remove]                                                        |
|                                                                                                      |
| Related groups                                                                                        |
| [WorkFamily] CHOPIN-NOCTURNES                                                                        |
| [Arrangement] CHOPIN-NOCTURNE-OP27-NO2-ARR-PIANO-DUET                                                |
| [Add group]                                                                                           |
|                                                                                                      |
| Manual controls                                                                                       |
| Manual group key: [____________________________] [Apply]                                             |
| Grouping lock: ( ) None  ( ) Protect manual  ( ) Do not auto assign                                 |
| Curator note: [___________________________________________________________________________]         |
+------------------------------------------------------------------------------------------------------+
```

## 4. Groups page with review tabs

```text
+------------------------------------------------------------------------------------------------------+
| Song groups                                                                                          |
| Search [_____________________]  Type [Any v]  Review [Any v]  Suspicious [ ]  Curated [ ] [Run]    |
|------------------------------------------------------------------------------------------------------|
| Tabs: [All groups] [Needs review] [Suspicious] [Dry-run proposals]                                  |
|------------------------------------------------------------------------------------------------------|
| Group title                         | Composer           | Type      | Members | Review | Confidence |
|-------------------------------------+--------------------+-----------+---------+--------+------------|
| Moonlight Sonata                    | Beethoven          | ExactWork | 2       | Draft  | High       |
| Nocturne in D-flat, Op. 27 No. 2    | Chopin             | ExactWork | 5       | Curated| Definite   |
| Symphony No. 5                      | Beethoven          | WorkFamily| 14      | Review | Mixed      |
+------------------------------------------------------------------------------------------------------+
```

## 5. Group detail page

```text
+------------------------------------------------------------------------------------------------------+
| [ExactWork] Nocturne in D-flat major, Op. 27, No. 2                                                  |
| Frédéric Chopin                                                                                       |
| GroupKey: CHOPIN-NOCTURNE-OP27-NO2                                                                   |
| State: Curated | Source: Hybrid | Members: 5 | Updated: 2026-03-19 12:42 UTC                         |
| [Edit canonical] [Merge] [Split selected] [Lock] [Sync tags] [Re-evaluate]                          |
+------------------------------------------------------------------------------------------------------+
| Cluster diagnostics                                                                                   |
| - Shared catalog: opus 27 number 2                                                                   |
| - Shared key: d_flat_major                                                                           |
| - No arrangement conflicts                                                                           |
| - 1 member has weaker composer alias confidence                                                      |
+------------------------------------------------------------------------------------------------------+
| Members                                                                                               |
| [ ] Title                                  | Role     | Confidence | Source | Why                   |
|--------------------------------------------+----------+------------+--------+-----------------------|
| Nocturne in D-flat major, Op. 27, No. 2    | Primary  | Definite   | Manual | Canonical            |
| Nocturne Op.27 No.2                        | Primary  | High       | Auto   | Catalog + embed      |
| Chopin Nocturne in Db                      | Primary  | Review     | Auto   | Alias + key + title  |
+------------------------------------------------------------------------------------------------------+
```

## 6. Dry-run review page

```text
+------------------------------------------------------------------------------------------------------+
| Grouping dry-run review                                                                               |
| Run #42 | Model: embeddinggemma | Norm v3 | Threshold profile: conservative                         |
| Auto-ready: 14,820 | Needs review: 1,982 | Rejected: 5,412 | Suspicious clusters: 14                |
| [Accept high-confidence safe] [Export review CSV] [Apply selected] [Discard run]                     |
+------------------------------------------------------------------------------------------------------+
| Proposed cluster                                                                                      |
| [ ] Beethoven / Moonlight Sonata / ExactWork / 2 members / High                                      |
| Why: exact composer, movement title alignment, embedding 0.944                                        |
| Warnings: full-work vs movement ambiguity                                                             |
| [Open side-by-side] [Accept] [Reject] [Split] [Change type]                                          |
+------------------------------------------------------------------------------------------------------+
```

## 7. Side-by-side evidence modal

```text
+--------------------------------------------------+--------------------------------------------------+
| Score A                                           | Score B                                           |
|--------------------------------------------------+--------------------------------------------------|
| Raw title: Moonlight Sonata, third movement       | Raw title: Piano Sonata No. 14...                 |
| Composer: Ludwig van Beethoven                    | Composer: Beethoven                               |
| Strict title: moonlight sonata movement 3         | Strict title: piano sonata number 14 ...          |
| Catalog: opus 27 number 2                         | Catalog: opus 27 number 2                         |
| Key: c_sharp_minor                                | Key: c_sharp_minor                                |
|--------------------------------------------------+--------------------------------------------------|
| Pair evidence                                                                                        |
| - Composer alias match: yes                                                                         |
| - Catalog match: yes                                                                                |
| - Movement conflict: yes                                                                            |
| - Embedding cosine: 0.944                                                                           |
| Final band: Review                                                                                  |
+------------------------------------------------------------------------------------------------------+
```
