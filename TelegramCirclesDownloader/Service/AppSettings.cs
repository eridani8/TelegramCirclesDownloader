namespace TelegramCirclesDownloader.Service;

public class AppSettings
{
    public required int GetPagesDelay { get; init; }
    public required string ChromakeyColor { get; init; }
    public required float Similarity { get; init; }
    public required float Blend { get; init; }
}