using Microsoft.EntityFrameworkCore;
using MGF.Domain.Entities;
using MGF.Infrastructure.Data;

namespace MGF.Infrastructure.IntegrationTests;

public sealed class SmokeTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task CanInsertClientPersonProjectMembership()
    {
        await fixture.ResetAsync();
        await using var db = TestDb.CreateContext();

        var client = new Client($"cli_{Guid.NewGuid():N}", "Test Client");
        var person = new Person($"per_{Guid.NewGuid():N}", "TT");

        db.Add(client);
        db.Add(person);
        await db.SaveChangesAsync();

        var project = new Project(
            prjId: $"prj_{Guid.NewGuid():N}",
            projectCode: $"MGF_TEST_{Guid.NewGuid():N}",
            cliId: client.CliId,
            name: "Test Project",
            statusKey: "active",
            phaseKey: "planning",
            priorityKey: "normal",
            typeKey: "video_edit",
            pathsRootKey: "local",
            folderRelpath: "test/project",
            dropboxUrl: null,
            archivedAt: null
        );

        db.Add(project);
        await db.SaveChangesAsync();

        db.Add(new ProjectMember(project.PrjId, person.PerId, "producer"));
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.Clients.CountAsync());
        Assert.Equal(1, await db.People.CountAsync());
        Assert.Equal(1, await db.Projects.CountAsync());
        Assert.Equal(1, await db.ProjectMembers.CountAsync());
    }

    [Fact]
    public async Task ForeignKeyEnforcementWorks()
    {
        await fixture.ResetAsync();
        await using var db = TestDb.CreateContext();

        var projectWithMissingClient = new Project(
            prjId: $"prj_{Guid.NewGuid():N}",
            projectCode: $"MGF_TEST_{Guid.NewGuid():N}",
            cliId: "cli_missing",
            name: "Bad Project",
            statusKey: "active",
            phaseKey: "planning",
            priorityKey: "normal",
            typeKey: "video_edit",
            pathsRootKey: "local",
            folderRelpath: "bad/project"
        );

        db.Add(projectWithMissingClient);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        await fixture.ResetAsync();
        await using var db2 = TestDb.CreateContext();

        var client = new Client($"cli_{Guid.NewGuid():N}", "Client");
        db2.Add(client);
        await db2.SaveChangesAsync();

        var project = new Project(
            prjId: $"prj_{Guid.NewGuid():N}",
            projectCode: $"MGF_TEST_{Guid.NewGuid():N}",
            cliId: client.CliId,
            name: "Project",
            statusKey: "active",
            phaseKey: "planning",
            priorityKey: "normal",
            typeKey: "video_edit",
            pathsRootKey: "local",
            folderRelpath: "ok/project"
        );

        db2.Add(project);
        await db2.SaveChangesAsync();

        db2.Add(new ProjectMember(project.PrjId, "per_missing", "producer"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task ProjectCodeUniquenessEnforced()
    {
        await fixture.ResetAsync();
        await using var db = TestDb.CreateContext();

        var client = new Client($"cli_{Guid.NewGuid():N}", "Client");
        db.Add(client);
        await db.SaveChangesAsync();

        var projectCode = $"MGF_TEST_DUP_{Guid.NewGuid():N}";

        db.Add(
            new Project(
                prjId: $"prj_{Guid.NewGuid():N}",
                projectCode: projectCode,
                cliId: client.CliId,
                name: "Project 1",
                statusKey: "active",
                phaseKey: "planning",
                priorityKey: "normal",
                typeKey: "video_edit",
                pathsRootKey: "local",
                folderRelpath: "dup/project1"
            )
        );
        await db.SaveChangesAsync();

        db.Add(
            new Project(
                prjId: $"prj_{Guid.NewGuid():N}",
                projectCode: projectCode,
                cliId: client.CliId,
                name: "Project 2",
                statusKey: "active",
                phaseKey: "planning",
                priorityKey: "normal",
                typeKey: "video_edit",
                pathsRootKey: "local",
                folderRelpath: "dup/project2"
            )
        );

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ProjectCodeCounterIncrements()
    {
        await fixture.ResetAsync();
        await using var db = TestDb.CreateContext();

        var a = await AllocateNextProjectSequenceAsync(db);
        var b = await AllocateNextProjectSequenceAsync(db);

        Assert.Equal(1, a);
        Assert.Equal(2, b);
    }

    [Fact]
    public async Task LookupTablesAreSeeded()
    {
        await fixture.ResetAsync();
        await using var db = TestDb.CreateContext();

        Assert.True(await db.ProjectStatuses.AnyAsync(x => x.StatusKey == "active"));
        Assert.True(await db.ProjectPhases.AnyAsync(x => x.PhaseKey == "planning"));
        Assert.True(await db.ProjectPriorities.AnyAsync(x => x.PriorityKey == "normal"));
        Assert.True(await db.ProjectTypes.AnyAsync(x => x.TypeKey == "video_edit"));
        Assert.True(await db.ProjectRoles.AnyAsync(x => x.RoleKey == "producer"));

        var year = DateTime.UtcNow.Year;
        Assert.True(await db.ProjectCodeCounters.AnyAsync(x => x.Year == year));
    }

    [Fact]
    public async Task ProjectMembersEnforceUniqueActiveMembership()
    {
        await fixture.ResetAsync();
        await using var db = TestDb.CreateContext();

        var client = new Client($"cli_{Guid.NewGuid():N}", "Client");
        var person = new Person($"per_{Guid.NewGuid():N}", "PM");

        db.Add(client);
        db.Add(person);
        await db.SaveChangesAsync();

        var project = new Project(
            prjId: $"prj_{Guid.NewGuid():N}",
            projectCode: $"MGF_TEST_{Guid.NewGuid():N}",
            cliId: client.CliId,
            name: "Project",
            statusKey: "active",
            phaseKey: "planning",
            priorityKey: "normal",
            typeKey: "video_edit",
            pathsRootKey: "local",
            folderRelpath: "members/project"
        );

        db.Add(project);
        await db.SaveChangesAsync();

        db.Add(
            new ProjectMember(
                project.PrjId,
                person.PerId,
                "producer",
                assignedAt: DateTimeOffset.Parse("2025-01-01T00:00:00Z"),
                releasedAt: null
            )
        );
        await db.SaveChangesAsync();

        db.Add(
            new ProjectMember(
                project.PrjId,
                person.PerId,
                "producer",
                assignedAt: DateTimeOffset.Parse("2025-02-01T00:00:00Z"),
                releasedAt: null
            )
        );

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static async Task<int> AllocateNextProjectSequenceAsync(AppDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText =
                """
                UPDATE public.project_code_counters
                SET next_seq = next_seq + 1
                WHERE year = (EXTRACT(YEAR FROM now()))::int
                RETURNING next_seq - 1;
                """;

            var result = await cmd.ExecuteScalarAsync();
            if (result is null)
            {
                throw new InvalidOperationException("project_code_counters row for current year not found.");
            }

            return Convert.ToInt32(result);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}

