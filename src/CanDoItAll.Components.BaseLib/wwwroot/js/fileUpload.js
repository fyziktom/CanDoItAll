const bindings = new WeakMap();

export function initializeFileUpload(root, inputId) {
    if (!root) {
        return;
    }

    disposeFileUpload(root);

    const zone = root.querySelector("[data-cda-file-upload-zone]");
    const input = document.getElementById(inputId);
    if (!zone || !input) {
        return;
    }

    const setDragActive = (active) => {
        zone.dataset.dragActive = active ? "true" : "false";
    };

    const isDisabled = () => input.disabled;

    const prevent = (event) => {
        event.preventDefault();
        event.stopPropagation();
    };

    const handleDragEnter = (event) => {
        if (isDisabled()) {
            return;
        }

        prevent(event);
        setDragActive(true);
    };

    const handleDragOver = (event) => {
        if (isDisabled()) {
            return;
        }

        prevent(event);
        if (event.dataTransfer) {
            event.dataTransfer.dropEffect = "copy";
        }

        setDragActive(true);
    };

    const handleDragLeave = (event) => {
        if (event.currentTarget !== event.target) {
            return;
        }

        prevent(event);
        setDragActive(false);
    };

    const handleDrop = (event) => {
        if (isDisabled()) {
            return;
        }

        prevent(event);
        setDragActive(false);

        const files = event.dataTransfer?.files;
        if (!files || files.length === 0) {
            return;
        }

        const transfer = new DataTransfer();
        for (const file of files) {
            transfer.items.add(file);
        }

        input.files = transfer.files;
        input.dispatchEvent(new Event("change", { bubbles: true }));
    };

    zone.addEventListener("dragenter", handleDragEnter);
    zone.addEventListener("dragover", handleDragOver);
    zone.addEventListener("dragleave", handleDragLeave);
    zone.addEventListener("drop", handleDrop);

    bindings.set(root, () => {
        setDragActive(false);
        zone.removeEventListener("dragenter", handleDragEnter);
        zone.removeEventListener("dragover", handleDragOver);
        zone.removeEventListener("dragleave", handleDragLeave);
        zone.removeEventListener("drop", handleDrop);
    });
}

export function disposeFileUpload(root) {
    const dispose = root ? bindings.get(root) : null;
    if (!dispose) {
        return;
    }

    bindings.delete(root);
    dispose();
}
