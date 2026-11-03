// JavaScript initializer for Devoctomy.Blazwiz.UI.Sync, run by Blazor as it starts: adds the sync module's styles, so
// the app's index.html does not list them. They go after the framework's styles and before the app's own.

export function beforeStart() {
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = '_content/Devoctomy.Blazwiz.UI.Sync/css/blazwiz-sync.css';
    const appStyles = document.querySelector('link[href="css/app.css"]');
    document.head.insertBefore(link, appStyles);
}
