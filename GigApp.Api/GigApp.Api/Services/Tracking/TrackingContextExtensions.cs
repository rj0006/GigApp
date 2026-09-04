namespace GigApp.Api.Services.Tracking
{
    public static class TrackingContextExtensions
    {
        public static void TrackDoc(this HttpContext context, object? docNo, object? result = null)
        {
            context.Items[TrackingKeys.DocNo] = docNo;
            context.Items[TrackingKeys.Result] = result;
        }
    }
}
