namespace BombasticIFC.Application.DTOs;

/// <summary>
/// Response DTO for authentication operations
/// </summary>
public record AuthResponseDto(
    Guid UserId,
    string Username,
    string Email,
    string Role,
    string AccessToken,
    string RefreshToken
);
