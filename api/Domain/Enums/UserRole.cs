namespace Domain.Enums;

/// <summary>
/// Role-based access control roles. Drives which data and processes a user can reach.
/// Kept deliberately small for the single-org MVP; extend as the access model grows.
/// </summary>
public enum UserRole
{
    Employee = 0,
    Manager = 1,
    Admin = 2,
}
