using NpgsqlTypes;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace finalyearproject.Models;

public class Minutes
{
    
    [Key]
    public Guid Id { get; set; } =  Guid.NewGuid();
    
    [ForeignKey("meetingId")]
    public Guid MeetingId { get; set; }
    
    public string Status { get; set; } = "pending_validation";
    public int Version { get; set; } = 1;
    
    [MaxLength(100000000)]
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