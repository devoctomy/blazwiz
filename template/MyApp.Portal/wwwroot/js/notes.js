// Note editor helpers, called from NoteEditorInteropService and the NoteEditor page via JS interop. Notes are stored
// as files by the sync engine in Devoctomy.Blazwiz.Sync (see _content/Devoctomy.Blazwiz.Sync/js/indexeddb.js).
window.notesInterop = (function () {
    const shortcutHandlers = new Map();

    const editor = {
        getSelection: function (id) {
            const element = document.getElementById(id);
            return element ? { text: element.value, start: element.selectionStart, end: element.selectionEnd } : null;
        },

        // Replaces a range of text. execCommand keeps the browser's undo history (Ctrl+Z works) and raises the input
        // event Blazor's binding listens for; setRangeText is the fallback where execCommand is unavailable.
        applyEdit: function (id, edit) {
            const element = document.getElementById(id);
            if (!element) {
                return;
            }

            element.focus();
            element.setSelectionRange(edit.start, edit.end);
            if (edit.start !== edit.end || edit.replacement.length > 0) {
                let applied = false;
                try {
                    applied = edit.replacement.length > 0
                        ? document.execCommand('insertText', false, edit.replacement)
                        : document.execCommand('delete', false);
                } catch (e) {
                    applied = false;
                }

                if (!applied || element.value.substring(edit.start, edit.start + edit.replacement.length) !== edit.replacement) {
                    element.setRangeText(edit.replacement, edit.start, edit.end, 'end');
                    element.dispatchEvent(new Event('input', { bubbles: true }));
                }
            }

            element.setSelectionRange(edit.selectionStart, edit.selectionEnd);
        },

        // Ctrl/Cmd + S, B and I call the page's OnShortcut method.
        attachShortcuts: function (id, dotNetRef) {
            const element = document.getElementById(id);
            if (!element || shortcutHandlers.has(id)) {
                return;
            }

            const handler = function (e) {
                if (!(e.ctrlKey || e.metaKey) || e.altKey || e.shiftKey) {
                    return;
                }

                const key = e.key.toLowerCase();
                if (key === 's' || key === 'b' || key === 'i') {
                    e.preventDefault();
                    dotNetRef.invokeMethodAsync('OnShortcut', key);
                }
            };

            // Save works anywhere on the page, formatting only in the editor.
            const documentHandler = function (e) {
                if (e.target === element) {
                    handler(e);
                } else if ((e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && e.key.toLowerCase() === 's') {
                    e.preventDefault();
                    dotNetRef.invokeMethodAsync('OnShortcut', 's');
                }
            };

            document.addEventListener('keydown', documentHandler);
            shortcutHandlers.set(id, documentHandler);
        },

        detachShortcuts: function (id) {
            const handler = shortcutHandlers.get(id);
            if (handler) {
                document.removeEventListener('keydown', handler);
                shortcutHandlers.delete(id);
            }
        }
    };

    return { editor: editor };
})();

// Firefox only starts a drag when data is set in dragstart, which Blazor event handlers cannot do, so set it here
// for any element marked with data-drag-id.
document.addEventListener('dragstart', function (e) {
    const item = e.target && e.target.closest ? e.target.closest('[data-drag-id]') : null;
    if (item && e.dataTransfer) {
        e.dataTransfer.setData('text/plain', item.getAttribute('data-drag-id'));
        e.dataTransfer.effectAllowed = 'move';
    }
});
