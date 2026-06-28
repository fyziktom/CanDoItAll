let activeBinding = null;

export function attachCalendarDoubleClick(host, dotNetReference) {
    if (!host || !dotNetReference) {
        return;
    }

    detachCalendarDoubleClick();

    const handler = event => {
        const target = event.target instanceof Element ? event.target : null;
        const canvas = target?.closest("canvas.zy-calendar-canvas");
        if (!canvas || !canvas.closest("[data-testid='scheduler-calendar']")) {
            return;
        }

        dotNetReference
            .invokeMethodAsync("OnCalendarItemDoubleClickedAsync")
            .catch(error => console.error("Scheduler calendar double-click callback failed.", error));
    };

    document.addEventListener("dblclick", handler, true);
    activeBinding = {
        host,
        handler
    };
}

export function detachCalendarDoubleClick() {
    if (!activeBinding) {
        return;
    }

    document.removeEventListener("dblclick", activeBinding.handler, true);
    activeBinding = null;
}
