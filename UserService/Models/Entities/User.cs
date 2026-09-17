using System.ComponentModel.DataAnnotations;

namespace UserService.Models.Entities;

public enum Role
{
    Patron,
    Librarian
}

public enum MembershipStatus
{
    Active,
    Suspended
}

public class User
{
    public Guid UserId { get; set; }

    [Required, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    public Role Role { get; set; } = Role.Patron;

    public MembershipStatus MembershipStatus { get; set; } = MembershipStatus.Active;

    public DateTime? MemberSince { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}