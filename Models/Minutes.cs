using NpgsqlTypes;

namespace finalyearproject.Models;

public class Minutes
{
    public Guid Id { get; set; }
    public Guid MeetingId { get; set; }
    public string Status { get; set; } = "pending_validation";
    public int Version { get; set; } = 1;
    public string DraftJson { get; set; } = string.Empty; // JSON
    public string? ApprovedHtml { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? LlmModel { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public NpgsqlTsVector? SearchVector { get; set; } // auto-generated
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Meeting Meeting { get; set; } = null!;
    public User? Approver { get; set; }
    public ICollection<MinutesVersion> Versions { get; set; } = [];
}