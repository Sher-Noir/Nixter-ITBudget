/* Budget wizard and Actuals dialog: external script for LedgerForge's self-only CSP. */
(() => {
    "use strict";

    // Progressive enhancement: keep native <select> elements as the form source of truth,
    // and provide an accessible type-to-filter combobox for every wizard/edit dropdown.
    // If scripting is unavailable, the original dropdown remains fully usable.
    const enhanceSelect = select => {
        if (!select.id || select.dataset.searchEnhanced === "true") return;
        const label = document.querySelector(`label[for="${select.id}"]`);
        const sourceOptions = [...select.options].map(option => ({
            value: option.value, label: option.textContent.trim()
        }));
        if (sourceOptions.length === 0) return;

        const wrapper = document.createElement("div");
        wrapper.className = "lf-select-combobox";
        const search = document.createElement("input");
        search.type = "search";
        search.className = "lf-select-search";
        search.id = select.id + "-search";
        search.setAttribute("role", "combobox");
        search.setAttribute("aria-autocomplete", "list");
        search.setAttribute("aria-haspopup", "listbox");
        search.setAttribute("aria-expanded", "false");
        search.setAttribute("autocomplete", "off");
        search.placeholder = "Search or select…";
        if (label) label.htmlFor = search.id;

        const popup = document.createElement("div");
        popup.className = "lf-select-options";
        popup.id = select.id + "-options";
        popup.setAttribute("role", "listbox");
        popup.hidden = true;
        search.setAttribute("aria-controls", popup.id);
        wrapper.append(search, popup);
        select.before(wrapper);
        select.dataset.searchEnhanced = "true";
        select.classList.add("lf-searchable-native");
        select.setAttribute("aria-hidden", "true");
        select.tabIndex = -1;

        const chosen = () => sourceOptions.find(option => option.value === select.value);
        const restoreText = () => { search.value = chosen()?.label || ""; };
        let matches = [];
        let highlighted = -1;

        const close = (restore = true) => {
            popup.hidden = true;
            search.setAttribute("aria-expanded", "false");
            search.removeAttribute("aria-activedescendant");
            if (restore) restoreText();
        };
        const pick = option => {
            select.value = option.value;
            select.dispatchEvent(new Event("change", {bubbles: true}));
            close(true);
            search.focus();
        };
        const render = filter => {
            const query = filter.trim().toLocaleLowerCase();
            matches = sourceOptions.filter(option => option.label.toLocaleLowerCase().includes(query));
            highlighted = -1;
            popup.replaceChildren();
            if (!matches.length) {
                const empty = document.createElement("div");
                empty.className = "lf-select-no-results";
                empty.textContent = "No matches";
                popup.append(empty);
            } else {
                matches.slice(0, 100).forEach((option, index) => {
                    const button = document.createElement("button");
                    button.type = "button";
                    button.className = "lf-select-option";
                    button.id = select.id + "-option-" + index;
                    button.setAttribute("role", "option");
                    button.setAttribute("aria-selected", String(option.value === select.value));
                    button.textContent = option.label;
                    button.addEventListener("click", () => pick(option));
                    popup.append(button);
                });
            }
            popup.hidden = false;
            search.setAttribute("aria-expanded", "true");
        };
        const highlight = nextIndex => {
            const choices = [...popup.querySelectorAll(".lf-select-option")];
            if (!choices.length) return;
            highlighted = Math.min(Math.max(nextIndex, 0), choices.length - 1);
            choices.forEach((choice, index) => {
                choice.classList.toggle("is-highlighted", index === highlighted);
            });
            const active = choices[highlighted];
            search.setAttribute("aria-activedescendant", active.id);
            active.scrollIntoView({block: "nearest"});
        };
        search.addEventListener("focus", () => {
            search.select(); // Typing immediately replaces the current selection.
            render("");
        });
        search.addEventListener("input", () => render(search.value));
        search.addEventListener("keydown", event => {
            if (event.key === "Escape") { event.preventDefault(); close(); }
            else if (event.key === "ArrowDown" || event.key === "ArrowUp") {
                event.preventDefault();
                if (popup.hidden) render("");
                highlight(event.key === "ArrowDown" ? highlighted + 1 : highlighted < 0 ? 0 : highlighted - 1);
            } else if (event.key === "Enter" && !popup.hidden) {
                event.preventDefault();
                const option = matches[highlighted >= 0 ? highlighted : 0];
                if (option) pick(option);
            } else if (event.key === "Tab") close();
        });
        search.addEventListener("blur", event => {
            if (!wrapper.contains(event.relatedTarget)) close();
        });
        document.addEventListener("pointerdown", event => {
            if (!wrapper.contains(event.target)) close();
        });
        select.addEventListener("change", restoreText);
        restoreText();
    };
    document.querySelectorAll("#lf-budget-create-form select, #edit-item select")
        .forEach(enhanceSelect);

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
            // Enter in a text/search field must not bypass the wizard's final step.
            if (step !== steps.length - 1) {
                event.preventDefault();
                next?.click();
                return;
            }
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
