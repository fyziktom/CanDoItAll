const registrations = new WeakMap();

export function attach(host, key, reference) {
    if (!host?.isConnected) {
        return;
    }
    const existing = registrations.get(host);
    if (existing?.key === key) {
        return;
    }
    if (existing) {
        detach(host, existing.key);
    }
    let sequence = 0;
    const invoke = (method, value) => reference.invokeMethodAsync(method, value).catch(error => console.debug("Scheduler calendar callback retired", error));
    const pointer = event => {
        if (event.target instanceof Element && event.target.closest("canvas.zy-calendar-canvas") && host.contains(event.target)) {
            event.target.closest("canvas.zy-calendar-canvas").focus({ preventScroll: true });
            event.preventDefault();
            sequence += 1;
            invoke("BeginCalendarPointer", sequence);
        }
    };
    const doubleClick = event => {
        if (event.target instanceof Element && event.target.closest("canvas.zy-calendar-canvas") && host.contains(event.target)) {
            invoke("OnCalendarItemDoubleClickedAsync", sequence);
        }
    };
    host.addEventListener("pointerdown", pointer, true);
    host.addEventListener("dblclick", doubleClick);
    registrations.set(host, { key, pointer, doubleClick });
    host.dataset.schedulerRegistration = key;
}

export function detach(host, key) {
    const entry = registrations.get(host);
    if (!entry || entry.key !== key) {
        return;
    }
    host.removeEventListener("pointerdown", entry.pointer, true);
    host.removeEventListener("dblclick", entry.doubleClick);
    registrations.delete(host);
    delete host.dataset.schedulerRegistration;
}
