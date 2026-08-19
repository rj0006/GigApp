using GigApp.Api.Data;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Files;
using GigApp.Api.Services.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Controllers
{
    /// <summary>
    /// Serves uploads that must not be publicly reachable. Profile pictures are
    /// plain static files under wwwroot; KYC documents are identity papers, so
    /// they live outside wwwroot and every read passes through here.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [SkipTracking]   // reads only
    public class FilesController : ControllerBase
    {
        private static readonly FileExtensionContentTypeProvider ContentTypes = new();

        private readonly IFileStorageService _storage;
        private readonly AppDbContext _context;

        public FilesController(IFileStorageService storage, AppDbContext context)
        {
            _storage = storage;
            _context = context;
        }

        // GET: api/files/kyc/{fileName}
        [HttpGet("kyc/{fileName}")]
        public async Task<IActionResult> GetKycDocument(string fileName, CancellationToken ct)
        {
            // Admins review every document; a partner may only see their own.
            if (!User.IsAdmin())
            {
                var userId = User.GetRequiredUserId();

                var ownsIt = await _context.Partners.AnyAsync(
                    p => p.UserId == userId
                      && (p.SelfieFileName == fileName
                       || p.AadhaarFrontFileName == fileName
                       || p.AadhaarBackFileName == fileName), ct);

                if (!ownsIt) return Forbid();
            }

            var path = _storage.ResolvePath(fileName, FileCategory.KycDocument);
            if (path is null) return NotFound();

            if (!ContentTypes.TryGetContentType(path, out var contentType))
                contentType = "application/octet-stream";

            // inline so an admin can view it in the browser rather than download it
            return PhysicalFile(path, contentType);
        }
    }
}
