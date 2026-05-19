namespace MAPS.Shared.DTOs.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(T data, string message = "Success") =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string error) =>
        new() { Success = false, Message = error, Errors = new List<string> { error } };

    public static ApiResponse<T> Fail(List<string> errors) =>
        new() { Success = false, Message = "Validation failed", Errors = errors };
}

public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Ok(string message = "Success") =>
        new() { Success = true, Message = message };

    public static new ApiResponse Fail(string error) =>
        new() { Success = false, Message = error, Errors = new List<string> { error } };
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

public class PaginationRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = false;
}

public class ValidationError
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
