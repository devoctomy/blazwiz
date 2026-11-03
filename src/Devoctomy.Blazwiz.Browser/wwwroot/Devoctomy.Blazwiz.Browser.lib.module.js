// JavaScript initializer for Devoctomy.Blazwiz.Browser, run by Blazor as it starts: loads the WebAuthn scripts
// (SimpleWebAuthn, and the bridge WebAuthnService calls), so the app's index.html does not list them.

const base = '_content/Devoctomy.Blazwiz.Browser/';

function loadScript(src) {
    return new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = src;
        script.onload = resolve;
        script.onerror = () => reject(new Error(`Could not load ${src}`));
        document.head.appendChild(script);
    });
}

export async function beforeStart() {
    // @simplewebauthn/browser v14.0.0 (dist/bundle/index.umd.min.js), then the bridge that uses it.
    await loadScript(base + 'lib/simplewebauthn/index.umd.min.js');
    await loadScript(base + 'js/webauthn-bridge.js');
}
