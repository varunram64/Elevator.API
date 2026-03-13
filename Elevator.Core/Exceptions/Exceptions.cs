namespace Elevator.Core.Exceptions;

public class DownloadJobNotFoundException(Guid jobId)
    : Exception($"Download job '{jobId}' was not found.");

public class DownloadJobAlreadyExistsException(Guid jobId)
    : Exception($"Download job '{jobId}' already exists.");

public class InvalidDownloadStateException(string message) : Exception(message);

public class StorageProviderException(string message, Exception? inner = null)
    : Exception(message, inner);

public class DownloadQueueException(string message, Exception? inner = null)
    : Exception(message, inner);
