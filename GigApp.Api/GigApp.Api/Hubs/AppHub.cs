using GigApp.Api.Data;
using GigApp.Api.Models;
using GigApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Hubs
{
    public static class RealtimeGroups
    {
        public static string User(int userId) => $"user-{userId}";
        public static string CategoryPartners(int categoryId) => $"category-{categoryId}-partners";
        public const string Admins = "admins";
    }

    // One hub for the whole app — a connection joins whichever groups its own
    // role and identity qualify for, then just listens. The web portals carry
    // the JWT in the gigapp_token cookie the same way every other request
    // does; a mobile client authenticates the same hub with nothing extra
    // beyond sending its access token the way the SignalR client always does
    // (Program.cs reads it from the query string for this path, since a
    // WebSocket upgrade cannot carry a custom Authorization header).
    [Authorize]
    public class AppHub : Hub
    {
        private readonly AppDbContext _context;

        public AppHub(AppDbContext context) => _context = context;

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.GetUserId();

            if (userId is not null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.User(userId.Value));

                if (Context.User!.IsInRole(UserRoles.Partner))
                {
                    var categoryId = await _context.Partners
                        .Where(p => p.UserId == userId)
                        .Select(p => (int?)p.SkillCategoryId)
                        .FirstOrDefaultAsync();

                    if (categoryId is not null)
                        await Groups.AddToGroupAsync(
                            Context.ConnectionId, RealtimeGroups.CategoryPartners(categoryId.Value));
                }

                if (Context.User!.IsAdmin())
                    await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Admins);
            }

            await base.OnConnectedAsync();
        }
    }
}
