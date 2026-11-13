// Generic IndexedDB access, imported by IndexedDbClient (Devoctomy.Blazwiz.Sync). Callers describe the object store they want
// ({ database, name, keyPath, indexes }) and it is created the first time it is used, by upgrading the database to
// its next version, so stores can be added to an existing database (e.g. two profiles sharing one) at any time.

const connections = new Map();
const verified = new Set();
let upgradeQueue = Promise.resolve();

function toKeyPath(paths) {
    return paths.length === 1 ? paths[0] : paths;
}

function openDatabase(name, version, upgrade) {
    return new Promise(function (resolve, reject) {
        const request = version ? indexedDB.open(name, version) : indexedDB.open(name);
        request.onupgradeneeded = function () {
            if (upgrade) {
                upgrade(request.result, request.transaction);
            }
        };
        request.onsuccess = function () {
            const db = request.result;

            // Another tab is upgrading the database: let it, and reconnect next time.
            db.onversionchange = function () {
                db.close();
                forget(name);
            };
            resolve(db);
        };
        request.onerror = function () {
            reject(request.error);
        };
    });
}

function forget(name) {
    connections.delete(name);
    for (const key of Array.from(verified)) {
        if (key.startsWith(name + '/')) {
            verified.delete(key);
        }
    }
}

function getConnection(name) {
    let promise = connections.get(name);
    if (!promise) {
        promise = openDatabase(name);
        connections.set(name, promise);
        promise.catch(function () {
            forget(name);
        });
    }

    return promise;
}

function hasStore(db, store) {
    if (!db.objectStoreNames.contains(store.name)) {
        return false;
    }

    const indexes = store.indexes || [];
    if (indexes.length === 0) {
        return true;
    }

    const names = db.transaction(store.name, 'readonly').objectStore(store.name).indexNames;
    return indexes.every(function (index) {
        return names.contains(index.name);
    });
}

// Upgrades the database (one upgrade at a time) to change its stores.
function upgradeDatabase(name, needed, change) {
    const run = upgradeQueue.then(async function () {
        const current = await getConnection(name);
        if (!needed(current)) {
            return current;
        }

        const version = current.version + 1;
        current.close();
        forget(name);
        const promise = openDatabase(name, version, change);
        connections.set(name, promise);
        promise.catch(function () {
            forget(name);
        });
        return promise;
    });

    upgradeQueue = run.catch(function () { });
    return run;
}

async function ensureStore(store) {
    const id = store.database + '/' + store.name;
    const db = await getConnection(store.database);
    if (verified.has(id) || hasStore(db, store)) {
        verified.add(id);
        return db;
    }

    const upgraded = await upgradeDatabase(store.database, function (current) {
        return !hasStore(current, store);
    }, function (upgradeDb, transaction) {
        const objectStore = upgradeDb.objectStoreNames.contains(store.name)
            ? transaction.objectStore(store.name)
            : upgradeDb.createObjectStore(store.name, { keyPath: toKeyPath(store.keyPath) });
        (store.indexes || []).forEach(function (index) {
            if (!objectStore.indexNames.contains(index.name)) {
                objectStore.createIndex(index.name, toKeyPath(index.keyPath), { unique: false });
            }
        });
    });

    verified.add(id);
    return upgraded;
}

// Runs one operation in a transaction and resolves with its result once the transaction has committed.
function run(db, storeName, mode, operation) {
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

function omit(records, fields) {
    if (!fields || fields.length === 0) {
        return records;
    }

    return records.map(function (record) {
        return Object.fromEntries(Object.entries(record).filter(function (entry) {
            return !fields.includes(entry[0]);
        }));
    });
}

export async function get(store, key) {
    const db = await ensureStore(store);
    return run(db, store.name, 'readonly', function (objectStore) {
        return objectStore.get(key);
    });
}

export async function getAllByIndex(store, indexName, key, omitFields) {
    const db = await ensureStore(store);
    const records = await run(db, store.name, 'readonly', function (objectStore) {
        return objectStore.index(indexName).getAll(key);
    });
    return omit(records || [], omitFields);
}

export async function put(store, value) {
    const db = await ensureStore(store);
    await run(db, store.name, 'readwrite', function (objectStore) {
        objectStore.put(value);
    });
}

export async function putMany(store, values) {
    const db = await ensureStore(store);
    await run(db, store.name, 'readwrite', function (objectStore) {
        values.forEach(function (value) {
            objectStore.put(value);
        });
    });
}

// The last record (in index order) whose index value is between lower and upper, or null.
export async function getLastByIndex(store, indexName, lower, upper) {
    const db = await ensureStore(store);
    return new Promise(function (resolve, reject) {
        const transaction = db.transaction(store.name, 'readonly');
        const request = transaction.objectStore(store.name).index(indexName).openCursor(IDBKeyRange.bound(lower, upper), 'prev');
        let result = null;
        request.onsuccess = function () {
            result = request.result ? request.result.value : null;
        };
        transaction.oncomplete = function () {
            resolve(result);
        };
        transaction.onerror = function () {
            reject(transaction.error);
        };
    });
}

export async function deleteMany(store, keys) {
    const db = await ensureStore(store);
    await run(db, store.name, 'readwrite', function (objectStore) {
        keys.forEach(function (key) {
            objectStore.delete(key);
        });
    });
}
