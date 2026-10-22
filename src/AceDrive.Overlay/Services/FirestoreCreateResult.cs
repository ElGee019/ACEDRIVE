namespace AceDrive.Overlay.Services;

/// <summary>Outcome of the create-only write used by global name reservation.</summary>
public enum FirestoreCreateResult
{
    Created,
    AlreadyExists,
    Failed,
}
