// Persistent storage (navigator.storage), imported by StorageDurabilityService (blazwiz.Core).

function isInstalled() {
    return (window.matchMedia && window.matchMedia('(display-mode: standalone)').matches) || window.navigator.standalone === true;
}

export async function getStatus() {
    const storage = navigator.storage;
    if (!storage || typeof storage.persisted !== 'function') {
        return { isSupported: false, isPersisted: false, isInstalled: isInstalled(), usageBytes: null, quotaBytes: null };
    }

    const persisted = await storage.persisted();
    let usage = null;
    let quota = null;
    if (typeof storage.estimate === 'function') {
        const estimate = await storage.estimate();
        usage = typeof estimate.usage === 'number' ? estimate.usage : null;
        quota = typeof estimate.quota === 'number' ? estimate.quota : null;
    }

    return { isSupported: true, isPersisted: persisted, isInstalled: isInstalled(), usageBytes: usage, quotaBytes: quota };
}

export async function persist() {
    const storage = navigator.storage;
    if (!storage || typeof storage.persist !== 'function') {
        return false;
    }

    return await storage.persist();
}
