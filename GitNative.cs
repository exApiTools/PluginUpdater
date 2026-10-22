using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LibGit2Sharp;

namespace PluginUpdater;

internal static class GitNative
{
    private static readonly FieldInfo ConfigHandleField = typeof(Configuration).GetField("configHandle", BindingFlags.NonPublic | BindingFlags.Instance);
    private const string Libgit2 = "git2-5853918";

    [StructLayout(LayoutKind.Sequential)]
    private struct git_repository;

    [StructLayout(LayoutKind.Sequential)]
    private struct git_config;

    [DllImport(Libgit2, CallingConvention = CallingConvention.Cdecl)]
    private static extern unsafe byte* git_repository_commondir(git_repository* repository);

    [DllImport(Libgit2, CallingConvention = CallingConvention.Cdecl)]
    private static extern unsafe int git_config_add_file_ondisk(
        git_config* cfg,
        byte* path,
        uint level,
        git_repository* repo,
        [MarshalAs(UnmanagedType.Bool)] bool force);

    [DllImport(Libgit2, CallingConvention = CallingConvention.Cdecl)]
    private static extern unsafe int git_repository_set_config(
        git_repository* repository,
        git_config* config);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_Handle")]
    [return: UnsafeAccessorType("LibGit2Sharp.Core.Handles.RepositoryHandle,LibGit2Sharp")]
    static extern object GetRepoHandleWrapper(Repository repo);

    static object GetRepoConfigHandleWrapper(Configuration repo)
    {
        return ConfigHandleField.GetValue(repo);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "AsIntPtr")]
    static extern IntPtr GetObjectInternalHandle([UnsafeAccessorType("LibGit2Sharp.Core.Handles.Libgit2Object,LibGit2Sharp")] object handle);

    public static unsafe string GetCommonDir(this Repository repository)
    {
        var handle = GetRepoHandleWrapper(repository);
        var ptr = GetObjectInternalHandle(handle);
        if (ptr == 0)
        {
            return null;
        }

        return Path.GetFullPath(Marshal.PtrToStringUTF8((IntPtr)git_repository_commondir((git_repository*)ptr)));
    }

    public static unsafe Repository CreateWithWorkspaceHandling(string path)
    {
        var repository = new Repository(path);
        var commonDir = GetCommonDir(repository);
        if (commonDir == null || commonDir == repository.Info.Path)
        {
            return repository;
        }

        string repoConfigLocation = Path.Combine(commonDir, "config");
        var configHandle = GetRepoConfigHandleWrapper(repository.Config);
        var configPtr = GetObjectInternalHandle(configHandle);
        var repoPtr = GetObjectInternalHandle(GetRepoHandleWrapper(repository));
        var locationBytes = Encoding.UTF8.GetBytes(repoConfigLocation);
        fixed (byte* b = locationBytes)
        {
            git_config_add_file_ondisk((git_config*)configPtr, b, (uint)ConfigurationLevel.Local, (git_repository*)repoPtr, true);
        }

        git_repository_set_config((git_repository*)repoPtr, (git_config*)configPtr);
        return repository;
    }
}