using NetTopologySuite.Geometries;

namespace GigApp.Api.Services.Geo
{
    /// <summary>
    /// Turns a latitude/longitude pair into the geography point PostGIS stores.
    /// Nothing else in the application constructs one, so the SRID and the
    /// coordinate order are decided exactly once.
    /// </summary>
    public static class GeoPoint
    {
        /// <summary>WGS 84 — what a phone GPS and every map provider report.</summary>
        public const int Srid = 4326;

        public const double MetresPerKm = 1000d;

        /// <summary>
        /// PostGIS takes X then Y, which is longitude then latitude. Swapping
        /// them silently puts Gurugram in the Indian Ocean.
        /// </summary>
        public static Point? From(double? latitude, double? longitude) =>
            latitude is null || longitude is null
                ? null
                : new Point(longitude.Value, latitude.Value) { SRID = Srid };

        public static double KmFromMetres(double metres) => metres / MetresPerKm;

        public static string Describe(double? km) => km switch
        {
            null => "distance unknown",
            < 1 => $"{Math.Round(km.Value * 1000 / 50) * 50:N0} m away",
            < 10 => $"{km.Value:0.0} km away",
            _ => $"{km.Value:0} km away",
        };
    }
}
