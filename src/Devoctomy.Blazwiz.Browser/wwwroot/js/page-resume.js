// Tells .NET when the app resumes: the page becomes visible again, or the browser comes back online. Imported by
// PageResumeService (Devoctomy.Blazwiz.Browser).

let listeners = null;

// Calls OnResumed() on both.
export function subscribe(dotNetRef) {
    unsubscribe();
    listeners = {
        online: function () {
            dotNetRef.invokeMethodAsync('OnResumed');
        },
        visibility: function () {
            if (document.visibilityState === 'visible') {
                dotNetRef.invokeMethodAsync('OnResumed');
            }
        }
    };

    window.addEventListener('online', listeners.online);
    document.addEventListener('visibilitychange', listeners.visibility);
}

export function unsubscribe() {
    if (!listeners) {
        return;
    }

    window.removeEventListener('online', listeners.online);
    document.removeEventListener('visibilitychange', listeners.visibility);
    listeners = null;
}
