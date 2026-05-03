namespace finalyearproject.Services.Interfaces;

public interface Iminutes
{
    Task<string> GenerateMinutesAsync( Guid transcriptId);
}