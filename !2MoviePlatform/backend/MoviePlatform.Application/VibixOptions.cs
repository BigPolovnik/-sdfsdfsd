namespace MoviePlatform.Application;

public sealed class VibixOptions
{
    public string BaseUrl { get; set; } = "https://vibix.org";
    public string ApiToken { get; set; } = "";
    public string AdminKey { get; set; } = "";

    public int SyncIntervalMinutes { get; set; } = 360;

    // Vibix проверен с limit=50 и limit=100.
    // Для первой синхронизации используем 50.
    public int PageSize { get; set; } = 50;

    // Автосинхронизацию включим после успешного ручного теста.
    public bool AutoSync { get; set; } = false;
}

public sealed record VibixSyncResult(
    int Read,
    int Created,
    int Updated,
    int Skipped,
    string? Error,
    DateTime FinishedAtUtc);
