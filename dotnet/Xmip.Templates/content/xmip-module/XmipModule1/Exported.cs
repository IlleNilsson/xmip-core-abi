using System.Runtime.CompilerServices;
using Xmip.Module;

namespace XmipModule1;

/// <summary>
/// The unit this library exports, as it loads into a node. A library holds
/// one unit; a second unit is a second project.
/// </summary>
internal static class Exported
{
    [ModuleInitializer]
    internal static void Unit()
    {
        Export.Unit(new OrderNumber());
    }
}
