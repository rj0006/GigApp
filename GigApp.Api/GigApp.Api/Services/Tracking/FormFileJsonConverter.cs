using System.Text.Json;
using System.Text.Json.Serialization;

namespace GigApp.Api.Services.Tracking
{
    /// <summary>
    /// Writes an uploaded file as its metadata instead of its bytes.
    ///
    /// Without this, System.Text.Json throws on any multipart request model and
    /// the whole payload is lost — which is exactly how partner registration
    /// ended up with no audit record at all. The bytes are on disk anyway; what
    /// the log needs is what was uploaded, not the file itself.
    /// </summary>
    public class FormFileJsonConverter : JsonConverter<IFormFile>
    {
        public override bool CanConvert(Type typeToConvert) =>
            typeof(IFormFile).IsAssignableFrom(typeToConvert);

        public override IFormFile Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException("Audit payloads are write-only.");

        public override void Write(Utf8JsonWriter writer, IFormFile value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("originalFileName", value.FileName);
            writer.WriteString("contentType", value.ContentType);
            writer.WriteNumber("sizeBytes", value.Length);
            writer.WriteEndObject();
        }
    }
}
