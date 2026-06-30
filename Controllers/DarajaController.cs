using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using finalyearproject.Data;
using finalyearproject.DTOs;
using finalyearproject.Models;
using finalyearproject.Services.Interfaces;
using System.Security.Claims;
using System.Text.Json;

namespace finalyearproject.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DarajaController : ControllerBase
{
    private readonly IDarajaService _darajaService;
    private readonly AppDbContext _context;
    private readonly ILogger<DarajaController> _logger;

    public DarajaController(
        IDarajaService darajaService, 
        AppDbContext context, 
        ILogger<DarajaController> logger)
    {
        _darajaService = darajaService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Initiate STK Push and create payment record
    /// </summary>
    [HttpPost("stk-push")]
    [Authorize]
    public async Task<IActionResult> StkPush([FromBody] StkPushRequestDto request)
    {
        try
        {
            _logger.LogInformation("📱 Initiating STK Push for {Phone}, Plan: {Plan}", 
                request.PhoneNumber, request.PlanType);

            // 1. Create payment record
            var payment = new Payment
            {
                CheckoutRequestID = "TEMP_" + Guid.NewGuid().ToString("N")[..8],
                UserId = Guid.Parse(request.UserId),
                PlanType = request.PlanType,
                Amount = int.Parse(request.Amount),
                Status = PaymentStatus.Pending
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            // 2. Call Daraja API
            var result = await _darajaService.InitiateStkPushAsync(request);

            // 3. Update with real CheckoutRequestID from Daraja
            if (result.ResponseCode == "0")
            {
                payment.CheckoutRequestID = result.CheckoutRequestID;
                await _context.SaveChangesAsync();
                _logger.LogInformation("✅ STK Push sent. CheckoutID: {ID}", result.CheckoutRequestID);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ STK Push failed");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Callback from Daraja after user enters PIN
    /// </summary>
    [HttpPost("callback")]
    public async Task<IActionResult> Callback([FromBody] JsonElement payload)
    {
        try
        {
            _logger.LogInformation("📞 Daraja Callback Received");

            if (payload.TryGetProperty("Body", out var body) && 
                body.TryGetProperty("stkCallback", out var stkCallback))
            {
                var resultCode = stkCallback.GetProperty("ResultCode").GetInt32();
                var checkoutRequestID = stkCallback.GetProperty("CheckoutRequestID").GetString();

                var payment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.CheckoutRequestID == checkoutRequestID);

                if (payment != null)
                {
                    if (resultCode == 0) // Success
                    {
                        payment.Status = PaymentStatus.Completed;
                        payment.CompletedAt = DateTime.UtcNow;

                        var user = await _context.Users.FindAsync(payment.UserId);
                        if (user != null)
                        {
                            // Apply pricing logic based on plan
                            switch (payment.PlanType)
                            {
                                case "payg_50":
                                    user.MonthlyMinuteLimit += 30; // Add 30 mins to limit
                                    break;

                                case "pro":
                                    user.SubscriptionTier = "pro";
                                    user.MonthlyMinuteLimit = 200;
                                    user.SubscriptionExpiryDate = DateTime.UtcNow.AddDays(30);
                                    user.MinutesUsedThisMonth = 0;
                                    break;

                                case "ultra_pro":
                                    user.SubscriptionTier = "ultra_pro";
                                    user.MonthlyMinuteLimit = 600;
                                    user.SubscriptionExpiryDate = DateTime.UtcNow.AddDays(30);
                                    user.MinutesUsedThisMonth = 0;
                                    break;
                            }
                            _logger.LogInformation("✅ Payment completed for {Email}, Plan: {Plan}", 
                                user.Email, payment.PlanType);
                        }
                    }
                    else // Failed or cancelled
                    {
                        payment.Status = PaymentStatus.Failed;
                        _logger.LogWarning("❌ Payment failed. ResultCode: {Code}", resultCode);
                    }

                    await _context.SaveChangesAsync();
                }
            }

            // Always return success to Daraja
            return Ok(new { ResultCode = 0, ResultDesc = "Accepted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Callback processing error");
            return Ok(new { ResultCode = 0, ResultDesc = "Accepted" });
        }
    }

    /// <summary>
    /// Check payment status (for frontend polling)
    /// </summary>
    [HttpGet("status/{checkoutRequestId}")]
    [Authorize]
    public async Task<IActionResult> GetPaymentStatus(string checkoutRequestId)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "");
            
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.CheckoutRequestID == checkoutRequestId && p.UserId == userId);

            if (payment == null)
            {
                return NotFound(new { error = "Payment not found" });
            }

            return Ok(new
            {
                checkoutRequestId = payment.CheckoutRequestID,
                status = payment.Status.ToString().ToLower(),
                planType = payment.PlanType,
                amount = payment.Amount,
                completedAt = payment.CompletedAt
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get user's payment history
    /// </summary>
    [HttpGet("history")]
    [Authorize]
    public async Task<IActionResult> GetPaymentHistory()
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "");
            
            var payments = await _context.Payments
                .Where(p => p.UserId == userId && p.Status == PaymentStatus.Completed)
                .OrderByDescending(p => p.CompletedAt)
                .Select(p => new
                {
                    p.CheckoutRequestID,
                    p.PlanType,
                    p.Amount,
                    p.CompletedAt,
                    status = p.Status.ToString().ToLower()
                })
                .ToListAsync();

            return Ok(payments);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}