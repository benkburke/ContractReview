namespace ContractReview.Api.Http;

public static class HeaderNames
{
    public const string Location = "Location";
    public const string LocationDescription = "URL of the created resource.";
    public const string SignalRConnectionId = "X-SignalR-Connection-Id";
    public const string SignalRConnectionIdDescription = "Optional SignalR connection id that receives live evaluation progress.";
    public const int MaxConnectionIdLength = 200;
    public const string FormUrlEncoded = "application/x-www-form-urlencoded";
}
