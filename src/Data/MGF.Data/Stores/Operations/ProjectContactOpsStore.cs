namespace MGF.Data.Stores.Operations;

using Microsoft.Extensions.Configuration;
using Npgsql;
using MGF.Contracts.Abstractions.Operations.Projects;
using MGF.Data.Configuration;

public sealed class ProjectContactOpsStore : IProjectContactOpsStore
{
    private readonly string connectionString;

    public ProjectContactOpsStore(IConfiguration configuration)
    {
        connectionString = DatabaseConnection.ResolveConnectionString(configuration);
    }

    public async Task<PrimaryContactEmailResult?> EnsurePrimaryContactEmailAsync(
        string clientId,
        string email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new ArgumentException("clientId is required.", nameof(clientId));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("email is required.", nameof(email));
        }

        var trimmedEmail = email.Trim();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        string? personId;
        await using (var cmd = new NpgsqlCommand(
                         """
                         SELECT primary_contact_person_id
                         FROM public.clients
                         WHERE client_id = @client_id;
                         """,
                         conn
                     ))
        {
            cmd.Parameters.AddWithValue("client_id", clientId);
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            personId = result?.ToString();
        }

        if (string.IsNullOrWhiteSpace(personId))
        {
            return null;
        }

        string? existingEmail = null;
        await using (var cmd = new NpgsqlCommand(
                         """
                         SELECT email
                         FROM public.person_contacts
                         WHERE person_id = @person_id;
                         """,
                         conn
                     ))
        {
            cmd.Parameters.AddWithValue("person_id", personId);
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            existingEmail = result?.ToString();
        }

        var inserted = false;
        var updated = false;

        if (string.IsNullOrWhiteSpace(existingEmail))
        {
            await using var insertCmd = new NpgsqlCommand(
                """
                INSERT INTO public.person_contacts (person_id, email, updated_at)
                VALUES (@person_id, @email, now());
                """,
                conn);
            insertCmd.Parameters.AddWithValue("person_id", personId);
            insertCmd.Parameters.AddWithValue("email", trimmedEmail);
            await insertCmd.ExecuteNonQueryAsync(cancellationToken);
            inserted = true;
        }
        else if (!string.Equals(existingEmail, trimmedEmail, StringComparison.OrdinalIgnoreCase))
        {
            await using var updateCmd = new NpgsqlCommand(
                """
                UPDATE public.person_contacts
                SET email = @email,
                    updated_at = now()
                WHERE person_id = @person_id;
                """,
                conn);
            updateCmd.Parameters.AddWithValue("person_id", personId);
            updateCmd.Parameters.AddWithValue("email", trimmedEmail);
            await updateCmd.ExecuteNonQueryAsync(cancellationToken);
            updated = true;
        }

        return new PrimaryContactEmailResult(
            ClientId: clientId,
            PersonId: personId,
            Email: trimmedEmail,
            Inserted: inserted,
            Updated: updated);
    }
}
