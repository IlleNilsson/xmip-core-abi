namespace Xmip.Abi.Operate;

/// <summary>One upgrade script: its file name,
/// <c>04-&lt;domain&gt;-upgrade-&lt;from&gt;-&lt;to&gt;.sql</c>, and its text, for
/// IT to run as the schema's owner (section 20).</summary>
public sealed record DatabaseScript(string Name, string Text);
