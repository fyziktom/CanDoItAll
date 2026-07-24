# Extracted Feedback

Source: `C:\repositories\CanDoItAll\codex\bundles\crm-hr-feedback10-improvement\inputs\feedback10.docx`

## Notes

- `N001` CRM:
- `N002` In directory Tags filed needs to use TagEditor. Same everywhere where tags are used.
- `N003` When I clicked to add contact, do not filled anything and then click to remove button on that line it crashed. It is usually better to solve those things as dialog, ideal as simple wizard. For example first step would contains sqared cards with centered icons and titles of options like Email, Phone, etc. and then next step input of value, tags, etc.
- `N004` We must have reusable component for list of persons, crm records, etc similar as we have in task edit dialog in project structure tasks. Right now there are mostly dropdowns, but it is not ok for more items. It must open as dialog with proper paging (crm can have hundreds of records, sometimes over thousand). And also search and tag filter. In some situations we need just companies, sometimes just people, etc. so we should have that list component flexible. Then it is necessary to use it on across the forms in crm/hr module (for example search of relations in directory item, etc).
- `N005` Same component should be used as lists for standard search trought items. More in style as we have in agent module agents tab for search trough the agents.
- `N006` Opportunity pipeline must be also as reusable component. Make search filters on one maximum two rows. Owners should be selector with our new contacts component for easier filter/search of contact (there will be at least hundreds of contacts usually).
- `N007` Opportunity creation needs some wizard for creating and then proper dialog for edit. Right now on opportunity tab there are just stacked components under each other. I think that on opportunity tab there should be just list of them and button to add new. If some of the card in list is clicked it will open dialog with detail and button for possible edit.
- `N008` Opportunity must allow selection of related project. For project selector we also need some reusable component similar as we have on project list page.
- `N009` Next to Overview tab we must have new tab called “Financials” or “Finances” and it will show snapshot over the opportunities in graphs and stats like total sold, total bought, overdue invoices (we will add invoices management later), also graphs as bar graph for months and years, and dounut/pie chart of distribution of sold/bought.
- `N010` When multuiple things in crm are browsed it opens new tabs (it is correct because they are subpages on crm), but it all title them just CRM/HR so the opened tabs looks all the same. It should have some better title of the tab

## Extracted Media

- `C:\repositories\CanDoItAll\codex\bundles\crm-hr-feedback10-improvement\inputs\feedback10-media\image1.png`
- `C:\repositories\CanDoItAll\codex\bundles\crm-hr-feedback10-improvement\inputs\feedback10-media\image2.png`
- `C:\repositories\CanDoItAll\codex\bundles\crm-hr-feedback10-improvement\inputs\feedback10-media\image3.png`
- `C:\repositories\CanDoItAll\codex\bundles\crm-hr-feedback10-improvement\inputs\feedback10-media\image4.png`
- `C:\repositories\CanDoItAll\codex\bundles\crm-hr-feedback10-improvement\inputs\feedback10-media\image5.png`

## Raw Identifier Classification

- `N001` is the section heading `CRM:` and is informational/N/A.
- `N002` through `N010` are actionable raw notes and must retain these identifiers in requirements, traceability, execution proof, and closure.
- `N003` contains two independently testable outcomes (crash prevention and wizard UX); normalized requirements split them without changing the raw ID.

## Rendered Source Pages

- `bundle://inputs/feedback10-rendered-pages/page-1.png`
- `bundle://inputs/feedback10-rendered-pages/page-2.png`
- `bundle://inputs/feedback10-rendered-pages/page-3.png`

The note text above remains verbatim. Normalized wording belongs in `requirements/01-normalized-requirements.md`.
