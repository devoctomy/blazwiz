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
const pendingLocks = new Map();

// Waits for a Web Lock and resolves with the request id, to release it with. The lock is held until releaseLock is
// called, or the tab closes. Browsers without Web Locks get a lock that excludes nothing (empty id).
export function acquireLock(name, requestId) {
    if (!navigator.locks || typeof navigator.locks.request !== 'function') {
        return Promise.resolve('');
    }

    const controller = new AbortController();
    pendingLocks.set(requestId, controller);
    return new Promise(function (resolveAcquired, rejectAcquired) {
        navigator.locks.request(name, { signal: controller.signal }, function () {
            return new Promise(function (release) {
                pendingLocks.delete(requestId);
                heldLocks.set(requestId, release);
                resolveAcquired(requestId);
            });
        }).catch(function (error) {
            pendingLocks.delete(requestId);
            rejectAcquired(error);
        });
    });
}

// Called when .NET stopped waiting: drops the request if it is still queued, or releases the lock if it was granted
// meanwhile, so it is never held with nothing to release it.
export function cancelLock(requestId) {
    const controller = pendingLocks.get(requestId);
    if (controller) {
        pendingLocks.delete(requestId);
        controller.abort();
        return;
    }

    releaseLock(requestId);
}

export function releaseLock(id) {
    const release = heldLocks.get(id);
    if (release) {
        heldLocks.delete(id);
        release();
    }
}
