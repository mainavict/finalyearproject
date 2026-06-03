using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace finalyearproject.Models;

public class User
{
    [Key]

public Guid UserId { get; set; } =  Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public  string PasswordHash = String.Empty;
    public bool IsDeleted { get; set; } = false;

    public ICollection<Meeting> Meetings { get; set; }
}