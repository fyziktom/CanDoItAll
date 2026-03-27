# Sub-Bundle 1: Canvas Shell And Toolbar Checklist

- [ ] remove the `Inspector` slot usage from `ProjectStructurePage`
- [ ] keep the stage in canvas-only mode so the structure canvas uses the recovered width
- [ ] preserve existing stage stats and page header behavior
- [ ] make the toolbar the true top frame of the canvas
- [ ] add a toolbar safe-top contract for floating windows
- [ ] compact the toolbar before allowing horizontal overflow
- [ ] verify the toolbar stays reachable in normal and maximized modes
- [ ] capture before and after screenshots at common desktop widths

Acceptance result:

- full-width structure canvas with an always-reachable toolbar
