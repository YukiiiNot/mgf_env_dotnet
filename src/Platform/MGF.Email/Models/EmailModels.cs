namespace MGF.Email.Models;

public sealed record DeliveryEmailFileSummary(
    string RelativePath,
    long SizeBytes,
    DateTimeOffset LastWriteTimeUtc);
