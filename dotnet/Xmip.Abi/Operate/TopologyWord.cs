namespace Xmip.Abi.Operate;

/// <summary>
/// What one topology value is called, as <c>observe::topology</c> says it
/// (<c>xmip_operate.h</c> section 8): the <paramref name="Word"/> a
/// publication writes it as — <c>virtual-machine</c>, <c>publish-consume</c>
/// — which a surface may style by, and the <paramref name="Name"/> a person
/// reads it by — <c>virtual machine</c>, <c>Publish → consume</c>.
/// </summary>
public sealed record TopologyWord(string Word, string Name);
