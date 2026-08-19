namespace GigApp.Api.Dtos
{
    /// <summary>
    /// Standard envelope for every list/lookup endpoint the front end calls.
    /// global.js reads exactly this shape, so do not return bare arrays.
    /// </summary>
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }

        public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };
        public static ApiResponse<T> Fail(string message) => new() { Success = false, Message = message };
    }

    /// <summary>Item shape every master returns — autocomplete needs nothing more.</summary>
    public class MasterItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional second line / hint shown in the dropdown.</summary>
        public string? Hint { get; set; }
    }
}
