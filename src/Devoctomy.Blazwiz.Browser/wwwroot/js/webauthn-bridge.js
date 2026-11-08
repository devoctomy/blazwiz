/* global SimpleWebAuthnBrowser */
/**
 * WebAuthn bridge for Blazor - Direct SimpleWebAuthn integration. Loaded by Devoctomy.Blazwiz.Browser.lib.module.js.
 */
window.webAuthnWrapper = (function() {
    const RP_ID = location.hostname || 'localhost';
    // The relying party's name: the app's name (data-app-name on <html>, the same name as AppIdentity.Name).
    const RP_NAME = document.documentElement.getAttribute('data-app-name') || RP_ID;
    const enc = new TextEncoder();

    // Check if SimpleWebAuthn is available
    function checkLibrary() {
        if (typeof SimpleWebAuthnBrowser === 'undefined') {
            throw new Error('SimpleWebAuthnBrowser library not loaded');
        }
    }

    function randomBytes(len = 32) {
        const a = new Uint8Array(len);
        crypto.getRandomValues(a);
        return a;
    }

    function toB64URL(bytes) {
        const str = btoa(String.fromCharCode(...bytes));
        return str.replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '');
    }

    function fromB64URL(str) {
        const b64 = str.replace(/-/g, '+').replace(/_/g, '/') + '='.repeat((4 - str.length % 4) % 4);
        return Uint8Array.from(atob(b64), c => c.charCodeAt(0));
    }

    // The authenticator data's flags byte (offset 32, after the RP id hash): bit 0 is user presence (UP), bit 2 is
    // user verification (UV). The library returns the authenticator data base64url encoded.
    function readFlags(authenticatorData) {
        try {
            const bytes = typeof authenticatorData === 'string' ? fromB64URL(authenticatorData) : new Uint8Array(authenticatorData);
            const flags = bytes.length > 32 ? bytes[32] : 0;
            return { up: (flags & 0x01) !== 0, uv: (flags & 0x04) !== 0 };
        } catch {
            return { up: false, uv: false };
        }
    }

    // The PRF output as bytes, which Blazor hands to .NET as a byte array. The copy here is wiped once the result has
    // been handed over: the result is serialised when the returned promise settles, before a timer can run.
    function takePrfOutput(clientExtensionResults) {
        const first = clientExtensionResults?.prf?.results?.first;
        if (!first) {
            return null;
        }

        const bytes = new Uint8Array(first);
        setTimeout(() => bytes.fill(0), 0);
        return bytes;
    }

    function translateError(error) {
        const errorMsg = error?.message || error?.toString() || 'Unknown error';

        if (errorMsg.includes('NotAllowedError')) {
            return 'Operation was cancelled or timed out';
        } else if (errorMsg.includes('InvalidStateError')) {
            return 'A credential for this user might already exist or is not available';
        }

        return errorMsg;
    }

    async function checkSupport() {
        try {
            checkLibrary();

            const {
                browserSupportsWebAuthn,
                platformAuthenticatorIsAvailable
            } = SimpleWebAuthnBrowser;

            const result = {
                supported: false,
                platformAuthenticator: false,
                error: null
            };

            if (!browserSupportsWebAuthn?.()) {
                result.error = 'WebAuthn not supported in this browser';
                return result;
            }

            result.supported = true;
            result.platformAuthenticator = await platformAuthenticatorIsAvailable();

            return result;
        } catch (error) {
            return {
                supported: false,
                platformAuthenticator: false,
                error: error.message
            };
        }
    }

    async function register(username) {
        try {
            checkLibrary();

            if (!username?.trim()) {
                throw new Error('Username is required');
            }

            const { startRegistration } = SimpleWebAuthnBrowser;

            const challenge = toB64URL(randomBytes(32));

            const optionsJSON = {
                rp: {
                    id: RP_ID,
                    name: RP_NAME
                },
                user: {
                    // Random: the user handle is stored by the authenticator and must not identify the user.
                    id: toB64URL(randomBytes(32)),
                    name: username,
                    displayName: username
                },
                challenge,
                pubKeyCredParams: [
                    { type: 'public-key', alg: -7 },   // ES256
                    { type: 'public-key', alg: -257 }, // RS256
                    { type: 'public-key', alg: -35 },  // ES384
                    { type: 'public-key', alg: -36 }  // ES512
                ],
                timeout: 60_000,
                attestation: 'none',
                authenticatorSelection: {
                    residentKey: 'preferred',
                    requireResidentKey: false,
                    userVerification: 'preferred'
                },
                excludeCredentials: [],
                extensions: {
                    prf: {}
                }
            };

            const attResp = await startRegistration({ optionsJSON });
            const clientExtensionResults = attResp.clientExtensionResults || {};

            let prfOutput = null;

            // If PRF is enabled, perform a quick authentication to get PRF output
            if (clientExtensionResults.prf?.enabled) {
                try {
                    const { startAuthentication } = SimpleWebAuthnBrowser;
                    const authChallenge = toB64URL(randomBytes(32));

                    const authOptionsJSON = {
                        challenge: authChallenge,
                        rpId: RP_ID,
                        userVerification: 'preferred',
                        allowCredentials: [
                            {
                                id: attResp.id,
                                type: 'public-key',
                                transports: ['internal', 'usb', 'ble', 'nfc', 'hybrid']
                            }
                        ],
                        timeout: 60_000,
                        extensions: {
                            prf: {
                                eval: {
                                    first: enc.encode('WebAuthn PRF Registration Salt')
                                }
                            }
                        }
                    };

                    const authResp = await startAuthentication({ optionsJSON: authOptionsJSON });
                    prfOutput = takePrfOutput(authResp.clientExtensionResults);
                } catch (prfError) {
                    console.warn('Failed to get PRF output during registration:', prfError);
                    // Continue without PRF output - registration was still successful
                }
            }

            return {
                success: true,
                credentialID: attResp.id,
                userID: optionsJSON.user.id,
                username: username,
                prfEnabled: clientExtensionResults.prf?.enabled || false,
                prfOutput: prfOutput,
                error: null
            };
        } catch (error) {
            console.error('WebAuthn registration failed:', error);

            return {
                success: false,
                credentialID: null,
                userID: null,
                username: null,
                prfEnabled: false,
                error: translateError(error)
            };
        }
    }

    async function authenticate(credentialID) {
        try {
            checkLibrary();

            if (!credentialID) {
                throw new Error('Credential ID is required');
            }

            const { startAuthentication } = SimpleWebAuthnBrowser;

            const challenge = toB64URL(randomBytes(32));

            const optionsJSON = {
                challenge,
                rpId: RP_ID,
                userVerification: 'preferred',
                allowCredentials: [
                    {
                        id: credentialID,
                        type: 'public-key',
                        transports: ['internal', 'usb', 'ble', 'nfc', 'hybrid']
                    }
                ],
                timeout: 60_000,
                extensions: {
                    prf: {
                        eval: {
                            first: enc.encode('WebAuthn PRF Registration Salt')
                        }
                    }
                }
            };

            const asseResp = await startAuthentication({ optionsJSON });
            const flags = readFlags(asseResp.response?.authenticatorData);

            return {
                success: true,
                userPresent: flags.up,
                userVerified: flags.uv,
                prfOutput: takePrfOutput(asseResp.clientExtensionResults),
                error: null
            };
        } catch (error) {
            console.error('WebAuthn authentication failed:', error);

            return {
                success: false,
                userPresent: false,
                userVerified: false,
                prfOutput: null,
                error: translateError(error)
            };
        }
    }

    // Public API
    return {
        checkSupport: checkSupport,
        register: register,
        authenticate: authenticate
    };
})();
