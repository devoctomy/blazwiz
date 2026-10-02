// Origin private file system (OPFS) access, imported by OpfsClient (blazwiz.Core). Files are kept under
// /<root>/<owner>/<key>, one directory per key segment. A folder marker (a key ending with '/') is a '.folder' file in
// its directory, so an empty folder that was created on purpose can be told apart from one that is only implied.

const folderMarker = '.folder';
let worker = null;
let nextWorkerId = 1;
const workerCalls = new Map();

// Segments OPFS rejects ('.', '..', containing '/' or '\'), the marker name, and '%' itself are percent encoded.
function encode(segment) {
    if (segment.length === 0) {
        throw new Error('File keys cannot have empty segments.');
    }

    const encoded = segment.replace(/%/g, '%25').replace(/\\/g, '%5C');
    return encoded === '.' || encoded === '..' || encoded === folderMarker ? '%2E' + encoded.substring(1) : encoded;
}

function decode(name) {
    return name.replace(/%(25|5C|2E)/gi, function (match, code) {
        return String.fromCharCode(parseInt(code, 16));
    });
}

function split(key) {
    const isFolder = key.endsWith('/');
    const segments = (isFolder ? key.substring(0, key.length - 1) : key).split('/').map(encode);
    return { isFolder: isFolder, directories: isFolder ? segments : segments.slice(0, -1), name: isFolder ? folderMarker : segments[segments.length - 1] };
}

function isMissing(error) {
    return error && (error.name === 'NotFoundError' || error.name === 'TypeMismatchError');
}

async function getOwnerDirectory(root, owner, create) {
    const top = await navigator.storage.getDirectory();
    const rootDirectory = await top.getDirectoryHandle(encode(root), { create: create });
    return await rootDirectory.getDirectoryHandle(encode(owner), { create: create });
}

async function getDirectory(root, owner, directories, create) {
    let directory = await getOwnerDirectory(root, owner, create);
    for (const segment of directories) {
        directory = await directory.getDirectoryHandle(segment, { create: create });
    }

    return directory;
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
    let ownerDirectory;
    try {
        ownerDirectory = await getOwnerDirectory(root, owner, false);
    } catch (error) {
        if (isMissing(error)) {
            return [];
        }

        throw error;
    }

    const entries = [];
    async function walk(directory, prefix) {
        for await (const [name, handle] of directory.entries()) {
            if (handle.kind === 'directory') {
                await walk(handle, prefix + decode(name) + '/');
                continue;
            }

            if (name === folderMarker && prefix === '') {
                continue;
            }

            const file = await handle.getFile();
            entries.push({
                key: name === folderMarker ? prefix : prefix + decode(name),
                size: name === folderMarker ? 0 : file.size,
                lastModified: file.lastModified
            });
        }
    }

    await walk(ownerDirectory, '');
    return entries;
}

// Reads a file as { content, lastModified (ms) }, or null if there is none.
export async function read(root, owner, key) {
    const path = split(key);
    try {
        const directory = await getDirectory(root, owner, path.directories, false);
        const handle = await directory.getFileHandle(path.name);
        const file = await handle.getFile();
        return { content: new Uint8Array(await file.arrayBuffer()), lastModified: file.lastModified };
    } catch (error) {
        if (isMissing(error)) {
            return null;
        }

        throw error;
    }
}

// Adds or replaces a file (or folder marker, with no content).
export async function write(root, owner, key, content) {
    const path = split(key);
    const directory = await getDirectory(root, owner, path.directories, true);
    const handle = await directory.getFileHandle(path.name, { create: true });
    const data = path.isFolder || !content ? new Uint8Array(0) : content;
    if (typeof handle.createWritable === 'function') {
        const writable = await handle.createWritable();
        try {
            await writable.write(data);
            await writable.close();
        } catch (error) {
            await writable.abort().catch(function () { });
            throw error;
        }

        return;
    }

    // Older Safari only writes through a sync access handle, which is only available in a worker.
    await writeInWorker([encode(root), encode(owner)].concat(path.directories, [path.name]), data);
}

// Deletes a file or folder marker, then any directories left empty. Does nothing if it does not exist.
export async function remove(root, owner, key) {
    const path = split(key);
    const chain = [];
    try {
        let directory = await getOwnerDirectory(root, owner, false);
        chain.push(directory);
        for (const segment of path.directories) {
            directory = await directory.getDirectoryHandle(segment);
            chain.push(directory);
        }

        await directory.removeEntry(path.name);
    } catch (error) {
        if (isMissing(error)) {
            return;
        }

        throw error;
    }

    // Walk back up, removing directories that are now empty (but never the owner's directory).
    for (let i = chain.length - 1; i > 0; i--) {
        if (!(await isEmpty(chain[i]))) {
            break;
        }

        await chain[i - 1].removeEntry(path.directories[i - 1]);
    }
}

async function isEmpty(directory) {
    for await (const _ of directory.keys()) {
        return false;
    }

    return true;
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
