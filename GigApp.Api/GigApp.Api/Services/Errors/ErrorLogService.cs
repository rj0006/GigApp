using GigApp.Api.Data;
using GigApp.Api.Models;
using GigApp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Errors
{
    public interface IErrorLogService
    {
        Task<string> LogAsync(Exception exception, string? module, CancellationToken ct = default);
    }

    public class ErrorLogService : IErrorLogService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IHttpContextAccessor _http;
        private readonly ILogger<ErrorLogService> _logger;

        public ErrorLogService(
            IServiceScopeFactory scopes, IHttpContextAccessor http, ILogger<ErrorLogService> logger)
        {
            _scopes = scopes;
            _http = http;
            _logger = logger;
        }

        public async Task<string> LogAsync(
            Exception exception, string? module, CancellationToken ct = default)
        {
            var reference = NewReference();
            var context = _http.HttpContext;

            var entry = new ErrorLog
            {
                Reference = reference,
                Message = Truncate(exception.Message, 2000),
                ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
                StackTrace = Truncate(exception.StackTrace, 8000),
                InnerMessage = Truncate(exception.InnerException?.Message, 2000),
                Module = Truncate(module, 200),
                RequestPath = Truncate(context?.Request.Path.Value, 400),
                RequestMethod = context?.Request.Method,
                UserId = context?.User.GetUserId(),
                UserName = Truncate(context?.User.Identity?.Name, 100),
                IpAddress = Truncate(context?.Connection.RemoteIpAddress?.ToString(), 45),
                OccurredAt = DateTime.UtcNow,
            };

            _logger.LogError(exception, "Unhandled error {Reference} in {Module}", reference, module);

            try
            {
                // A fresh scope, because the request's own DbContext is usually
                // the thing that just failed and cannot save anything more.
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                db.ErrorLogs.Add(entry);
                await db.SaveChangesAsync(ct);
            }
            catch (Exception saveFailure)
            {
                _logger.LogError(saveFailure, "Could not persist error {Reference}", reference);
            }

            return reference;
        }

        private static string NewReference() =>
            $"E{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        private static string? Truncate(string? value, int max) =>
            string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
    }
}
