using System.Data;
using System.Data.Common;
using Tika.BatchIngestor.Abstractions;

namespace Tika.BatchIngestor.Tests.Fakes;

public class FakeConnectionFactory : IConnectionFactory
{
    private readonly int _delayMs;
    private int _currentConnections;
    private int _maxConcurrent;

    public int MaxConcurrentConnections => _maxConcurrent;

    public FakeConnectionFactory(int delayMs = 0)
    {
        _delayMs = delayMs;
    }

    public async Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _currentConnections);
        
        var current = _currentConnections;
        if (current > _maxConcurrent)
        {
            _maxConcurrent = current;
        }

        if (_delayMs > 0)
        {
            await Task.Delay(_delayMs, cancellationToken);
        }

        var connection = new FakeDbConnection();
        await connection.OpenAsync(cancellationToken);
        
        var wrapper = new DisposableConnectionWrapper(connection, () =>
        {
            Interlocked.Decrement(ref _currentConnections);
        });

        return wrapper;
    }

    private class DisposableConnectionWrapper : DbConnection
    {
        private readonly DbConnection _inner;
        private readonly Action _onDispose;

        public DisposableConnectionWrapper(DbConnection inner, Action onDispose)
        {
            _inner = inner;
            _onDispose = onDispose;
        }

        public override string ConnectionString
        {
            get => _inner.ConnectionString;
            set => _inner.ConnectionString = value!;
        }

        public override string Database => _inner.Database;
        public override string DataSource => _inner.DataSource;
        public override string ServerVersion => _inner.ServerVersion;
        public override ConnectionState State => _inner.State;

        public override void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);
        public override void Close() => _inner.Close();
        public override void Open() => _inner.Open();

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            _inner.BeginTransaction(isolationLevel);

        protected override DbCommand CreateDbCommand() => _inner.CreateCommand();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _onDispose();
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            _onDispose();
            await _inner.DisposeAsync();
            await base.DisposeAsync();
        }
    }
}
