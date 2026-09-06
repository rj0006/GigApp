namespace GigApp.Api.Dtos
{
    public class PartnerMatchDto
    {
        public int PartnerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        public decimal? AverageRating { get; set; }
        public int RatingCount { get; set; }
        public int RoundedRating => AverageRating is null
            ? 0
            : (int)Math.Round(AverageRating.Value, MidpointRounding.AwayFromZero);

        public bool IsAvailable { get; set; }
        public int ServiceRadiusKm { get; set; }

        public double? DistanceKm { get; set; }
        public string DistanceLabel => Services.Geo.GeoPoint.Describe(DistanceKm);

        public bool HasLocation => DistanceKm is not null;

        /// <summary>Beyond what the partner said they would travel.</summary>
        public bool IsOutOfRange => DistanceKm is not null && DistanceKm > ServiceRadiusKm;

        /// <summary>
        /// Distance and rating, nothing else. Distance dominates because a
        /// plumber four kilometres away with no ratings is a better answer than
        /// a five-star one across the city, and a partner with no pin at all
        /// scores below everyone who has one rather than being hidden.
        /// </summary>
        public double Score
        {
            get
            {
                var proximity = DistanceKm switch
                {
                    null => 0.35,
                    <= 2 => 1.0,
                    <= 5 => 0.8,
                    <= 10 => 0.6,
                    <= 20 => 0.35,
                    _ => 0.1,
                };

                // An unrated partner sits at the midpoint rather than at zero,
                // or nobody new would ever be picked.
                var quality = AverageRating is null
                    ? 0.5
                    : (double)(AverageRating.Value - 1) / 4;

                var confidence = Math.Min(RatingCount, 10) / 10d;
                var rated = quality * (0.5 + confidence * 0.5);

                var score = proximity * 0.6 + rated * 0.4;

                if (IsOutOfRange) score *= 0.4;
                if (!IsAvailable) score *= 0.5;

                return Math.Round(score, 4);
            }
        }
    }
}
