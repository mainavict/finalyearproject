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

    public DarajaController(IDarajaService darajaService, AppDbContext context, ILogger<DarajaController> logger)
    {
        _darajaService = darajaService;
        _context = context;
        _logger = logger;
    }

    [HttpPost("stk-push")]
    [Authorize]
    public async Task<IActionResult> StkPush([FromBody] StkPushRequestDto request)
    {
        try
        {
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

            var result = await _darajaService.InitiateStkPushAsync(request);

            if (result.ResponseCode == "0")
            {
                payment.CheckoutRequestID = result.CheckoutRequestID;
                await _context.SaveChangesAsync();
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("callback")]
    public async Task<IActionResult> Callback([FromBody] JsonElement payload)
    {
        try
        {
            if (payload.TryGetProperty("Body", out var body) && 
                body.TryGetProperty("stkCallback", out var stkCallback))
            {
                var resultCode = stkCallback.GetProperty("ResultCode").GetInt32();
                var checkoutRequestID = stkCallback.GetProperty("CheckoutRequestID").GetString();

                var payment = await _context.Payments.FirstOrDefaultAsync(p => p.CheckoutRequestID == checkoutRequestID);

                if (payment != null)
                {
                    if (resultCode == 0) // Success
                    {
                        payment.Status = PaymentStatus.Completed;
                        payment.CompletedAt = DateTime.UtcNow;

                        var user = await _context.Users.FindAsync(payment.UserId);
                        if (user != null)
                        {
                            // ✅ DYNAMIC PRICING: Fetch plan from database
                            var plan = await _context.SubscriptionPlans
                                .FirstOrDefaultAsync(p => p.PlanCode == payment.PlanType && p.IsActive);

                            if (plan != null)
                            {
                                if (plan.DurationDays > 0) // Monthly Subscription
                                {
                                    user.SubscriptionTier = plan.PlanCode;
                                    user.MonthlyMinuteLimit = plan.IncludedMinutes;
                                    user.SubscriptionExpiryDate = DateTime.UtcNow.AddDays(plan.DurationDays);
                                    user.MinutesUsedThisMonth = 0; // Reset usage
                                }
                                else // Top-Up
                                {
                                    user.MonthlyMinuteLimit += plan.IncludedMinutes;
                                }
                                _logger.LogInformation("✅ Applied plan {Plan} for {Email}", plan.DisplayName, user.Email);
                            }
                            else
                            {
                                _logger.LogError("❌ Plan not found for code: {PlanType}", payment.PlanType);
                            }
                        }
                    }
                    else // Failed
                    {
                        payment.Status = PaymentStatus.Failed;
                    }
                    await _context.SaveChangesAsync();
                }
            }
            return Ok(new { ResultCode = 0, ResultDesc = "Accepted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Callback error");
            return Ok(new { ResultCode = 0, ResultDesc = "Accepted" });
        }
    }

    [HttpGet("status/{checkoutRequestId}")]
    [Authorize]
    public async Task<IActionResult> GetPaymentStatus(string checkoutRequestId)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "");
        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.CheckoutRequestID == checkoutRequestId && p.UserId == userId);

        if (payment == null) return NotFound(new { error = "Payment not found" });

        return Ok(new
        {
            checkoutRequestId = payment.CheckoutRequestID,
            status = payment.Status.ToString().ToLower(),
            planType = payment.PlanType,
            amount = payment.Amount,
            completedAt = payment.CompletedAt
        });
    }

    [HttpGet("history")]
    [Authorize]
    public async Task<IActionResult> GetPaymentHistory()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "");
        var payments = await _context.Payments
            .Where(p => p.UserId == userId && p.Status == PaymentStatus.Completed)
            .OrderByDescending(p => p.CompletedAt)
            .Select(p => new { p.CheckoutRequestID, p.PlanType, p.Amount, p.CompletedAt, status = p.Status.ToString().ToLower() })
            .ToListAsync();

        return Ok(payments);
    }

    // ✅ NEW ENDPOINT: Fetch plans for the frontend
    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans()
    {
        var plans = await _context.SubscriptionPlans
            .Where(p => p.IsActive)
            .Select(p => new
            {
                p.PlanCode,
                p.DisplayName,
                p.Description,
                p.PriceKSH,
                p.IncludedMinutes,
                p.DurationDays,
                isTopUp = p.DurationDays == 0
            })
            .ToListAsync();

        return Ok(plans);
    }
}