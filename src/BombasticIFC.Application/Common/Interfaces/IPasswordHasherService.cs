namespace BombasticIFC.Application.Common.Interfaces;

/// <summary>
/// Service for hashing and verifying passwords
/// </summary>
public interface IPasswordHasherService
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}
