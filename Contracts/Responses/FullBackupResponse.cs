namespace DoIt.Api.Contracts.Responses;

public sealed record FullBackupResponse(
    string FileName,
    long SizeBytes,
    DateTime CreatedAt,
    string DestinationPath);
