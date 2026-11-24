using System.Runtime.CompilerServices;
using Xmip.Abi.Module;

namespace Xmip.Abi.Test;

/// <summary>The shapes of <c>xmip_module.h</c> a module fills, held to the
/// header: the value kinds of section 10 by name and number, and the tables
/// of sections 7, 8 and 14 by their size in pointers.</summary>
public sealed class ModuleShapeTest
{
    [Fact]
    public void EveryValueKindIsTheHeadersByNameAndNumber()
    {
        IReadOnlyDictionary<string, int> header = Header.Enumerators("VALUE", "xmip_module.h");

        Assert.Equal(header.Count, Enum.GetValues<XmipValueKind>().Length);

        foreach ((string name, int value) in header)
        {
            Assert.Equal(value, (int)Enum.Parse<XmipValueKind>(Header.MemberName(name)));
        }
    }

    [Fact]
    public void TheTablesAreTheHeadersSize()
    {
        int pointer = IntPtr.Size;
        int header = Unsafe.SizeOf<XmipVtableHeader>();

        // Two words of trait version, then three function pointers.
        Assert.Equal(8 + (3 * pointer), header);
        Assert.Equal(header + (3 * pointer), Unsafe.SizeOf<XmipPromoteVtable>());
        Assert.Equal(2 * pointer, Unsafe.SizeOf<XmipPromotionSink>());
        Assert.Equal(2 * pointer, Unsafe.SizeOf<XmipReader>());
        Assert.Equal(2 * pointer, Unsafe.SizeOf<XmipSlice>());
        Assert.Equal(5 * pointer, Unsafe.SizeOf<XmipHost>());
        Assert.Equal(
            Unsafe.SizeOf<XmipModuleDescriptor>() + (4 * pointer),
            Unsafe.SizeOf<XmipModuleHandle>());
    }
}
