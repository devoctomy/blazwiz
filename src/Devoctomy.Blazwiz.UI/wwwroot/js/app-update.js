// Reports a new version of the app downloading in the background, for AppUpdateMonitor (and the AppUpdateIndicator
// it drives). The service worker is registered by Devoctomy.Blazwiz.UI.lib.module.js; this module only watches it.
//
// A download is a worker installing while another controls the page (the first install, on the first visit, is not an
// update). Progress comes from the service worker, when it posts { type: 'INSTALL_PROGRESS', done, total } to its
// clients as it caches each file (the template's service-worker.published.js does); without that, the indicator shows
// no percentage.

let dotNet = null;
let watching = false;

export async function watch(dotNetReference) {
    dotNet = dotNetReference;
    if (watching || !('serviceWorker' in navigator)) {
        return;
    }

    watching = true;
    navigator.serviceWorker.addEventListener('message', function (event) {
        const data = event.data;
        if (!data || data.type !== 'INSTALL_PROGRESS' || !navigator.serviceWorker.controller) {
            return;
        }

        if (data.done >= data.total) {
            report('OnFinished');
        }
        else {
            report('OnDownloading', data.done, data.total);
        }
    });

    const registration = await navigator.serviceWorker.ready;
    if (registration.installing) {
        track(registration.installing);
    }

    registration.addEventListener('updatefound', function () {
        if (registration.installing) {
            track(registration.installing);
        }
    });
}

function track(worker) {
    if (!navigator.serviceWorker.controller) {
        return;
    }

    report('OnDownloading', 0, 0);
    worker.addEventListener('statechange', function () {
        if (worker.state !== 'installing') {
            report('OnFinished');
        }
    });
}

function report(method) {
    if (!dotNet) {
        return;
    }

    const args = Array.prototype.slice.call(arguments, 1);
    dotNet.invokeMethodAsync.apply(dotNet, [method].concat(args)).catch(function () { });
}
