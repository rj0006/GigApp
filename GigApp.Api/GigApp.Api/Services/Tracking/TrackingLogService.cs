using System.Text.Json;
using System.Text.Json.Nodes;
using GigApp.Api.Data;
using GigApp.Api.Models;

namespace GigApp.Api.Services.Tracking
{
    public interface ITrackingLogService
    {
        /// <summary>
        /// Writes one audit row. Never throws — a logging failure must not fail
        /// the operation it is recording.
        /// </summary>
        Task WriteAsync(TrackingLogEntry entry, CancellationToken ct = default);
    }

    /// <summary>What the caller knows about the operation being logged.</summary>
    public class TrackingLogEntry
    {
        public string EntryType { get; set; } = TrackingEntryType.Update;
        public string FormType { get; set; } = string.Empty;
        public string? DocNo { get; set; }
        public DateTime? DocDate { get; set; }
        public int? UserId { get; set; }
        public string? UserName { get; set; }
        public string? Remark { get; set; }
        public string? IpAddress { get; set; }
        public string? RequestPath { get; set; }

        /// <summary>The model the controller received.</summary>
        public object? Request { get; set; }

        /// <summary>
        /// The record after the operation — normally the DTO the action returned.
        /// For a delete, pass the record as it was before removal.
        /// </summary>
        public object? Result { get; set; }
    }

    public class TrackingLogService : ITrackingLogService
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            MaxDepth = 16,

            // Multipart models carry IFormFile, which cannot be serialised —
            // without this the entire payload is dropped.
            Converters = { new FormFileJsonConverter() },
        };

        private readonly AppDbContext _context;
        private readonly ILogger<TrackingLogService> _logger;

        public TrackingLogService(AppDbContext context, ILogger<TrackingLogService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task WriteAsync(TrackingLogEntry entry, CancellationToken ct = default)
        {
            try
            {
                var payload = new JsonObject
                {
                    ["request"] = JsonRedactor.ToRedactedNode(entry.Request, SerializerOptions),
                    ["result"] = JsonRedactor.ToRedactedNode(entry.Result, SerializerOptions),
                };

                _context.TrackingLogs.Add(new TrackingLog
                {
                    TransactionDate = DateTime.UtcNow,
                    EntryType = entry.EntryType,
                    FormType = entry.FormType,
                    DocNo = entry.DocNo,
                    DocDate = entry.DocDate?.ToUtc(),
                    UserId = entry.UserId,
                    UserName = entry.UserName,
                    Payload = payload.ToJsonString(),
                    Remark = string.IsNullOrWhiteSpace(entry.Remark) ? null : entry.Remark.Trim(),
                    IpAddress = entry.IpAddress,
                    RequestPath = entry.RequestPath,
                });

                await _context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                // Swallow deliberately: the user's action already succeeded, and
                // failing it now because the audit write broke would be worse.
                _logger.LogError(ex, "Failed to write tracking log for {FormType} {DocNo}",
                    entry.FormType, entry.DocNo);
            }
        }
    }
}
