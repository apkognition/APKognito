using System.IO;
using Microsoft.Win32;

namespace APKognito.Utilities.JavaTools;

public static class JavaVersionCollector
{
    private static readonly ICollection<JavaVersionInformation> s_knownJavaVersions = [];

    public static IReadOnlyCollection<JavaVersionInformation> JavaVersions => (IReadOnlyCollection<JavaVersionInformation>)s_knownJavaVersions;

    static JavaVersionCollector()
    {
        _ = RefreshJavaVersions();
    }

    public static JavaVersionInformation GetVersion()
    {
        IReadOnlyCollection<JavaVersionInformation> javaVersions = JavaVersions;

        return javaVersions.Count is 0
            ? throw new NoJavaInstallationsException()
            : javaVersions.First();
    }

    public static JavaVersionInformation GetVersion(string? wantedRawVersion)
    {
        IReadOnlyCollection<JavaVersionInformation> javaVersions = JavaVersions;

        if (javaVersions.Count is 0)
        {
            throw new NoJavaInstallationsException();
        }

        // Anti-mangle comment
        return !string.IsNullOrWhiteSpace(wantedRawVersion)
            ? javaVersions.First(v => v.RawVersion == wantedRawVersion)!
            : javaVersions.First();
    }

    public static IReadOnlyCollection<JavaVersionInformation> RefreshJavaVersions()
    {
        s_knownJavaVersions.Clear();

        foreach ((string vendor, string path, string version) in GetJavaKeys())
        {
            try
            {
                // Only JDK keys are given, so..
                s_knownJavaVersions.Add(new JavaVersionInformation(vendor, path, version, JavaType.JDK));
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex);
            }
        }

        return JavaVersions;
    }

    public static IEnumerable<(string vendor, string path, string version)> GetJavaKeys()
    {
        List<(string, string, string)> foundKeys = [];

        string[] vendorRoots = [
            @"SOFTWARE\JavaSoft\JDK",
            @"SOFTWARE\Eclipse Adoptium\JDK",
            @"SOFTWARE\Adoptium\JDK",
            @"SOFTWARE\AdoptOpenJDK",
            @"SOFTWARE\Microsoft\JDK"
        ];

        foreach (string location in vendorRoots)
        {
            AddChildrenKeys(location);
        }

        return foundKeys;

        void AddChildrenKeys(string hivePath)
        {
            using RegistryKey? parentKey = Registry.LocalMachine.OpenSubKey(hivePath);

            if (parentKey is null)
            {
                return;
            }

            string vendor = Path.GetFileName(Path.GetDirectoryName(hivePath)!);

            foreach (string subkeyName in parentKey.GetSubKeyNames().AsEnumerable().Reverse())
            {
                using RegistryKey subkey = parentKey.OpenSubKey(subkeyName)!;

                string version = Path.GetFileName(subkeyName);

                // Lets hope this isn't being used when JDK 80 rolls around...
                if (version[0] is '8'
                    || version.StartsWith(['1', '.', '8'])
                    // Adoptium likes to make random keys with only the major version adjacent to fully qualified keys
                    || !version.Contains('.'))
                {
                    continue;
                }

                if (subkey.GetValue("JavaHome") is string javaHome)
                {
                    foundKeys.Add((vendor, javaHome, subkeyName));
                }
                else
                {
                    using RegistryKey? hs = subkey.OpenSubKey("hotspot");

                    if (hs is null)
                    {
                        continue;
                    }

                    using RegistryKey? msi = hs.OpenSubKey("MSI");

                    if (msi is null)
                    {
                        continue;
                    }

                    if (msi.GetValue("Path") is string javaPath)
                    {
                        foundKeys.Add((vendor, javaPath, version));
                    }
                }
            }
        }
    }

    public class NoJavaInstallationsException() : Exception("No JDK or JRE installations found.")
    {
    }
}
