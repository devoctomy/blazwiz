// Browser helpers called from BrowserInteropService via JS interop.
window.portalInterop = {
    setTheme: function (theme) {
        document.documentElement.setAttribute('data-bs-theme', theme);
    },

    getPreferredTheme: function () {
        return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    },

    // Focuses (and selects the text of) the first input in an element, or its first button if it has no inputs.
    // Used by ModalDialog; the autofocus attribute only works when a page first loads.
    focusFirstInput: function (element) {
        if (!element) {
            return;
        }

        const target = element.querySelector('input:not([type=hidden]), select, textarea') || element.querySelector('.modal-footer button');
        if (target) {
            target.focus();
            if (typeof target.select === 'function' && target.tagName === 'INPUT') {
                target.select();
            }
        }
    }
};

// Apply the saved theme (or the OS preference) immediately, before Blazor has loaded. The settings key is named after
// the app (data-app-name on <html>, the same name as AppIdentity.Name), as everything the app keeps in the browser is.
(function () {
    var theme = null;
    try {
        var prefix = document.documentElement.getAttribute('data-app-name') + '_';
        var settings = JSON.parse(localStorage.getItem(prefix + 'user_settings') || '{}');
        theme = settings.Theme;
    } catch (e) {
        // Ignore unreadable settings and fall back to the OS preference.
    }

    window.portalInterop.setTheme(theme === 'dark' || theme === 'light' ? theme : window.portalInterop.getPreferredTheme());
})();
