using System.Collections;
using Microsoft.Extensions.Configuration;
using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// How a .NET Xmip program audits (ADR-0062): what it started and stopped,
/// every act an operator took through it, and every failure, an unhandled one
/// first. The record is the audit capability's, reached through the runtime's
/// library (<see cref="RuntimeAudit"/>); this class keeps no record, policy
/// or sink of its own.
/// </summary>
/// <remarks>
/// It never throws. When the capability cannot be reached at all — the
/// runtime's library is not loadable — the program writes one entry to the
/// operating system's log itself, saying why (<see cref="OperatingSystemLog"/>,
/// the one .NET place that does). When the capability is reached and its own
/// sink fails, the capability writes to that log, not this.
/// </remarks>
/// <param name="program">The program's name, as its README calls it:
/// <c>Xmip.Gui.Web</c>, <c>xmip-cli</c>, <c>Xmip.PowerShell</c>.</param>
/// <param name="directory">Where its records go when it was told one (its
/// configuration's <see cref="ConfigurationKey"/>); null lets the capability
/// decide — <c>XMIP_AUDIT_DIRECTORY</c>, else the operating system's log.</param>
/// <param name="library">The runtime library the program was told to load
/// (<see cref="RuntimeLibrary.Stated"/>) and so the one its records go
/// through, where no rule of the runtime was called before; null lets the
/// rule with nothing configured decide.</param>
public sealed class ProgramAudit(
    string program, string? directory = null, string? library = null)
{
    /// <summary>The configuration key, in a host's <c>[Xmip]</c> table, that
    /// names the audit directory.</summary>
    public const string ConfigurationKey = "Xmip:AuditDirectory";

    /// <summary>The program's name, as every record carries it.</summary>
    public string Program { get; } = program;

    /// <summary>The directory it was told, or null.</summary>
    public string? Directory { get; } = directory;

    /// <summary>The runtime library it was told to record through, or
    /// null.</summary>
    public string? Library { get; } =
        string.IsNullOrWhiteSpace(library) ? null : RuntimeLibrary.Prefer(library);

    /// <summary>The audit directory a configuration names, resolved against
    /// <paramref name="basePath"/>; null when it names none.</summary>
    public static string? Stated(IConfiguration configuration, string basePath)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? configured = configuration[ConfigurationKey];

        return string.IsNullOrWhiteSpace(configured)
            ? null
            : TomlDocument.Resolve(configured, basePath);
    }

    /// <summary>
    /// What a program's values say as a record's properties, name to text, by
    /// <see cref="Said"/>. The one flattening every .NET program and the
    /// estate's script module call: until 2026-09-27 the cmdlets wrote a
    /// table as its type's name and the script module as its pairs.
    /// </summary>
    public static Dictionary<string, string> Properties(IDictionary? values)
    {
        Dictionary<string, string> said = new(StringComparer.Ordinal);

        if (values is null)
        {
            return said;
        }

        foreach (DictionaryEntry entry in values)
        {
            said[$"{entry.Key}"] = Said(entry.Value);
        }

        return said;
    }

    /// <summary>One value as a record says it: nothing as empty, a flag as
    /// <c>yes</c> or <c>no</c>, a table as its <c>name=value</c> pairs, a list
    /// joined, anything else as its text — each part said the same way.</summary>
    public static string Said(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            bool flag => flag ? "yes" : "no",
            IDictionary table => string.Join(
                ", ",
                table.Cast<DictionaryEntry>().Select(entry => $"{entry.Key}={Said(entry.Value)}")),
            IEnumerable many => string.Join(", ", many.Cast<object?>().Select(Said)),
            _ => value.ToString() ?? string.Empty,
        };
    }

    /// <summary>Record one act. Never throws: what the capability could not
    /// keep is in the operating system's log, and the outcome says so.</summary>
    public AuditOutcome Record(
        string action,
        AuditPhase phase,
        AuditSeverity severity,
        string? message = null,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            return RuntimeLibrary.Rules.Audit.Record(
                Program, Directory, action, phase, severity, message, properties);
        }
        catch (Exception unreachable) when (unreachable is not OutOfMemoryException)
        {
            return OperatingSystemLog.Write(
                Program, action, phase, severity, message, unreachable.Message);
        }
    }

    /// <summary>
    /// Audit every exception this process does not handle — on any thread,
    /// and every faulted task nobody observed — as <c>unhandled</c>, before
    /// the runtime does whatever it would have done. Once per process, at
    /// its start.
    /// </summary>
    public void WatchUnhandled()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, raised) =>
        {
            Exception failure = raised.ExceptionObject as Exception
                ?? new InvalidOperationException($"{raised.ExceptionObject}");

            Failed("unhandled", failure, new Dictionary<string, string>
            {
                ["terminating"] = raised.IsTerminating ? "yes" : "no",
            });
        };
        TaskScheduler.UnobservedTaskException += (_, raised) =>
            Failed("unobserved task", raised.Exception);
    }

    /// <summary>Record that <paramref name="action"/> failed with
    /// <paramref name="failure"/>: the Failure phase, Error, the exception's
    /// message, and its type and stack as properties. One exception is one
    /// record: a failure a host's log already recorded, and then its catch
    /// and the process's unhandled handler meet again, is not recorded twice
    /// — null says it was recorded before.</summary>
    public AuditOutcome? Failed(
        string action,
        Exception failure,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        ArgumentNullException.ThrowIfNull(failure);

        if (!FirstTime(failure))
        {
            return null;
        }

        Dictionary<string, string> said = new(properties ?? new Dictionary<string, string>())
        {
            ["exception"] = failure.GetType().FullName ?? failure.GetType().Name,
            ["detail"] = failure.ToString(),
        };

        return Record(action, AuditPhase.Failure, AuditSeverity.Error, failure.Message, said);
    }

    // Marks the exception as recorded; false when it already was.
    private static bool FirstTime(Exception failure)
    {
        const string Recorded = "Xmip.Audit.Recorded";

        try
        {
            if (failure.Data.Contains(Recorded))
            {
                return false;
            }

            failure.Data[Recorded] = true;
        }
        catch (Exception unmarkable) when (
            unmarkable is NotSupportedException or ArgumentException)
        {
            // An exception whose data cannot be written is recorded every time
            // it is met, rather than never.
        }

        return true;
    }
}
