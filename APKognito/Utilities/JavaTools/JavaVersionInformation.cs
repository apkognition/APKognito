using System.IO;

namespace APKognito.Utilities.JavaTools;

public record JavaVersionInformation
{
    public string Vendor { get; init; }

    public string JavaPath { get; init; }

    public Version Version { get; init; }

    public string RawVersion { get; init; }

    public JavaType JavaType { get; set; }

    public JavaVersionInformation(string javaPath, Version version, string rawVersion)
    {
        JavaPath = javaPath;
        Version = version;
        RawVersion = rawVersion;
    }

    internal JavaVersionInformation(string vendor, string javaHomePath, string rawVersion, JavaType type)
    {
        if (!Version.TryParse(TrimVersionString(rawVersion), out Version? version))
        {
            throw new InvalidJavaRegistryException($"Invalid and untrimmable Java version '{rawVersion}'");
        }

        JavaPath = Path.Combine(javaHomePath, "bin\\java.exe");

        Vendor = vendor;
        Version = version;
        RawVersion = rawVersion;

        JavaType = type;
    }

    public bool UpToDate => VersionUpToDate(Version, RawVersion);

    public string FixedRawVersion => TrimVersionString(RawVersion);

    public static bool VersionUpToDate(Version version, string rawVersion)
    {
        return
            // Java versions 8-22
            version.Major == 1 && version.Minor >= 8
            // Formatting for Java versions 23+ (or the JAVA_HOME path)
            || int.TryParse(rawVersion.Split('.')[0], out int major) && major >= 9;
    }

    public override string ToString()
    {
        return $"{JavaType} {Version.Major} ({RawVersion}, {Vendor})";
    }

    private static string TrimVersionString(string rawVersion)
    {
        int underscoreIndex = rawVersion.IndexOf('_');

        return underscoreIndex is not -1
            ? rawVersion[..underscoreIndex]
            : rawVersion;
    }

    public sealed class InvalidJavaRegistryException(string message) : Exception(message)
    {
    }
}

public enum JavaType
{
    Unknown,
    JDK,
    JRE
}
