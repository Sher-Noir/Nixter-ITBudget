/* Budget wizard and Actuals dialog: external script for LedgerForge's self-only CSP. */
(() => {
    "use strict";

    const wizard = document.getElementById("new-budget-item");
    const form = document.getElementById("lf-budget-create-form");
    if (wizard && form) {
        const steps = [...form.querySelectorAll("[data-budget-step]")];
        const next = form.querySelector("[data-budget-next]");
        const back = form.querySelector("[data-budget-back]");
        const save = form.querySelector("[data-budget-save]");
        const progress = document.getElementById("lf-budget-wizard-progress");
        let step = 0;

        const showStep = () => {
            steps.forEach((panel, index) => { panel.hidden = index !== step; });
            back.hidden = step === 0;
            next.hidden = step === steps.length - 1;
            save.hidden = step !== steps.length - 1;
            if (progress) progress.textContent = `Step ${step + 1} of ${steps.length}`;
        };
        const firstInvalid = panel => [...panel.querySelectorAll("input,select,textarea")]
            .find(control => !control.checkValidity());
        const openWizard = () => {
            wizard.hidden = false;
            step = 0;
            showStep();
            wizard.scrollIntoView({behavior: "smooth", block: "start"});
            wizard.querySelector('input:not([type="hidden"])')?.focus();
        };
        document.querySelectorAll("[data-open-budget-wizard]")
            .forEach(button => button.addEventListener("click", event => {
                event.preventDefault();
                openWizard();
            }));
        wizard.querySelector("[data-close-budget-wizard]")?.addEventListener("click", () => {
            wizard.hidden = true;
        });
        next?.addEventListener("click", () => {
            const invalid = firstInvalid(steps[step]);
            if (invalid) { invalid.reportValidity(); return; }
            step = Math.min(step + 1, steps.length - 1);
            showStep();
        });
        back?.addEventListener("click", () => {
            step = Math.max(step - 1, 0);
            showStep();
        });
        const quantity = document.getElementById("create-quantity");
        const cost = document.getElementById("create-unitCost");
        const total = document.getElementById("create-total");
        const updateTotal = () => {
            const amount = Number(quantity?.value || 0) * Number(cost?.value || 0);
            if (total) total.textContent = Number.isFinite(amount)
                ? amount.toLocaleString(undefined, {style: "currency", currency: "USD"})
                : "Invalid amount";
        };
        quantity?.addEventListener("input", updateTotal);
        cost?.addEventListener("input", updateTotal);
        form.addEventListener("submit", event => {
            for (let i = 0; i < steps.length; i++) {
                const invalid = firstInvalid(steps[i]);
                if (invalid) {
                    event.preventDefault();
                    step = i;
                    showStep();
                    invalid.reportValidity();
                    return;
                }
            }
        });
        showStep();
        updateTotal();
        if (wizard.dataset.openOnError === "true") openWizard();
    }

    const editQuantity = document.getElementById("quantity");
    const editCost = document.getElementById("unitCost");
    const editTotal = document.getElementById("edit-total");
    if (editQuantity && editCost && editTotal) {
        const updateEditTotal = () => {
            const amount = Number(editQuantity.value || 0) * Number(editCost.value || 0);
            editTotal.textContent = Number.isFinite(amount)
                ? amount.toLocaleString(undefined, {style: "currency", currency: "USD"})
                : "Invalid amount";
        };
        editQuantity.addEventListener("input", updateEditTotal);
        editCost.addEventListener("input", updateEditTotal);
        updateEditTotal();
    }

    const actualEntry = document.getElementById("lf-actual-entry");
    if (actualEntry) {
        document.querySelectorAll("[data-open-actual-entry]").forEach(link =>
            link.addEventListener("click", event => {
                event.preventDefault();
                actualEntry.open = true;
                actualEntry.scrollIntoView({behavior: "smooth", block: "center"});
                actualEntry.querySelector("#actual-amount")?.focus();
            })
        );
    }

    const dialog = document.getElementById("lf-actuals-dialog");
    if (dialog) {
        const content = dialog.querySelector("[data-actuals-content]");
        const openActuals = async () => {
            if (!dialog.open) dialog.showModal();
            if (!content) return;
            content.textContent = "Loading Actuals…";
            try {
                const response = await fetch(dialog.dataset.actualsUrl, {
                    credentials: "same-origin",
                    headers: {"Accept": "text/html"},
                    cache: "no-store"
                });
                if (!response.ok || response.redirected)
                    throw new Error("Actuals could not be loaded. Check your sign-in and permissions.");
                content.innerHTML = await response.text();
            } catch (error) {
                content.textContent = error instanceof Error ? error.message : "Unable to load Actuals.";
            }
        };
        document.querySelectorAll("[data-open-actuals-modal]")
            .forEach(button => button.addEventListener("click", () => { void openActuals(); }));
        dialog.querySelector("[data-close-actuals-modal]")?.addEventListener("click", () => dialog.close());
        dialog.addEventListener("click", event => { if (event.target === dialog) dialog.close(); });
        if (dialog.dataset.openOnLoad === "true") void openActuals();
    }
})();
