using Microsoft.AspNetCore.Mvc;
using finalyearproject.DTOs;
using finalyearproject.Services.Interfaces;
using System.Text.Json;

namespace finalyearproject.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DarajaController : ControllerBase
{
    private readonly IDarajaService _darajaService;
    private readonly ILogger<DarajaController> _logger;

    public DarajaController(IDarajaService darajaService, ILogger<DarajaController> logger)
    {
        _darajaService = darajaService;
        _logger = logger;
    }

    /// <summary>
    /// Triggers the STK Push to the user's phone
    /// </summary>
    [HttpPost("stk-push")]
    public async Task<IActionResult> StkPush([FromBody] StkPushRequestDto request)
    {
        try
        {
            _logger.LogInformation("Initiating STK Push for {Phone}", request.PhoneNumber);
            
            var result = await _darajaService.InitiateStkPushAsync(request);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate STK Push");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Receives the callback from Safaricom after the user enters their PIN
    /// </summary>
    [HttpPost("callback")]
    public IActionResult Callback([FromBody] JsonElement payload)
    {
        try
        {
            _logger.LogInformation("📞 Daraja Callback Received: {Payload}", payload.GetRawText());

            // Safaricom sends a complex nested JSON. We parse it dynamically here.
            if (payload.TryGetProperty("Body", out var body) && 
                body.TryGetProperty("stkCallback", out var stkCallback))
            {
                var resultCode = stkCallback.GetProperty("ResultCode").GetInt32();
                var checkoutRequestID = stkCallback.GetProperty("CheckoutRequestID").GetString();

                if (resultCode == 0)
                {
                    _logger.LogInformation("✅ Payment Successful! CheckoutID: {ID}", checkoutRequestID);
                    
                    // TODO: In the next step, we will find the user by CheckoutRequestID 
                    // and update their subscription status in the database.
                }
                else
                {
                    _logger.LogWarning("❌ Payment Failed/Cancelled. ResultCode: {Code}", resultCode);
                }
            }

            // Always return 0 to Safaricom to acknowledge receipt
            return Ok(new { ResultCode = 0, ResultDesc = "Accepted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Daraja callback");
            return Ok(new { ResultCode = 0, ResultDesc = "Accepted" }); 
        }
    }
}