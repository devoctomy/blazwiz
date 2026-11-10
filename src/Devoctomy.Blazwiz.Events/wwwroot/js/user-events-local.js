// The user's event channel between the tabs of one browser, over BroadcastChannel. Imported by LocalUserEventChannel
// (Devoctomy.Blazwiz.Events). A BroadcastChannel never delivers to the object that posted, so this tab does not hear
// itself; the .NET side also ignores events from its own instance id.

let channel = null;

export function connect(name, dotNetRef) {
    disconnect();
    if (typeof BroadcastChannel !== 'function') {
        return;
    }

    channel = new BroadcastChannel(name);
    channel.onmessage = function (e) {
        if (typeof e.data === 'string') {
            dotNetRef.invokeMethodAsync('OnMessage', e.data);
        }
    };
}

export function disconnect() {
    if (channel) {
        channel.close();
        channel = null;
    }
}

// Posts on the open channel when it is the user's, otherwise on one opened for the purpose (which this tab's open
// channel would hear: the .NET side ignores its own instance's events).
export function publish(name, json) {
    if (typeof BroadcastChannel !== 'function') {
        return;
    }

    if (channel && channel.name === name) {
        channel.postMessage(json);
        return;
    }

    const once = new BroadcastChannel(name);
    once.postMessage(json);
    once.close();
}
