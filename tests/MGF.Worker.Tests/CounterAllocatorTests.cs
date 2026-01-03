namespace MGF.Worker.Tests;

using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using MGF.Data.Data;
using MGF.Data.Stores.Counters;

public sealed class CounterAllocatorTests
{
    [Fact]
    public async Task AllocateProjectCodeAsync_UsesExecuteScalar()
    {
        var connection = new FakeDbConnection("MGF25-0001");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=mgf_test;Username=mgf;Password=mgf")
            .Options;

        await using var db = new AppDbContext(options);
        db.Database.SetDbConnection(connection);

        var allocator = new CounterAllocator(db);
        var result = await allocator.AllocateProjectCodeAsync();

        Assert.Equal("MGF25-0001", result);
        Assert.Equal(1, connection.ExecuteScalarCalls);
        Assert.Equal(0, connection.ExecuteReaderCalls);
        Assert.Equal(CounterSql.AllocateProjectCodeQuery, connection.LastCommandText);
    }

    private sealed class FakeDbConnection : DbConnection
    {
        private readonly string scalarResult;
        private ConnectionState state = ConnectionState.Closed;

        public FakeDbConnection(string scalarResult)
        {
            this.scalarResult = scalarResult;
        }

        public int ExecuteScalarCalls { get; private set; }

        public int ExecuteReaderCalls { get; private set; }

        public string? LastCommandText { get; private set; }

        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;

        public override string Database => "fake";

        public override string DataSource => "fake";

        public override string ServerVersion => "0";

        public override ConnectionState State => state;

        public override void ChangeDatabase(string databaseName)
        {
        }

        public override void Close()
        {
            state = ConnectionState.Closed;
        }

        public override void Open()
        {
            state = ConnectionState.Open;
        }

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            state = ConnectionState.Open;
            return Task.CompletedTask;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            throw new NotSupportedException();
        }

        protected override DbCommand CreateDbCommand()
        {
            return new FakeDbCommand(this);
        }

        private sealed class FakeDbCommand : DbCommand
        {
            private readonly FakeDbConnection connection;

            public FakeDbCommand(FakeDbConnection connection)
            {
                this.connection = connection;
            }

            [AllowNull]
            public override string CommandText { get; set; } = string.Empty;

            public override int CommandTimeout { get; set; }

            public override CommandType CommandType { get; set; } = CommandType.Text;

            public override bool DesignTimeVisible { get; set; }

            public override UpdateRowSource UpdatedRowSource { get; set; }

            [AllowNull]
            protected override DbConnection DbConnection
            {
                get => connection;
                set => throw new NotSupportedException();
            }

            protected override DbParameterCollection DbParameterCollection { get; } = new FakeDbParameterCollection();

            protected override DbTransaction? DbTransaction { get; set; }

            public override void Cancel()
            {
            }

            public override int ExecuteNonQuery()
            {
                throw new NotSupportedException();
            }

            public override object? ExecuteScalar()
            {
                connection.ExecuteScalarCalls++;
                connection.LastCommandText = CommandText;
                return connection.scalarResult;
            }

            public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(ExecuteScalar());
            }

            public override void Prepare()
            {
            }

            protected override DbParameter CreateDbParameter()
            {
                throw new NotSupportedException();
            }

            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
            {
                connection.ExecuteReaderCalls++;
                throw new InvalidOperationException("ExecuteReader should not be used for counter allocation.");
            }
        }

        private sealed class FakeDbParameterCollection : DbParameterCollection
        {
            private readonly List<DbParameter> items = new();

            public override int Count => items.Count;

            public override object SyncRoot { get; } = new();

            public override int Add(object value)
            {
                items.Add((DbParameter)value);
                return items.Count - 1;
            }

            public override void AddRange(Array values)
            {
                foreach (var value in values)
                {
                    Add(value);
                }
            }

            public override void Clear()
            {
                items.Clear();
            }

            public override bool Contains(object value) => items.Contains((DbParameter)value);

            public override bool Contains(string value) => items.Exists(item => item.ParameterName == value);

            public override void CopyTo(Array array, int index)
            {
                items.ToArray().CopyTo(array, index);
            }

            public override System.Collections.IEnumerator GetEnumerator() => items.GetEnumerator();

            public override int IndexOf(object value) => items.IndexOf((DbParameter)value);

            public override int IndexOf(string parameterName) => items.FindIndex(item => item.ParameterName == parameterName);

            public override void Insert(int index, object value)
            {
                items.Insert(index, (DbParameter)value);
            }

            public override void Remove(object value)
            {
                items.Remove((DbParameter)value);
            }

            public override void RemoveAt(int index)
            {
                items.RemoveAt(index);
            }

            public override void RemoveAt(string parameterName)
            {
                var index = IndexOf(parameterName);
                if (index >= 0)
                {
                    RemoveAt(index);
                }
            }

            protected override DbParameter GetParameter(int index) => items[index];

            protected override DbParameter GetParameter(string parameterName)
                => items.First(item => item.ParameterName == parameterName);

            protected override void SetParameter(int index, DbParameter value)
            {
                items[index] = value;
            }

            protected override void SetParameter(string parameterName, DbParameter value)
            {
                var index = IndexOf(parameterName);
                if (index >= 0)
                {
                    items[index] = value;
                    return;
                }

                items.Add(value);
            }
        }
    }
}
