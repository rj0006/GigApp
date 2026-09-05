using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GigApp.Api.Services.Errors
{
    public class GlobalExceptionFilter : IAsyncExceptionFilter
    {
        private readonly IErrorLogService _errorLog;

        public GlobalExceptionFilter(IErrorLogService errorLog) => _errorLog = errorLog;

        public async Task OnExceptionAsync(ExceptionContext context)
        {
            // A cancelled request is the browser navigating away, not a fault.
            if (context.Exception is OperationCanceledException
                && context.HttpContext.RequestAborted.IsCancellationRequested)
            {
                context.Result = new EmptyResult();
                context.ExceptionHandled = true;
                return;
            }

            var module = $"{context.RouteData.Values["controller"]}/{context.RouteData.Values["action"]}";
            var reference = await _errorLog.LogAsync(context.Exception, module);

            var path = context.HttpContext.Request.Path;
            var wantsJson = path.StartsWithSegments("/api")
                || context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            if (wantsJson)
            {
                context.Result = new ObjectResult(new ProblemDetails
                {
                    Title = "Something went wrong.",
                    Detail = $"Quote reference {reference} when reporting this.",
                    Status = StatusCodes.Status500InternalServerError,
                    Extensions = { ["reference"] = reference },
                })
                { StatusCode = StatusCodes.Status500InternalServerError };
            }
            else
            {
                context.Result = new RedirectToActionResult(
                    "Error", "Home", new { reference });
            }

            context.ExceptionHandled = true;
        }
    }
}
