(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const calendars = new WeakMap();

    function asText(value) {
        return String(value ?? "").trim();
    }

    function asArray(value) {
        return Array.isArray(value) ? value : [];
    }

    function safeParse(json, fallback) {
        if (!json) {
            return fallback;
        }

        try {
            return Object.assign({}, fallback, JSON.parse(json));
        } catch {
            return fallback;
        }
    }

    function browserTimeZone(fallback) {
        try {
            const value = Intl.DateTimeFormat().resolvedOptions().timeZone;
            return asText(value) || fallback;
        } catch {
            return fallback;
        }
    }

    function browserLocale(fallback) {
        return asText(navigator.language) || fallback;
    }

    function uniqueStrings(values) {
        const seen = new Set();
        const result = [];

        for (const value of asArray(values)) {
            const normalized = asText(value);
            if (normalized === "" || seen.has(normalized)) {
                continue;
            }

            seen.add(normalized);
            result.push(normalized);
        }

        return result;
    }

    function buildContext(controller) {
        const state = controller?.state || {};
        return {
            view: asText(state.view) || "week",
            scope: asText(state.listScope) || "week",
            selectedDate: asText(state.selectedDateKey || state.anchorDateKey),
            timezone: asText(state.timezone) || "UTC"
        };
    }

    function buildStateJson(controller) {
        const context = buildContext(controller);
        return JSON.stringify({
            preferredView: context.view,
            selectedEventId: asText(controller?.state?.selectedEventId),
            selectedDate: context.selectedDate,
            anchorDateKey: asText(controller?.state?.anchorDateKey) || context.selectedDate,
            timezone: context.timezone,
            scope: context.scope
        });
    }

    function parseViewState(surface) {
        const parsed = safeParse(surface?.viewStateJson, {});
        return {
            initialView: asText(parsed.preferredView) || asText(surface?.initialView) || "week",
            selectedDate: asText(parsed.selectedDate || parsed.selectedDateKey || parsed.anchorDateKey) || asText(surface?.selectedDate),
            selectedEventId: asText(parsed.selectedEventId) || asText(surface?.selectedEventId),
            timezone: asText(parsed.timezone) || asText(surface?.timezone)
        };
    }

    function parseJson(json, fallback) {
        if (!json) {
            return fallback;
        }

        try {
            return JSON.parse(json);
        } catch {
            return fallback;
        }
    }

    function asNumberOrNull(value) {
        if (value === null || value === undefined || value === "") {
            return null;
        }

        const parsed = Number(value);
        return Number.isFinite(parsed) ? parsed : null;
    }

    function normalizeCalendarEventForDotNet(event) {
        const source = event || {};
        return {
            ...source,
            id: asText(source.id || source.eventId),
            eventId: asText(source.eventId || source.id),
            title: asText(source.title),
            description: asText(source.description),
            startUtc: asText(source.startUtc) || null,
            endUtc: asText(source.endUtc) || null,
            timezone: asText(source.timezone || source.timezoneName) || "UTC",
            timezoneName: asText(source.timezoneName || source.timezone) || "UTC",
            location: asText(source.location),
            locationLabel: asText(source.locationLabel || source.location),
            locationAddress: asText(source.locationAddress),
            locationLat: asNumberOrNull(source.locationLat),
            locationLng: asNumberOrNull(source.locationLng),
            customerName: asText(source.customerName),
            customerEmail: asText(source.customerEmail),
            customerPhone: asText(source.customerPhone),
            priceAmount: asNumberOrNull(source.priceAmount),
            currency: asText(source.currency) || "USD",
            category: asText(source.category),
            color: asText(source.color) || "#4f46e5",
            allDay: !!source.allDay,
            readOnly: !!source.readOnly,
            eventType: asText(source.eventType),
            status: asText(source.status),
            notes: asText(source.notes),
            logisticsNote: asText(source.logisticsNote),
            linkedPlaylistCount: asNumberOrNull(source.linkedPlaylistCount) ?? 0,
            linkedPlaylists: asArray(source.linkedPlaylists),
            checklistItemCount: asNumberOrNull(source.checklistItemCount) ?? 0,
            checklistRows: asArray(source.checklistRows),
            repositoryId: asText(source.repositoryId),
            currentCommitSha256: asText(source.currentCommitSha256),
            playlistsBuilderUrl: asText(source.playlistsBuilderUrl),
            createdUtc: asText(source.createdUtc) || null,
            updatedUtc: asText(source.updatedUtc) || null
        };
    }

    async function emitState(entry) {
        if (!entry?.dotNetRef) {
            return;
        }

        const context = buildContext(entry.controller);
        await entry.dotNetRef.invokeMethodAsync(
            "OnStateChanged",
            buildStateJson(entry.controller),
            asText(entry.controller?.state?.selectedEventId) || null,
            context.selectedDate,
            context.view,
            context.scope,
            context.timezone);
    }

    async function emitSelection(entry, selectedEvent) {
        if (!entry?.dotNetRef) {
            return;
        }

        await entry.dotNetRef.invokeMethodAsync(
            "OnSelectionChanged",
            JSON.stringify(selectedEvent || null),
            JSON.stringify(buildContext(entry.controller)));

        await emitState(entry);
    }

    async function invokeSave(dotNetRef, event, context, mode) {
        const normalizedEvent = normalizeCalendarEventForDotNet(event);
        const result = await dotNetRef.invokeMethodAsync(
            "OnEventSave",
            JSON.stringify(normalizedEvent),
            JSON.stringify(context || {}),
            mode);

        return parseJson(result, normalizedEvent);
    }

    async function invokeDelete(dotNetRef, event, context) {
        await dotNetRef.invokeMethodAsync(
            "OnEventDelete",
            JSON.stringify(normalizeCalendarEventForDotNet(event)),
            JSON.stringify(context || {}));
    }

    async function invokePlaylistSearch(dotNetRef, query, event, context) {
        const result = await dotNetRef.invokeMethodAsync(
            "OnPlaylistSearch",
            asText(query),
            JSON.stringify(event || null),
            JSON.stringify(context || {}));

        return parseJson(result, []);
    }

    async function invokePlaylistMutation(methodName, dotNetRef, event, playlist, context) {
        const result = await dotNetRef.invokeMethodAsync(
            methodName,
            JSON.stringify(event || {}),
            JSON.stringify(playlist || {}),
            JSON.stringify(context || {}));

        return parseJson(result, event || {});
    }

    async function invokeExport(dotNetRef, format, visibleEvents, context) {
        await dotNetRef.invokeMethodAsync(
            "OnExportRequested",
            asText(format),
            JSON.stringify(visibleEvents || []),
            JSON.stringify(context || {}));
    }

    function buildOptions(host, dotNetRef, surface) {
        const persistedState = parseViewState(surface);
        const events = asArray(surface?.events);
        const timeZoneOptions = uniqueStrings([
            ...asArray(surface?.timeZoneOptions),
            ...events.map(event => asText(event?.timezone || event?.timezoneName)),
            browserTimeZone(asText(surface?.timezone) || "UTC")
        ]);

        return {
            host,
            events,
            initialView: persistedState.initialView,
            selectedDate: persistedState.selectedDate,
            selectedEventId: persistedState.selectedEventId,
            timezone: persistedState.timezone || browserTimeZone(asText(surface?.timezone) || "UTC"),
            locale: asText(surface?.locale) || browserLocale("en-US"),
            weekStartsOn: Number.isFinite(surface?.weekStartsOn) ? surface.weekStartsOn : 1,
            slotMinutes: Number.isFinite(surface?.slotMinutes) ? surface.slotMinutes : 30,
            businessHoursStart: Number.isFinite(surface?.businessHoursStart) ? surface.businessHoursStart : 7,
            businessHoursEnd: Number.isFinite(surface?.businessHoursEnd) ? surface.businessHoursEnd : 22,
            miniMonthCount: Number.isFinite(surface?.miniMonthCount) ? surface.miniMonthCount : 2,
            allowCreate: surface?.allowCreate !== false,
            allowEdit: surface?.allowEdit !== false,
            allowDelete: surface?.allowDelete !== false,
            allowDragDrop: surface?.allowDragDrop !== false,
            allowResize: surface?.allowResize !== false,
            enableListExport: surface?.enableListExport !== false,
            workspaceModal: surface?.workspaceModal !== false,
            eventTypes: asArray(surface?.eventTypes),
            eventStatuses: asArray(surface?.eventStatuses),
            timeZoneOptions,
            onEventCreate: function (event, context) {
                return invokeSave(dotNetRef, event, context, "create");
            },
            onEventUpdate: function (event, context) {
                return invokeSave(dotNetRef, event, context, "update");
            },
            onEventDelete: function (event, context) {
                return invokeDelete(dotNetRef, event, context);
            },
            onPlaylistSearch: function (query, context) {
                return invokePlaylistSearch(dotNetRef, query, context?.event || null, context || {});
            },
            onPlaylistLink: function (event, playlist, context) {
                return invokePlaylistMutation("OnPlaylistLink", dotNetRef, event, playlist, context);
            },
            onPlaylistClone: function (event, playlist, context) {
                return invokePlaylistMutation("OnPlaylistClone", dotNetRef, event, playlist, context);
            },
            onPlaylistUnlink: function (event, playlist, context) {
                return invokePlaylistMutation("OnPlaylistUnlink", dotNetRef, event, playlist, context);
            },
            onSelectionChange: function (selectedEvent) {
                return emitSelection(calendars.get(host), selectedEvent);
            },
            onDateChange: function () {
                return emitState(calendars.get(host));
            },
            onViewChange: function () {
                return emitState(calendars.get(host));
            },
            onTimezoneChange: function () {
                return emitState(calendars.get(host));
            },
            onExportRequest: function (format, visibleEvents, context) {
                return invokeExport(dotNetRef, format, visibleEvents, context);
            }
        };
    }

    function ensureRuntime() {
        if (!window.ZyCanvasPrimitives || !window.ZyCanvasCalendar) {
            throw new Error("The PHP canvas calendar runtime is not loaded.");
        }
    }

    root.canvasCalendar = {
        create: function (host, dotNetRef, surface) {
            ensureRuntime();

            const controller = window.ZyCanvasCalendar.create(buildOptions(host, dotNetRef, surface));
            const entry = {
                controller,
                dotNetRef
            };

            calendars.set(host, entry);
            return emitState(entry);
        },

        update: function (host, surface) {
            const entry = calendars.get(host);
            if (!entry) {
                return;
            }

            entry.controller.updateOptions(buildOptions(host, entry.dotNetRef, surface));
        },

        dispose: function (host) {
            const entry = calendars.get(host);
            if (!entry) {
                return;
            }

            entry.controller.destroy();
            calendars.delete(host);
        }
    };
})();
