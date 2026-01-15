namespace MGF.Domain.Entities;

public sealed class ProjectRole
{
    public ProjectRole(string roleKey, string displayName, int sortOrder)
    {
        RoleKey = roleKey;
        DisplayName = displayName;
        SortOrder = sortOrder;
    }

    public string RoleKey { get; }
    public string DisplayName { get; }
    public int SortOrder { get; }
}

public sealed class ProjectMember
{
    public ProjectMember(string prjId, string perId, string roleKey, DateTimeOffset assignedAt, DateTimeOffset? releasedAt)
    {
        PrjId = prjId;
        PerId = perId;
        RoleKey = roleKey;
        AssignedAt = assignedAt;
        ReleasedAt = releasedAt;
    }

    public ProjectMember(string prjId, string perId, string roleKey)
        : this(prjId, perId, roleKey, DateTimeOffset.UtcNow, null)
    {
    }

    public string PrjId { get; }
    public string PerId { get; }
    public string RoleKey { get; }
    public DateTimeOffset AssignedAt { get; }
    public DateTimeOffset? ReleasedAt { get; }
}
