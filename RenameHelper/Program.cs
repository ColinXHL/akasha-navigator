using System.Text;

var rootDir = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
Console.WriteLine($"Working in: {rootDir}");
Directory.SetCurrentDirectory(rootDir);

var replacements = new[] {
    ("SandronePlayer", "AkashaNavigator"),
    ("sandrone-player", "akasha-navigator")
};

// 1. Rename folders
RenameIfExists("SandronePlayer", "AkashaNavigator");
RenameIfExists("SandronePlayer.Tests", "AkashaNavigator.Tests");

// 2. Rename project files
RenameIfExists("AkashaNavigator/SandronePlayer.csproj", "AkashaNavigator/AkashaNavigator.csproj");
RenameIfExists("AkashaNavigator.Tests/SandronePlayer.Tests.csproj", "AkashaNavigator.Tests/AkashaNavigator.Tests.csproj");
RenameIfExists("SandronePlayer.sln", "AkashaNavigator.sln");

// 3. Rename icons
if (Directory.Exists("assets/icons"))
{
    foreach (var file in Directory.GetFiles("assets/icons", "sandrone-player-*"))
    {
        var newName = file.Replace("sandrone-player", "akasha-navigator");
        File.Move(file, newName);
        Console.WriteLine($"Renamed: {Path.GetFileName(file)} -> {Path.GetFileName(newName)}");
    }
}

// 4. Rename logo
RenameIfExists("assets/sandrone-player-logo.png", "assets/akasha-navigator-logo.png");

// 5. Replace content
var extensions = new[] { "*.cs", "*.csproj", "*.xaml", "*.json", "*.md", "*.sln" };
var excludeDirs = new[] { "bin", "obj", ".git", ".vs", "node_modules", "RenameHelper" };

foreach (var ext in extensions)
{
    foreach (var file in Directory.GetFiles(rootDir, ext, SearchOption.AllDirectories))
    {
        if (excludeDirs.Any(d => file.Contains(Path.DirectorySeparatorChar + d + Path.DirectorySeparatorChar)))
            continue;

        try
        {
            var bytes = File.ReadAllBytes(file);
            var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var encoding = hasBom ? new UTF8Encoding(true) : new UTF8Encoding(false);
            
            var content = File.ReadAllText(file, Encoding.UTF8);
            var original = content;
            
            foreach (var (oldStr, newStr) in replacements)
                content = content.Replace(oldStr, newStr);

            if (content != original)
            {
                File.WriteAllText(file, content, encoding);
                Console.WriteLine($"Updated: {file}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {file} - {ex.Message}");
        }
    }
}

Console.WriteLine("\nDone! Run 'dotnet build' to verify.");

static void RenameIfExists(string oldPath, string newPath)
{
    if (Directory.Exists(oldPath) && !Directory.Exists(newPath))
    {
        Directory.Move(oldPath, newPath);
        Console.WriteLine($"Renamed folder: {oldPath} -> {newPath}");
    }
    else if (File.Exists(oldPath) && !File.Exists(newPath))
    {
        File.Move(oldPath, newPath);
        Console.WriteLine($"Renamed file: {oldPath} -> {newPath}");
    }
}
