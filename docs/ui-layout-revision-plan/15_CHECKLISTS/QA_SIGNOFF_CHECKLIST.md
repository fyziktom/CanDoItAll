# QA Signoff Checklist

- Tailwind output was rebuilt before screenshots were accepted
- Live browser review was done against the running application
- Layout hierarchy is improved on standard pages
- Similar pages now feel structurally consistent
- Primary navigation still works on smaller screens
- Protected workbench routes retained behavior
- Docked protected workbench routes still show the desktop main menu
- Maximized protected workbench routes anchor to the viewport correctly
- Radial-menu spacing and the numeric priority submenu were reviewed from screenshots, not assumed from code
- Picker-based image upload was reviewed from a live browser flow and only accepted after the node was visibly media-backed on the canvas and re-opened in the inspector
- The shared workbench zoom floor was validated on a live large-map view
- Migrated filters/actions were checked on resources, test lab, prompt gallery, and settings
- Required component and Playwright tests passed
- No major route lost deep-link or selected-state behavior
- No major page still relies on obvious ad hoc layout hacks
