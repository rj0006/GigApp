using System.Text.Json;
using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Kyc
{
    public interface IKycHistoryService
    {
        Task<IReadOnlyList<KycHistoryEntryDto>> ForPartnerAsync(
            int partnerId, CancellationToken ct = default);

        Task<IReadOnlyDictionary<int, IReadOnlyList<KycHistoryEntryDto>>> ForPartnersAsync(
            IReadOnlyCollection<int> partnerIds, CancellationToken ct = default);
    }

    public class KycHistoryService : IKycHistoryService
    {
        public const string FormType = "Partner";

        private readonly AppDbContext _context;

        public KycHistoryService(AppDbContext context) => _context = context;

        public async Task<IReadOnlyList<KycHistoryEntryDto>> ForPartnerAsync(
            int partnerId, CancellationToken ct = default)
        {
            var all = await ForPartnersAsync(new[] { partnerId }, ct);
            return all.TryGetValue(partnerId, out var entries)
                ? entries
                : Array.Empty<KycHistoryEntryDto>();
        }

        public async Task<IReadOnlyDictionary<int, IReadOnlyList<KycHistoryEntryDto>>> ForPartnersAsync(
            IReadOnlyCollection<int> partnerIds, CancellationToken ct = default)
        {
            var result = new Dictionary<int, IReadOnlyList<KycHistoryEntryDto>>();
            if (partnerIds.Count == 0) return result;

            var docNos = partnerIds.Select(id => id.ToString()).ToList();

            var rows = await _context.TrackingLogs
                .AsNoTracking()
                .Where(t => t.FormType == FormType && t.DocNo != null && docNos.Contains(t.DocNo))
                .OrderBy(t => t.TransactionDate).ThenBy(t => t.Id)
                .Select(t => new
                {
                    t.DocNo,
                    t.TransactionDate,
                    t.EntryType,
                    t.UserName,
                    t.Remark,
                    t.RequestPath,
                    t.Payload,
                })
                .ToListAsync(ct);

            foreach (var group in rows.GroupBy(r => r.DocNo))
            {
                if (!int.TryParse(group.Key, out var partnerId)) continue;

                var entries = new List<KycHistoryEntryDto>();
                string? previousStatus = null;
                string? previousSkill = null;

                foreach (var row in group)
                {
                    var snapshot = ReadResult(row.Payload);

                    var status = snapshot.Status ?? previousStatus ?? KycStatus.NotSubmitted;
                    var entry = new KycHistoryEntryDto
                    {
                        At = row.TransactionDate,
                        By = row.UserName,
                        Remark = row.Remark,
                        Status = status,
                        Action = Describe(row.EntryType, row.RequestPath, snapshot, previousStatus, previousSkill),
                        Detail = Detail(snapshot, previousSkill),
                    };

                    entries.Add(entry);
                    previousStatus = status;
                    previousSkill = snapshot.Skill ?? previousSkill;
                }

                entries.Reverse();
                result[partnerId] = entries;
            }

            return result;
        }

        private static string Describe(
            string entryType, string? path, Snapshot snapshot, string? previousStatus, string? previousSkill)
        {
            if (snapshot.Status == KycStatus.Approved && previousStatus != KycStatus.Approved)
                return "Approved";

            if (snapshot.Status == KycStatus.Rejected)
                return "Rejected";

            if (snapshot.ReviewNote is not null
                && snapshot.ReviewNote.StartsWith("Skill changed", StringComparison.Ordinal))
            {
                return "Skill changed";
            }

            if (previousSkill is not null && snapshot.Skill is not null && snapshot.Skill != previousSkill)
                return "Skill changed";

            if (entryType == TrackingEntryType.Insert && previousStatus is null)
                return "Registered with documents";

            if (path is not null && path.Contains("kyc", StringComparison.OrdinalIgnoreCase))
                return "Documents uploaded";

            return snapshot.Status == KycStatus.Pending ? "Sent for review" : "Updated";
        }

        private static string? Detail(Snapshot snapshot, string? previousSkill)
        {
            if (!string.IsNullOrWhiteSpace(snapshot.RejectionReason))
                return snapshot.RejectionReason;

            if (!string.IsNullOrWhiteSpace(snapshot.ReviewNote))
                return snapshot.ReviewNote;

            if (snapshot.Skill is not null && previousSkill is not null && snapshot.Skill != previousSkill)
                return $"{previousSkill} to {snapshot.Skill}";

            return snapshot.Skill;
        }

        private static Snapshot ReadResult(string payload)
        {
            try
            {
                using var document = JsonDocument.Parse(payload);

                if (!document.RootElement.TryGetProperty("result", out var result)
                    || result.ValueKind != JsonValueKind.Object)
                {
                    return new Snapshot();
                }

                return new Snapshot
                {
                    Status = Text(result, "kycStatus"),
                    Skill = Text(result, "skillCategoryName"),
                    RejectionReason = Text(result, "kycRejectionReason"),
                    ReviewNote = Text(result, "kycReviewNote"),
                };
            }
            catch (JsonException)
            {
                return new Snapshot();
            }
        }

        private static string? Text(JsonElement element, string property) =>
            element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        private sealed class Snapshot
        {
            public string? Status { get; init; }
            public string? Skill { get; init; }
            public string? RejectionReason { get; init; }
            public string? ReviewNote { get; init; }
        }
    }
}
