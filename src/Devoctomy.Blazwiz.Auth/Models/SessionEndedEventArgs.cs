namespace Devoctomy.Blazwiz.Auth.Models;

/// <summary>
/// Describes a session that ended (<see cref="Services.Interfaces.IAuthenticationService.SessionEnded"/>).
/// </summary>
public sealed class SessionEndedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionEndedEventArgs"/> class.
    /// </summary>
    /// <param name="reason">Why the session ended.</param>
    /// <param name="deviceName">The name of the device that ended it, when another device did and said so.</param>
    public SessionEndedEventArgs(SessionEndedReason reason, string? deviceName = null)
    {
        Reason = reason;
        DeviceName = deviceName;
    }

    /// <summary>
    /// Gets why the session ended.
    /// </summary>
    public SessionEndedReason Reason { get; }

    /// <summary>
    /// Gets the name of the device that ended it, when another device did and said so. It comes from that device:
    /// show it as plain text only.
    /// </summary>
    public string? DeviceName { get; }
}
