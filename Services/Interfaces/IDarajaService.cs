using finalyearproject.DTOs;

namespace finalyearproject.Services.Interfaces;

public interface IDarajaService
{
    Task<string> GetAccessTokenAsync();
    Task<StkPushResponseDto> InitiateStkPushAsync(StkPushRequestDto request);
}