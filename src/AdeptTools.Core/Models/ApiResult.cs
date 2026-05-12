namespace AdeptTools.Core.Models;

public class ApiResult
{
    public int StatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ResultMsg { get; set; }

    public bool IsSuccess => StatusCode == 0;

    public static ApiResult Success(string? message = null) =>
        new() { StatusCode = 0, ResultMsg = message };

    public static ApiResult Failure(int statusCode, string errorMessage) =>
        new() { StatusCode = statusCode, ErrorMessage = errorMessage };
}
