using Microsoft.Testing.Platform.Builder;
using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using uTest.Logging;

// ReSharper disable InconsistentNaming

namespace uTest.Runner.Unturned;

internal static class ModuleFiles
{
    internal static Assembly uTestAssembly { get; } = typeof(ITestContext).Assembly;

    internal static Assembly uTestRunnerAssembly { get; } = typeof(ModuleFiles).Assembly;

    internal static Assembly MTPAssembly { get; } = typeof(ITestApplicationBuilder).Assembly;

    internal static SupportedTargetFramework CurrentTargetFramework { get; } = IsNetStandard(uTestAssembly) || IsNetStandard(uTestRunnerAssembly)
        ? SupportedTargetFramework.NetStandard
        : SupportedTargetFramework.NetFramework;

    internal static ModuleFile[] Files { get; } =
    [
        // note: all these weird libraries are used by xunit cause they still target .net standard 1.1
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.netstandard.dll", "netstandard.dll") { ShouldBeReferencedByModuleConfig = true },
        new LoadedAssemblyModuleFile(SupportedTargetFramework.Both, MTPAssembly, "Microsoft.Testing.Platform.dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.Microsoft.Testing.Extensions.TrxReport.Abstractions.dll", "Microsoft.Testing.Extensions.TrxReport.Abstractions.dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.NetStandard, "uTest.Runner.Module.System.Runtime.CompilerServices.Unsafe.dll", "System.Runtime.CompilerServices.Unsafe.dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.NetFramework, "uTest.Runner.Module.System.Runtime.CompilerServices.Unsafe (.NET Framework).dll", "System.Runtime.CompilerServices.Unsafe (.NET Framework).dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Threading.Tasks.Extensions.dll", "System.Threading.Tasks.Extensions.dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.NetFramework, "uTest.Runner.Module.System.Buffers.dll", "System.Buffers.dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Threading.Tasks.dll", "System.Threading.Tasks.dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Collections.dll", "System.Collections.dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Collections.Concurrent.dll", "System.Collections.Concurrent.dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Reflection.dll", "System.Reflection.dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Reflection.Extensions.dll", "System.Reflection.Extensions.dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.ObjectModel.dll", "System.ObjectModel.dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Globalization.dll", "System.Globalization.dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Diagnostics.Debug.dll", "System.Diagnostics.Debug.dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Diagnostics.Tools.dll", "System.Diagnostics.Tools.dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Linq.dll", "System.Linq.dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Runtime.dll", "System.Runtime.dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Runtime.Extensions.dll", "System.Runtime.Extensions.dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Text.RegularExpressions.dll", "System.Text.RegularExpressions.dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Memory.dll", "System.Memory.dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.Microsoft.Extensions.FileSystemGlobbing.dll", "Microsoft.Extensions.FileSystemGlobbing.dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.xunit.assert.dll", "xunit.assert.dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.0Harmony.dll", "0Harmony.dll") { ShouldBeReferencedByModuleConfig = true },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.xunit.assert (License).txt", "xunit.assert (License).txt"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System+Microsoft .NET Libraries (License).txt", "System+Microsoft .NET Libraries (License).txt"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.Mono Class Libraries (License).txt", "Mono Class Libraries (License).txt"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.Microsoft.Testing.Platform (License).txt", "Microsoft.Testing.Platform (License).txt"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.0Harmony (License).txt", "0Harmony (License).txt"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.AssemblyDeletionWarning.txt", "!! DONT PUT DLLs IN HERE !!.txt"),
        new LoadedAssemblyModuleFile(SupportedTargetFramework.Both, uTestAssembly, "uTest.dll") { ShouldBeReferencedByModuleConfig = true },
        new LoadedAssemblyModuleFile(SupportedTargetFramework.Both, uTestRunnerAssembly, "uTest.Runner.dll") { ShouldBeReferencedByModuleConfig = true },
        new ModuleConfigFile()
    ];

    private static readonly ModuleConfigFile DisabledModule = new ModuleConfigFile { IsEnabled = false };

    private static readonly HashSet<string> ExpectedFiles;

    static ModuleFiles()
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_0_OR_GREATER || NET472_OR_GREATER
        ExpectedFiles = new HashSet<string>(Files.Length + 1, FileHelper.FileNameComparer)
#else
        ExpectedFiles = new HashSet<string>(FileHelper.FileNameComparer)
#endif
        {
            "test-settings.json"
        };
        foreach (ModuleFile file in Files)
        {
            ExpectedFiles.Add(file.FileName);
        }
    }

    /// <summary>
    /// Checks if a file is supposed to be in the modules folder.
    /// </summary>
    /// <remarks>The test assembly should also be expected specially.</remarks>
    internal static bool IsFileExpected(string relativeFilePath) => ExpectedFiles.Contains(relativeFilePath);

    /// <summary>
    /// Checks if an assembly references <c>netstandard.dll</c>.
    /// </summary>
    internal static bool IsNetStandard(Assembly asm)
    {
        return asm
            .GetReferencedAssemblies()
            .Any(x => x.FullName.StartsWith("netstandard, Version=2."));
    }

    internal static bool IsFileApplicable(ModuleFile file)
    {
        return file.Framework switch
        {
            SupportedTargetFramework.NetFramework => CurrentTargetFramework == SupportedTargetFramework.NetFramework,
            SupportedTargetFramework.NetStandard => CurrentTargetFramework == SupportedTargetFramework.NetStandard,
            _ => true
        };
    }

    /// <summary>
    /// Disables the module immediately after startup so it doesn't interfere with other modules.
    /// </summary>
    internal static bool DisableModule(string moduleFolder, ILogger logger, Assembly testAssembly)
    {
        LoadedAssemblyModuleFile testAssemblyFile = new LoadedAssemblyModuleFile(SupportedTargetFramework.Both, testAssembly)
        {
            ShouldBeReferencedByModuleConfig = true
        };

        if (!Directory.Exists(moduleFolder) || !File.Exists(Path.Combine(moduleFolder, DisabledModule.FileName)))
        {
            return true;
        }

        return DisabledModule.TryWrite(moduleFolder, logger, out _, testAssemblyFile);
    }

    /// <summary>
    /// Write or update all files necessary for the module to run.
    /// </summary>
    internal static bool WriteModuleFiles(string moduleFolder, ILogger logger, Assembly? testAssembly)
    {
        moduleFolder = Path.GetFullPath(moduleFolder);
        Directory.CreateDirectory(moduleFolder);

        LoadedAssemblyModuleFile? testAssemblyFile = testAssembly == null
            ? null
            : new LoadedAssemblyModuleFile(SupportedTargetFramework.Both, testAssembly)
            {
                ShouldBeReferencedByModuleConfig = true
            };

        bool anyFailed = false;
        foreach (ModuleFile file in Files)
        {
            if (!IsFileApplicable(file))
                continue;

            if (!file.TryWrite(moduleFolder, logger, out _, testAssemblyFile))
            {
                anyFailed = true;
                break;
            }

            if (logger.IsEnabled(LogLevel.Trace))
                logger.LogTrace(string.Format(Properties.Resources.LogTraceCopiedFile, file.FileName));
        }

        string? testAssemblyLocation = null;

        if (!anyFailed && testAssemblyFile != null)
        {
            anyFailed = !testAssemblyFile.TryWrite(moduleFolder, logger, out testAssemblyLocation, testAssemblyFile);
        }

        try
        {
            foreach (string file in Directory.EnumerateFiles(moduleFolder, "*.dll", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(file);
                if (FileHelper.FileNameComparer.Equals(file, testAssemblyLocation) || IsFileExpected(fileName))
                    continue;

                try
                {
                    File.Delete(file);
                    if (logger.IsEnabled(LogLevel.Trace))
                        logger.LogTrace(string.Format(Properties.Resources.LogTraceDeletedUnusedFile, fileName));
                }
                catch (Exception ex)
                {
                    logger.LogError(string.Format(Properties.Resources.LogErrorDeletingUnusedFile, fileName), ex);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(Properties.Resources.LogErrorDeletingUnusedFiles, ex);
        }

        return !anyFailed;
    }
}

internal enum SupportedTargetFramework
{
    Both,
    NetFramework,
    NetStandard
}

internal abstract class ModuleFile
{
    internal SupportedTargetFramework Framework { get; }
    internal string FileName { get; }
    internal bool ShouldBeReferencedByModuleConfig { get; init; }

    protected ModuleFile(SupportedTargetFramework framework, string fileName)
    {
        Framework = framework;
        FileName = fileName;
    }

    // wtf microsoft
    private static readonly DateTime FileNotExistsWriteTimeReturnValue = new DateTime(1601, 01, 01, 00, 00, 00);

    protected static DateTime GetLastWriteTimeUTCSafe(string file, DateTime defaultValue)
    {
        DateTime dt;
        try
        {
            dt = File.GetLastWriteTimeUtc(file);
        }
        catch
        {
            return defaultValue;
        }

        return dt == FileNotExistsWriteTimeReturnValue ? defaultValue : dt;
    }

    public abstract bool TryWrite(string baseFolder, ILogger logger, [MaybeNullWhen(false)] out string fileName, LoadedAssemblyModuleFile? testAssembly);

    /// <returns>The exception if there was one.</returns>
    protected Exception? TryCopyIfNewer(string src, string dst, ILogger logger)
    {
        DateTime srcTime = GetLastWriteTimeUTCSafe(src, DateTime.MaxValue);
        DateTime existingTime = GetLastWriteTimeUTCSafe(dst, DateTime.MinValue);

        if (srcTime <= existingTime)
        {
            if (logger.IsEnabled(LogLevel.Trace))
                logger.LogTrace(string.Format(Properties.Resources.LogTraceSkipCopyingFile, FileName));
            return null;
        }

        try
        {
            File.Copy(src, dst, overwrite: true);
        }
        catch (Exception ex)
        {
            return ex;
        }

        return null;
    }
}

internal sealed class LoadedAssemblyModuleFile : ModuleFile
{
    internal Assembly Assembly { get; }

    public LoadedAssemblyModuleFile(SupportedTargetFramework framework, Assembly assembly, string? fileName = null)
        : base(framework, fileName ?? assembly.GetName().Name + ".dll")
    {
        Assembly = assembly;
    }

    public override bool TryWrite(string baseFolder, ILogger logger, [MaybeNullWhen(false)] out string fileName, LoadedAssemblyModuleFile? testAssembly)
    {
        string? location;
        try
        {
            location = Assembly.Location;
        }
        catch (Exception ex)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorFindingAssemblyLocation, Assembly.FullName), ex);
            fileName = null;
            return false;
        }

        if (!File.Exists(location))
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorFindingAssemblyLocation, Assembly.FullName));
            fileName = null;
            return false;
        }

        string path = Path.Combine(baseFolder, FileName);

        switch (TryCopyIfNewer(location, path, logger))
        {
            case null:
                fileName = path;
                return true;

            case FileNotFoundException:
                logger.LogError(string.Format(Properties.Resources.LogErrorFindingAssemblyLocation, Assembly.FullName));
                break;

            case var ex: // default:
                logger.LogError(string.Format(Properties.Resources.LogErrorCopyingFile, location), ex);
                break;
        }

        fileName = null;
        return false;
    }
}

internal sealed class EmbeddedModuleFile : ModuleFile
{
    internal string EmbeddedResourceName { get; }
    public Assembly Assembly { get; }

    public EmbeddedModuleFile(SupportedTargetFramework framework, string embeddedResourceName, string fileName, Assembly? assembly = null) : base(framework, fileName)
    {
        EmbeddedResourceName = embeddedResourceName;
        Assembly = assembly ?? ModuleFiles.uTestRunnerAssembly;
    }

    public override bool TryWrite(string baseFolder, ILogger logger, [MaybeNullWhen(false)] out string fileName, LoadedAssemblyModuleFile? testAssembly)
    {
        string path = Path.Combine(baseFolder, FileName);

        Stream? embeddedResourceStream = null;
        try
        {
            embeddedResourceStream = Assembly.GetManifestResourceStream(EmbeddedResourceName);
            if (embeddedResourceStream == null)
            {
                logger.LogError(string.Format(Properties.Resources.LogErrorCopyingFile, FileName + $" ({EmbeddedResourceName})"));
                fileName = null;
                return false;
            }

            DateTime assemblyCreateDate;
            DateTime fileCreateDate = GetLastWriteTimeUTCSafe(path, DateTime.MinValue);
            try
            {
                // GetLastWriteTimeUTCSafe is safe but Assembly.Location.get might not be
                assemblyCreateDate = GetLastWriteTimeUTCSafe(Assembly.Location, DateTime.MaxValue);
            }
            catch
            {
                assemblyCreateDate = DateTime.MaxValue;
            }

            if (assemblyCreateDate <= fileCreateDate)
            {
                if (logger.IsEnabled(LogLevel.Trace))
                    logger.LogTrace(string.Format(Properties.Resources.LogTraceSkipCopyingFile, FileName));
                fileName = path;
                return true;
            }

            using (FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 16384, FileOptions.SequentialScan))
            {
                embeddedResourceStream.CopyTo(fileStream);
            }

            if (assemblyCreateDate != DateTime.MaxValue)
            {
                try
                {
                    File.SetLastWriteTime(path, assemblyCreateDate);
                }
                catch { /* ignored */ }
            }

            fileName = path;
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorCopyingFile, FileName), ex);
            fileName = null;
            return false;
        }
        finally
        {
            embeddedResourceStream?.Dispose();
        }
    }
}

internal sealed class ModuleConfigFile : ModuleFile
{
    public bool IsEnabled { get; init; } = true;

    public ModuleConfigFile() : base(SupportedTargetFramework.Both, "uTest.module") { }

    public override bool TryWrite(string baseFolder, ILogger logger, [MaybeNullWhen(false)] out string fileName, LoadedAssemblyModuleFile? testAssembly)
    {
        string path = Path.Combine(baseFolder, FileName);
        DateTime assemblyCreateDate = DateTime.MaxValue;

        try
        {
            using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 1024, FileOptions.SequentialScan);
            using StreamWriter sw = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 1024, leaveOpen: true);

            using JsonTextWriter writer = new JsonTextWriter(sw);

            writer.CloseOutput = false;
#if DEBUG
            writer.Formatting = Formatting.Indented;
            writer.IndentChar = ' ';
            writer.Indentation = 4;
#endif

            writer.WriteStartObject();
            
            writer.WritePropertyName("IsEnabled");
            writer.WriteValue(IsEnabled);

            writer.WritePropertyName("Name");
            writer.WriteValue("uTest");

            writer.WritePropertyName("Version");
            writer.WriteValue(ModuleFiles.uTestAssembly.GetName().Version.ToString());

            writer.WritePropertyName("Assemblies");
            writer.WriteStartArray();

            foreach (ModuleFile? file in ModuleFiles.Files.Concat(Enumerable.Repeat(testAssembly, 1)))
            {
                if (file is not { ShouldBeReferencedByModuleConfig: true } || !ModuleFiles.IsFileApplicable(file))
                    continue;

                writer.WriteStartObject();

                writer.WritePropertyName("Path");
                // nelson just appends the file name so this is correct
                writer.WriteValue("/" + file.FileName);

                writer.WritePropertyName("Role");
                writer.WriteValue("Both_Optional");

                writer.WritePropertyName("Load_As_Byte_Array");
                writer.WriteValue(true);

                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteEndObject();

            writer.Flush();
        }
        catch (Exception ex)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorCopyingFile, FileName), ex);
            fileName = null;
            return false;
        }

        if (assemblyCreateDate != DateTime.MaxValue)
        {
            try
            {
                File.SetLastWriteTime(path, assemblyCreateDate);
            }
            catch { /* ignored */ }
        }

        fileName = path;
        return true;
    }
}