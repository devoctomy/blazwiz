// Browser helpers called from BrowserInteropService via JS interop.
window.blazwiz = {
    setTheme: function (theme) {
        document.documentElement.setAttribute('data-bs-theme', theme);
    },

    getPreferredTheme: function () {
        return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }
};

// Apply the saved theme (or the OS preference) immediately, before Blazor has loaded.
(function () {
    var theme = null;
    try {
        var settings = JSON.parse(localStorage.getItem('blazwiz_user_settings') || '{}');
        theme = settings.Theme;
    } catch (e) {
        // Ignore unreadable settings and fall back to the OS preference.
    }

    window.blazwiz.setTheme(theme === 'dark' || theme === 'light' ? theme : window.blazwiz.getPreferredTheme());
})();
