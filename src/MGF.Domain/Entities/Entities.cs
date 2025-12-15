namespace MGF.Domain.Entities;

public sealed class Project : EntityBase
{
    public Project(
        string prjId,
        string projectCode,
        string cliId,
        string name,
        string statusKey,
        string phaseKey,
        string? priorityKey,
        string? typeKey,
        string pathsRootKey,
        string folderRelpath,
        string? dropboxUrl,
        DateTimeOffset? archivedAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt
    )
        : base(createdAt, updatedAt)
    {
        PrjId = prjId;
        ProjectCode = projectCode;
        CliId = cliId;
        Name = name;
        StatusKey = statusKey;
        PhaseKey = phaseKey;
        PriorityKey = priorityKey;
        TypeKey = typeKey;
        PathsRootKey = pathsRootKey;
        FolderRelpath = folderRelpath;
        DropboxUrl = dropboxUrl;
        ArchivedAt = archivedAt;
    }

    public Project(
        string prjId,
        string projectCode,
        string cliId,
        string name,
        string statusKey,
        string phaseKey,
        string pathsRootKey,
        string folderRelpath,
        string? priorityKey = null,
        string? typeKey = null,
        string? dropboxUrl = null,
        DateTimeOffset? archivedAt = null
    )
        : this(
            prjId,
            projectCode,
            cliId,
            name,
            statusKey,
            phaseKey,
            priorityKey,
            typeKey,
            pathsRootKey,
            folderRelpath,
            dropboxUrl,
            archivedAt,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow
        )
    {
    }

    public string PrjId { get; }
    public string ProjectCode { get; }
    public string CliId { get; }
    public string Name { get; }
    public string StatusKey { get; }
    public string PhaseKey { get; }
    public string? PriorityKey { get; }
    public string? TypeKey { get; }
    public string PathsRootKey { get; }
    public string FolderRelpath { get; }
    public string? DropboxUrl { get; }
    public DateTimeOffset? ArchivedAt { get; }
}

public sealed class Client : EntityBase
{
    public Client(string cliId, string name, DateTimeOffset createdAt, DateTimeOffset updatedAt)
        : base(createdAt, updatedAt)
    {
        CliId = cliId;
        Name = name;
    }

    public Client(string cliId, string name)
        : this(cliId, name, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
    {
    }

    public string CliId { get; }
    public string Name { get; }
}

public sealed class Person : EntityBase
{
    public Person(string perId, string initials, DateTimeOffset createdAt, DateTimeOffset updatedAt)
        : base(createdAt, updatedAt)
    {
        PerId = perId;
        Initials = initials;
    }

    public Person(string perId, string initials)
        : this(perId, initials, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
    {
    }

    public string PerId { get; }
    public string Initials { get; }
}
