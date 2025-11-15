namespace SS14.Watchdog.Components.ServerManagement.ApiModels;

public class ReplayFileInfo
{
    public required string FileName { get; set; }

    public required long FileSize { get; set; }

    public static ReplayFileInfo Create(string fileName, long fileSize)
    {
        return new()
        {
            FileName = fileName,
            FileSize = fileSize
        };
    }
}
