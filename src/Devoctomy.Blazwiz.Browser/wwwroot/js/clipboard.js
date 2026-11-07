// Copies text to the clipboard, and clears it again later (for secrets such as passwords). Used by ClipboardService.

let pending = null;

// Clears the clipboard if it still holds the text copied, once the page has focus again: browsers only let a focused
// page write to the clipboard.
window.addEventListener('focus', () => {
    if (pending !== null) {
        tryClear();
    }
});

export async function copy(text) {
    pending = null;
    await navigator.clipboard.writeText(text);
}

export async function clearIfUnchanged(text) {
    pending = text;
    return await tryClear();
}

async function tryClear() {
    const text = pending;
    if (text === null) {
        return true;
    }

    const current = await readIfAllowed();
    if (current !== null && current !== text) {
        // Something else was copied since: leave it.
        pending = null;
        return false;
    }

    try {
        // Not knowing what the clipboard holds, clear it: leaving a secret there is worse than clearing something else.
        await navigator.clipboard.writeText('');
        pending = null;
        return true;
    } catch {
        // Not focused: cleared when the page has focus again.
        return false;
    }
}

async function readIfAllowed() {
    try {
        // Only read without asking: a permission prompt every time a copied secret expires would be worse.
        const permission = await navigator.permissions.query({ name: 'clipboard-read' });
        if (permission.state !== 'granted') {
            return null;
        }

        return await navigator.clipboard.readText();
    } catch {
        return null;
    }
}
