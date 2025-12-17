using System.Data;
using System.Data.Common;

namespace Tika.BatchIngestor.Tests.Fakes;

public class FakeDbConnection : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;

    public override string ConnectionString { get; set; } = string.Empty;
    public override string Database => "FakeDatabase";
    public override string DataSource => "FakeDataSource";
    public override string ServerVersion => "1.0";
    public override ConnectionState State => _state;

    public override void ChangeDatabase(string databaseName) { }

    public override void Close()
    {
        _state = ConnectionState.Closed;
    }

    public override void Open()
    {
        _state = ConnectionState.Open;
    }

    public override Task OpenAsync(CancellationToken cancellationToken)
    {
        _state = ConnectionState.Open;
        return Task.CompletedTask;
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        return new FakeDbTransaction(this);
    }

    protected override DbCommand CreateDbCommand()
    {
        return new FakeDbCommand(this);
    }
}

public class FakeDbTransaction : DbTransaction
{
    private readonly FakeDbConnection _connection;

    public FakeDbTransaction(FakeDbConnection connection)
    {
        _connection = connection;
    }

    public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
    protected override DbConnection DbConnection => _connection;

    public override void Commit() { }
    public override void Rollback() { }
}
