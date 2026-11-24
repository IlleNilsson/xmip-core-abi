using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Module.Test;

/// <summary>The table the entrypoint fills, driven as a node drives it
/// (<c>xmip_module.h</c> sections 7, 8 and 14).</summary>
public sealed class EntrypointTest
{
    public EntrypointTest()
    {
        Export.Unit(Measured.Exported);
    }

    [Fact]
    public void TheModuleSaysItIsAPromoteUnitAndPromotesThroughItsTable()
    {
        unsafe
        {
            XmipHost host = new() { AbiVersion = ModuleAbi.AbiVersion };
            XmipModuleHandle handle = default;
            delegate* unmanaged[Cdecl]<XmipHost*, XmipModuleHandle*, int> create =
                &Entrypoint.Create;

            Assert.Equal((int)XmipStatus.Ok, create(&host, &handle));
            Assert.Equal("promote", handle.Descriptor.Module.Read());
            Assert.Equal("Measured", handle.Descriptor.Standard.Read());
            Assert.Equal((1u, 0u), (handle.Descriptor.TraitMajor, handle.Descriptor.TraitMinor));

            XmipPromoteVtable* table = (XmipPromoteVtable*)handle.Vtable;

            Assert.Equal((int)XmipStatus.Ok, table->Header.Start(handle.State));

            void* step = Opened(handle, """{"property":"Order","limit":10}""", out int opened);

            Assert.Equal((int)XmipStatus.Ok, opened);

            List<(string, XmipValueKind, string)> values =
                Promoted(handle, step, "PO-4711", out int answer);

            Assert.Equal((int)XmipStatus.Ok, answer);
            Assert.Equal(
                [
                    ("Order", XmipValueKind.Number, "7"),
                    ("Text", XmipValueKind.String, "PO-4711"),
                    ("Empty", XmipValueKind.Bool, "false"),
                ],
                values);

            _ = Promoted(handle, step, "refuse", out int refused);

            Assert.Equal((int)XmipStatus.Malformed, refused);
            Assert.Equal("the content says refuse", handle.LastError(handle.State).Read());

            table->Release(handle.State, step);

            _ = Opened(handle, """{"property":""}""", out int empty);

            Assert.Equal((int)XmipStatus.Contract, empty);
            Assert.Equal("the step's property is empty", handle.LastError(handle.State).Read());

            _ = Opened(handle, "{}", out int unread);

            Assert.Equal((int)XmipStatus.Contract, unread);
            Assert.Equal(
                "the step gives no setting 'property'",
                handle.LastError(handle.State).Read());

            Assert.Equal((int)XmipStatus.Ok, table->Header.Stop(handle.State));
            handle.Destroy(handle.State);
        }
    }

    [Fact]
    public void AHostOfAnotherVersionIsRefused()
    {
        unsafe
        {
            XmipHost host = new() { AbiVersion = ModuleAbi.AbiVersion + 1 };
            XmipModuleHandle handle = default;
            delegate* unmanaged[Cdecl]<XmipHost*, XmipModuleHandle*, int> create =
                &Entrypoint.Create;

            Assert.Equal((int)XmipStatus.Unsupported, create(&host, &handle));
            Assert.Equal((int)XmipStatus.Invalid, create(null, &handle));
        }
    }

    private static unsafe void* Opened(XmipModuleHandle handle, string settings, out int answer)
    {
        unsafe
        {
            XmipPromoteVtable* table = (XmipPromoteVtable*)handle.Vtable;
            using PinnedStr stage = XmipStr.Pin("receive");
            using PinnedStr given = XmipStr.Pin(settings);
            void* step = null;

            answer = table->Open(handle.State, stage.Value, given.Value, &step);

            return step;
        }
    }

    private static unsafe List<(string, XmipValueKind, string)> Promoted(
        XmipModuleHandle handle,
        void* step,
        string content,
        out int answer)
    {
        unsafe
        {
            XmipPromoteVtable* table = (XmipPromoteVtable*)handle.Vtable;
            Source source = new(Encoding.UTF8.GetBytes(content));
            List<(string, XmipValueKind, string)> values = [];
            GCHandle reading = GCHandle.Alloc(source);
            GCHandle taking = GCHandle.Alloc(values);

            try
            {
                XmipReader reader = new()
                {
                    Ctx = (void*)GCHandle.ToIntPtr(reading),
                    Read = &Read,
                };
                XmipPromotionSink sink = new()
                {
                    Ctx = (void*)GCHandle.ToIntPtr(taking),
                    Promoted = &Take,
                };

                answer = table->Promote(handle.State, step, &reader, &sink);

                return values;
            }
            finally
            {
                reading.Free();
                taking.Free();
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe long Read(void* ctx, byte* buffer, nuint length)
    {
        unsafe
        {
            Source source = (Source)GCHandle.FromIntPtr((nint)ctx).Target!;

            return source.Into(new Span<byte>(buffer, (int)length));
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int Take(void* ctx, XmipStr property, XmipValueKind kind, XmipSlice value)
    {
        unsafe
        {
            List<(string, XmipValueKind, string)> values =
                (List<(string, XmipValueKind, string)>)GCHandle.FromIntPtr((nint)ctx).Target!;
            string text = Encoding.UTF8.GetString(value.Data, (int)value.Len);

            values.Add((property.Read(), kind, text));

            return (int)XmipStatus.Ok;
        }
    }

    /// <summary>Content handed a piece at a time, as a node's reader does.</summary>
    private sealed class Source(byte[] content)
    {
        private int _at;

        public long Into(Span<byte> buffer)
        {
            int count = Math.Min(buffer.Length, content.Length - _at);

            content.AsSpan(_at, count).CopyTo(buffer);
            _at += count;

            return count;
        }
    }
}
