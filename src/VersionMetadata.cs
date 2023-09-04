using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MKUtils;

public static partial class VersionMetadata
{
    internal static CoreDTO Core;
    internal static ProgramDTO Program;
    internal static InstallerDTO Installer;

    public static string ProgramDisplayName => Program.DisplayName;
    public static string ProgramVersion => Program.Version;
    public static string ProgramAuthor => Program.Author;
    public static Dictionary<string, string> ProgramDownloadLink => Program.Download;
    public static Dictionary<string, string> ProgramLaunchFile => Program.LaunchFile;
    public static string[] ProgramFileAssociations => Program.FileAssociations;
    public static string ProgramInstallPath => Program.InstallPath;
    public static string ProgramEULAText => Program.EULA;

    public static Dictionary<string, string> CoreLibraryDownloadLink => Core.Download;
	public static string CoreLibraryPath => Core.InstallPath;
    public static Dictionary<string, string[]> RequiredFiles => Core.RequiredFiles;

    public static string InstallerInstallPath => Installer.InstallPath;
    public static Dictionary<string, string> InstallerInstallFilename => Installer.InstallFilename;
    public static string InstallerVersion => Installer.Version;
	public static Dictionary<string, string> InstallerDownloadLink => Installer.Download;

	//internal static readonly string[] Sources =
	//{
	//	"https://link-to-your-metadata-file.com/"
	//};

	public static bool Load(DynamicCallbackManager<DownloadProgress>? callbackManager = null)
    {
        Logger.Instance?.WriteLine("Attempting to load metadata from the internet.");
        bool success = false;
        Type vType = typeof(VersionMetadata);
        string[] sources = new string[0];
        MemberInfo[] members = vType.GetMember("Sources", MemberTypes.Field, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.GetField);
        if (members.Length > 0)
        {
            object sourcesObj = vType.InvokeMember("Sources", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.GetField, null, null, null);
            if (sourcesObj is not null && sourcesObj is string[]) sources = (string[]) sourcesObj;
        }
        foreach (string metadataDownload in sources)
        {
            try
            {
                Logger.Instance?.WriteLine($"Trying source '{metadataDownload}'...");
                Stream dataStream = Downloader.DownloadStream(metadataDownload, null, callbackManager);
                Logger.Instance?.WriteLine("Obtained content from the source. Attempting to deserialize as JSON...");
                var data = JsonSerializer.Deserialize<MainDTO>(dataStream, new JsonSerializerOptions() { IncludeFields = true });
                Logger.Instance?.WriteLine("Metadata is valid!");
                data.Program.Version = MKUtils.TrimVersion(data.Program.Version);
                data.Installer.Version = MKUtils.TrimVersion(data.Installer.Version);
                Core = data.Core;
                Program = data.Program;
                Installer = data.Installer;
                success = true;
                break;
            }
            catch (Exception ex)
            {
                Logger.Instance?.Error($"Source failed: {ex.Message}\n{ex.StackTrace}");
            }
        }
        if (sources.Length == 0) Logger.Instance?.WriteLine("No metadata sources found.");
        else Logger.Instance?.WriteLine($"Metadata successfully read: {success}");
        return success;
    }

    public static int CompareVersions(string version1, string version2)
    {
        List<string> split1 = version1.Split('.').ToList();
        List<string> split2 = version2.Split('.').ToList();
        int maxLength = Math.Max(split1.Count, split2.Count);
        for (int i = 0; i < maxLength; i++)
        {
            string? num1 = i < split1.Count ? split1[i] : null;
            string? num2 = i < split2.Count ? split2[i] : null;
            if (num1 is null && num2 == "0" || num1 == "0" && num2 is null) continue;
            if (num1 is null) return -1;
            if (num2 is null) return 1;
            int cmp = num1.CompareTo(num2);
            if (cmp != 0) return cmp;
        }
        return 0;
    }
}

internal struct MainDTO
{
    [JsonPropertyName("program")]
    public ProgramDTO Program;
    [JsonPropertyName("core")]
    public CoreDTO Core;
    [JsonPropertyName("installer")]
    public InstallerDTO Installer;
}

internal struct ProgramDTO
{
    [JsonPropertyName("display_name")]
    public string DisplayName;
    [JsonPropertyName("version")]
    public string Version;
    [JsonPropertyName("author")]
    public string Author;
    [JsonPropertyName("download")]
    public Dictionary<string, string> Download;
    [JsonPropertyName("launch_file")]
    public Dictionary<string, string> LaunchFile;
    [JsonPropertyName("install_path")]
    public string InstallPath;
    [JsonPropertyName("file_associations")]
    public string[] FileAssociations;
    [JsonPropertyName("eula")]
    public string[] RawEULA;

    public string EULA => RawEULA.Aggregate((a, b) => a + '\n' + b);
}

internal struct CoreDTO
{
    [JsonPropertyName("install_path")]
    public string InstallPath;
    [JsonPropertyName("download")]
    public Dictionary<string, string> Download;
    [JsonPropertyName("required_files")]
    public Dictionary<string, string[]> RequiredFiles;
}

internal struct InstallerDTO
{
    [JsonPropertyName("install_path")]
    public string InstallPath;
    [JsonPropertyName("install_filename")]
    public Dictionary<string, string> InstallFilename;
    [JsonPropertyName("version")]
    public string Version;
    [JsonPropertyName("download")]
    public Dictionary<string, string> Download;
}
