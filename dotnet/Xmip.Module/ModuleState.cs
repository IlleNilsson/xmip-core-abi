using System.Runtime.InteropServices;
using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Module;

/// <summary>
/// One module instance, as the entrypoint created it: the unit, its table
/// and the strings its descriptor lends, in memory the node never frees —
/// no allocator is shared across the boundary (specification section 4) —
/// and the detail of its most recent failing call, lent until the next.
/// </summary>
internal sealed class ModuleState : IDisposable
{
    private readonly GCHandle _self;
    private readonly XmipStr _name;
    private readonly XmipStr _capability;
    private readonly XmipPromoteVtable* _table;
    private readonly Lock _gate = new();

    private ModuleState(PromoteUnit unit, XmipPromoteVtable table)
    {
        Unit = unit;
        _name = Native(unit.Name);
        _capability = Native(unit.Capability);
        _table = (XmipPromoteVtable*)NativeMemory.Alloc((nuint)sizeof(XmipPromoteVtable));

        unsafe
        {
            *_table = table;
        }

        _self = GCHandle.Alloc(this);
    }

    /// <summary>The unit it serves.</summary>
    internal PromoteUnit Unit { get; }

    /// <summary>What <c>last_error</c> lends: valid until the next failing
    /// call on this instance.</summary>
    internal XmipStr LastError { get; private set; }

    /// <summary>A new instance serving <paramref name="unit"/> through
    /// <paramref name="table"/>.</summary>
    internal static ModuleState Of(PromoteUnit unit, XmipPromoteVtable table)
    {
        return new ModuleState(unit, table);
    }

    /// <summary>The instance behind the <c>state</c> the node hands back.
    /// Unsafe: the caller promises <paramref name="state"/> is one this
    /// instance's handle gave and it is not yet destroyed.</summary>
    internal static unsafe ModuleState From(void* state)
    {
        unsafe
        {
            return (ModuleState)GCHandle.FromIntPtr((nint)state).Target!;
        }
    }

    /// <summary>The handle the entrypoint fills (<c>xmip_module.h</c>
    /// section 7): the descriptor naming the trait and the unit, as a Work
    /// Process unit's does, and the module's own version.</summary>
    internal XmipModuleHandle Handle(
        delegate* unmanaged[Cdecl]<void*, XmipStr> lastError,
        delegate* unmanaged[Cdecl]<void*, void> destroy)
    {
        Version version = Unit.GetType().Assembly.GetName().Version ?? new Version(0, 0, 0);

        unsafe
        {
            return new XmipModuleHandle
            {
                Descriptor = new XmipModuleDescriptor
                {
                    AbiVersion = ModuleAbi.AbiVersion,
                    Provider = _name,
                    Module = _capability,
                    Standard = _name,
                    TraitMajor = 1,
                    TraitMinor = 0,
                    ModuleMajor = (uint)Math.Max(version.Major, 0),
                    ModuleMinor = (uint)Math.Max(version.Minor, 0),
                    ModulePatch = (uint)Math.Max(version.Build, 0),
                },
                State = (void*)GCHandle.ToIntPtr(_self),
                Vtable = _table,
                LastError = lastError,
                Destroy = destroy,
            };
        }
    }

    /// <summary>Say why the call failing now failed; what was said before
    /// is let go.</summary>
    internal void Fail(string why)
    {
        lock (_gate)
        {
            Free(LastError);
            LastError = Native(why);
        }
    }

    /// <summary>Let everything go: the table, the strings, the instance.</summary>
    public void Dispose()
    {
        unsafe
        {
            NativeMemory.Free(_table);
            _self.Free();
        }

        Free(_name);
        Free(_capability);
        Free(LastError);
    }

    private static XmipStr Native(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        byte* copy = (byte*)NativeMemory.Alloc((nuint)Math.Max(bytes.Length, 1));

        unsafe
        {
            bytes.CopyTo(new Span<byte>(copy, bytes.Length));

            return new XmipStr(copy, (nuint)bytes.Length);
        }
    }

    private static void Free(XmipStr text)
    {
        unsafe
        {
            NativeMemory.Free(text.Data);
        }
    }
}
