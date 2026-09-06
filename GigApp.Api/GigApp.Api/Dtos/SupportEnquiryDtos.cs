using System.ComponentModel.DataAnnotations;
using GigApp.Api.Models;

namespace GigApp.Api.Dtos
{
    public class SupportEnquiryDto
    {
        public int Id { get; set; }
        public int GigTaskId { get; set; }
        public string? TaskDescription { get; set; }
        public string? CategoryName { get; set; }

        public int RaisedByUserId { get; set; }
        public string? RaisedByName { get; set; }
        public string RaisedByRole { get; set; } = string.Empty;

        public string Topic { get; set; } = EnquiryTopics.Other;
        public string TopicLabel => EnquiryTopics.Label(Topic);

        public string Message { get; set; } = string.Empty;

        public string Status { get; set; } = EnquiryStatus.Open;
        public string StatusLabel => EnquiryStatus.Label(Status);
        public string StatusBadgeClass => EnquiryStatus.BadgeClass(Status);
        public bool IsLive => EnquiryStatus.IsLive(Status);

        public string? Resolution { get; set; }
        public string? ResolvedByName { get; set; }
        public DateTime? ResolvedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public string Reference => $"SUP-{Id}";

        public static SupportEnquiryDto From(SupportEnquiry enquiry) => new()
        {
            Id = enquiry.Id,
            GigTaskId = enquiry.GigTaskId,
            TaskDescription = enquiry.GigTask?.Description,
            CategoryName = enquiry.GigTask?.Category?.Name,
            RaisedByUserId = enquiry.RaisedByUserId,
            RaisedByName = enquiry.RaisedByUser?.Name,
            RaisedByRole = enquiry.RaisedByRole,
            Topic = enquiry.Topic,
            Message = enquiry.Message,
            Status = enquiry.Status,
            Resolution = enquiry.Resolution,
            ResolvedByName = enquiry.ResolvedByUser?.Name,
            ResolvedAt = enquiry.ResolvedAt,
            CreatedAt = enquiry.CreatedAt,
        };
    }

    public class RaiseEnquiryRequest
    {
        [Required(ErrorMessage = "Choose what the problem is about.")]
        public string Topic { get; set; } = EnquiryTopics.Other;

        [Required(ErrorMessage = "Describe the problem.")]
        [StringLength(1000, MinimumLength = 10,
            ErrorMessage = "Describe the problem in at least ten characters.")]
        public string Message { get; set; } = string.Empty;
    }

    public class ResolveEnquiryRequest
    {
        [Required]
        public string Status { get; set; } = EnquiryStatus.Resolved;

        [StringLength(1000)]
        public string? Resolution { get; set; }
    }
}
