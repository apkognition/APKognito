namespace APKognito.ApkLib;

public sealed record ProgressInfo(
    string Data,
    ProgressUpdateType UpdateType
);

public enum ProgressUpdateType : byte
{
    Content,
    Title,
    Reset,
}
