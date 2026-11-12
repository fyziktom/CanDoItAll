export function inspect(formId) {
    const form = document.getElementById(formId);
    if (!form) throw new Error("The acquired project form is unavailable.");
    const invalid = form.querySelector(":invalid");
    const step = invalid?.closest("[data-wizard-step]")?.dataset.wizardStep;
    return { isValid: !invalid, step: step === undefined ? null : Number(step), message: invalid ? "Complete the invalid date before saving." : null };
}

export function focusInvalid(formId) {
    const invalid = document.getElementById(formId)?.querySelector(":invalid");
    invalid?.focus();
    invalid?.reportValidity();
}
