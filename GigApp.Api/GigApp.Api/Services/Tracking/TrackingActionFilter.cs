using System.Reflection;
using GigApp.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GigApp.Api.Services.Tracking
{
    /// <summary>
    /// Logs every state-changing controller action. Registered globally so a new
    /// endpoint is covered the moment it is written — nobody has to remember to
    /// add logging.
    /// </summary>
    public class TrackingActionFilter : IAsyncActionFilter
    {
        /// <summary>Argument names that carry a user-supplied reason or feedback.</summary>
        private static readonly string[] RemarkKeys = { "remark", "reason", "feedback", "comment" };

        private readonly IServiceScopeFactory _scopeFactory;

        public TrackingActionFilter(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var request = context.HttpContext.Request;

            if (!ShouldTrack(context, request.Method))
            {
                await next();
                return;
            }

            var arguments = context.ActionArguments
                .Where(a => a.Value is not CancellationToken)
                .ToDictionary(a => a.Key, a => a.Value);

            var executed = await next();

            // Only record operations that actually succeeded.
            if (executed.Exception is not null && !executed.ExceptionHandled) return;
            if (!IsSuccess(executed.Result)) return;
            if (FailedWithFlash(executed.Controller)) return;

            // The Razor portals redirect, so there is no DTO to read. They can
            // hand the record over through HttpContext.Items instead.
            var items = context.HttpContext.Items;
            var result = items.TryGetValue(TrackingKeys.Result, out var tracked) && tracked is not null
                ? tracked
                : (executed.Result as ObjectResult)?.Value;

            var entry = new TrackingLogEntry
            {
                EntryType = ResolveEntryType(context, request.Method),
                FormType = ResolveFormType(context),
                DocNo = items.TryGetValue(TrackingKeys.DocNo, out var docNo) && docNo is not null
                    ? docNo.ToString()
                    : ResolveDocNo(context, result),
                UserId = context.HttpContext.User.GetUserId(),
                UserName = context.HttpContext.User.Identity?.Name,
                Remark = ResolveRemark(arguments),
                IpAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString(),
                RequestPath = request.Path + request.QueryString,
                Request = arguments.Count == 1 ? arguments.Values.First() : arguments,
                Result = result,
            };

            // A separate scope so the audit write cannot accidentally commit
            // whatever the action left tracked on its own DbContext.
            using var scope = _scopeFactory.CreateScope();
            var tracker = scope.ServiceProvider.GetRequiredService<ITrackingLogService>();
            await tracker.WriteAsync(entry, context.HttpContext.RequestAborted);
        }

        private static bool ShouldTrack(ActionExecutingContext context, string method)
        {
            if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
                return false;

            return context.ActionDescriptor is not ControllerActionDescriptor descriptor
                || (descriptor.MethodInfo.GetCustomAttribute<SkipTrackingAttribute>() is null
                    && descriptor.ControllerTypeInfo.GetCustomAttribute<SkipTrackingAttribute>() is null);
        }

        /// <summary>
        /// The portals redirect on failure too, so a redirect alone does not mean
        /// success. Our PRG convention puts the reason in TempData["Error"] —
        /// if that is set, the operation did not happen.
        /// </summary>
        private static bool FailedWithFlash(object? controller) =>
            controller is Controller mvc && mvc.TempData.ContainsKey("Error");

        /// <summary>A redirect counts as success — the Razor portals use PRG.</summary>
        private static bool IsSuccess(IActionResult? result) => result switch
        {
            null => false,
            ObjectResult obj => obj.StatusCode is null or (>= 200 and < 300),
            StatusCodeResult status => status.StatusCode is >= 200 and < 300,
            RedirectResult or RedirectToActionResult or RedirectToRouteResult => true,
            ViewResult => false,   // a re-rendered form means validation failed
            _ => true,
        };

        /// <summary>Explicit attribute wins; otherwise the HTTP verb decides.</summary>
        private static string ResolveEntryType(ActionExecutingContext context, string method)
        {
            if (context.ActionDescriptor is ControllerActionDescriptor descriptor)
            {
                var attribute = descriptor.MethodInfo.GetCustomAttribute<TrackEntryAttribute>();
                if (attribute is not null) return attribute.EntryType;
            }

            return TrackingEntryType.FromHttpMethod(method);
        }

        private static string ResolveFormType(ActionExecutingContext context)
        {
            if (context.ActionDescriptor is ControllerActionDescriptor descriptor)
            {
                var attribute = descriptor.MethodInfo.GetCustomAttribute<TrackFormAttribute>()
                             ?? descriptor.ControllerTypeInfo.GetCustomAttribute<TrackFormAttribute>();

                if (attribute is not null) return attribute.FormType;
                return descriptor.ControllerName;
            }

            return "Unknown";
        }

        /// <summary>Route id first; otherwise the Id on whatever the action returned.</summary>
        private static string? ResolveDocNo(ActionExecutingContext context, object? result)
        {
            if (context.RouteData.Values.TryGetValue("id", out var routeId) && routeId is not null)
                return routeId.ToString();

            if (result is null) return null;

            var idProperty = result.GetType().GetProperty("Id",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            return idProperty?.GetValue(result)?.ToString();
        }

        private static string? ResolveRemark(IDictionary<string, object?> arguments)
        {
            foreach (var (key, value) in arguments)
            {
                if (value is string text && RemarkKeys.Contains(key.ToLowerInvariant()))
                    return text;

                if (value is null || value is string) continue;

                // Also look one level into a posted model for a remark field.
                var property = value.GetType().GetProperties()
                    .FirstOrDefault(p => p.PropertyType == typeof(string)
                                      && RemarkKeys.Contains(p.Name.ToLowerInvariant()));

                if (property?.GetValue(value) is string found && !string.IsNullOrWhiteSpace(found))
                    return found;
            }

            return null;
        }
    }
}
