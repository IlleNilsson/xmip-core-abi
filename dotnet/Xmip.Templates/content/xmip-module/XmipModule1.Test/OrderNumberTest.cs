using System.Text;
using Xmip.Module;

namespace XmipModule1.Test;

public sealed class OrderNumberTest
{
    [Fact]
    public void TheOrdersNumberIsReadFromItsMember()
    {
        using MemoryStream order = Content("""{"number":"PO-4711","lines":3}""");

        Assert.Equal("PO-4711", OrderNumber.Read(order, "number"));
    }

    [Fact]
    public void AnOrderWithoutTheMemberHasNoNumber()
    {
        Assert.Null(OrderNumber.Read(Content("""{"reference":"PO-4711"}"""), "number"));
    }

    [Fact]
    public void ContentThatIsNotJsonIsRefused()
    {
        Assert.Throws<RefusedException>(() => OrderNumber.Read(Content("not json"), "number"));
    }

    [Fact]
    public void AStepWithAnEmptyMemberIsRefused()
    {
        StepSettings settings = StepSettings.Read("receive", """{"member":""}""");

        Assert.Throws<RefusedException>(() => new OrderNumber().Open(settings));
    }

    private static MemoryStream Content(string json)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }
}
