// Messages between tabs of the app (BroadcastChannel), imported by SyncStorageChannel and E2eLockChannel
// (Devoctomy.Blazwiz.Sync). A tab does not receive its own messages. Browsers without BroadcastChannel send and receive
// nothing.

const channels = new Map();

// Calls OnMessage(string) for each message posted on the named channel by another tab.
export function subscribe(name, dotNetRef) {
    unsubscribe(name);
    if (typeof BroadcastChannel !== 'function') {
        return;
    }

    const channel = new BroadcastChannel(name);
    channel.onmessage = function (e) {
        if (typeof e.data === 'string') {
            dotNetRef.invokeMethodAsync('OnMessage', e.data);
        }
    };
    channels.set(name, channel);
}

export function unsubscribe(name) {
    const channel = channels.get(name);
    if (channel) {
        channel.close();
        channels.delete(name);
    }
}

// Posts through the subscribed channel when there is one: a BroadcastChannel does not receive its own messages, but
// any other one in this tab would, and this tab would then react to its own message.
export function post(name, message) {
    if (typeof BroadcastChannel !== 'function') {
        return;
    }

    const channel = channels.get(name);
    if (channel) {
        channel.postMessage(message);
        return;
    }

    const sender = new BroadcastChannel(name);
    sender.postMessage(message);
    sender.close();
}
