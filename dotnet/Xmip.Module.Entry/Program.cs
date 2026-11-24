using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Xmip.Module.Entry;

/// <summary>
/// Writes a module's <c>[[modules]]</c> entry from its built assembly:
/// <c>Xmip.Module.Entry &lt;assembly&gt; &lt;module&gt; &lt;version&gt;
/// &lt;library&gt; &lt;entry file&gt;</c>.
/// </summary>
internal static class Program
{
    private static int Main(string[] arguments)
    {
        if (arguments.Length != 5)
        {
            Console.Error.WriteLine(
                "Xmip.Module.Entry <assembly> <module> <version> <library> <entry file>");
            return 2;
        }

        string built = Path.GetFullPath(arguments[0]);
        AssemblyDependencyResolver resolver = new(built);

        AssemblyLoadContext.Default.Resolving += (context, name) =>
            resolver.ResolveAssemblyToPath(name) is { } path
                ? context.LoadFromAssemblyPath(path)
                : null;

        try
        {
            Assembly module = AssemblyLoadContext.Default.LoadFromAssemblyPath(built);

            // The module's initializers run as they run when a node loads
            // the library, and export its unit.
            RuntimeHelpers.RunModuleConstructor(module.ManifestModule.ModuleHandle);
            File.WriteAllText(
                arguments[4],
                ModuleEntry.OfExported(arguments[1], arguments[2], arguments[3]));

            return 0;
        }
        catch (Exception refused) when (refused is InvalidOperationException or IOException
            or BadImageFormatException or TypeInitializationException)
        {
            Console.Error.WriteLine($"{built}: {refused.Message}");
            return 1;
        }
    }
}
