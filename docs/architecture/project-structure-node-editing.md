# Project structure node editing

Project Structure organizes general project work. Choosing a node should describe
the user's intent, not force a software-development workflow onto it.

## Capture first, configure when needed

| User situation | Suitable starting point | Details that can wait |
|---|---|---|
| Taking notes during a call | A **Group** named Meetings, with inline Notes beneath it | Meeting schedule, participants, recording links |
| Planning an event | Groups for Venue, Suppliers, and Guests; Tasks for commitments | Estimates, assignments, supporting files |
| Renovating a room | Groups for rooms or trades; a Delivery block for a handover | File destinations and repository links |
| Researching a purchase | A Research block, Notes, Links, and Decisions | Additional evidence and participant details |
| Handing over client work | A Delivery block with its expected outcome | Output and delivery folders when files are involved |
| Building software | Specialized repositories, scripts, environments, and deployment nodes | Optional runtime and provider settings; actual required execution inputs remain required |

**Group** exposes the existing `ProjectBlock` with an empty subtype. It does not
introduce a second container model or a database migration. It is available beside
Note in the create menu, in Capture and Blocks, and through existing type conversion.
Specialized block types remain available when their meaning is useful.

## Creation and editing

New nodes start at 0% complete. During creation, the optional Priority, Progress and
Markers steps use the same choices as the canvas toolbar. A planner can record an
urgent, half-finished task at creation or skip the steps and set its signals later.
For tasks, the progress ring is the same manual signal as the toolbar. Gantt uses
the task's execution state and actual dates to report completion.

### Connecting nodes

With **Select** active, drag a round handle on a node to another node to create a
dashed violet **visual link**. For example, link a supplier's quote to the task
that mentions it. The association works in either direction and leaves the
schedule and readiness unchanged.

Use **Dependency** to select a task and then its prerequisite. A solid blue arrow
records that the first task depends on the second. Gantt schedules the prerequisite
first. Both kinds of link can coexist, and the Delete tool can remove either one.

Creation presents short sections as steps. Start with the item's name and context,
then fill the relevant settings. Optional steps can be skipped. The create action
is available as soon as the node's actual requirements are satisfied; users do not
have to visit every optional section. A required file or execution setting cannot
be bypassed by skipping a step. Invalid fields reveal their owning section.

Editing an existing node opens a conventional form at its name. The description is
near the identity fields. Optional settings use disclosure so that a rename does
not begin at an output path or a technical identifier. Existing values remain in
the draft when sections are hidden. Only the body scrolls; navigation and submission
actions remain available.

Delivery describes an outcome or handover. Its file locations are optional and
grouped separately. The UI no longer assumes that every delivery is a software
release. Transcripts have one editable body rather than two competing text areas.

Simple Notes keep their direct double-click editor and Enter / Shift+Enter / Escape
behavior. The editing surface uses the active theme's foreground, background, hint,
and border tokens, rather than the note card's fixed pigment colors.

The specialized task renderer follows the same distinction: short creation steps
for basics, planning, and assignment; a regular edit form with optional details.
Repository and execution settings do not displace the task's name and notes.
The repository link is selected during task creation. Editing shows its saved
label without offering a change that the task's existing save boundary cannot
persist; an unavailable linked repository is reported explicitly.
Payment creation presents amount and currency before optional scheduling and
assignment details.
Text-file creation offers Details, Content (write or upload), Notes and an optional
Planning step with priority, progress and markers. Moving between steps keeps the
upload and unfinished text. Generated image and secret reference creation offer the
same three choices in expandable sections of their own dialogs.

## Ownership and draft safety

CanvasLib owns the generic composer presentation and focus behavior. The Workbench
catalog owns field labels, semantic sections, defaults, and required settings.
Planning UI owns the specialized task form. No additional global keyboard handler
or alternative mutation owner is introduced.

Changing a step or opening a disclosure does not recreate the draft or its child
editors. Existing opening/revision checks, comparison, close guards, write admission,
submission outcomes, and reconciliation continue to belong to their current owners.
The wizard is presentation, not a separate persistence lifecycle.

Task creation links its selected repository through the existing canonical task
relationship owner, using the committed typed repository reference. Other pending
links retain the generic policy. Project admission and commit authorization remain
unchanged; choosing a repository must not produce a committed task followed by a
rejected generic resource link.

## Previewing Excel files

Select an XLSX file or revision and choose **Expand preview**. The worksheet tabs
show formatted cell values. Common formulas can use cells on other sheets or outside
the visible grid. FileTools uses saved Excel results for formulas outside its
calculation support. A cell displays **Calculation unavailable** when it has neither
a usable saved result nor a supported calculation. The preview is read-only.

## Manual review

- In dark mode, double-click a Note and verify text, caret, selection, hint, and
  comparison controls. Save, add a line, and cancel using the existing shortcuts.
- Create a Group named Meetings using only its name. Add a Note beneath it, rename
  the Group, and return to the canvas.
- Rename an existing Delivery block without entering a path. Open File locations
  and verify existing values remain intact.
- Create and edit a scheduled meeting, an attachment, a participant, a payment,
  and a runnable item. Verify required inputs and optional settings are distinct.
- Edit Transcript text once, save, and reopen it. Verify the same text is retained.
- Create a Task from its basics, then reopen it to add planning or assignment
  details. Navigate between steps with unfinished input and verify it survives.
- Repeat representative editors in light mode, at low and high canvas zoom, and
  with the canvas maximized. Check focus, footer visibility, and dialog scrolling.
