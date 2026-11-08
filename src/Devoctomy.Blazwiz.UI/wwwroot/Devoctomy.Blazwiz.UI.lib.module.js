// JavaScript initializer for Devoctomy.Blazwiz.UI, run by Blazor once it has started.
//
// Registers the service worker and offers new versions of the app. A published service worker caches the whole app and
// a new version normally waits until every tab of the app is closed, which on phones (tabs kept alive) can be never.
// So: check for a new version on load and whenever the app comes back to the foreground, and when one is ready, show
// #app-update-banner. Its button tells the waiting service worker to take over, then the page reloads into it.
// Nothing switches without the user asking: swapping files under a running app can break it, and a reload loses
// things such as a picked folder's permission on some browsers. While the new version downloads, js/app-update.js
// (AppUpdateMonitor) shows the app update indicator.

export function afterStarted() {
    if (!('serviceWorker' in navigator)) {
        return;
    }

    let reloading = false;

    function showBanner(worker) {
        const banner = document.getElementById('app-update-banner');
        if (!banner) {
            return;
        }

        banner.hidden = false;
        banner.querySelector('.app-update-button').onclick = function () {
            this.disabled = true;
            worker.postMessage({ type: 'SKIP_WAITING' });
        };
    }

    // A worker that has installed while another controls the page is a new version waiting to take over.
    function watch(worker) {
        if (worker.state === 'installed' && navigator.serviceWorker.controller) {
            showBanner(worker);
            return;
        }

        worker.addEventListener('statechange', function () {
            if (worker.state === 'installed' && navigator.serviceWorker.controller) {
                showBanner(worker);
            }
        });
    }

    navigator.serviceWorker.addEventListener('controllerchange', function () {
        // The new version has taken over: load it. Only once, and only if this page asked (not on the first install).
        if (reloading || !document.getElementById('app-update-banner') || document.getElementById('app-update-banner').hidden) {
            return;
        }

        reloading = true;
        window.location.reload();
    });

    navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' }).then(function (registration) {
        if (registration.waiting) {
            watch(registration.waiting);
        }

        // The browser checks for a new version while loading the page, so one may already be installing (its
        // updatefound has been and gone): watch it too, or it finishes with no banner.
        if (registration.installing) {
            watch(registration.installing);
        }

        registration.addEventListener('updatefound', function () {
            if (registration.installing) {
                watch(registration.installing);
            }
        });

        document.addEventListener('visibilitychange', function () {
            if (document.visibilityState === 'visible') {
                registration.update().catch(function () { });
            }
        });
    });
}
