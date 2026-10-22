using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// The upgrade scripts the runtime wrote (<see cref="RuntimeDatabases"/>, section
/// 20), written once for every surface into the folder an operator names:
/// <c>xmip database --out</c> and <c>Get-XmipDatabaseScript -Path</c>
/// (ADR-0015, amendment 2026-10-20). Nothing is overwritten: where any script
/// is there already, none is written and the refusal names it.
/// </summary>
public static class DatabaseScripts
{
    /// <summary>
    /// Each of <paramref name="upgrade"/>'s scripts written into
    /// <paramref name="folder"/>, made where it is missing: the paths
    /// written, in the order to run them; or null with
    /// <paramref name="refused"/> naming the file already there.
    /// </summary>
    public static IReadOnlyList<string>? Write(
        DatabaseUpgrade upgrade, string folder, out string refused)
    {
        ArgumentNullException.ThrowIfNull(upgrade);
        ArgumentNullException.ThrowIfNull(folder);

        string[] paths = [.. upgrade.Scripts.Select(script => Path.Combine(folder, script.Name))];

        if (paths.FirstOrDefault(File.Exists) is { } there)
        {
            refused = $"REFUSED: {there} is there already, and nothing is overwritten.";
            return null;
        }

        Directory.CreateDirectory(folder);

        foreach ((DatabaseScript script, string path) in upgrade.Scripts.Zip(paths))
        {
            using FileStream file = new(path, FileMode.CreateNew, FileAccess.Write);
            using StreamWriter writer = new(file);
            writer.Write(script.Text);
        }

        refused = string.Empty;
        return paths;
    }
}
