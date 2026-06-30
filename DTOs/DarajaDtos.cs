namespace finalyearproject.DTOs;

// What your frontend sends to your backend
public class StkPushRequestDto
{
    public string PhoneNumber { get; set; } = string.Empty; // e.g., 254708374149
    public string Amount { get; set; } = string.Empty;      // e.g., "100"
}

// What Daraja sends back when you trigger the STK Push
public class StkPushResponseDto
{
    public string MerchantRequestID { get; set; } = string.Empty;
    public string CheckoutRequestID { get; set; } = string.Empty;
    public string ResponseCode { get; set; } = string.Empty;
    public string ResponseDescription { get; set; } = string.Empty;
    public string CustomerMessage { get; set; } = string.Empty;
}

// Helper class for the Callback metadata
public class DarajaCallbackMetadataItem
{
    public string Name { get; set; } = string.Empty;
    public object Value { get; set; } = null!;
}