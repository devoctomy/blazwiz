// Messages between tabs of the app (BroadcastChannel), imported by SyncStorageChannel (blazwiz.Core). A tab does not
// receive its own messages. Browsers without BroadcastChannel send and receive nothing.

let channel = null;

// Calls OnMessage(string) for each message posted by another tab.
export function subscribe(name, dotNetRef) {
    unsubscribe();
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

export function unsubscribe() {
    if (channel) {
        channel.close();
        channel = null;
    }
}

export function post(name, message) {
    if (typeof BroadcastChannel !== 'function') {
        return;
    }

    const sender = new BroadcastChannel(name);
    sender.postMessage(message);
    sender.close();
}
