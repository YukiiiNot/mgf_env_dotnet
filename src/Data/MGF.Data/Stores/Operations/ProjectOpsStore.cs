namespace MGF.Data.Stores.Operations;

using Microsoft.Extensions.Configuration;
using Npgsql;
using MGF.Contracts.Abstractions.Operations.Projects;
using MGF.Data.Configuration;

public sealed class ProjectOpsStore : IProjectOpsStore
{
    private readonly string connectionString;

    public ProjectOpsStore(IConfiguration configuration)
    {
        connectionString = DatabaseConnection.ResolveConnectionString(configuration);
    }

    public async Task<ProjectInfo?> GetProjectAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            """
            SELECT project_id, project_code, name, status_key, data_profile, metadata::text, client_id
            FROM public.projects
            WHERE project_id = @project_id;
            """,
            conn
        );

        cmd.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ProjectInfo(
            ProjectId: reader.GetString(0),
            ProjectCode: reader.GetString(1),
            ProjectName: reader.GetString(2),
            StatusKey: reader.GetString(3),
            DataProfile: reader.GetString(4),
            MetadataJson: reader.GetString(5),
            ClientId: reader.GetString(6));
    }

    public async Task<IReadOnlyList<ProjectStorageRootInfo>> GetProjectStorageRootsAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var roots = new List<ProjectStorageRootInfo>();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            """
            SELECT project_storage_root_id,
                   storage_provider_key,
                   root_key,
                   folder_relpath,
                   is_primary,
                   created_at
            FROM public.project_storage_roots
            WHERE project_id = @project_id
            ORDER BY created_at DESC;
            """,
            conn
        );

        cmd.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            roots.Add(new ProjectStorageRootInfo(
                ProjectStorageRootId: reader.GetString(0),
                StorageProviderKey: reader.GetString(1),
                RootKey: reader.GetString(2),
                FolderRelpath: reader.GetString(3),
                IsPrimary: reader.GetBoolean(4),
                CreatedAtUtc: reader.GetFieldValue<DateTimeOffset>(5)));
        }

        return roots;
    }

    public async Task<IReadOnlyList<ProjectListItem>> ListProjectsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var projects = new List<ProjectListItem>();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            """
            SELECT project_id, project_code, name
            FROM public.projects
            ORDER BY created_at DESC
            LIMIT @limit;
            """,
            conn
        );

        cmd.Parameters.AddWithValue("limit", limit);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            projects.Add(new ProjectListItem(
                ProjectId: reader.GetString(0),
                ProjectCode: reader.GetString(1),
                ProjectName: reader.GetString(2)));
        }

        return projects;
    }

    public async Task<int> UpdateProjectStatusAsync(
        string projectId,
        string statusKey,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            """
            UPDATE public.projects
            SET status_key = @status_key,
                updated_at = now()
            WHERE project_id = @project_id;
            """,
            conn
        );

        cmd.Parameters.AddWithValue("project_id", projectId);
        cmd.Parameters.AddWithValue("status_key", statusKey);

        return await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<string?> GetClientNameAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            """
            SELECT display_name
            FROM public.clients
            WHERE client_id = @client_id;
            """,
            conn
        );

        cmd.Parameters.AddWithValue("client_id", clientId);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result?.ToString();
    }

    public async Task<TestProjectInfo?> FindTestProjectAsync(
        string testKey,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            """
            SELECT project_id, project_code, name, client_id
            FROM public.projects
            WHERE metadata->>'test_key' = @test_key
            ORDER BY created_at DESC
            LIMIT 1;
            """,
            conn
        );

        cmd.Parameters.AddWithValue("test_key", testKey);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new TestProjectInfo(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3));
    }

    public async Task<CreatedTestProject> CreateTestProjectAsync(
        CreateTestProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var projectCode = await AllocateProjectCodeAsync(conn, tx, cancellationToken);

        await using (var personCmd = new NpgsqlCommand(
                         """
                         INSERT INTO public.people (person_id, first_name, last_name, initials, status_key, data_profile)
                         VALUES (@person_id, @first_name, @last_name, @initials, 'active', 'real');
                         """,
                         conn,
                         tx
                     ))
        {
            personCmd.Parameters.AddWithValue("person_id", request.PersonId);
            personCmd.Parameters.AddWithValue("first_name", request.EditorFirstName);
            personCmd.Parameters.AddWithValue("last_name", request.EditorLastName);
            personCmd.Parameters.AddWithValue("initials", request.EditorInitials);
            await personCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var clientCmd = new NpgsqlCommand(
                         """
                         INSERT INTO public.clients (client_id, display_name, client_type_key, status_key, data_profile, primary_contact_person_id)
                         VALUES (@client_id, @display_name, 'organization', 'active', 'real', @primary_contact_person_id);
                         """,
                         conn,
                         tx
                     ))
        {
            clientCmd.Parameters.AddWithValue("client_id", request.ClientId);
            clientCmd.Parameters.AddWithValue("display_name", request.ClientName);
            clientCmd.Parameters.AddWithValue("primary_contact_person_id", request.PersonId);
            await clientCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var roleCmd = new NpgsqlCommand(
                         """
                         INSERT INTO public.person_roles (person_id, role_key)
                         VALUES (@person_id, 'editor')
                         ON CONFLICT (person_id, role_key) DO NOTHING;
                         """,
                         conn,
                         tx
                     ))
        {
            roleCmd.Parameters.AddWithValue("person_id", request.PersonId);
            await roleCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var projectCmd = new NpgsqlCommand(
                         """
                         INSERT INTO public.projects (project_id, project_code, client_id, name, status_key, phase_key, data_profile, metadata)
                         VALUES (@project_id, @project_code, @client_id, @name, 'active', 'planning', 'real', @metadata::jsonb);
                         """,
                         conn,
                         tx
                     ))
        {
            projectCmd.Parameters.AddWithValue("project_id", request.ProjectId);
            projectCmd.Parameters.AddWithValue("project_code", projectCode);
            projectCmd.Parameters.AddWithValue("client_id", request.ClientId);
            projectCmd.Parameters.AddWithValue("name", request.ProjectName);
            projectCmd.Parameters.AddWithValue("metadata", request.MetadataJson);
            await projectCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var memberCmd = new NpgsqlCommand(
                         """
                         INSERT INTO public.project_members (project_member_id, project_id, person_id, role_key, assigned_at)
                         VALUES (@project_member_id, @project_id, @person_id, 'editor', now());
                         """,
                         conn,
                         tx
                     ))
        {
            memberCmd.Parameters.AddWithValue("project_member_id", request.ProjectMemberId);
            memberCmd.Parameters.AddWithValue("project_id", request.ProjectId);
            memberCmd.Parameters.AddWithValue("person_id", request.PersonId);
            await memberCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);

        return new CreatedTestProject(
            request.ProjectId,
            projectCode,
            request.ProjectName,
            request.ClientId,
            request.PersonId,
            request.EditorInitials);
    }

    private static async Task<string> AllocateProjectCodeAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            """
            WITH ensured AS (
              INSERT INTO public.project_code_counters(prefix, year_2, next_seq)
              VALUES ('MGF', (EXTRACT(YEAR FROM now())::int % 100)::smallint, 1)
              ON CONFLICT (prefix, year_2) DO NOTHING
            ),
            updated AS (
              UPDATE public.project_code_counters
              SET next_seq = next_seq + 1, updated_at = now()
              WHERE prefix = 'MGF' AND year_2 = (EXTRACT(YEAR FROM now())::int % 100)::smallint
              RETURNING year_2, (next_seq - 1) AS allocated_seq
            )
            SELECT 'MGF' || lpad(year_2::text, 2, '0') || '-' || lpad(allocated_seq::text, 4, '0')
            FROM updated;
            """,
            conn,
            tx
        );

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result?.ToString() ?? throw new InvalidOperationException("Failed to allocate project code.");
    }
}
