// Describes the platform and browser for the default device name, imported by DeviceNameService
// (Devoctomy.Blazwiz.Browser). A best guess: nothing relies on it being right.

function platformFromAgentData(data) {
    switch ((data && data.platform) || '') {
        case 'Android': return 'Android';
        case 'iOS': return 'iOS';
        case 'Windows': return 'Windows';
        case 'macOS': return 'macOS';
        case 'Chrome OS':
        case 'Chromium OS': return 'ChromeOS';
        case 'Linux': return 'Linux';
        default: return null;
    }
}

function platformFromUserAgent(ua) {
    if (/Android/.test(ua)) {
        return 'Android';
    }

    // iPadOS asks for desktop sites and says Macintosh; a touch screen gives it away.
    if (/iPhone|iPad|iPod/.test(ua) || (/Macintosh/.test(ua) && navigator.maxTouchPoints > 1)) {
        return 'iOS';
    }

    if (/Windows/.test(ua)) {
        return 'Windows';
    }

    if (/CrOS/.test(ua)) {
        return 'ChromeOS';
    }

    if (/Macintosh|Mac OS X/.test(ua)) {
        return 'macOS';
    }

    if (/Linux/.test(ua)) {
        return 'Linux';
    }

    return null;
}

function browserFromAgentData(data) {
    const brands = ((data && data.brands) || []).map(b => b.brand);
    if (brands.includes('Microsoft Edge')) {
        return 'Edge';
    }

    if (brands.includes('Opera')) {
        return 'Opera';
    }

    if (brands.includes('Brave')) {
        return 'Brave';
    }

    if (brands.includes('Google Chrome')) {
        return 'Chrome';
    }

    return null;
}

function browserFromUserAgent(ua) {
    // Order matters: Edge, Opera and Samsung also say Chrome and Safari; on iOS every browser is Safari underneath,
    // but Chrome, Edge and Firefox still name themselves (CriOS, EdgiOS, FxiOS).
    if (/EdgiOS|EdgA|Edg\//.test(ua)) {
        return 'Edge';
    }

    if (/OPR\/|OPiOS/.test(ua)) {
        return 'Opera';
    }

    if (/SamsungBrowser/.test(ua)) {
        return 'Samsung';
    }

    if (/CriOS|Chrome\//.test(ua)) {
        return 'Chrome';
    }

    if (/FxiOS|Firefox\//.test(ua)) {
        return 'Firefox';
    }

    if (/Safari\//.test(ua)) {
        return 'Safari';
    }

    return null;
}

export function describe() {
    const data = navigator.userAgentData;
    const ua = navigator.userAgent || '';
    return {
        platform: platformFromAgentData(data) || platformFromUserAgent(ua),
        browser: browserFromAgentData(data) || browserFromUserAgent(ua),
    };
}
