using System.Data.Common;
using GigApp.Api.Data;
using GigApp.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Pricing
{
    /// <summary>
    /// Turns completed work into a view of what a service is actually worth.
    ///
    /// Deliberately reads <c>AgreedAmount</c> and not the bid asking price:
    /// partners ask high expecting to negotiate, so asks overstate the rate.
    /// It also reports quartiles rather than an average, because one large job
    /// drags a mean far away from what people really pay.
    /// </summary>
    public interface IPriceInsightService
    {
        Task<IReadOnlyList<PriceInsightDto>> GetAsync(CancellationToken ct = default);
        Task<PriceInsightDto?> GetForItemAsync(int serviceItemId, CancellationToken ct = default);
    }

    public class PriceInsightService : IPriceInsightService
    {
        /// <summary>
        /// percentile_cont is a Postgres window function EF cannot express, so
        /// this runs as raw SQL. Only completed tasks with a settled amount
        /// count — pending and cancelled work says nothing about price.
        /// </summary>
        private const string InsightSql = @"
            SELECT  s.""Id""                AS service_item_id,
                    s.""Name""              AS service_item_name,
                    c.""Name""              AS category_name,
                    s.""BasePayout""        AS current_payout,
                    COUNT(t.""Id"")         AS completed_count,
                    percentile_cont(0.5)  WITHIN GROUP (ORDER BY t.""AgreedAmount"") AS median_amount,
                    percentile_cont(0.25) WITHIN GROUP (ORDER BY t.""AgreedAmount"") AS lower_quartile,
                    percentile_cont(0.75) WITHIN GROUP (ORDER BY t.""AgreedAmount"") AS upper_quartile
              FROM  ""ServiceItems"" s
              JOIN  ""SkillCategories"" c ON c.""Id"" = s.""SkillCategoryId""
              LEFT JOIN ""GigTasks"" t
                    ON  t.""ServiceItemId"" = s.""Id""
                    AND t.""Status"" = 'completed'
                    AND t.""AgreedAmount"" IS NOT NULL
             GROUP BY s.""Id"", s.""Name"", c.""Name"", s.""BasePayout""
             ORDER BY COUNT(t.""Id"") DESC, c.""Name"", s.""Name"";";

        private readonly AppDbContext _context;

        public PriceInsightService(AppDbContext context) => _context = context;

        public async Task<IReadOnlyList<PriceInsightDto>> GetAsync(CancellationToken ct = default)
        {
            var results = new List<PriceInsightDto>();

            var connection = _context.Database.GetDbConnection();
            var opened = connection.State != System.Data.ConnectionState.Open;
            if (opened) await connection.OpenAsync(ct);

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = InsightSql;

                await using var reader = await command.ExecuteReaderAsync(ct);

                while (await reader.ReadAsync(ct))
                {
                    results.Add(new PriceInsightDto
                    {
                        ServiceItemId = reader.GetInt32(0),
                        ServiceItemName = reader.GetString(1),
                        CategoryName = reader.GetString(2),
                        CurrentPayout = GetNullableDecimal(reader, 3),
                        CompletedCount = (int)reader.GetInt64(4),
                        MedianAmount = GetNullableDecimal(reader, 5),
                        LowerQuartile = GetNullableDecimal(reader, 6),
                        UpperQuartile = GetNullableDecimal(reader, 7),
                    });
                }
            }
            finally
            {
                if (opened) await connection.CloseAsync();
            }

            return results;
        }

        public async Task<PriceInsightDto?> GetForItemAsync(int serviceItemId, CancellationToken ct = default)
        {
            var all = await GetAsync(ct);
            return all.FirstOrDefault(i => i.ServiceItemId == serviceItemId);
        }

        /// <summary>percentile_cont returns double; the column is numeric. Both arrive untyped.</summary>
        private static decimal? GetNullableDecimal(DbDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return null;

            var value = reader.GetValue(ordinal);
            return Math.Round(Convert.ToDecimal(value), 2);
        }
    }
}
