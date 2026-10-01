// Browser network status and cross tab locks, imported by BrowserNetworkStatus and WebLockSyncLockService (blazwiz.Core).

let listeners = null;

export function isOnline() {
    return navigator.onLine !== false;
}

// Calls OnOnlineChanged(bool) on online / offline, and OnBecameVisible() when the page becomes visible again.
export function subscribe(dotNetRef) {
    unsubscribe();
    listeners = {
        online: function () {
            dotNetRef.invokeMethodAsync('OnOnlineChanged', true);
        },
        offline: function () {
            dotNetRef.invokeMethodAsync('OnOnlineChanged', false);
        },
        visibility: function () {
            if (document.visibilityState === 'visible') {
                dotNetRef.invokeMethodAsync('OnBecameVisible');
            }
        }
    };

    window.addEventListener('online', listeners.online);
    window.addEventListener('offline', listeners.offline);
    document.addEventListener('visibilitychange', listeners.visibility);
}

export function unsubscribe() {
    if (!listeners) {
        return;
    }

    window.removeEventListener('online', listeners.online);
    window.removeEventListener('offline', listeners.offline);
    document.removeEventListener('visibilitychange', listeners.visibility);
    listeners = null;
}

const heldLocks = new Map();
let nextLockId = 1;

// Waits for a Web Lock and resolves with an id to release it with. The lock is held until releaseLock is called, or
// the tab closes. Browsers without Web Locks get a lock that excludes nothing (id 0).
export function acquireLock(name) {
    if (!navigator.locks || typeof navigator.locks.request !== 'function') {
        return Promise.resolve(0);
    }

    return new Promise(function (resolveAcquired, rejectAcquired) {
        navigator.locks.request(name, function () {
            return new Promise(function (release) {
                const id = nextLockId++;
                heldLocks.set(id, release);
                resolveAcquired(id);
            });
        }).catch(rejectAcquired);
    });
}

export function releaseLock(id) {
    const release = heldLocks.get(id);
    if (release) {
        heldLocks.delete(id);
        release();
    }
}
