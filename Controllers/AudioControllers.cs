using Microsoft.AspNetCore.Mvc;
using finalyearproject.Services.Interfaces;
using finalyearproject.Wrappers;
using finalyearproject.Models;
using finalyearproject.Extensions;
using finalyearproject.DTOs.Audio;
using Microsoft.AspNetCore.Authorization;

namespace finalyearproject.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AudioControllers : ControllerBase
{
    private readonly IAudio _audio;
    private readonly IStorage _storage;

    public AudioControllers(IAudio audio, IStorage storage)
    {
        _audio = audio;
        _storage = storage;
    }

    [HttpPost("upload")]
    [DisableRequestSizeLimit]
    public async Task<ApiResponse<AudioFile>> Upload(Guid meetingId, IFormFile file)
    {
        var result = await _audio.UploadAsync(meetingId, file);
        return result;
    }

    [HttpGet("presigned-url")]
    public async Task<ApiResponse<PresignedUploadResponseDto>> GetPresignedUrl([FromQuery] PresignedUploadRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.FileName) || request.MeetingId == Guid.Empty)
            return ApiResponse<PresignedUploadResponseDto>.FailureResponse(
                "FileName and MeetingId are required.", 
                400
            );

        // Generate unique file path
        var safeFileName = Path.GetFileNameWithoutExtension(request.FileName)
            .Replace(" ", "_")
            .Replace("[", "")
            .Replace("]", "")
            .Replace("(", "")
            .Replace(")", "");

        var extension = Path.GetExtension(request.FileName);
        var filePath = $"audio/{request.MeetingId}/{Guid.NewGuid()}_{safeFileName}{extension}";

        // Get presigned URL (valid for 15 minutes)
        var uploadUrl = await _storage.GetPresignedUploadUrlAsync(
            filePath,
            request.ContentType,
            15
        );

        var fileUrl = _storage.GetFileUrl(filePath);

        var response = new PresignedUploadResponseDto
        {
            UploadUrl = uploadUrl,
            FileUrl = fileUrl,
            FilePath = filePath
        };

        return ApiResponse<PresignedUploadResponseDto>.SuccessResponse(
            response,
            "Presigned URL generated successfully",
            200
        );
    }

    [HttpPost("confirm-upload")]
    public async Task<ApiResponse<AudioFile>> ConfirmUpload([FromBody] ConfirmUploadDto request)
    {
        // Create audio record matching your model exactly
        var audioFile = new AudioFile
        {
            Id = Guid.NewGuid(),
            MeetingId = request.MeetingId,
            FileName = request.FileName,
            FilePath = request.FilePath,
            FileUrl = request.FileUrl,
            FileSize = request.FileSize,
            ContentType = request.ContentType,
            Status = stutus.pending // Use your enum
        };

        var result = await _audio.CreateAudioRecordAsync(audioFile);
        return result;
    }

    [HttpGet("user/{id}")]
    public async Task<ApiResponse<List<AudioFile>>> GetAllAudioFileByUserIdAsync()
    {
        Guid currentUserId = User.GetUserId();
        var result = await _audio.GetAllAudioFileByUserIdAsync(currentUserId);
        return result;
    }

    [HttpGet]
    public async Task<ApiResponse<List<AudioFile>>> GetAllAudioFilesAsync()
    {
        var result = await _audio.GetAllAudioFilesAsync();
        return result;
    }

    [HttpGet("meeting/{meetingId}")]
    public async Task<ApiResponse<AudioFile>> GetAudioByMeetingIdAsync(Guid meetingId)
    {
        var result = await _audio.GetAudioByMeetingIdAsync(meetingId);
        return result;
    }
}