namespace GigApp.Api.Services.Tracking
{
    /// <summary>
    /// HttpContext.Items keys a Razor portal action uses to hand the saved record
    /// to the audit filter. API actions do not need these — their returned DTO is
    /// picked up automatically.
    /// </summary>
    public static class TrackingKeys
    {
        public const string DocNo = "__track_docno";
        public const string Result = "__track_result";
    }

    /// <summary>
    /// Keeps an action out of the audit trail. Use for things that change no
    /// data — sign-in, sign-out — not to hide sensitive writes; secrets are
    /// handled by <see cref="JsonRedactor"/> instead.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class SkipTrackingAttribute : Attribute { }

    /// <summary>
    /// Overrides the entry type. The HTTP verb is the default, but a POST is not
    /// always an insert — re-uploading KYC is a POST because it is multipart,
    /// yet it updates an existing partner.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TrackEntryAttribute : Attribute
    {
        public string EntryType { get; }
        public TrackEntryAttribute(string entryType) => EntryType = entryType;
    }

    /// <summary>
    /// Overrides the FormType written to the log. Without it the controller name
    /// is used, which is usually right — set this when one controller writes to
    /// several entities.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class TrackFormAttribute : Attribute
    {
        public string FormType { get; }
        public TrackFormAttribute(string formType) => FormType = formType;
    }
}
