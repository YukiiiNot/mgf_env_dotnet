using Microsoft.EntityFrameworkCore;
using MGF.Domain.Entities;
using MGF.Data.Data;

namespace MGF.Data.IntegrationTests;

public sealed class SmokeTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task CanInsertClientPersonProjectAndProjectMember()
    {
        await fixture.ResetAsync();
        await using var db = TestDb.CreateContext();

        var clientId = EntityIds.NewClientId();
        var personId = EntityIds.NewPersonId();
        var projectId = EntityIds.NewProjectId();
        var projectMemberId = EntityIds.NewWithPrefix("prm");

        db.Add(new MGF.Domain.Entities.Client(clientId, "Test Client"));
        db.Add(new MGF.Domain.Entities.Person(personId, "Test", "User", initials: "TU"));

        db.Add(
            new MGF.Domain.Entities.Project(
                projectId: projectId,
                projectCode: "MGF99-0001",
                clientId: clientId,
                name: "Test Project",
                statusKey: "active",
                phaseKey: "planning",
                priorityKey: "normal"
            )
        );

        db.Set<Dictionary<string, object>>("project_members").Add(
            new Dictionary<string, object>
            {
                ["project_member_id"] = projectMemberId,
                ["project_id"] = projectId,
                ["person_id"] = personId,
                ["role_key"] = "producer",
                ["is_active"] = true,
                ["assigned_at"] = DateTimeOffset.UtcNow,
            }
        );

        await db.SaveChangesAsync();

        Assert.Equal(1, await db.Clients.CountAsync());
        Assert.Equal(1, await db.People.CountAsync());
        Assert.Equal(1, await db.Projects.CountAsync());
        Assert.Equal(1, await db.Set<Dictionary<string, object>>("project_members").CountAsync());
    }

    [Fact]
    public async Task ForeignKeysAreEnforced()
    {
        await fixture.ResetAsync();
        await using var db = TestDb.CreateContext();

        // projects.client_id FK -> clients.client_id
        db.Add(
            new MGF.Domain.Entities.Project(
                projectId: EntityIds.NewProjectId(),
                projectCode: "MGF99-0002",
                clientId: "cli_missing",
                name: "Bad Project",
                statusKey: "active",
                phaseKey: "planning",
                priorityKey: "normal"
            )
        );

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ProjectCodeIsUnique()
    {
        await fixture.ResetAsync();
        await using var db = TestDb.CreateContext();

        var clientId = EntityIds.NewClientId();
        db.Add(new MGF.Domain.Entities.Client(clientId, "Client"));
        await db.SaveChangesAsync();

        const string code = "MGF99-0100";

        db.Add(
            new MGF.Domain.Entities.Project(
                projectId: EntityIds.NewProjectId(),
                projectCode: code,
                clientId: clientId,
                name: "Project 1",
                statusKey: "active",
                phaseKey: "planning",
                priorityKey: "normal"
            )
        );
        await db.SaveChangesAsync();

        db.Add(
            new MGF.Domain.Entities.Project(
                projectId: EntityIds.NewProjectId(),
                projectCode: code,
                clientId: clientId,
                name: "Project 2",
                statusKey: "active",
                phaseKey: "planning",
                priorityKey: "normal"
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

        Assert.True(await ExistsAsync(db, "data_profiles", "profile_key", "real"));
        Assert.True(await ExistsAsync(db, "data_profiles", "profile_key", "legacy"));
        Assert.True(await ExistsAsync(db, "client_types", "type_key", "organization"));
        Assert.True(await ExistsAsync(db, "client_statuses", "status_key", "active"));
        Assert.True(await ExistsAsync(db, "person_statuses", "status_key", "active"));
        Assert.True(await ExistsAsync(db, "project_statuses", "status_key", "active"));
        Assert.True(await ExistsAsync(db, "project_phases", "phase_key", "planning"));
        Assert.True(await ExistsAsync(db, "project_priorities", "priority_key", "normal"));
        Assert.True(await ExistsAsync(db, "roles", "role_key", "producer"));
        Assert.True(await ExistsAsync(db, "role_scopes", "scope_key", "project"));
        Assert.True(await ExistsAsync(db, "job_types", "job_type_key", "square.reconcile.payments"));
        Assert.True(await ExistsAsync(db, "job_types", "job_type_key", "square.payment.upsert"));
        Assert.True(await ExistsAsync(db, "job_types", "job_type_key", "square.webhook_event.process"));
        Assert.True(
            await ExistsCompositeAsync(
                db,
                "role_scope_roles",
                new[] { ("scope_key", (object)"project"), ("role_key", (object)"producer") }
            )
        );

        var year2 = (short)(DateTime.UtcNow.Year % 100);
        Assert.True(
            await ExistsCompositeAsync(
                db,
                "project_code_counters",
                new[] { ("prefix", (object)"MGF"), ("year_2", (object)year2) }
            )
        );
        Assert.True(
            await ExistsCompositeAsync(
                db,
                "invoice_number_counters",
                new[] { ("prefix", (object)"MGF"), ("year_2", (object)year2) }
            )
        );
    }

    [Fact]
    public async Task ProjectMembersCheckConstraintIsEnforced()
    {
        await fixture.ResetAsync();
        await using var db = TestDb.CreateContext();

        var clientId = EntityIds.NewClientId();
        var personId = EntityIds.NewPersonId();
        var projectId = EntityIds.NewProjectId();

        db.Add(new MGF.Domain.Entities.Client(clientId, "Client"));
        db.Add(new MGF.Domain.Entities.Person(personId, "Person", "One", initials: "PO"));
        db.Add(
            new MGF.Domain.Entities.Project(
                projectId: projectId,
                projectCode: "MGF99-0200",
                clientId: clientId,
                name: "Project",
                statusKey: "active",
                phaseKey: "planning",
                priorityKey: "normal"
            )
        );

        await db.SaveChangesAsync();

        db.Set<Dictionary<string, object>>("project_members").Add(
            new Dictionary<string, object>
            {
                ["project_member_id"] = EntityIds.NewWithPrefix("prm"),
                ["project_id"] = projectId,
                ["person_id"] = personId,
                ["role_key"] = "producer",
                ["is_active"] = false,
                ["released_at"] = null!,
            }
        );

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task BookingAttendeesUniqueConstraintIsEnforced()
    {
        await fixture.ResetAsync();
        await using var db = TestDb.CreateContext();

        var clientId = EntityIds.NewClientId();
        var personId = EntityIds.NewPersonId();
        var projectId = EntityIds.NewProjectId();
        var bookingId = EntityIds.NewWithPrefix("bkg");

        db.Add(new MGF.Domain.Entities.Client(clientId, "Client"));
        db.Add(new MGF.Domain.Entities.Person(personId, "Booking", "Person", initials: "BP"));
        db.Add(
            new MGF.Domain.Entities.Project(
                projectId: projectId,
                projectCode: "MGF99-0300",
                clientId: clientId,
                name: "Project",
                statusKey: "active",
                phaseKey: "planning",
                priorityKey: "normal"
            )
        );

        await db.SaveChangesAsync();

        db.Set<Dictionary<string, object>>("bookings").Add(
            new Dictionary<string, object>
            {
                ["booking_id"] = bookingId,
                ["project_id"] = projectId,
                ["title"] = "Test Booking",
                ["start_at"] = DateTimeOffset.UtcNow,
                ["end_at"] = DateTimeOffset.UtcNow.AddHours(1),
                ["data_profile"] = "real",
            }
        );

        await db.SaveChangesAsync();

        var attendees = db.Set<Dictionary<string, object>>("booking_attendees");
        attendees.Add(
            new Dictionary<string, object>
            {
                ["booking_attendee_id"] = EntityIds.NewWithPrefix("bka"),
                ["booking_id"] = bookingId,
                ["person_id"] = personId,
                ["role_key"] = "producer",
            }
        );
        await db.SaveChangesAsync();

        attendees.Add(
            new Dictionary<string, object>
            {
                ["booking_attendee_id"] = EntityIds.NewWithPrefix("bka"),
                ["booking_id"] = bookingId,
                ["person_id"] = personId,
                ["role_key"] = "editor",
            }
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
                SET next_seq = next_seq + 1,
                    updated_at = now()
                WHERE prefix = 'MGF'
                  AND year_2 = (EXTRACT(YEAR FROM now())::int % 100)::smallint
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

    private static async Task<bool> ExistsAsync(AppDbContext db, string table, string keyColumn, string keyValue)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = $"SELECT 1 FROM public.{table} WHERE {keyColumn} = @p LIMIT 1;";

        var param = cmd.CreateParameter();
        param.ParameterName = "@p";
        param.Value = keyValue;
        cmd.Parameters.Add(param);

        await db.Database.OpenConnectionAsync();
        try
        {
            var result = await cmd.ExecuteScalarAsync();
            return result is not null;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<bool> ExistsCompositeAsync(AppDbContext db, string table, IReadOnlyList<(string Column, object Value)> keys)
    {
        var where = string.Join(" AND ", keys.Select((k, i) => $"{k.Column} = @p{i}"));
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = $"SELECT 1 FROM public.{table} WHERE {where} LIMIT 1;";

        for (var i = 0; i < keys.Count; i++)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = $"@p{i}";
            param.Value = keys[i].Value;
            cmd.Parameters.Add(param);
        }

        await db.Database.OpenConnectionAsync();
        try
        {
            var result = await cmd.ExecuteScalarAsync();
            return result is not null;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}


