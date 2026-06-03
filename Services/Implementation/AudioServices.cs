using finalyearproject.Data;
using finalyearproject.Services.Interfaces;
using  finalyearproject.Models;
using finalyearproject.Wrappers;
using Microsoft.EntityFrameworkCore; 
namespace finalyearproject.Services.Implementation;

public class AudioServices: IAudio
{
    private readonly IStorage _storage;
    private  readonly AppDbContext  _context;
    private ILogger<AudioServices> _logger;

    public AudioServices(IStorage storage, AppDbContext context, ILogger<AudioServices> logger)
    {
        _storage = storage;
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResponse<AudioFile>> UploadAsync(Guid meetingId,IFormFile file)
    {
        
        var meeting  = await _context.Meetings.FindAsync(meetingId);

        if (meeting == null)
        {
            _logger.LogWarning("Meeting with id {MeetingId} not found", meetingId);
            return ApiResponse<AudioFile>.FailureResponse("Meeting not found",404);
        }
        
        ValidateAudio(file);
        
        _logger.LogInformation("Starting upload for {FileName}, size={Size}",
            file.FileName,
            file.Length);

        var fileKey = await _storage.UploadAsync(file, "audio");
        var fileUrl = _storage.GetFileUrl(fileKey);
        
        _logger.LogInformation("Storage upload completed");

        var audio = new AudioFile
        {
            Id = Guid.NewGuid(),
            MeetingId =  meeting.Id,
            FileName = file.FileName,
            FilePath = fileKey,
            FileUrl = fileUrl,
            ContentType = file.ContentType,
            FileSize = file.Length,
            Status = stutus.pending
        };
        
        _context.AudioFiles.Add(audio);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Audio file {FileName} uploaded for meeting {MeetingId}", file.FileName, meetingId);
        
         return ApiResponse<AudioFile>.SuccessResponse(audio,message: "Audio uploaded successfully",statusCode: 201);
    }

    private void ValidateAudio(IFormFile file)
    {
        _logger.LogInformation("Validating audio file: {FileName}, Size: {FileSize} bytes", file.FileName, file.Length);
        var allowedExtensions =
            new[] { ".mp3", ".wav", ".m4a" };

        var extension =
            Path.GetExtension(file.FileName).ToLower();

        if (!allowedExtensions.Contains(extension))
        { _logger.LogError("Invalid audio format: {Extension}", extension);
            throw new Exception("Invalid audio format");
           
        }

        const long maxSize = 50 * 1024 * 1024;

        if (file.Length > maxSize)
        {
            _logger.LogError("File too large: {FileSize} bytes", file.Length);
            throw new Exception("File too large");
        }

    }
    
    public async Task <ApiResponse<AudioFile>> GetAudioByMeetingIdAsync(Guid meetingId)
    {
        var audio = await _context.AudioFiles.FirstOrDefaultAsync(a => a.MeetingId == meetingId);
        if (audio == null)
        {
            _logger.LogWarning("No audio file found for meeting {MeetingId}", meetingId);
            return ApiResponse<AudioFile>.FailureResponse("Audio file not found for this meeting",404);
        }
        
        _logger.LogInformation("Audio file {FileName} retrieved for meeting {MeetingId}", audio.FileName, meetingId);
        return ApiResponse<AudioFile>.SuccessResponse(audio,message: "Audio file retrieved successfully",statusCode: 200);
    }
    
    public async Task <ApiResponse<List<AudioFile>>> GetAllAudioFilesAsync()
    {
        var audioFiles = await _context.AudioFiles
            .AsNoTracking()
            .ToListAsync();

        _logger.LogInformation("{Count} audio files retrieved", audioFiles.Count);
        return ApiResponse<List<AudioFile>>.SuccessResponse(audioFiles,message: "Audio files retrieved successfully",statusCode: 200);
    }

    public async Task<ApiResponse<List<AudioFile>>> GetAllAudioFileByUserIdAsync(Guid id)
    {
        var user =await _context.Users.FirstAsync(u=>u.UserId == id);

        if (user == null)
        {
            _logger.LogWarning("User with id {UserId} not found", id);
            
            return ApiResponse<List<AudioFile>>.FailureResponse("Audio file not found",404);
        }
        
        var audioFiles = await _context.AudioFiles
            .Where(a => a.Meeting.CreatorId == user.UserId)
            .Select(a => new AudioFile
            {
                Id = a.Id,
                MeetingId = a.MeetingId,
                FileName = a.FileName,
                FilePath = a.FileName,
                FileUrl = a.FileUrl,
                ContentType = a.ContentType,
                Status = stutus.pending
            })
            .ToListAsync();
        
        
   
        return ApiResponse<List<AudioFile>>.SuccessResponse(audioFiles,"Audio files retrived successfully",statusCode:200);
        
    }
    
}