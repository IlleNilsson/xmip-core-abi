using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// The one .NET place that writes to the operating system's log, and only for
/// the case ADR-0062 clause 3 leaves to a program: the audit capability could
/// not be reached at all — the runtime's library is not loadable — so the
/// program says so itself. Everything else, including the capability's own
/// fallback when its sink fails, is <c>xmip-core-audit</c>'s, in Rust.
/// </summary>
/// <remarks>
/// The Windows Event Log under the source <c>Xmip</c>, or, while that source
/// is not registered and this process cannot register it, under the
/// Application log's <c>.NET Runtime</c> source, saying so. Elsewhere one
/// datagram to the local syslog socket, which the journal reads on a systemd
/// machine and the unified log on macOS.
/// </remarks>
internal static class OperatingSystemLog
{
    private const string Source = OperateAbi.EventSource;

    private const string Unregistered = OperateAbi.EventSourceUnregistered;

    private static readonly string[] Sockets = ["/dev/log", "/var/run/syslog", "/var/run/log"];

    /// <summary>Write the act the capability could not take, with
    /// <paramref name="reason"/> as the sentence that says why, and nothing
    /// else (ADR-0062 clause 3): the act and its message, never its
    /// properties, which only the capability knows how to keep — an
    /// address's password among them.</summary>
    public static AuditOutcome Write(
        string program,
        string action,
        AuditPhase phase,
        AuditSeverity severity,
        string? message,
        string reason)
    {
        string text = Sentence(program, action, phase, severity, message, reason);

        try
        {
            string where = OperatingSystem.IsWindows()
                ? EventLogEntry(text, severity)
                : Syslog(text, severity);

            return new AuditOutcome(AuditKept.OperatingSystem, $"{where}: {reason}");
        }
        catch (Exception refused) when (refused is not OutOfMemoryException)
        {
            return new AuditOutcome(null,
                $"{reason}, and the operating system's log refused it too: {refused.Message}");
        }
    }

    /// <summary>The entry's text: why first, then the act and its message,
    /// then where it came from.</summary>
    internal static string Sentence(
        string program,
        string action,
        AuditPhase phase,
        AuditSeverity severity,
        string? message,
        string reason)
    {
        StringBuilder text = new(
            $"Xmip audit could not be reached ({reason}), so {program} writes this itself. " +
            $"{action} {phase.ToString().ToLowerInvariant()} " +
            $"{severity.ToString().ToLowerInvariant()}");

        if (!string.IsNullOrEmpty(message))
        {
            text.Append(CultureInfo.InvariantCulture, $": {message}");
        }

        text.Append(CultureInfo.InvariantCulture,
            $" ({program} pid {Environment.ProcessId} on {Environment.MachineName})");

        return text.ToString();
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static string EventLogEntry(string text, AuditSeverity severity)
    {
        EventLogEntryType type = severity switch
        {
            AuditSeverity.Error => EventLogEntryType.Error,
            AuditSeverity.Warning => EventLogEntryType.Warning,
            _ => EventLogEntryType.Information,
        };

        // The Event Log's limit on one entry is 31 839 characters.
        string entry = text.Length > 31_000 ? text[..31_000] : text;

        try
        {
            EventLog.WriteEntry(Source, entry, type, 1000);
            return $"the Windows Event Log, Application, source {Source}";
        }
        catch (Exception unregistered) when (
            unregistered is System.Security.SecurityException or InvalidOperationException
                or ArgumentException)
        {
            EventLog.WriteEntry(".NET Runtime", $"{Unregistered} {entry}", type, 1000);
            return "the Windows Event Log, Application, source .NET Runtime";
        }
    }

    private static string Syslog(string text, AuditSeverity severity)
    {
        // RFC 5424: facility user (1); error 3, warning 4, informational 6.
        int level = severity switch
        {
            AuditSeverity.Error => 3,
            AuditSeverity.Warning => 4,
            _ => 6,
        };
        byte[] datagram = Encoding.UTF8.GetBytes(
            $"<{8 + level}>xmip[{Environment.ProcessId}]: {text}");

        using Socket socket = new(AddressFamily.Unix, SocketType.Dgram, ProtocolType.Unspecified);
        List<string> refusals = [];

        foreach (string path in Sockets)
        {
            try
            {
                socket.SendTo(datagram.AsSpan(0, Math.Min(datagram.Length, 8 * 1024)),
                    new UnixDomainSocketEndPoint(path));
                return $"syslog at {path}";
            }
            catch (SocketException refused)
            {
                refusals.Add($"{path}: {refused.Message}");
            }
        }

        throw new IOException($"no local syslog took it ({string.Join("; ", refusals)})");
    }
}
