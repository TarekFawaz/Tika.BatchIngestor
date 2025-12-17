namespace Tika.BatchIngestor.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when a batch ingestion operation fails.
/// </summary>
public class BatchIngestException : Exception
{
    /// <summary>
    /// The batch number that failed (1-based).
    /// </summary>
    public int BatchNumber { get; }

    /// <summary>
    /// Number of rows successfully processed before the failure.
    /// </summary>
    public long RowsProcessedBeforeFailure { get; }

    /// <summary>
    /// Initializes a new instance of BatchIngestException.
    /// </summary>
    public BatchIngestException(
        string message,
        int batchNumber,
        long rowsProcessedBeforeFailure,
        Exception? innerException = null)
        : base(message, innerException)
    {
        BatchNumber = batchNumber;
        RowsProcessedBeforeFailure = rowsProcessedBeforeFailure;
    }
}
