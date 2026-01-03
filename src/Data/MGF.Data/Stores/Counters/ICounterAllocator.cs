namespace MGF.Data.Stores.Counters;

public interface ICounterAllocator
{
    Task<string> AllocateProjectCodeAsync(CancellationToken cancellationToken = default);

    Task<string> AllocateInvoiceNumberAsync(short year2, CancellationToken cancellationToken = default);
}
