namespace MGF.Data.Stores.ProjectWorkflows;

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MGF.Contracts.Abstractions.ProjectWorkflows;
using MGF.Data.Data;
using Npgsql;

public sealed class ProjectWorkflowLockStore : IProjectWorkflowLock
{
    private readonly string connectionString;

    public ProjectWorkflowLockStore(AppDbContext db)
    {
        connectionString = db.Database.GetDbConnection().ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Database connection string is required for project workflow locks.");
        }
    }

    public async Task<IProjectWorkflowLease?> TryAcquireAsync(
        string projectId,
        string workflowKind,
        string holderId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new ArgumentException("ProjectId is required.", nameof(projectId));
        }

        if (string.IsNullOrWhiteSpace(workflowKind))
        {
            throw new ArgumentException("WorkflowKind is required.", nameof(workflowKind));
        }

        var resolvedHolderId = ProjectWorkflowLockHolder.Resolve(holderId);
        var lockKey = ProjectWorkflowLockKey.Build(projectId, workflowKind);
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ProjectWorkflowLockSql.TryAcquire;
            command.Parameters.AddWithValue("lock_id", lockKey);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is bool acquired && acquired)
            {
                return new ProjectWorkflowLease(connection, projectId, workflowKind, resolvedHolderId, lockKey);
            }
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        await connection.DisposeAsync();
        return null;
    }

    private sealed class ProjectWorkflowLease : IProjectWorkflowLease
    {
        private readonly NpgsqlConnection connection;
        private readonly long lockKey;
        private int disposed;

        public ProjectWorkflowLease(
            NpgsqlConnection connection,
            string projectId,
            string workflowKind,
            string holderId,
            long lockKey)
        {
            this.connection = connection;
            this.lockKey = lockKey;
            ProjectId = projectId;
            WorkflowKind = workflowKind;
            HolderId = holderId;
        }

        public string ProjectId { get; }
        public string WorkflowKind { get; }
        public string HolderId { get; }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = ProjectWorkflowLockSql.Release;
                command.Parameters.AddWithValue("lock_id", lockKey);
                await command.ExecuteScalarAsync();
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}

internal static class ProjectWorkflowLockSql
{
    internal const string TryAcquire = "SELECT pg_try_advisory_lock(@lock_id);";
    internal const string Release = "SELECT pg_advisory_unlock(@lock_id);";
}

internal static class ProjectWorkflowLockHolder
{
    internal static string Resolve(string holderId)
    {
        return string.IsNullOrWhiteSpace(holderId) ? "unknown" : holderId;
    }
}

internal static class ProjectWorkflowLockKey
{
    internal static long Build(string projectId, string workflowKind)
    {
        var input = $"{projectId}|{workflowKind}";
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return BinaryPrimitives.ReadInt64LittleEndian(hash);
    }
}
