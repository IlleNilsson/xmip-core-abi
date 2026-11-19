using System.Runtime.InteropServices;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary><c>XmipBoundaries</c>, section 28.3 of <c>xmip_operate.h</c>: the
/// two boundaries this build speaks, every text static.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct XmipBoundaries
{
    /// <summary><c>module_version</c>.</summary>
    public uint ModuleVersion;

    /// <summary><c>module_entrypoint</c>.</summary>
    public XmipStr ModuleEntrypoint;

    /// <summary><c>module_library_file_name</c>.</summary>
    public XmipStr ModuleLibraryFileName;

    /// <summary><c>operate_version</c>.</summary>
    public uint OperateVersion;

    /// <summary><c>operate_entrypoint</c>.</summary>
    public XmipStr OperateEntrypoint;
}
