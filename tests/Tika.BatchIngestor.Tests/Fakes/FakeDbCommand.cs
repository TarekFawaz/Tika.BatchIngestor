using System.Data;
using System.Data.Common;

namespace Tika.BatchIngestor.Tests.Fakes;

public class FakeDbCommand : DbCommand
{
    private readonly FakeDbConnection _connection;
    private string _commandText = string.Empty;

    public FakeDbCommand(FakeDbConnection connection)
    {
        _connection = connection;
        DbParameterCollection = new FakeDbParameterCollection();
    }

    public override string CommandText
    {
        get => _commandText;
        set => _commandText = value ?? string.Empty;
    }
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get; set; }
    protected override DbParameterCollection DbParameterCollection { get; }
    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel() { }
    public override void Prepare() { }

    public override int ExecuteNonQuery()
    {
        return EstimateRowCount();
    }

    public override async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken);
        return EstimateRowCount();
    }

    public override object? ExecuteScalar()
    {
        return null;
    }

    protected override DbParameter CreateDbParameter()
    {
        return new FakeDbParameter();
    }

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        throw new NotImplementedException();
    }

    private int EstimateRowCount()
    {
        var valuesClauses = CommandText.Split(new[] { "VALUES" }, StringSplitOptions.None).Length - 1;
        return Math.Max(1, valuesClauses);
    }
}

public class FakeDbParameterCollection : DbParameterCollection
{
    private readonly List<object> _parameters = new();

    public override int Count => _parameters.Count;
    public override object SyncRoot => _parameters;

    public override int Add(object value)
    {
        _parameters.Add(value);
        return _parameters.Count - 1;
    }

    public override void AddRange(Array values)
    {
        foreach (var value in values)
            _parameters.Add(value);
    }

    public override void Clear() => _parameters.Clear();
    public override bool Contains(object value) => _parameters.Contains(value);
    public override bool Contains(string value) => false;
    public override void CopyTo(Array array, int index) => _parameters.CopyTo((object[])array, index);
    public override System.Collections.IEnumerator GetEnumerator() => _parameters.GetEnumerator();
    public override int IndexOf(object value) => _parameters.IndexOf(value);
    public override int IndexOf(string parameterName) => -1;
    public override void Insert(int index, object value) => _parameters.Insert(index, value);
    public override void Remove(object value) => _parameters.Remove(value);
    public override void RemoveAt(int index) => _parameters.RemoveAt(index);
    public override void RemoveAt(string parameterName) { }
    protected override DbParameter GetParameter(int index) => (DbParameter)_parameters[index];
    protected override DbParameter GetParameter(string parameterName) => throw new NotImplementedException();
    protected override void SetParameter(int index, DbParameter value) => _parameters[index] = value;
    protected override void SetParameter(string parameterName, DbParameter value) { }
}

public class FakeDbParameter : DbParameter
{
    private string _parameterName = string.Empty;
    private string _sourceColumn = string.Empty;

    public override DbType DbType { get; set; }
    public override ParameterDirection Direction { get; set; }
    public override bool IsNullable { get; set; }

    public override string ParameterName
    {
        get => _parameterName;
        set => _parameterName = value ?? string.Empty;
    }

    public override int Size { get; set; }

    public override string SourceColumn
    {
        get => _sourceColumn;
        set => _sourceColumn = value ?? string.Empty;
    }

    public override bool SourceColumnNullMapping { get; set; }
    public override object? Value { get; set; }

    public override void ResetDbType() { }
}
