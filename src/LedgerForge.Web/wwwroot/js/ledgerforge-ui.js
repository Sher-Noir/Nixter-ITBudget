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
})();
