namespace MGF.Domain.Entities;

public abstract class EntityBase
{
    protected EntityBase(DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }
}
