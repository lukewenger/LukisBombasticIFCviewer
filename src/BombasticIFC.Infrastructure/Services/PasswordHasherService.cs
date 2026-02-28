using BombasticIFC.Application.Common.Interfaces;

namespace BombasticIFC.Infrastructure.Services;

/// <summary>
/// BCrypt-based password hashing service
/// </summary>
public class PasswordHasherService : IPasswordHasherService
{
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    public bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
