using finalyearproject.Services.Interfaces;
using finalyearproject.Models;
using finalyearproject.Data;
using finalyearproject.Wrappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace finalyearproject.Services.Implementation;

public class ParticipantService : IParticipantService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ParticipantService> _logger;

    public ParticipantService(AppDbContext context, ILogger<ParticipantService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResponse<List<Participant>>> GetParticipantsByMeetingIdAsync(Guid meetingId)
    {
        try
        {
            var participants = await _context.Participants
                .Where(p => p.MeetingId == meetingId)
                .OrderBy(p => p.JoinedAt)
                .ToListAsync();

            return ApiResponse<List<Participant>>.SuccessResponse(
                participants, 
                "Participants retrieved successfully", 
                200);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching participants for meeting {MeetingId}", meetingId);
            return ApiResponse<List<Participant>>.FailureResponse("Failed to fetch participants", 500);
        }
    }

    public async Task<ApiResponse<Participant>> AddParticipantAsync(Guid meetingId, Participant participant)
    {
        try
        {
            // Check if meeting exists
            var meeting = await _context.Meetings.FindAsync(meetingId);
            if (meeting == null)
            {
                return ApiResponse<Participant>.FailureResponse("Meeting not found", 404);
            }

            participant.MeetingId = meetingId;
            participant.CreatedAt = DateTime.UtcNow;

            _context.Participants.Add(participant);
            await _context.SaveChangesAsync();

            return ApiResponse<Participant>.SuccessResponse(
                participant, 
                "Participant added successfully", 
                201);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding participant to meeting {MeetingId}", meetingId);
            return ApiResponse<Participant>.FailureResponse("Failed to add participant", 500);
        }
    }

    public async Task<ApiResponse<bool>> AddParticipantsFromRecallAsync(Guid meetingId, List<RecallParticipant> recallParticipants)
    {
        try
        {
            // Check if meeting exists
            var meeting = await _context.Meetings.FindAsync(meetingId);
            if (meeting == null)
            {
                return ApiResponse<bool>.FailureResponse("Meeting not found", 404);
            }

            // Remove existing participants for this meeting (to avoid duplicates)
            var existingParticipants = await _context.Participants
                .Where(p => p.MeetingId == meetingId)
                .ToListAsync();
            
            _context.Participants.RemoveRange(existingParticipants);

            // Add new participants from Recall.ai
            var newParticipants = recallParticipants.Select(rp => new Participant
            {
                MeetingId = meetingId,
                Name = rp.Name,
                Email = rp.Email,
                Role = rp.Role,
                IsHost = rp.IsHost,
                JoinedAt = rp.JoinedAt,
                LeftAt = rp.LeftAt,
                DurationSeconds = rp.DurationSeconds,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            _context.Participants.AddRange(newParticipants);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Added {Count} participants to meeting {MeetingId}", newParticipants.Count, meetingId);

            return ApiResponse<bool>.SuccessResponse(
                true, 
                $"Added {newParticipants.Count} participants successfully", 
                200);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding Recall participants to meeting {MeetingId}", meetingId);
            return ApiResponse<bool>.FailureResponse("Failed to add participants", 500);
        }
    }

    public async Task<ApiResponse<bool>> UpdateParticipantAsync(Guid participantId, Participant participant)
    {
        try
        {
            var existing = await _context.Participants.FindAsync(participantId);
            if (existing == null)
            {
                return ApiResponse<bool>.FailureResponse("Participant not found", 404);
            }

            existing.Name = participant.Name;
            existing.Email = participant.Email;
            existing.Role = participant.Role;
            existing.IsHost = participant.IsHost;
            existing.JoinedAt = participant.JoinedAt;
            existing.LeftAt = participant.LeftAt;
            existing.DurationSeconds = participant.DurationSeconds;

            await _context.SaveChangesAsync();

            return ApiResponse<bool>.SuccessResponse(true, "Participant updated successfully", 200);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating participant {ParticipantId}", participantId);
            return ApiResponse<bool>.FailureResponse("Failed to update participant", 500);
        }
    }

    public async Task<ApiResponse<bool>> DeleteParticipantAsync(Guid participantId)
    {
        try
        {
            var participant = await _context.Participants.FindAsync(participantId);
            if (participant == null)
            {
                return ApiResponse<bool>.FailureResponse("Participant not found", 404);
            }

            _context.Participants.Remove(participant);
            await _context.SaveChangesAsync();

            return ApiResponse<bool>.SuccessResponse(true, "Participant deleted successfully", 200);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting participant {ParticipantId}", participantId);
            return ApiResponse<bool>.FailureResponse("Failed to delete participant", 500);
        }
    }
}