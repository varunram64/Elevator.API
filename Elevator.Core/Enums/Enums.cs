namespace Elevator.Core.Enums;

public enum DownloadStatus
{
    Queued,
    InProgress,
    Paused,
    Completed,
    Failed,
    Cancelled
}

public enum DownloadType
{
    File,
    Report,
    DataExport,
    ExternalUrl
}

public enum FileFormat
{
    Unknown,
    Pdf,
    Csv,
    Excel,
    Json,
    Zip,
    Image,
    Binary
}

public enum StorageProvider
{
    LocalDisk,
    AzureBlob,
    AwsS3,
    Database
}
