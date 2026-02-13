using BombasticIFC.Domain.Enums;

namespace BombasticIFC.Domain.Entities;

/// <summary>
/// Represents a user in the system
/// </summary>
public class User : BaseEntity
{
    public string Username { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    
    // Relationships
    public ICollection<IfcModel> Models { get; private set; }

    private User() 
    {
        Username = string.Empty;
        Email = string.Empty;
        PasswordHash = string.Empty;
        Models = new List<IfcModel>();
    }

    public static User Create(string username, string email, string passwordHash, UserRole role = UserRole.User)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be empty", nameof(username));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty", nameof(email));

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash cannot be empty", nameof(passwordHash));

        return new User
        {
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            Role = role,
            IsActive = true
        };
    }

    public void UpdateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty", nameof(email));

        Email = email;
        MarkAsUpdated();
    }

    public void UpdatePassword(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash cannot be empty", nameof(passwordHash));

        PasswordHash = passwordHash;
        MarkAsUpdated();
    }

    public void UpdateRole(UserRole role)
    {
        Role = role;
        MarkAsUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkAsUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        MarkAsUpdated();
    }
}
