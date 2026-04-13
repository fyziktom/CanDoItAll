async function writeClipboard(text) {
    const normalized = text ?? "";
    if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(normalized);
        return normalized;
    }

    const fallback = document.createElement("textarea");
    fallback.value = normalized;
    fallback.setAttribute("readonly", "readonly");
    fallback.style.position = "fixed";
    fallback.style.left = "-9999px";
    document.body.appendChild(fallback);
    fallback.select();

    try {
        document.execCommand("copy");
    } finally {
        document.body.removeChild(fallback);
    }

    return normalized;
}

function resolveTarget(targetId, targetSelector) {
    if (targetSelector) {
        return document.querySelector(targetSelector);
    }

    if (targetId) {
        return document.getElementById(targetId);
    }

    throw new Error("CopyButton requires either a target element id, selector, or explicit value.");
}

function readTargetText(target) {
    if (target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement) {
        const value = target.value ?? "";
        if (typeof target.selectionStart === "number"
            && typeof target.selectionEnd === "number"
            && target.selectionEnd > target.selectionStart) {
            return value.slice(target.selectionStart, target.selectionEnd);
        }

        return value;
    }

    return target.textContent?.trim() ?? "";
}

export async function copyText(text) {
    return await writeClipboard(text);
}

export async function copyFromTarget(targetId, targetSelector) {
    const target = resolveTarget(targetId, targetSelector);
    if (!target) {
        throw new Error("CopyButton could not resolve the target element.");
    }

    return await writeClipboard(readTargetText(target));
}
