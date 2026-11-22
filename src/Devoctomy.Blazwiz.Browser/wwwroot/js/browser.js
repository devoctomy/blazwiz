// Browser helpers, imported by BrowserInteropService (Devoctomy.Blazwiz.Browser).

export function setTheme(theme) {
    document.documentElement.setAttribute('data-bs-theme', theme);
}

export function setLanguage(language) {
    document.documentElement.setAttribute('lang', language);
}

export function getPreferredTheme() {
    return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

// Focus helpers used by ModalDialog (the autofocus attribute only works when a page first loads). Each returns whether
// it found something to focus; the caller decides what to do when it did not.

// Focuses (and selects the text of) the first input, select or textarea in an element.
export function focusFirstInput(element) {
    const target = element ? element.querySelector('input:not([type=hidden]), select, textarea') : null;
    if (!target) {
        return false;
    }

    target.focus();
    if (typeof target.select === 'function' && target.tagName === 'INPUT') {
        target.select();
    }

    return true;
}

// Focuses the first button in an element's modal footer.
export function focusFirstButton(element) {
    return focusTarget(element ? element.querySelector('.modal-footer button') : null);
}

// Focuses the first element marked autofocus in an element.
export function focusAutofocus(element) {
    return focusTarget(element ? element.querySelector('[autofocus]') : null);
}

// Focuses the element itself.
export function focusElement(element) {
    return focusTarget(element);
}

function focusTarget(target) {
    if (!target) {
        return false;
    }

    target.focus();
    return true;
}
