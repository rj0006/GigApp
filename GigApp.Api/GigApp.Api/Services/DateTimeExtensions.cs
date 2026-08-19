namespace GigApp.Api.Services
{
    public static class DateTimeExtensions
    {
        /// <summary>
        /// Coerces a client-supplied timestamp to UTC. Npgsql rejects any
        /// non-UTC DateTime bound to a <c>timestamp with time zone</c> column,
        /// and JSON without an offset deserializes as Unspecified — which would
        /// otherwise throw at save time.
        /// </summary>
        public static DateTime ToUtc(this DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

        public static DateTime? ToUtc(this DateTime? value) => value?.ToUtc();
    }
}
