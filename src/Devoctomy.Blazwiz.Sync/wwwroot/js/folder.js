// A folder the user picks on their own computer (File System Access API, Chromium desktop only), imported by
// FolderAccessClient (Devoctomy.Blazwiz.Sync). The picked directory handle is kept in IndexedDB under a key (one per user) so it
// can be used again after a reload; the browser may then ask for permission again. Files are laid out as in filetree.js.
// FolderAccessClient calls configure first, so the database and picker are named after the app.

import { listTree, readFile, removeFile, writeFile } from './filetree.js';

let database = 'folders';
let pickerId = 'sync';
const storeName = 'handles';
const mode = { mode: 'readwrite' };

function openDatabase() {
    return new Promise(function (resolve, reject) {
        const request = indexedDB.open(database, 1);
        request.onupgradeneeded = function () {
            request.result.createObjectStore(storeName);
        };
        request.onsuccess = function () {
            resolve(request.result);
        };
        request.onerror = function () {
            reject(request.error);
        };
    });
}

async function run(access, action) {
    const db = await openDatabase();
    try {
        return await new Promise(function (resolve, reject) {
            const request = action(db.transaction(storeName, access).objectStore(storeName));
            request.onsuccess = function () {
                resolve(request.result);
            };
            request.onerror = function () {
                reject(request.error);
            };
        });
    } finally {
        db.close();
    }
}

function getHandle(key) {
    return run('readonly', function (store) {
        return store.get(key);
    });
}

function putHandle(key, handle) {
    return run('readwrite', function (store) {
        return store.put(handle, key);
    });
}

async function requireHandle(key) {
    const handle = await getHandle(key);
    if (!handle) {
        throw new Error('No folder has been chosen.');
    }

    return handle;
}

// Names the database and the picker after the app, e.g. 'acmeportal_folders'.
export function configure(prefix) {
    database = prefix + 'folders';
    pickerId = prefix + 'sync';
}

export function isSupported() {
    return typeof window.showDirectoryPicker === 'function';
}

// Asks the user to pick a folder (must follow a click) and keeps it. Resolves with its name, or null if cancelled.
export async function pick(key) {
    let handle;
    try {
        handle = await window.showDirectoryPicker({ id: pickerId, mode: 'readwrite' });
    } catch (error) {
        if (error && error.name === 'AbortError') {
            return null;
        }

        throw error;
    }

    if (await handle.queryPermission(mode) !== 'granted' && await handle.requestPermission(mode) !== 'granted') {
        return null;
    }

    await putHandle(key, handle);
    return handle.name;
}

// Resolves with { name, permission ('granted', 'prompt' or 'denied') }, or null if no folder has been chosen.
export async function getStatus(key) {
    const handle = await getHandle(key);
    return handle ? { name: handle.name, permission: await handle.queryPermission(mode) } : null;
}

// Asks for permission to the chosen folder again (must follow a click). Resolves with true if granted.
export async function reconnect(key) {
    const handle = await getHandle(key);
    return !!handle && await handle.requestPermission(mode) === 'granted';
}

// Lists the files under a prefix ending in '/' (e.g. 'documents/'), or the whole folder for an empty prefix.
export async function list(key, prefix) {
    return await listTree(await requireHandle(key), prefix);
}

export async function read(key, fileKey) {
    return await readFile(await requireHandle(key), fileKey);
}

// Resolves with the file's lastModified (ms) after writing.
export async function write(key, fileKey, content) {
    return await writeFile(await requireHandle(key), fileKey, content, null);
}

export async function remove(key, fileKey) {
    await removeFile(await requireHandle(key), fileKey);
}
