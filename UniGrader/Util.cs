using System.Diagnostics;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.Tar;
using UniGrader.Models;

namespace UniGrader;
using System.IO;

public static class Util
{
    private static string BasePath => AppDomain.CurrentDomain.BaseDirectory;
    
    /// <summary>
    /// Path which will contain our config files and submission data
    /// </summary>
    internal static string PlatformDataPath => Path.Join(BasePath, "PlatformData");

    /// <summary>
    /// Path which will contain cloned repositories
    /// </summary>
    internal static string SubmissionDataPath => Path.Join(BasePath, "SubmissionData");

    /// <summary>
    /// Path to file templates
    /// </summary>
    internal static string TemplatesPath => Path.Join(BasePath, "Templates");

    /// <summary>
    /// Path to log output
    /// </summary>
    internal static string LogsPath => Path.Join(BasePath, "Logs");

    /// <summary>
    /// Regex which will grab the last segment of URL
    /// </summary>
    internal static Regex LastSegmentOfUrlRegex = new("[^/]+(?=/$|$)");

    internal static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    internal static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public static string AsPercent(double value) => value.ToString("0.000000%");
    
    /// <summary>
    /// Retrieve the starting point file in a given <paramref name="repoPath"/>
    /// </summary>
    /// <remarks>
    /// In python we can first check for main.py, app.py, or a file with __name__ == "__main__"
    /// </remarks>
    /// <param name="repoPath">Cloned repo path on disk</param>
    /// <exception cref="FileNotFoundException">When entrypoint file could not be located</exception>
    /// <returns>Entrypoint file to execute in our entrypoint args</returns>
    public static async Task<string> GetPythonEntrypointFile(string repoPath)
    {
        var files = Directory.GetFiles(repoPath, "*.py")
                                    .Select(x => new FileInfo(x))
                                    .ToList();

        if (!files.Any())
            throw new FileNotFoundException("Could not locate any python files at given path: " + repoPath);

        if (files.Any(x => x.Name.Equals("main.py", StringComparison.OrdinalIgnoreCase)))
            return "main.py";

        if (files.Any(x => x.Name.Equals("app.py", StringComparison.OrdinalIgnoreCase)))
            return "app.py";
        
        Regex regexMain = new Regex("(__name__\\s==\\s(\"|')__main__(\"|'))");
        
        foreach (var file in files)
        {
            string contents = await File.ReadAllTextAsync(file.FullName);
            var scan = regexMain.Match(contents);
            
            // If we found the file with __main__ --> we found our entrypoint file
            if (scan.Success)
                return file.Name;
        }

        throw new FileNotFoundException("Could not locate a valid entrypoint file at: " + repoPath);
    }

    /// <summary>
    /// Substitute variables start and end with '%'
    /// </summary>
    /// <param name="text">Text to check</param>
    /// <returns>True if the text is considered a substitute variable</returns>
    public static bool IsSubVariable(this string text) => text.StartsWith('%') && text.EndsWith('%');

    /// <summary>
    /// Update all occurrences of <paramref name="varName"/> with <paramref name="value"/> 
    /// </summary>
    /// <param name="contents">block of text</param>
    /// <param name="varName">Name of sub var without %</param>
    /// <param name="value">Value to insert</param>
    /// <returns>Modified version of <paramref name="contents"/></returns>
    public static string UpdateVar(this string contents, string varName, string value)
     => contents.Replace($"%{varName}%", value);

    /// <summary>
    /// Attempt to figure out what kind of programming language is used in
    /// given <paramref name="directoryPath"/>
    /// </summary>
    /// <param name="directoryPath">Directory to analyze</param>
    /// <exception cref="FileNotFoundException">When no files are in <paramref name="directoryPath"/></exception>
    /// <returns><see cref="Language"/> based on directory contents</returns>
    public static Language DetermineLanguageOfDir(string directoryPath)
    {
        var files = Directory.GetFiles(directoryPath)
            .Select(x => new FileInfo(x))
            .ToList();

        if (!files.Any())
            throw new FileNotFoundException("No files were found at: " + directoryPath);

        var extensions = files.Select(x => x.Extension.Replace(".", ""))
            .ToList();

        if (extensions.Contains("csproj") || extensions.Contains("sln"))
            return Language.CSharp;

        if (extensions.Contains("py"))
            return Language.Python;

        if (extensions.Contains("cpp"))
            return Language.Cplusplus;

        if (extensions.Contains("java"))
            return Language.Java;
        
        if (files.Any(x => x.Name.Equals("package.json")))
            return Language.NodeJs;

        return Language.Unknown;
    }

    public static async Task DeleteDir(string dirPath)
    {
        if (!Directory.Exists(dirPath))
            return;
        
        foreach (var file in Directory.GetFiles(dirPath))
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }

        foreach (var dir in Directory.GetDirectories(dirPath))
            await DeleteDir(dir);

        await ExecutePowershell($"Remove-Item -Recurse -Force \"{dirPath}\"");
    }

    /// <summary>
    /// Enable/Disable PowerShell scripts to execute on LocalMachine
    /// </summary>
    /// <remarks>
    /// If not enabled -- output will go straight into stderr
    /// </remarks>
    /// <param name="enabled"></param>
    public static async Task SetExecutionPolicy(bool enabled)
    {
        await ExecutePowershell($"Set-ExecutionPolicy {(enabled ? "Unrestricted" : "AllSigned")} LocalMachine");
    }

    public static bool MatchArray(object[]? submittedValue, object[]? array, Shared.Models.MatchType matchType)
    {
        if (submittedValue is null || array is null)
            return false;

        if (submittedValue.Length != array.Length)
            return false;
        
        switch (matchType)
        {
            case Shared.Models.MatchType.Exact:
                for(int i = 0; i < array.Length; i++)
                    if (array[i] != submittedValue[i])
                        return false;
                return true;
            
            case Shared.Models.MatchType.Any:
                var intersect = array.Intersect(submittedValue);
                return intersect.Count() >= 0;
            
            case Shared.Models.MatchType.All:
                var allIntersect = array.Intersect(submittedValue);
                return allIntersect.Count() == array.Length;
            
            default:
                return false;
        }
    }
    
    public static async Task ExecutePowershell(string script, 
        Action<object?, DataAddingEventArgs>? onProgress = null,
        Action<object?, DataAddingEventArgs>? onError = null)
    {
        using var ps = PowerShell.Create();
        ps.AddScript(script);
        
        if(onProgress is not null)
            ps.Streams.Progress.DataAdding += (sender, args) => onProgress(sender,args);
        
        if(onError is not null)
            ps.Streams.Error.DataAdding += (sender,args)=>onError(sender,args);
        
        await ps.InvokeAsync();
    }

    public static Stream CreateTarballForDockerfileDirectory(string directory)
    {
        var tarball = new MemoryStream();
        var files = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories);

        using var archive = new TarOutputStream(tarball)
        {
            // Prevent the TarOutputStream from closing underlying memory stream when done
            IsStreamOwner = false
        };

        foreach (var file in files)
        {
            // Replacing slashes
            string tarName = file.Substring(directory.Length).Replace('\\', '/').TrimStart('/');
            
            // Lets create the entry header
            var entry = TarEntry.CreateTarEntry(tarName);
            using var fileStream = File.OpenRead(file);
            entry.Size = fileStream.Length;
            archive.PutNextEntry(entry);
            
            // Write bytes of data
            byte[] localBuffer = new byte[32 * 1024];
            while (true)
            {
                int numRead = fileStream.Read(localBuffer, 0, localBuffer.Length);
                if (numRead <= 0)
                    break;

                archive.Write(localBuffer, 0, numRead);
            }
            
            archive.CloseEntry();
        }

        archive.Close();

        // Reset the stream and return it so it can be used by the caller
        tarball.Position = 0;
        return tarball;
    }
}