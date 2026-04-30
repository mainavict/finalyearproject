using System.Net;

namespace finalyearproject.Wrappers;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public int StatusCode { get; set; }
    
    //Success
    public static ApiResponse<T> SuccessResponse(T data, string message = "Success", int statusCode = 200)
    {
        return new ApiResponse<T> { Success = true, Data = data, Message = message, StatusCode = statusCode };
    }

    // Failure
    public static ApiResponse<T> FailureResponse(string message, int statusCode = 400)
    {
        return new ApiResponse<T> { Success = false, Message = message, StatusCode = statusCode };
    }
    
}