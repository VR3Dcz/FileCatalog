using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace FileCatalog.Services.Scanner;

public static class SafeDirectoryTraverser
{
    private static readonly bool IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    private static readonly HashSet<string> LinuxVirtualRoots = new(StringComparer.Ordinal)
    {
        "/proc",
        "/sys",
        "/dev",
        "/run",
        "/snap",
        "/var/run"
    };

    public static IEnumerable<DirectoryInfo> EnumerateDirectoriesSafely(DirectoryInfo root)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System,
            ReturnSpecialDirectories = false
        };

        IEnumerable<DirectoryInfo> subDirectories = Array.Empty<DirectoryInfo>();

        try
        {
            subDirectories = root.EnumerateDirectories("*", options);
        }
        catch (Exception)
        {
            // Silent fallback for edge cases where the OS denies directory handle creation
            yield break;
        }

        foreach (var directory in subDirectories)
        {
            if (IsLinux && IsVirtualLinuxMount(directory.FullName))
            {
                continue;
            }

            yield return directory;
        }
    }

    public static IEnumerable<FileInfo> EnumerateFilesSafely(DirectoryInfo directory)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System
        };

        try
        {
            return directory.EnumerateFiles("*", options);
        }
        catch (Exception)
        {
            return Array.Empty<FileInfo>();
        }
    }

    private static bool IsVirtualLinuxMount(string path)
    {
        foreach (var virtualRoot in LinuxVirtualRoots)
        {
            if (path.Equals(virtualRoot, StringComparison.Ordinal) ||
                path.StartsWith(virtualRoot + "/", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}