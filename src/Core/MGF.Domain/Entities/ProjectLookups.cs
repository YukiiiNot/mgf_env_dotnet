namespace MGF.Domain.Entities;

public sealed class ProjectStatus
{
    public ProjectStatus(string statusKey, string displayName, int sortOrder)
    {
        StatusKey = statusKey;
        DisplayName = displayName;
        SortOrder = sortOrder;
    }

    public string StatusKey { get; }
    public string DisplayName { get; }
    public int SortOrder { get; }
}

public sealed class ProjectPhase
{
    public ProjectPhase(string phaseKey, string displayName, int sortOrder)
    {
        PhaseKey = phaseKey;
        DisplayName = displayName;
        SortOrder = sortOrder;
    }

    public string PhaseKey { get; }
    public string DisplayName { get; }
    public int SortOrder { get; }
}

public sealed class ProjectPriority
{
    public ProjectPriority(string priorityKey, string displayName, int sortOrder)
    {
        PriorityKey = priorityKey;
        DisplayName = displayName;
        SortOrder = sortOrder;
    }

    public string PriorityKey { get; }
    public string DisplayName { get; }
    public int SortOrder { get; }
}

public sealed class ProjectType
{
    public ProjectType(string typeKey, string displayName, int sortOrder)
    {
        TypeKey = typeKey;
        DisplayName = displayName;
        SortOrder = sortOrder;
    }

    public string TypeKey { get; }
    public string DisplayName { get; }
    public int SortOrder { get; }
}

public sealed class ProjectCodeCounter
{
    public ProjectCodeCounter(int year, int nextSeq)
    {
        Year = year;
        NextSeq = nextSeq;
    }

    public int Year { get; }
    public int NextSeq { get; }
}
