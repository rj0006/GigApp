namespace GigApp.Api.Models
{
    public class TaskCancellation
    {
        public int Id { get; set; }

        public int GigTaskId { get; set; }
        public GigTask? GigTask { get; set; }

        public int PartnerId { get; set; }
        public Partner? Partner { get; set; }

        public string? Reason { get; set; }
        public DateTime CancelledAt { get; set; } = DateTime.UtcNow;
    }
}
