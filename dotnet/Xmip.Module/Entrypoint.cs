using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xmip.Abi.Module;

namespace Xmip.Module;

/// <summary>
/// The module boundary as a .NET unit meets it: the one exported symbol,
/// <c>xmip_create_module_v1</c> (<c>xmip_module.h</c> section 7), and the
/// functions of the table it fills, sections 8 and 14. NativeAOT exports
/// the symbol from this assembly into the module's library
/// (<c>UnmanagedEntryPointsAssembly</c>, <c>build/Xmip.Module.targets</c>),
/// so a node opens a .NET module exactly as it opens a Rust one.
/// </summary>
/// <remarks>
/// Nothing unwinds across the boundary (specification section 6): every
/// function answers what was thrown in it as a status, a refusal as the
/// refusal it is, the node's own answer as that answer, anything else as
/// the unit's defect, and a failure of this code itself as <c>PANIC</c>.
/// Each is unsafe: its pointers are the node's, valid as the header says.
/// </remarks>
internal static class Entrypoint
{
    private const int Ok = (int)XmipStatus.Ok;

    [UnmanagedCallersOnly(EntryPoint = ModuleAbi.Entrypoint, CallConvs = [typeof(CallConvCdecl)])]
    internal static unsafe int Create(XmipHost* host, XmipModuleHandle* handle)
    {
        unsafe
        {
            try
            {
                if (host is null || handle is null)
                {
                    return (int)XmipStatus.Invalid;
                }

                if (host->AbiVersion != ModuleAbi.AbiVersion)
                {
                    return (int)XmipStatus.Unsupported;
                }

                if (Export.Exported is not PromoteUnit unit)
                {
                    return (int)XmipStatus.NotFound;
                }

                XmipPromoteVtable table = new()
                {
                    Header = new XmipVtableHeader
                    {
                        TraitMajor = 1,
                        TraitMinor = 0,
                        Configure = &Configure,
                        Start = &Lifecycle,
                        Stop = &Lifecycle,
                    },
                    Open = &Open,
                    Release = &Release,
                    Promote = &Promote,
                };

                *handle = ModuleState.Of(unit, table).Handle(&LastError, &Destroy);

                return Ok;
            }
            catch (Exception)
            {
                return (int)XmipStatus.Panic;
            }
        }
    }

    /// <summary>A unit takes no module configuration of its own; keys it does
    /// not know are tolerated (conformance rule 7).</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int Configure(void* state, XmipStr toml)
    {
        return Ok;
    }

    /// <summary><c>start</c> and <c>stop</c>: a unit holds nothing that
    /// starts.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int Lifecycle(void* state)
    {
        return Ok;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe XmipStr LastError(void* state)
    {
        unsafe
        {
            try
            {
                return ModuleState.From(state).LastError;
            }
            catch (Exception)
            {
                return default;
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void Destroy(void* state)
    {
        unsafe
        {
            try
            {
                ModuleState.From(state).Dispose();
            }
            catch (Exception)
            {
                // Nothing is left to tell: the node is letting the instance go.
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int Open(void* state, XmipStr stage, XmipStr settings, void** step)
    {
        unsafe
        {
            ModuleState? module = null;

            try
            {
                module = ModuleState.From(state);

                if (step is null)
                {
                    return (int)XmipStatus.Invalid;
                }

                StepSettings read = StepSettings.Read(stage.Read(), settings.Read());
                Promotion promotion = module.Unit.Open(read);

                *step = (void*)GCHandle.ToIntPtr(GCHandle.Alloc(promotion));

                return Ok;
            }
            catch (RefusedException refused)
            {
                return Failed(module, XmipStatus.Contract, refused.Message);
            }
            catch (Exception defect)
            {
                return Failed(module, XmipStatus.Internal, defect.Message);
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void Release(void* state, void* step)
    {
        unsafe
        {
            try
            {
                GCHandle held = GCHandle.FromIntPtr((nint)step);

                (held.Target as IDisposable)?.Dispose();
                held.Free();
            }
            catch (Exception)
            {
                // A step that will not let go is let go of all the same.
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int Promote(
        void* state,
        void* step,
        XmipReader* content,
        XmipPromotionSink* sink)
    {
        unsafe
        {
            ModuleState? module = null;

            try
            {
                module = ModuleState.From(state);

                if (step is null || content is null || sink is null)
                {
                    return (int)XmipStatus.Invalid;
                }

                Promotion promotion = (Promotion)GCHandle.FromIntPtr((nint)step).Target!;
                Promoted promoted = new(sink);

                try
                {
                    using ContentStream stream = new(content);
                    promotion.Promote(stream, promoted);
                }
                finally
                {
                    promoted.Close();
                }

                return Ok;
            }
            catch (BoundaryException answered)
            {
                return Failed(module, (XmipStatus)answered.Status, answered.Message);
            }
            catch (RefusedException refused)
            {
                return Failed(module, XmipStatus.Malformed, refused.Message);
            }
            catch (Exception defect)
            {
                return Failed(module, XmipStatus.Internal, defect.Message);
            }
        }
    }

    private static int Failed(ModuleState? module, XmipStatus status, string why)
    {
        try
        {
            module?.Fail(why);
        }
        catch (Exception)
        {
            return (int)XmipStatus.Panic;
        }

        return (int)status;
    }
}
