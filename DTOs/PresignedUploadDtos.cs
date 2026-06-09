namespace finalyearproject.DTOs.Audio;

public class PresignedUploadRequestDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public Guid MeetingId { get; set; }
}

public class PresignedUploadResponseDto
{
    public string UploadUrl { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}

public class ConfirmUploadDto
{
    public Guid MeetingId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
}