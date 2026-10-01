// Notes storage (IndexedDB) and editor helpers, called from IndexedDbNoteIndexStore, IndexedDbNoteContentStore,
// NoteEditorInteropService and the NoteEditor page via JS interop.
window.notesInterop = (function () {
    const DB_NAME = 'blazwiz_notes';
    const DB_VERSION = 1;
    let dbPromise = null;

    // Object stores: 'entries' is the file index (folders and note metadata, indexed by owner), 'content' holds the
    // content of notes stored by the IndexedDB provider, and 'state' holds per user state such as the welcome note flag.
    function openDb() {
        if (!dbPromise) {
            dbPromise = new Promise(function (resolve, reject) {
                const request = indexedDB.open(DB_NAME, DB_VERSION);
                request.onupgradeneeded = function () {
                    const db = request.result;
                    if (!db.objectStoreNames.contains('entries')) {
                        db.createObjectStore('entries', { keyPath: 'id' }).createIndex('owner', 'owner', { unique: false });
                    }
                    if (!db.objectStoreNames.contains('content')) {
                        db.createObjectStore('content', { keyPath: 'id' });
                    }
                    if (!db.objectStoreNames.contains('state')) {
                        db.createObjectStore('state', { keyPath: 'owner' });
                    }
                };
                request.onsuccess = function () {
                    resolve(request.result);
                };
                request.onerror = function () {
                    dbPromise = null;
                    reject(request.error);
                };
            });
        }

        return dbPromise;
    }

    // Runs one operation in a transaction and resolves with its result once the transaction has committed.
    async function run(storeName, mode, operation) {
        const db = await openDb();
        return new Promise(function (resolve, reject) {
            const transaction = db.transaction(storeName, mode);
            const request = operation(transaction.objectStore(storeName));
            transaction.oncomplete = function () {
                resolve(request && request.result !== undefined ? request.result : null);
            };
            transaction.onerror = function () {
                reject(transaction.error);
            };
            transaction.onabort = function () {
                reject(transaction.error);
            };
        });
    }

    const db = {
        get: function (storeName, key) {
            return run(storeName, 'readonly', function (store) {
                return store.get(key);
            });
        },

        getAllByOwner: function (storeName, owner) {
            return run(storeName, 'readonly', function (store) {
                return store.index('owner').getAll(owner);
            });
        },

        put: function (storeName, value) {
            return run(storeName, 'readwrite', function (store) {
                store.put(value);
            });
        },

        deleteMany: function (storeName, keys) {
            return run(storeName, 'readwrite', function (store) {
                keys.forEach(function (key) {
                    store.delete(key);
                });
            });
        }
    };

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

    return { db: db, editor: editor };
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
