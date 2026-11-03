// Browser helpers, imported by BrowserInteropService (Devoctomy.Blazwiz.Browser).

export function setTheme(theme) {
    document.documentElement.setAttribute('data-bs-theme', theme);
}

export function getPreferredTheme() {
    return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

// Focuses (and selects the text of) the first input in an element, or its first button if it has no inputs. Used by
// ModalDialog; the autofocus attribute only works when a page first loads.
export function focusFirstInput(element) {
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
