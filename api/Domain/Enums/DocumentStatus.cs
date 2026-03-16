namespace Domain.Enums;

/// <summary>
/// Lifecycle of an uploaded document as it moves toward being searchable by RAG.
/// Ingestion/embedding (the transition to Indexed) is implemented in Milestone 3.
/// </summary>
public enum DocumentStatus
{
    Uploaded = 0,
    Processing = 1,
    Indexed = 2,
    Failed = 3,
}
