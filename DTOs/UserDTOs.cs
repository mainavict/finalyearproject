namespace finalyearproject.DTOs;

public class ReadUserDTOs
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }
    
}
public class CreateUserDto
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}


public class UpdateUserDto
{
    public string? FullName { get; set; }

    public string? Email { get; set; }
}