using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Menus
{
    public interface IMenuService
    {
        Task<IReadOnlyList<MenuNodeDto>> GetSidebarAsync(bool isSuperAdmin, CancellationToken ct = default);
        Task<IReadOnlyList<MenuItemDto>> GetAllAsync(CancellationToken ct = default);
        Task<MenuItemDto?> GetAsync(int id, CancellationToken ct = default);
        Task<IReadOnlyList<MenuOptionDto>> GetParentOptionsAsync(int? excludingId, CancellationToken ct = default);
        Task<MenuResult> SaveAsync(int? id, SaveMenuItemRequest request, CancellationToken ct = default);
        Task<MenuResult> DeleteAsync(int id, CancellationToken ct = default);
        bool ActionExists(string controller, string action);
    }

    public class MenuService : IMenuService
    {
        private readonly AppDbContext _context;
        private readonly IActionDescriptorCollectionProvider _actions;

        public MenuService(AppDbContext context, IActionDescriptorCollectionProvider actions)
        {
            _context = context;
            _actions = actions;
        }

        public async Task<IReadOnlyList<MenuNodeDto>> GetSidebarAsync(
            bool isSuperAdmin, CancellationToken ct = default)
        {
            var rows = await _context.MenuItems
                .AsNoTracking()
                .Where(m => m.IsActive)
                .OrderBy(m => m.SortOrder).ThenBy(m => m.Label)
                .ToListAsync(ct);

            var visible = rows
                .Where(m => isSuperAdmin || m.Visibility != MenuVisibility.SuperAdmin)
                .ToList();

            var byParent = visible.ToLookup(m => m.ParentId);

            return byParent[null]
                .Select(root => new MenuNodeDto
                {
                    Item = MenuItemDto.From(root),
                    Children = byParent[root.Id].Select(child => new MenuNodeDto
                    {
                        Item = MenuItemDto.From(child),
                    }).ToList(),
                })
                .Where(node => !node.Item.IsGroup || node.Children.Count > 0)
                .ToList();
        }

        public async Task<IReadOnlyList<MenuItemDto>> GetAllAsync(CancellationToken ct = default)
        {
            var rows = await _context.MenuItems
                .AsNoTracking()
                .Include(m => m.Parent)
                .OrderBy(m => m.ParentId == null ? m.SortOrder : 0)
                .ToListAsync(ct);

            var byParent = rows.ToLookup(m => m.ParentId);
            var ordered = new List<MenuItemDto>();

            foreach (var root in byParent[null].OrderBy(m => m.SortOrder).ThenBy(m => m.Label))
            {
                ordered.Add(MenuItemDto.From(root));
                ordered.AddRange(byParent[root.Id]
                    .OrderBy(m => m.SortOrder).ThenBy(m => m.Label)
                    .Select(MenuItemDto.From));
            }

            return ordered;
        }

        public async Task<MenuItemDto?> GetAsync(int id, CancellationToken ct = default)
        {
            var item = await _context.MenuItems.AsNoTracking()
                .Include(m => m.Parent)
                .FirstOrDefaultAsync(m => m.Id == id, ct);

            return item is null ? null : MenuItemDto.From(item);
        }

        public async Task<IReadOnlyList<MenuOptionDto>> GetParentOptionsAsync(
            int? excludingId, CancellationToken ct = default)
        {
            var rows = await _context.MenuItems.AsNoTracking()
                .Where(m => m.ParentId == null && (excludingId == null || m.Id != excludingId))
                .OrderBy(m => m.SortOrder).ThenBy(m => m.Label)
                .ToListAsync(ct);

            return rows.Select(m => new MenuOptionDto { Id = m.Id, Name = m.Label }).ToList();
        }

        public async Task<MenuResult> SaveAsync(
            int? id, SaveMenuItemRequest request, CancellationToken ct = default)
        {
            var error = await ValidateAsync(id, request, ct);
            if (error is not null) return MenuResult.Fail(error);

            MenuItem item;

            if (id is null)
            {
                item = new MenuItem { CreatedAt = DateTime.UtcNow };
                _context.MenuItems.Add(item);
            }
            else
            {
                var existing = await _context.MenuItems.FirstOrDefaultAsync(m => m.Id == id, ct);
                if (existing is null) return MenuResult.Fail("That menu item no longer exists.");

                item = existing;
                item.UpdatedAt = DateTime.UtcNow;
            }

            item.Label = request.Label.Trim();
            item.ParentId = request.ParentId;
            item.ControllerName = Blank(request.ControllerName);
            item.ActionName = Blank(request.ActionName);
            item.Url = Blank(request.Url);
            item.Icon = Blank(request.Icon);
            item.SortOrder = request.SortOrder;
            item.IsActive = request.IsActive;
            item.OpensInNewTab = request.OpensInNewTab;
            item.Visibility = request.Visibility;
            item.BadgeKey = Blank(request.BadgeKey);

            await _context.SaveChangesAsync(ct);

            return MenuResult.Ok(MenuItemDto.From(item));
        }

        public async Task<MenuResult> DeleteAsync(int id, CancellationToken ct = default)
        {
            var item = await _context.MenuItems.FirstOrDefaultAsync(m => m.Id == id, ct);
            if (item is null) return MenuResult.Fail("That menu item no longer exists.");

            if (await _context.MenuItems.AnyAsync(m => m.ParentId == id, ct))
                return MenuResult.Fail("Move or remove the items inside this group first.");

            _context.MenuItems.Remove(item);
            await _context.SaveChangesAsync(ct);

            return MenuResult.Ok(MenuItemDto.From(item));
        }

        public bool ActionExists(string controller, string action) =>
            _actions.ActionDescriptors.Items
                .OfType<ControllerActionDescriptor>()
                .Any(d => string.Equals(d.ControllerName, controller, StringComparison.OrdinalIgnoreCase)
                       && string.Equals(d.ActionName, action, StringComparison.OrdinalIgnoreCase));

        private async Task<string?> ValidateAsync(
            int? id, SaveMenuItemRequest request, CancellationToken ct)
        {
            if (!MenuVisibility.IsValid(request.Visibility))
                return "Choose who this item is visible to.";

            if (!MenuBadgeKeys.IsValid(request.BadgeKey))
                return "That badge is not one the application knows how to count.";

            var hasRoute = !string.IsNullOrWhiteSpace(request.ControllerName)
                        || !string.IsNullOrWhiteSpace(request.ActionName);

            if (hasRoute && (string.IsNullOrWhiteSpace(request.ControllerName)
                          || string.IsNullOrWhiteSpace(request.ActionName)))
            {
                return "Give both a controller and an action, or neither.";
            }

            if (hasRoute && !ActionExists(request.ControllerName!.Trim(), request.ActionName!.Trim()))
                return $"There is no {request.ControllerName}Controller.{request.ActionName} action to link to.";

            if (hasRoute && !string.IsNullOrWhiteSpace(request.Url))
                return "Use either a controller and action, or a URL — not both.";

            if (request.ParentId is not null)
            {
                if (request.ParentId == id)
                    return "A menu item cannot sit inside itself.";

                var parent = await _context.MenuItems.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == request.ParentId, ct);

                if (parent is null) return "Choose a valid group.";
                if (parent.ParentId is not null) return "The sidebar is only two levels deep.";
            }

            var isGroup = !hasRoute && string.IsNullOrWhiteSpace(request.Url);

            if (isGroup && request.ParentId is not null)
                return "A group heading cannot sit inside another group.";

            if (!isGroup && request.ParentId is null && id is not null
                && await _context.MenuItems.AnyAsync(m => m.ParentId == id, ct))
            {
                return "This item has children, so it has to stay a group heading.";
            }

            return null;
        }

        private static string? Blank(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public class MenuResult
    {
        public bool Succeeded { get; private init; }
        public string? Error { get; private init; }
        public MenuItemDto? Item { get; private init; }

        public static MenuResult Ok(MenuItemDto item) => new() { Succeeded = true, Item = item };
        public static MenuResult Fail(string error) => new() { Succeeded = false, Error = error };
    }
}
