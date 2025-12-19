// C# Script: dotnet script RenameProject.csx
// Or compile: dotnet new console -n RenameHelper && copy this to Program.cs

using System;
using System.IO;
using System.Text;

var rootDir = Directory.GetCurrentDirectory();

// Replacements
var replacements = new[] {
    ("SandronePlayer", "AkashaNavigator"),
    ("sandrone-player", "akasha-navigator")
};

// 1. Rename folders
RenameIfExists("SandronePlayer", "AkashaNavigator");
RenameIfExists("SandronePlayer.Tests", "AkashaNavigator.Tests");

// 2. Rename files
RenameIfExists("AkashaNavigator/SandronePlayer.csproj", "AkashaNavigator/AkashaNavigator.csproj");
RenameIfExists("AkashaNavigator.Tests/SandronePlayer.Tests.csproj", "AkashaNavigator.Tests/AkashaNavigator.Tests.csproj");
RenameIfExists("SandronePlayer.sln", "AkashaNavigator.sln");

// 3. Rename icons
foreach (var file in Directory.GetFiles("assets/icons", "sandrone-player-*"))
{
    var newName = file.Replace("sandrone-player", "akasha-navigator");
    File.Move(file, newName);
    Console.WriteLine($"Renamed: {file} -> {newName}");
}

// 4. Rename logo
RenameIfExists("assets/sandrone-player-logo.png", "assets/akasha-navigator-logo.png");

// 5. Replace content in files
var extensions = new[] { "*.cs", "*.csproj", "*.xaml", "*.json", "*.md", "*.sln" };
var excludeDirs = new[] { "bin", "obj", ".git", ".vs", "node_modules" };

foreach (var ext in extensions)
{
    foreach (var file in Directory.GetFiles(rootDir, ext, SearchOption.AllDirectories))
    {
        if (excludeDirs.Any(d => file.Contains(Path.DirectorySeparatorChar + d + Path.DirectorySeparatorChar)))
            continue;

        try
        {
            // Read with auto-detect encoding
            var bytes = File.ReadAllBytes(file);
            var encoding = DetectEncoding(bytes);
            var content = encoding.GetString(bytes);
            
            // Skip BOM in content if present
            if (content.Length > 0 && content[0] == '\uFEFF')
                content = content.Substring(1);

            var original = content;
            foreach (var (oldStr, newStr) in replacements)
            {
                content = content.Replace(oldStr, newStr);
            }

            if (content != original)
            {
                // Write back with same encoding
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

Console.WriteLine("\nDone!");

void RenameIfExists(string oldPath, string newPath)
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

Encoding DetectEncoding(byte[] bytes)
{
    if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        return new UTF8Encoding(true); // UTF-8 with BOM
    return new UTF8Encoding(false); // UTF-8 without BOM
}
