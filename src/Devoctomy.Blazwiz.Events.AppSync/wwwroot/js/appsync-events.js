// The user's event channel over AWS AppSync Events, imported by AppSyncUserEventChannel
// (Devoctomy.Blazwiz.Events.AppSync). Speaks the documented protocol directly rather than through Amplify:
//   - subscribe: a WebSocket to wss://<realtime host>/event/realtime with the subprotocols 'aws-appsync-event-ws' and
//     'header-<base64url of { host, Authorization }>', then connection_init, connection_ack (with the keep-alive
//     timeout), subscribe / subscribe_success, data messages and ka keep-alives;
//   - publish: POST https://<http host>/event with { channel, events: [<JSON string>] }.
// Authorization is the session's Cognito access token.

const connectTimeoutMs = 15000;
const defaultKeepAliveMs = 300000;

let socket = null;
let keepAliveTimer = null;
let closing = false;

function base64Url(value) {
    return btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function stopKeepAlive() {
    if (keepAliveTimer !== null) {
        clearTimeout(keepAliveTimer);
        keepAliveTimer = null;
    }
}

// The service sends 'ka' within the keep-alive timeout; when it stops, the connection is gone even if the browser
// has not noticed, so it is closed (and .NET told) to reconnect.
function resetKeepAlive(ws, timeoutMs) {
    stopKeepAlive();
    keepAliveTimer = setTimeout(function () {
        ws.close();
    }, timeoutMs || defaultKeepAliveMs);
}

export function connect(realtimeHost, httpHost, token, channel, dotNetRef) {
    disconnect();
    closing = false;
    const authorization = { host: httpHost, Authorization: token };

    return new Promise(function (resolve, reject) {
        let settled = false;
        let keepAliveMs = defaultKeepAliveMs;
        const ws = new WebSocket('wss://' + realtimeHost + '/event/realtime', ['aws-appsync-event-ws', 'header-' + base64Url(authorization)]);
        socket = ws;

        const timer = setTimeout(function () {
            fail('Timed out connecting to the event channel.');
        }, connectTimeoutMs);

        function fail(message) {
            clearTimeout(timer);
            if (!settled) {
                settled = true;
                reject(new Error(message));
            }

            ws.close();
        }

        ws.onopen = function () {
            ws.send(JSON.stringify({ type: 'connection_init' }));
        };

        ws.onmessage = function (e) {
            let message;
            try {
                message = JSON.parse(e.data);
            } catch {
                return;
            }

            switch (message.type) {
                case 'connection_ack':
                    keepAliveMs = message.connectionTimeoutMs || defaultKeepAliveMs;
                    resetKeepAlive(ws, keepAliveMs);
                    ws.send(JSON.stringify({ type: 'subscribe', id: crypto.randomUUID(), channel: channel, authorization: authorization }));
                    break;
                case 'ka':
                    resetKeepAlive(ws, keepAliveMs);
                    break;
                case 'subscribe_success':
                    clearTimeout(timer);
                    settled = true;
                    resolve();
                    break;
                case 'data':
                    if (typeof message.event === 'string') {
                        dotNetRef.invokeMethodAsync('OnMessage', message.event);
                    } else if (message.event) {
                        dotNetRef.invokeMethodAsync('OnMessage', JSON.stringify(message.event));
                    }
                    break;
                case 'connection_error':
                case 'subscribe_error':
                case 'error':
                    fail('The event channel refused the connection: ' + message.type);
                    break;
                default:
                    break;
            }
        };

        ws.onclose = function () {
            clearTimeout(timer);
            if (!settled) {
                settled = true;
                reject(new Error('The event channel closed while connecting.'));
            }

            if (socket === ws) {
                socket = null;
                stopKeepAlive();
                if (!closing) {
                    dotNetRef.invokeMethodAsync('OnClosed');
                }
            }
        };
    });
}

export function disconnect() {
    stopKeepAlive();
    if (socket) {
        closing = true;
        const ws = socket;
        socket = null;
        ws.close();
    }
}

export async function publish(httpHost, token, channel, json) {
    const response = await fetch('https://' + httpHost + '/event', {
        method: 'POST',
        headers: { 'content-type': 'application/json', Authorization: token },
        body: JSON.stringify({ channel: channel, events: [json] })
    });

    if (!response.ok) {
        throw new Error('The event channel refused the event: HTTP ' + response.status);
    }

    let body = null;
    try {
        body = await response.json();
    } catch {
        body = null;
    }

    if (body && Array.isArray(body.failed) && body.failed.length > 0) {
        throw new Error('The event channel refused the event.');
    }
}
