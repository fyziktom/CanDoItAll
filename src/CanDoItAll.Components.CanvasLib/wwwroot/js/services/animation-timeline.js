(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const previewControllers = new WeakMap();
    const reduceMotionQuery = "(prefers-reduced-motion: reduce)";

    function prefersReducedMotion() {
        return !!window.matchMedia && window.matchMedia(reduceMotionQuery).matches;
    }

    function clamp01(value) {
        return Math.max(0, Math.min(1, value));
    }

    function lerp(from, to, progress) {
        return from + ((to - from) * progress);
    }

    function resolveEasing(name) {
        switch ((name || "").toLowerCase()) {
            case "cubicout":
                return value => 1 - Math.pow(1 - value, 3);
            case "softinout":
            default:
                return value => value < 0.5
                    ? 4 * value * value * value
                    : 1 - Math.pow(-2 * value + 2, 3) / 2;
        }
    }

    function createController(options) {
        const activeAnimations = new Map();
        const pendingTimers = new Set();
        const reducedMotion = options?.reducedMotion === true || prefersReducedMotion();

        function cancel(key) {
            const record = activeAnimations.get(key);
            if (!record) {
                return;
            }

            record.cancelled = true;
            if (record.frame) {
                window.cancelAnimationFrame(record.frame);
            }

            activeAnimations.delete(key);
        }

        function animate(key, config) {
            cancel(key);

            const durationMs = Math.max(0, Number(config?.durationMs) || 0);
            const delayMs = Math.max(0, Number(config?.delayMs) || 0);
            const from = Number(config?.from) || 0;
            const to = Number(config?.to) || 0;
            const easing = resolveEasing(config?.easing);
            const apply = typeof config?.apply === "function" ? config.apply : () => { };
            const complete = typeof config?.complete === "function" ? config.complete : null;

            if (reducedMotion || durationMs === 0) {
                apply(to, 1);
                if (complete) {
                    complete(true);
                }

                return;
            }

            const record = {
                cancelled: false,
                frame: 0
            };

            activeAnimations.set(key, record);

            const startAt = performance.now() + delayMs;
            function step(now) {
                if (record.cancelled) {
                    return;
                }

                if (now < startAt) {
                    record.frame = window.requestAnimationFrame(step);
                    return;
                }

                const progress = clamp01((now - startAt) / durationMs);
                const value = lerp(from, to, easing(progress));
                apply(value, progress);

                if (progress >= 1) {
                    activeAnimations.delete(key);
                    if (complete) {
                        complete(false);
                    }

                    return;
                }

                record.frame = window.requestAnimationFrame(step);
            }

            record.frame = window.requestAnimationFrame(step);
        }

        function animateViewport(config) {
            const key = config?.key || "viewport";
            const from = config?.from || {};
            const to = config?.to || {};
            const apply = typeof config?.apply === "function" ? config.apply : () => { };
            const complete = typeof config?.complete === "function" ? config.complete : null;

            if (reducedMotion || !config) {
                apply({
                    panX: Number(to.panX) || 0,
                    panY: Number(to.panY) || 0,
                    zoom: Number(to.zoom) || 1
                }, 1);

                if (complete) {
                    complete(true);
                }

                return;
            }

            cancel(key);

            const durationMs = Math.max(0, Number(config.durationMs) || 0);
            const delayMs = Math.max(0, Number(config.delayMs) || 0);
            const easing = resolveEasing(config.easing);
            const record = {
                cancelled: false,
                frame: 0
            };

            activeAnimations.set(key, record);

            const startAt = performance.now() + delayMs;
            function step(now) {
                if (record.cancelled) {
                    return;
                }

                if (now < startAt) {
                    record.frame = window.requestAnimationFrame(step);
                    return;
                }

                const progress = durationMs === 0
                    ? 1
                    : clamp01((now - startAt) / durationMs);
                const eased = easing(progress);
                apply({
                    panX: lerp(Number(from.panX) || 0, Number(to.panX) || 0, eased),
                    panY: lerp(Number(from.panY) || 0, Number(to.panY) || 0, eased),
                    zoom: lerp(Number(from.zoom) || 1, Number(to.zoom) || 1, eased)
                }, progress);

                if (progress >= 1) {
                    activeAnimations.delete(key);
                    if (complete) {
                        complete(false);
                    }

                    return;
                }

                record.frame = window.requestAnimationFrame(step);
            }

            record.frame = window.requestAnimationFrame(step);
        }

        function fadeElement(key, element, config) {
            if (!element) {
                return;
            }

            const currentOpacity = Number.parseFloat(element.style.opacity || "1");
            const from = Number.isFinite(config?.from) ? Number(config.from) : (Number.isFinite(currentOpacity) ? currentOpacity : 1);
            const to = Number.isFinite(config?.to) ? Number(config.to) : 1;
            element.style.opacity = `${from}`;
            animate(key, {
                from,
                to,
                durationMs: config?.durationMs ?? 180,
                easing: config?.easing ?? "cubicOut",
                apply(value) {
                    element.style.opacity = `${Math.round(value * 1000) / 1000}`;
                }
            });
        }

        function schedule(callback, delayMs) {
            const handle = window.setTimeout(() => {
                pendingTimers.delete(handle);
                callback();
            }, Math.max(0, Number(delayMs) || 0));

            pendingTimers.add(handle);
            return handle;
        }

        function setReducedMotionAttribute(element) {
            if (element) {
                element.dataset.reducedMotion = reducedMotion ? "true" : "false";
            }
        }

        function dispose() {
            for (const key of activeAnimations.keys()) {
                cancel(key);
            }

            for (const handle of pendingTimers) {
                window.clearTimeout(handle);
            }

            pendingTimers.clear();
        }

        return {
            reducedMotion,
            animate,
            animateViewport,
            fadeElement,
            schedule,
            setReducedMotionAttribute,
            cancel,
            dispose
        };
    }

    function setPreviewPhase(host, phaseKey) {
        host.dataset.activePhase = phaseKey;
        for (const track of host.querySelectorAll("[data-phase-key]")) {
            const active = track.getAttribute("data-phase-key") === phaseKey;
            track.dataset.active = active ? "true" : "false";
        }
    }

    function animatePreviewTrack(controller, track, phaseKey, sequence) {
        const fill = track.querySelector(".cw-animation-track__fill");
        if (!fill) {
            return;
        }

        fill.style.transform = "scaleX(0.001)";
        fill.style.opacity = "0.5";
        controller.animate(`preview-${phaseKey}-${sequence}`, {
            from: 0.001,
            to: 1,
            durationMs: controller.reducedMotion ? 0 : 620,
            easing: "softInOut",
            apply(value) {
                fill.style.transform = `scaleX(${Math.max(0.001, value)})`;
                fill.style.opacity = `${Math.min(1, 0.42 + (value * 0.58))}`;
            }
        });
    }

    function mountPreview(host) {
        if (!host) {
            return;
        }

        disposePreview(host);

        const controller = createController();
        controller.setReducedMotionAttribute(host);
        previewControllers.set(host, controller);

        const tracks = Array.from(host.querySelectorAll("[data-phase-key]"));
        if (!tracks.length) {
            return;
        }

        let sequence = 0;
        function advance() {
            if (!host.isConnected) {
                return;
            }

            const track = tracks[sequence % tracks.length];
            const phaseKey = track.getAttribute("data-phase-key") || "viewport";
            setPreviewPhase(host, phaseKey);
            animatePreviewTrack(controller, track, phaseKey, sequence);
            sequence += 1;
            controller.schedule(advance, controller.reducedMotion ? 1800 : 960);
        }

        advance();
    }

    function disposePreview(host) {
        const controller = previewControllers.get(host);
        if (!controller) {
            return;
        }

        controller.dispose();
        previewControllers.delete(host);
    }

    root.animationTimeline = {
        createController,
        mountPreview,
        disposePreview,
        prefersReducedMotion
    };
})();
