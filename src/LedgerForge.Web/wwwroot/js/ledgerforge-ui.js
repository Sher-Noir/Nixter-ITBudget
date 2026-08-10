(() => {
    document.addEventListener("click", event => {
        const link = event.target.closest("a[href^='#']");
        if (link) {
            const selector = link.getAttribute("href");
            if (selector && selector.length > 1) {
                const target = document.querySelector(selector);
                if (target instanceof HTMLDetailsElement) {
                    target.open = true;
                    requestAnimationFrame(() => target.scrollIntoView({ behavior: "smooth", block: "start" }));
                }
            }
        }

        const menuButton = event.target.closest("[data-mobile-menu]");
        if (menuButton) document.documentElement.classList.toggle("lf-nav-open");
    });

    const showUpdateNotice = async () => {
        const actions = document.querySelector(".lf-top-actions");
        if (!actions || actions.querySelector("[data-ledgerforge-update]")) return;

        try {
            const response = await fetch("/updates/status", {
                method: "GET",
                credentials: "same-origin",
                cache: "no-store",
                headers: { "Accept": "application/json" }
            });
            if (!response.ok) return;

            const status = await response.json();
            if (!status.isUpdateAvailable || !status.latestVersion || !status.releaseUrl) return;

            const notice = document.createElement("a");
            notice.className = "button lf-update-notice";
            notice.dataset.ledgerforgeUpdate = "true";
            notice.href = status.releaseUrl;
            notice.target = "_blank";
            notice.rel = "noopener noreferrer";
            notice.textContent = `↑ Update ${status.latestVersion}`;
            notice.title = `LedgerForge ${status.latestVersion} is available. Installed version: ${status.currentVersion}.`;

            const themeToggle = actions.querySelector("[data-theme-toggle]");
            actions.insertBefore(notice, themeToggle || actions.firstChild);
        } catch {
            // Update availability must never interfere with normal LedgerForge use.
        }
    };

    if (document.readyState === "loading")
        document.addEventListener("DOMContentLoaded", showUpdateNotice, { once: true });
    else
        void showUpdateNotice();
})();
