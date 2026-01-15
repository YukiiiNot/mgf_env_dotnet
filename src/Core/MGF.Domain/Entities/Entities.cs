namespace MGF.Domain.Entities;

using System.Text.Json;

public sealed class Project : EntityBase
{
    public Project(
        string projectId,
        string projectCode,
        string clientId,
        string name,
        string statusKey,
        string phaseKey,
        string? priorityKey,
        DateOnly? dueDate,
        DateTimeOffset? archivedAt,
        string dataProfile,
        string? currentInvoiceId,
        JsonElement metadata,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt
    )
        : base(createdAt, updatedAt)
    {
        ProjectId = projectId;
        ProjectCode = projectCode;
        ClientId = clientId;
        Name = name;
        StatusKey = statusKey;
        PhaseKey = phaseKey;
        PriorityKey = priorityKey;
        DueDate = dueDate;
        ArchivedAt = archivedAt;
        DataProfile = dataProfile;
        CurrentInvoiceId = currentInvoiceId;
        Metadata = metadata;
    }

    public Project(
        string projectId,
        string projectCode,
        string clientId,
        string name,
        string statusKey,
        string phaseKey,
        string dataProfile = "real",
        string? priorityKey = null,
        DateOnly? dueDate = null,
        DateTimeOffset? archivedAt = null,
        string? currentInvoiceId = null,
        JsonElement? metadata = null
    )
        : this(
            projectId,
            projectCode,
            clientId,
            name,
            statusKey,
            phaseKey,
            priorityKey,
            dueDate,
            archivedAt,
            dataProfile,
            currentInvoiceId,
            metadata ?? EmptyMetadata(),
            DateTimeOffset.UtcNow,
            null
        )
    {
    }

    public string ProjectId { get; }
    public string ProjectCode { get; }
    public string ClientId { get; }
    public string Name { get; }
    public string StatusKey { get; }
    public string PhaseKey { get; }
    public string? PriorityKey { get; }
    public DateOnly? DueDate { get; }
    public DateTimeOffset? ArchivedAt { get; }
    public string DataProfile { get; }
    public string? CurrentInvoiceId { get; }
    public JsonElement Metadata { get; }

    private static JsonElement EmptyMetadata()
    {
        return JsonDocument.Parse("{}").RootElement.Clone();
    }
}

public sealed class Client : EntityBase
{
    public Client(
        string clientId,
        string displayName,
        string clientTypeKey,
        string statusKey,
        string? primaryContactPersonId,
        string? accountOwnerPersonId,
        string? notes,
        string dataProfile,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt
    )
        : base(createdAt, updatedAt)
    {
        ClientId = clientId;
        DisplayName = displayName;
        ClientTypeKey = clientTypeKey;
        StatusKey = statusKey;
        PrimaryContactPersonId = primaryContactPersonId;
        AccountOwnerPersonId = accountOwnerPersonId;
        Notes = notes;
        DataProfile = dataProfile;
    }

    public Client(string clientId, string displayName)
        : this(
            clientId,
            displayName,
            clientTypeKey: "organization",
            statusKey: "active",
            primaryContactPersonId: null,
            accountOwnerPersonId: null,
            notes: null,
            dataProfile: "real",
            createdAt: DateTimeOffset.UtcNow,
            updatedAt: null
        )
    {
    }

    public string ClientId { get; }
    public string DisplayName { get; }
    public string ClientTypeKey { get; }
    public string StatusKey { get; }
    public string? PrimaryContactPersonId { get; }
    public string? AccountOwnerPersonId { get; }
    public string? Notes { get; }
    public string DataProfile { get; }
}

public sealed class Person : EntityBase
{
    public Person(
        string personId,
        string firstName,
        string lastName,
        string? displayName,
        string? initials,
        string statusKey,
        string? timezone,
        string? defaultHostKey,
        string? notes,
        string dataProfile,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt
    )
        : base(createdAt, updatedAt)
    {
        PersonId = personId;
        FirstName = firstName;
        LastName = lastName;
        DisplayName = displayName;
        Initials = initials;
        StatusKey = statusKey;
        Timezone = timezone;
        DefaultHostKey = defaultHostKey;
        Notes = notes;
        DataProfile = dataProfile;
    }

    public Person(string personId, string firstName, string lastName, string? initials = null)
        : this(
            personId,
            firstName,
            lastName,
            displayName: null,
            initials: initials,
            statusKey: "active",
            timezone: null,
            defaultHostKey: null,
            notes: null,
            dataProfile: "real",
            createdAt: DateTimeOffset.UtcNow,
            updatedAt: null
        )
    {
    }

    public string PersonId { get; }
    public string FirstName { get; }
    public string LastName { get; }
    public string? DisplayName { get; }
    public string? Initials { get; }
    public string StatusKey { get; }
    public string? Timezone { get; }
    public string? DefaultHostKey { get; }
    public string? Notes { get; }
    public string DataProfile { get; }
}
