(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};

    function clear(host) {
        while (host.firstChild) {
            host.removeChild(host.firstChild);
        }
    }

    function createButton(document, text, className, onClick) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = className;
        button.textContent = text;
        button.addEventListener("click", onClick);
        return button;
    }

    root.workbenchCanvas = {
        create(host, dotNetRef, surface, selectedNodeId) {
            host.__workbenchCanvas = { dotNetRef };
            this.update(host, surface, selectedNodeId);
        },
        update(host, surface, selectedNodeId) {
            clear(host);
            if (!surface || !surface.nodes || surface.nodes.length === 0) {
                host.textContent = "No structure nodes available.";
                return;
            }

            const document = host.ownerDocument;
            const container = document.createElement("div");
            container.style.display = "grid";
            container.style.gap = "0.75rem";
            container.style.padding = "1rem";

            for (const node of surface.nodes) {
                const card = document.createElement("button");
                card.type = "button";
                card.style.display = "block";
                card.style.width = "100%";
                card.style.textAlign = "left";
                card.style.borderRadius = "1rem";
                card.style.border = selectedNodeId === node.id ? "1px solid #0f172a" : "1px solid #cbd5e1";
                card.style.background = selectedNodeId === node.id ? "#0f172a" : "#ffffff";
                card.style.color = selectedNodeId === node.id ? "#ffffff" : "#0f172a";
                card.style.padding = "0.85rem 1rem";
                card.style.cursor = "pointer";

                const title = document.createElement("div");
                title.style.fontWeight = "600";
                title.textContent = node.title;
                card.appendChild(title);

                const subtitle = document.createElement("div");
                subtitle.style.fontSize = "0.75rem";
                subtitle.style.marginTop = "0.25rem";
                subtitle.style.opacity = "0.75";
                subtitle.textContent = `${node.kind} · ${node.status} · ${node.subtitle}`;
                card.appendChild(subtitle);

                card.addEventListener("click", () => {
                    host.__workbenchCanvas.dotNetRef.invokeMethodAsync("OnNodeSelected", node.id);
                });

                container.appendChild(card);
            }

            host.appendChild(container);
        },
        dispose(host) {
            delete host.__workbenchCanvas;
            clear(host);
        }
    };

    root.workbenchCalendar = {
        create(host, dotNetRef, surface, selectedEventId) {
            host.__workbenchCalendar = { dotNetRef, view: "list" };
            this.update(host, surface, selectedEventId);
        },
        update(host, surface, selectedEventId) {
            clear(host);
            if (!surface || !surface.events || surface.events.length === 0) {
                host.textContent = "No scheduled project events are available.";
                return;
            }

            const document = host.ownerDocument;
            const wrapper = document.createElement("div");
            wrapper.style.display = "grid";
            wrapper.style.gap = "0.75rem";
            wrapper.style.padding = "1rem";

            const toolbar = document.createElement("div");
            toolbar.style.display = "flex";
            toolbar.style.gap = "0.5rem";
            ["day", "week", "month", "year", "list"].forEach(view => {
                toolbar.appendChild(createButton(
                    document,
                    view,
                    "workbench-calendar-view",
                    () => { host.__workbenchCalendar.view = view; }
                ));
            });
            wrapper.appendChild(toolbar);

            for (const item of surface.events) {
                const card = document.createElement("button");
                card.type = "button";
                card.style.display = "block";
                card.style.width = "100%";
                card.style.textAlign = "left";
                card.style.borderRadius = "1rem";
                card.style.border = selectedEventId === item.id ? "1px solid #0f172a" : "1px solid #cbd5e1";
                card.style.background = selectedEventId === item.id ? "#0f172a" : "#ffffff";
                card.style.color = selectedEventId === item.id ? "#ffffff" : "#0f172a";
                card.style.padding = "0.85rem 1rem";
                card.style.cursor = "pointer";

                const title = document.createElement("div");
                title.style.fontWeight = "600";
                title.textContent = item.title;
                card.appendChild(title);

                const meta = document.createElement("div");
                meta.style.fontSize = "0.75rem";
                meta.style.marginTop = "0.25rem";
                meta.style.opacity = "0.75";
                meta.textContent = `${new Date(item.startUtc).toLocaleString()} → ${new Date(item.endUtc).toLocaleString()} · ${item.status}`;
                card.appendChild(meta);

                card.addEventListener("click", () => {
                    host.__workbenchCalendar.dotNetRef.invokeMethodAsync("OnEventSelected", item.id);
                });

                wrapper.appendChild(card);
            }

            host.appendChild(wrapper);
        },
        dispose(host) {
            delete host.__workbenchCalendar;
            clear(host);
        }
    };
})();
