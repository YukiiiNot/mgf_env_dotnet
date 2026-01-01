namespace MGF.Worker.Tests;

using MGF.Data.Stores.Counters;

public sealed class CounterSqlTests
{
    [Fact]
    public void AllocateProjectCodeQuery_UsesExpectedTables()
    {
        var sql = CounterSql.AllocateProjectCodeQuery;

        Assert.Contains("project_code_counters", sql);
        Assert.Contains("INSERT INTO public.project_code_counters", sql);
        Assert.Contains("UPDATE public.project_code_counters", sql);
        Assert.Contains("SELECT 'MGF' || lpad", sql);
    }

    [Fact]
    public void BuildAllocateInvoiceNumberQuery_UsesExpectedArguments()
    {
        var command = CounterSql.BuildAllocateInvoiceNumberQuery(24);

        Assert.Contains("invoice_number_counters", command.Format);

        var args = command.GetArguments();
        Assert.Equal(24, Convert.ToInt32(args[0]));
        Assert.Equal(24, Convert.ToInt32(args[1]));
    }
}
