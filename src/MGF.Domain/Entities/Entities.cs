namespace MGF.Domain.Entities;

public sealed class Project
{
    public Project(string projectId, string clientId)
    {
        // TODO: enforce invariants (format, non-empty, relationships)
        ProjectId = projectId;
        ClientId = clientId;
    }

    public string ProjectId { get; }
    public string ClientId { get; }
}

public sealed class Client
{
    public Client(string clientId, string name)
    {
        // TODO: enforce invariants (unique slug/name, non-empty)
        ClientId = clientId;
        Name = name;
    }

    public string ClientId { get; }
    public string Name { get; }
}

public sealed class Person
{
    public Person(string personId, string initials)
    {
        // TODO: enforce invariants (initials uniqueness, role requirements)
        PersonId = personId;
        Initials = initials;
    }

    public string PersonId { get; }
    public string Initials { get; }
}
