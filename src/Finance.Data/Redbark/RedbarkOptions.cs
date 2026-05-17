namespace Finance.Data.Redbark;

public sealed class RedbarkOptions
{
    public string BaseUrl { get; set; } = "https://api.redbark.example";
    public string ApiKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
}
