namespace BombasticIFC.Application.DTOs;

/// <summary>
/// DTO for the current user profile
/// </summary>
public record UserProfileDto(
    Guid Id,
    string Username,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt
);
