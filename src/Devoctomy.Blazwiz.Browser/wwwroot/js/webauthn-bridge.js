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

    async function register(username, existingCredentialId = null) {
        try {
            checkLibrary();

            if (!username?.trim()) {
                throw new Error('Username is required');
            }

            const { startRegistration } = SimpleWebAuthnBrowser;

            const challenge = toB64URL(randomBytes(32));
            const userIdBytes = enc.encode(username + '-' + Date.now());

            const optionsJSON = {
                rp: {
                    id: RP_ID,
                    name: RP_NAME
                },
                user: {
                    id: toB64URL(userIdBytes),
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
                excludeCredentials: existingCredentialId ? [{
                    id: existingCredentialId,
                    type: 'public-key',
                    transports: ['internal', 'usb', 'ble', 'nfc', 'hybrid']
                }] : [],
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
                    const authExtResults = authResp.clientExtensionResults || {};

                    if (authExtResults.prf?.results?.first) {
                        const prfResult = new Uint8Array(authExtResults.prf.results.first);
                        prfOutput = Array.from(prfResult).map(b => b.toString(16).padStart(2, '0')).join('');
                    }
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
            const flags = asseResp.response?.authenticatorDataFlags || {};
            const clientExtensionResults = asseResp.clientExtensionResults || {};

            let prfOutput = null;
            if (clientExtensionResults.prf?.results?.first) {
                const prfResult = new Uint8Array(clientExtensionResults.prf.results.first);
                prfOutput = Array.from(prfResult).map(b => b.toString(16).padStart(2, '0')).join('');
            }

            return {
                success: true,
                userPresent: flags.up || false,
                userVerified: flags.uv || false,
                prfOutput: prfOutput,
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
