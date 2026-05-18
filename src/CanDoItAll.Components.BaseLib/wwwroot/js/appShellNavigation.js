window.CanDoItAll = window.CanDoItAll || {};

window.CanDoItAll.appShellNavigation = (function () {
    const registrations = new Map();

    function parsePixels(value, fallback) {
        const parsed = Number.parseFloat(value);
        return Number.isFinite(parsed) ? parsed : fallback;
    }

    function resolveRows(sidebar) {
        const navigation = sidebar.querySelector(".cda-shell-nav");
        if (!navigation) {
            return 0;
        }

        const button = navigation.querySelector(".cda-shell-nav-button");
        const navigationStyle = window.getComputedStyle(navigation);
        const buttonHeight = button?.getBoundingClientRect().height || 44;
        const gap = parsePixels(navigationStyle.rowGap, 6);
        const navigationHeight = navigation.getBoundingClientRect().height;
        const reservedOpticalGap = buttonHeight + gap;
        const availableHeight = Math.max(0, navigationHeight - reservedOpticalGap);
        const rowPitch = buttonHeight + gap;

        return Math.max(1, Math.floor((availableHeight + gap) / rowPitch));
    }

    function notify(sidebar, dotNetRef) {
        const rows = resolveRows(sidebar);
        if (rows <= 0) {
            return;
        }

        dotNetRef.invokeMethodAsync("UpdateNavigationRowCapacity", rows)
            .catch(function () {
            });
    }

    function observe(id, sidebar, dotNetRef) {
        if (!sidebar || !dotNetRef) {
            return;
        }

        unobserve(id);

        let animationFrame = 0;
        const schedule = function () {
            window.cancelAnimationFrame(animationFrame);
            animationFrame = window.requestAnimationFrame(function () {
                notify(sidebar, dotNetRef);
            });
        };

        const resizeObserver = new window.ResizeObserver(schedule);
        resizeObserver.observe(sidebar);

        const navigation = sidebar.querySelector(".cda-shell-nav");
        if (navigation) {
            resizeObserver.observe(navigation);
        }

        const mutationObserver = new window.MutationObserver(schedule);
        mutationObserver.observe(sidebar, {
            attributes: true,
            attributeFilter: ["data-navigation-mode", "class"]
        });

        window.addEventListener("resize", schedule);
        registrations.set(id, {
            resizeObserver,
            mutationObserver,
            schedule,
            dispose: function () {
                window.cancelAnimationFrame(animationFrame);
                resizeObserver.disconnect();
                mutationObserver.disconnect();
                window.removeEventListener("resize", schedule);
            }
        });

        schedule();
    }

    function unobserve(id) {
        const registration = registrations.get(id);
        if (!registration) {
            return;
        }

        registration.dispose();
        registrations.delete(id);
    }

    return {
        observe,
        unobserve
    };
})();
