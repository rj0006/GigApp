using System.Text.Json;
using System.Text.Json.Nodes;

namespace GigApp.Api.Services.Tracking
{
    /// <summary>
    /// Strips secrets before anything is written to the audit trail. Registration
    /// and login models carry a plaintext password, and the log table is read by
    /// more people than the users table — those values must never land in it.
    /// </summary>
    public static class JsonRedactor
    {
        private const string Mask = "***";

        /// <summary>Matched case-insensitively against property names, as a substring.</summary>
        private static readonly string[] SensitiveKeys =
        {
            "password",
            "passwordhash",
            "confirmpassword",
            "currentpassword",
            "newpassword",
            "token",
            "secret",
            "otp",
            "apikey",

            // The identity number only. Not a bare "aadhaar" — that would also
            // mask aadhaarFront / aadhaarBack, which are just file metadata and
            // are useful in the log.
            "aadhaarnumber",
        };

        public static JsonNode? Redact(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject obj:
                    // Materialise the keys first — the collection is edited in the loop.
                    foreach (var key in obj.Select(p => p.Key).ToList())
                    {
                        if (IsSensitive(key))
                        {
                            obj[key] = Mask;
                            continue;
                        }

                        obj[key] = Redact(obj[key]);
                    }
                    return obj;

                case JsonArray array:
                    for (var i = 0; i < array.Count; i++)
                        array[i] = Redact(array[i]);
                    return array;

                default:
                    return node;
            }
        }

        /// <summary>Serialises and redacts in one step. Never throws — a logging
        /// failure must not break the request it is logging.</summary>
        public static JsonNode? ToRedactedNode(object? value, JsonSerializerOptions options)
        {
            if (value is null) return null;

            try
            {
                var node = JsonSerializer.SerializeToNode(value, value.GetType(), options);
                return Redact(node);
            }
            catch
            {
                return JsonValue.Create($"<unserialisable {value.GetType().Name}>");
            }
        }

        private static bool IsSensitive(string key) =>
            SensitiveKeys.Any(s => key.Contains(s, StringComparison.OrdinalIgnoreCase));
    }
}
