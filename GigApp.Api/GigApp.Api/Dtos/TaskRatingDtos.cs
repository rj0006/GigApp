using System.ComponentModel.DataAnnotations;
using GigApp.Api.Models;

namespace GigApp.Api.Dtos
{
    public class TaskRatingDto
    {
        public int GigTaskId { get; set; }
        public string RaterRole { get; set; } = string.Empty;
        public int Stars { get; set; }
        public string StarsLabel => RatingScale.Label(Stars);
        public string? Feedback { get; set; }
        public DateTime CreatedAt { get; set; }

        public static TaskRatingDto From(TaskRating rating) => new()
        {
            GigTaskId = rating.GigTaskId,
            RaterRole = rating.RaterRole,
            Stars = rating.Stars,
            Feedback = rating.Feedback,
            CreatedAt = rating.CreatedAt,
        };
    }

    public class RateTaskRequest
    {
        [Range(RatingScale.Minimum, RatingScale.Maximum,
            ErrorMessage = "Choose between one and five stars.")]
        public int Stars { get; set; }

        [StringLength(500)]
        public string? Feedback { get; set; }
    }
}
