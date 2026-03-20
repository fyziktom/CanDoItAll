# Layout Patterns And ASCII Sketches

## Pattern 1: Standard Management Page

Use for:

- Projects
- Resources
- Prompt Gallery
- Validation Center
- Test Lab
- Settings

```text
+--------------------------------------------------------------+
| Compact shell context / tabs                                 |
+--------------------------------------------------------------+
| Page header: title | subtitle | primary action | secondary   |
+--------------------------------------------------------------+
| Optional summary tiles / context hint                        |
+--------------------------------------------------------------+
| List/detail shell                                            |
| +----------------------+ +---------------------------------+ |
| | list header          | | detail header                   | |
| | search / filters     | | selected item title            | |
| | count / new action   | | status / metadata              | |
| +----------------------+ +---------------------------------+ |
| | selectable rows      | | structured detail content       | |
| | with selected state  | | split into sections             | |
| | and compact metadata | |                                 | |
| +----------------------+ +---------------------------------+ |
|                        sticky action footer in detail pane   |
+--------------------------------------------------------------+
```

Responsive notes:

- below `xl`, list stacks above detail
- navigation must still be reachable below `lg`
- sticky actions may become a full-width bottom bar on narrow screens

## Pattern 2: Focus Workbench Page

Use for:

- Project Structure
- Prompt Factory

```text
+--------------------------------------------------------------+
| Minimal shell context / tabs / workbench route context       |
+--------------------------------------------------------------+
| Optional compact page header or stage intro                  |
+--------------------------------------------------------------+
| Full-width workbench stage                                   |
| +----------------------------------------------------------+ |
| | canvas stage                                             | |
| +----------------------------------+-----------------------+ |
| | inspector                        | supporting panels     | |
| +----------------------------------+-----------------------+ |
+--------------------------------------------------------------+
```

Rules:

- no global shell right rail
- no second large route-description band
- keep maximum width generous
- do not alter inner workbench behavior in phase 1

## Pattern 3: Dashboard / Resume Surface

```text
+--------------------------------------------------------------+
| Page header: "Resume work" | New project | Open projects     |
+--------------------------------------------------------------+
| Quick actions                                                      |
| [New project] [Open recent project] [Open prompt session]          |
+--------------------------------------------------------------+
| Recent work                     | Issues / follow-up              |
| +-----------------------------+ | +-----------------------------+ |
| | recent projects             | | | failed jobs / pending QA    | |
| | recent prompt sessions      | | | provider health warnings    | |
| | recent validations          | | | recovery hints              | |
| +-----------------------------+ | +-----------------------------+ |
+--------------------------------------------------------------+
```

Rules:

- recent/resume content first
- explanatory system copy second

## Pattern 4: Form-Heavy Editor

```text
+--------------------------------------------------------------+
| detail header: item title | status | summary chips           |
+--------------------------------------------------------------+
| form section: identity                                        |
| form section: associations / context                          |
| form section: type-specific details                           |
| form section: validation / capabilities                       |
| form section: notes / history                                 |
+--------------------------------------------------------------+
| sticky footer: save | secondary actions | destructive action  |
+--------------------------------------------------------------+
```

Rules:

- avoid one uninterrupted form column
- destructive action separated visually from save/reset

## Pattern 5: Validation / Results Review

```text
+--------------------------------------------------------------+
| header: validation center | New validation                   |
+--------------------------------------------------------------+
| list/detail shell                                             |
| +----------------------+ +---------------------------------+ |
| | runs list            | | input section                    | |
| | type / decision filt | | source content / checklist       | |
| +----------------------+ +---------------------------------+ |
|                        | | result summary                    | |
|                        | | findings list                     | |
|                        | | decision controls                 | |
|                        | +---------------------------------+ |
+--------------------------------------------------------------+
```

Rules:

- result summary must appear before full findings list
- finding severity should be scannable immediately

## Pattern 6: Search / Timeline Page

```text
+--------------------------------------------------------------+
| header: activity | search                                    |
+--------------------------------------------------------------+
| search bar | filters | clear                                 |
+--------------------------------------------------------------+
| search results                | timeline                      |
| +---------------------------+ | +---------------------------+ |
| | no query / no results /   | | | grouped by date or type  | |
| | result cards              | | | open action              | |
| +---------------------------+ | +---------------------------+ |
+--------------------------------------------------------------+
```

Rules:

- distinguish "no query yet" from "no results"
- search affordance should look like a first-class tool, not a loose form row

## Pattern 7: Settings With Secondary Tabs

```text
+--------------------------------------------------------------+
| header: settings                                              |
+--------------------------------------------------------------+
| local tabs: Workspace | Secrets | Providers                  |
+--------------------------------------------------------------+
| selected tab content                                           |
| +----------------------+ +---------------------------------+ |
| | list (if applicable) | | editor / details               | |
| +----------------------+ +---------------------------------+ |
+--------------------------------------------------------------+
```

Rules:

- separate unrelated admin jobs
- keep save/clear/delete rules consistent across tabs

## Pattern 8: Calendar + Details

```text
+--------------------------------------------------------------+
| header: project calendar | legend / quick range             |
+--------------------------------------------------------------+
| calendar surface                 | selected event details    |
| +------------------------------+ | +-----------------------+ |
| | month/week view              | | | title / dates / state | |
| | event colors                 | | | linked artifact       | |
| +------------------------------+ | +-----------------------+ |
+--------------------------------------------------------------+
```

Rules:

- detail panel should include enough metadata to avoid immediate route hopping

## Pattern 9: Empty State

```text
+--------------------------------------------------------------+
| icon                                                         |
| title                                                        |
| short explanation                                            |
| primary action                                               |
| optional secondary action                                    |
+--------------------------------------------------------------+
```

Use when:

- list has no items
- filters return no results
- no project/resource/prompt is selected yet

## Pattern 10: Modal / Dialog Internal Layout

Needed later, but keep the standard ready:

```text
+-----------------------------------------------+
| dialog title              close               |
+-----------------------------------------------+
| short context / warning / instruction         |
| main body                                     |
+-----------------------------------------------+
| secondary action    primary action            |
+-----------------------------------------------+
```

Phase-1 note:

- use only if a minimal confirmation dialog becomes necessary
- do not let dialog work expand into a phase-1 UI platform rewrite
