// Files kept as a directory tree, shared by opfs.js and folder.js (Devoctomy.Blazwiz.Sync). A key such as 'documents/Work/Ideas.md'
// is one directory per segment under a base directory. A folder marker (a key ending with '/') is a '.folder' file in
// its directory, so an empty folder that was created on purpose can be told apart from one that is only implied.

export const folderMarker = '.folder';

// Segments a file system rejects ('.', '..', containing '/' or '\'), the marker name, and '%' itself are percent encoded.
export function encode(segment) {
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

// Splits a key into its encoded directories and file name.
export function split(key) {
    const isFolder = key.endsWith('/');
    const segments = (isFolder ? key.substring(0, key.length - 1) : key).split('/').map(encode);
    return { isFolder: isFolder, directories: isFolder ? segments : segments.slice(0, -1), name: isFolder ? folderMarker : segments[segments.length - 1] };
}

export function isMissing(error) {
    return !!error && (error.name === 'NotFoundError' || error.name === 'TypeMismatchError');
}

async function getDirectory(base, directories, create) {
    let directory = base;
    for (const segment of directories) {
        directory = await directory.getDirectoryHandle(segment, { create: create });
    }

    return directory;
}

// Lists every file and folder marker under a directory as { key, size, lastModified (ms) }. With a prefix ending in
// '/' (e.g. 'documents/'), only that directory is walked.
export async function listTree(base, prefix) {
    const entries = [];
    let start = base;
    if (prefix) {
        try {
            start = await getDirectory(base, split(prefix).directories, false);
        } catch (error) {
            if (isMissing(error)) {
                return entries;
            }

            throw error;
        }
    }

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

    await walk(start, prefix || '');
    return entries;
}

// Reads a file as { content, lastModified (ms) }, or null if there is none.
export async function readFile(base, key) {
    const path = split(key);
    try {
        const directory = await getDirectory(base, path.directories, false);
        const file = await (await directory.getFileHandle(path.name)).getFile();
        return { content: new Uint8Array(await file.arrayBuffer()), lastModified: file.lastModified };
    } catch (error) {
        if (isMissing(error)) {
            return null;
        }

        throw error;
    }
}

// Adds or replaces a file (or folder marker, with no content), creating its directories. Resolves with the file's
// lastModified (ms) afterwards. Without createWritable, fallback(path, data) is called to write it another way.
export async function writeFile(base, key, content, fallback) {
    const path = split(key);
    const directory = await getDirectory(base, path.directories, true);
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
    } else if (fallback) {
        await fallback(path, data);
    } else {
        throw new Error('This browser cannot write files here.');
    }

    return (await handle.getFile()).lastModified;
}

// Deletes a file or folder marker, then any directories left empty (never the base). Does nothing if it does not exist.
export async function removeFile(base, key) {
    const path = split(key);
    const chain = [base];
    try {
        let directory = base;
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

    for (let i = chain.length - 1; i > 0; i--) {
        // eslint-disable-next-line security/detect-object-injection -- chain[i] is a numeric loop index into an array built here.
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
