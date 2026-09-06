using GigApp.Api.Models;

namespace GigApp.Api.Dtos
{
    public class TaskOfferDto
    {
        public int Id { get; set; }
        public int GigTaskId { get; set; }
        public int PartnerId { get; set; }
        public int Rank { get; set; }
        public string Status { get; set; } = OfferStatus.Pending;
        public DateTime OfferedAt { get; set; }
        public DateTime ExpiresAt { get; set; }

        public GigTaskDto? Task { get; set; }

        public int SecondsLeft =>
            Math.Max(0, (int)(ExpiresAt - DateTime.UtcNow).TotalSeconds);

        public bool IsLive => Status == OfferStatus.Pending && SecondsLeft > 0;

        public static TaskOfferDto From(TaskOffer offer) => new()
        {
            Id = offer.Id,
            GigTaskId = offer.GigTaskId,
            PartnerId = offer.PartnerId,
            Rank = offer.Rank,
            Status = offer.Status,
            OfferedAt = offer.OfferedAt,
            ExpiresAt = offer.ExpiresAt,
            Task = offer.GigTask is null ? null : GigTaskDto.From(offer.GigTask),
        };
    }
}
