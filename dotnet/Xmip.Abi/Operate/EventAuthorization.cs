namespace Xmip.Abi.Operate;

/// <summary>
/// One attempt to subscribe, as the hosting program's policy is asked of it
/// (section 11, <c>XmipEventAuthorizerFn</c>; ADR-0065, amendment 2026-09-26):
/// the accountable identity and what it asks for. The policy answers allow
/// (<see langword="true"/>), deny (<see langword="false"/>) or no opinion
/// (<see langword="null"/>); nothing having an opinion is a refusal.
/// </summary>
/// <param name="Party">The Party's UUID the identity resolved to; empty for
/// none.</param>
/// <param name="Mechanism">How it was recognized.</param>
/// <param name="Value">What it was recognized by.</param>
/// <param name="Scope">The scope the filter reaches.</param>
/// <param name="Type">The Event type asked for; empty for every type.</param>
public sealed record EventAuthorization(
    string Party, string Mechanism, string Value, string Scope, string Type);
