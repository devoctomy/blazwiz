// Origin private file system (OPFS) access, imported by OpfsClient (blazwiz.Core). Files are kept under
// /<root>/<owner>/<key>; see filetree.js for the layout.

import { encode, isMissing, listTree, readFile, removeFile, writeFile } from './filetree.js';

let worker = null;
let nextWorkerId = 1;
const workerCalls = new Map();

async function getOwnerDirectory(root, owner, create) {
    const top = await navigator.storage.getDirectory();
    const rootDirectory = await top.getDirectoryHandle(encode(root), { create: create });
    return await rootDirectory.getDirectoryHandle(encode(owner), { create: create });
}

// Resolves with the owner's directory, or null if it does not exist yet.
async function findOwnerDirectory(root, owner) {
    try {
        return await getOwnerDirectory(root, owner, false);
    } catch (error) {
        if (isMissing(error)) {
            return null;
        }

        throw error;
    }
}

export async function isSupported() {
    if (!navigator.storage || typeof navigator.storage.getDirectory !== 'function') {
        return false;
    }

    try {
        // Throws in some private browsing modes.
        await navigator.storage.getDirectory();
        return true;
    } catch {
        return false;
    }
}

// Lists every file and folder marker as { key, size, lastModified (ms) }.
export async function list(root, owner) {
    const directory = await findOwnerDirectory(root, owner);
    return directory ? await listTree(directory) : [];
}

// Reads a file as { content, lastModified (ms) }, or null if there is none.
export async function read(root, owner, key) {
    const directory = await findOwnerDirectory(root, owner);
    return directory ? await readFile(directory, key) : null;
}

// Adds or replaces a file (or folder marker, with no content).
export async function write(root, owner, key, content) {
    const directory = await getOwnerDirectory(root, owner, true);
    await writeFile(directory, key, content, function (path, data) {
        // Older Safari only writes through a sync access handle, which is only available in a worker.
        return writeInWorker([encode(root), encode(owner)].concat(path.directories, [path.name]), data);
    });
}

// Deletes a file or folder marker, then any directories left empty. Does nothing if it does not exist.
export async function remove(root, owner, key) {
    const directory = await findOwnerDirectory(root, owner);
    if (directory) {
        await removeFile(directory, key);
    }
}

const workerSource = `
self.onmessage = async function (e) {
    const { id, segments, data } = e.data;
    try {
        let directory = await navigator.storage.getDirectory();
        for (const segment of segments.slice(0, -1)) {
            directory = await directory.getDirectoryHandle(segment, { create: true });
        }

        const file = await directory.getFileHandle(segments[segments.length - 1], { create: true });
        const handle = await file.createSyncAccessHandle();
        try {
            handle.truncate(0);
            handle.write(data, { at: 0 });
            handle.flush();
        } finally {
            handle.close();
        }

        self.postMessage({ id: id });
    } catch (error) {
        self.postMessage({ id: id, error: (error && error.name ? error.name + ': ' : '') + (error && error.message || error) });
    }
};`;

function writeInWorker(segments, data) {
    if (!worker) {
        worker = new Worker(URL.createObjectURL(new Blob([workerSource], { type: 'text/javascript' })));
        worker.onmessage = function (e) {
            const call = workerCalls.get(e.data.id);
            if (!call) {
                return;
            }

            workerCalls.delete(e.data.id);
            if (e.data.error) {
                call.reject(new Error(e.data.error));
            } else {
                call.resolve();
            }
        };
    }

    return new Promise(function (resolve, reject) {
        const id = nextWorkerId++;
        workerCalls.set(id, { resolve: resolve, reject: reject });
        worker.postMessage({ id: id, segments: segments, data: data });
    });
}
