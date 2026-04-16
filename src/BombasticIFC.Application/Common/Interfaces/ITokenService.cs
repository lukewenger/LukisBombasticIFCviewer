namespace BombasticIFC.Application.Common.Interfaces;

/// <summary>
/// Service for generating and validating JWT tokens
/// </summary>
public interface ITokenService
{
    string GenerateAccessToken(Guid userId, string username, string email, string role);
    string GenerateRefreshToken();
    string HashToken(string token);
}
