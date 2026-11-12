const observers = new WeakMap();

export function observe(strip) {
    const keydown = event => {
        const selected = event.target.closest('[role="tab"]');
        if (!selected) {
            return;
        }
        const tabs = [...strip.querySelectorAll('[role="tab"]')];
        const index = tabs.indexOf(selected);
        let next;
        switch (event.key) {
            case 'ArrowLeft':
                next = (index + tabs.length - 1) % tabs.length;
                break;
            case 'ArrowRight':
                next = (index + 1) % tabs.length;
                break;
            case 'Home':
                next = 0;
                break;
            case 'End':
                next = tabs.length - 1;
                break;
            default:
                return;
        }
        event.preventDefault();
        tabs[next].focus({ preventScroll: true });
        tabs[next].click();
    };
    strip.addEventListener('keydown', keydown);
    const resize = new ResizeObserver(() => revealActive(strip));
    const tabs = strip.querySelector('[role="tablist"]');
    if (tabs) {
        resize.observe(tabs);
    }
    observers.set(strip, { keydown, resize });
}

export function dispose(strip) {
    const observer = observers.get(strip);
    if (observer) {
        strip.removeEventListener('keydown', observer.keydown);
        observer.resize.disconnect();
        observers.delete(strip);
    }
}

export function closeMenus(strip) {
    for (const menu of strip.querySelectorAll('details[open]')) {
        menu.open = false;
    }
}

export function revealActive(strip) {
    const tabs = strip.querySelector('[role="tablist"]');
    const active = tabs?.querySelector('[aria-selected="true"]')?.closest('.app-inline-tab');
    if (!tabs || !active) {
        return;
    }
    const bounds = tabs.getBoundingClientRect();
    const target = active.getBoundingClientRect();
    if (target.left < bounds.left) {
        tabs.scrollLeft -= bounds.left - target.left;
    } else if (target.right > bounds.right) {
        tabs.scrollLeft += target.right - bounds.right;
    }
}
