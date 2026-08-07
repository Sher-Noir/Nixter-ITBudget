(() => {
  const storageKey = "ledgerforge-theme";
  const root = document.documentElement;
  const defaultTheme = root.dataset.defaultTheme || "system";

  const resolveTheme = (value) => {
    if (value === "light" || value === "dark") return value;
    return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
  };

  const applyTheme = (value) => {
    const resolved = resolveTheme(value);
    root.dataset.theme = resolved;
    root.dataset.themePreference = value;
    const label = document.querySelector("[data-theme-label]");
    if (label) label.textContent = resolved === "dark" ? "Light mode" : "Dark mode";
    const icon = document.querySelector("[data-theme-icon]");
    if (icon) icon.textContent = resolved === "dark" ? "☀" : "☾";
  };

  const stored = localStorage.getItem(storageKey) || defaultTheme;
  applyTheme(stored);

  window.matchMedia("(prefers-color-scheme: dark)").addEventListener("change", () => {
    if ((localStorage.getItem(storageKey) || defaultTheme) === "system") applyTheme("system");
  });

  document.addEventListener("click", (event) => {
    const themeButton = event.target.closest("[data-theme-toggle]");
    if (themeButton) {
      const next = root.dataset.theme === "dark" ? "light" : "dark";
      localStorage.setItem(storageKey, next);
      applyTheme(next);
      return;
    }

    if (event.target.closest("[data-mobile-menu]")) {
      document.body.classList.toggle("lf-nav-open");
    }
  });
})();
