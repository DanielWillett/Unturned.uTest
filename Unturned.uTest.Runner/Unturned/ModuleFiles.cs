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
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.Unturned.uTest.Bootstrapper.dll",                            "uTest.Bootstrapper.dll", hasSymbols: true),
        new BootstrapperModuleConfigFile(),
        
        new LoadedAssemblyModuleFile(SupportedTargetFramework.Both, MTPAssembly,                                                                "Microsoft.Testing.Platform.dll") { ModuleReferenceMode = ModuleFileReferenceMode.TestClient },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.ReflectionTools.dll",                                        "ReflectionTools.exe") { ModuleReferenceMode = ModuleFileReferenceMode.Dummies },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.ReflectionTools.Harmony.dll",                                "ReflectionTools.Harmony.exe") { ModuleReferenceMode = ModuleFileReferenceMode.Dummies },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.DanielWillett.SpeedBytes.dll",                               "DanielWillett.SpeedBytes.exe") { ModuleReferenceMode = ModuleFileReferenceMode.Dummies },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.ModularRpcs.dll",                                            "ModularRpcs.exe") { ModuleReferenceMode = ModuleFileReferenceMode.Dummies },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.ModularRpcs.NamedPipes.dll",                                 "ModularRpcs.NamedPipes.exe") { ModuleReferenceMode = ModuleFileReferenceMode.Dummies },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.ModularRpcs.Unity.dll",                                      "ModularRpcs.Unity.exe") { ModuleReferenceMode = ModuleFileReferenceMode.Dummies },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.Microsoft.Testing.Extensions.TrxReport.Abstractions.dll",    "Microsoft.Testing.Extensions.TrxReport.Abstractions.dll") { ModuleReferenceMode = ModuleFileReferenceMode.TestClient },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Threading.Tasks.Extensions.dll",                      "System.Threading.Tasks.Extensions.exe") { ModuleReferenceMode = ModuleFileReferenceMode.Dummies },
        

        // note: all these weird libraries are used by xunit cause they still target .net standard 1.1

        new EmbeddedModuleFile(SupportedTargetFramework.NetFramework, "uTest.Runner.Module.System.Buffers.dll",                                 "System.Buffers.exe") { ModuleReferenceMode = ModuleFileReferenceMode.Dummies },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Threading.Tasks.dll",                                 "System.Threading.Tasks.exe"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Collections.Concurrent.dll",                          "System.Collections.Concurrent.exe"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Reflection.dll",                                      "System.Reflection.exe"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Reflection.Extensions.dll",                           "System.Reflection.Extensions.exe"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.ObjectModel.dll",                                     "System.ObjectModel.exe"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Globalization.dll",                                   "System.Globalization.exe"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Diagnostics.Debug.dll",                               "System.Diagnostics.Debug.exe"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Diagnostics.Tools.dll",                               "System.Diagnostics.Tools.exe"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Linq.dll",                                            "System.Linq.exe"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Text.RegularExpressions.dll",                         "System.Text.RegularExpressions.exe"),
        
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System.Diagnostics.DiagnosticSource.dll",                    "System.Diagnostics.DiagnosticSource.exe") { ModuleReferenceMode = ModuleFileReferenceMode.Dummies },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.Microsoft.Bcl.AsyncInterfaces.dll",                          "Microsoft.Bcl.AsyncInterfaces.exe") { ModuleReferenceMode = ModuleFileReferenceMode.Dummies },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.Microsoft.Extensions.FileSystemGlobbing.dll",                "Microsoft.Extensions.FileSystemGlobbing.exe") { ModuleReferenceMode = ModuleFileReferenceMode.Dummies },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.Microsoft.Extensions.DependencyInjection.Abstractions.dll",  "Microsoft.Extensions.DependencyInjection.Abstractions.exe") { ModuleReferenceMode = ModuleFileReferenceMode.Dummies },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.Microsoft.Extensions.Logging.Abstractions.dll",              "Microsoft.Extensions.Logging.Abstractions.exe") { ModuleReferenceMode = ModuleFileReferenceMode.Dummies },
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.xunit.assert.dll",                                           "xunit.assert.dll"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.0Harmony.dll",                                               "0Harmony.exe") { ModuleReferenceMode = ModuleFileReferenceMode.Dummies },

        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.xunit.assert (License).txt",                                 "xunit.assert (License).txt"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.System+Microsoft .NET Libraries (License).txt",              "System+Microsoft .NET Libraries (License).txt"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.Mono Class Libraries (License).txt",                         "Mono Class Libraries (License).txt"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.Microsoft.Testing.Platform (License).txt",                   "Microsoft.Testing.Platform (License).txt"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.0Harmony (License).txt",                                     "0Harmony (License).txt"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.ModularRpcs (License).txt",                                  "ModularRpcs (License).txt"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.ReflectionTools (License).txt",                              "ReflectionTools (License).txt"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.AssemblyDeletionWarning.txt",                                "!! DONT PUT FILES IN HERE !!.txt"),
        
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.README.md",                                                  "README.md"),
        
        new LoadedAssemblyModuleFile(SupportedTargetFramework.Both, uTestAssembly,                                                              "uTest.dll") { ModuleReferenceMode = ModuleFileReferenceMode.Both },
        new LoadedAssemblyModuleFile(SupportedTargetFramework.Both, uTestRunnerAssembly,                                                        "uTest.Runner.dll"),
        new EmbeddedModuleFile(SupportedTargetFramework.Both, "uTest.Runner.Module.Unturned.uTest.DummyPlayerHost.dll",                         "uTest.DummyPlayerHost.dll", hasSymbols: true) { ModuleReferenceMode = ModuleFileReferenceMode.Dummies },
        
        new ModuleConfigFile()
    ];

    private static readonly ModuleConfigFile DisabledModule = new ModuleConfigFile { IsEnabled = false };
    private static readonly BootstrapperModuleConfigFile DisabledBootstrapperModule = new BootstrapperModuleConfigFile { IsEnabled = false };

    private static readonly HashSet<string> ExpectedFiles;

    static ModuleFiles()
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_0_OR_GREATER || NET472_OR_GREATER
        ExpectedFiles = new HashSet<string>(Files.Length + 2, FileHelper.FileNameComparer)
#else
        ExpectedFiles = new HashSet<string>(FileHelper.FileNameComparer)
#endif
        {
            "test-settings.json",
            "0Harmony (2.3.3).exe"
        };
        foreach (ModuleFile file in Files)
        {
            ExpectedFiles.Add(file.FileName);
            if (file.OtherFileName is not { Length: > 0 })
                continue;

            foreach (string s in file.OtherFileName)
                ExpectedFiles.Add(s);
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
        LoadedAssemblyModuleFile testAssemblyFile = new LoadedAssemblyModuleFile(SupportedTargetFramework.Both, testAssembly, hasSymbolsInRelease: true)
        {
            ModuleReferenceMode = ModuleFileReferenceMode.TestClient
        };

        if (!Directory.Exists(moduleFolder) || !File.Exists(Path.Combine(moduleFolder, DisabledModule.FileName)))
        {
            return true;
        }

        return DisabledModule.TryWrite(moduleFolder, logger, out _, testAssemblyFile)
               && DisabledBootstrapperModule.TryWrite(moduleFolder, logger, out _, testAssemblyFile);
    }

    internal static bool IsServer { get; set; }

    /// <summary>
    /// Write or update all files necessary for the module to run.
    /// </summary>
    internal static bool WriteModuleFiles(string moduleFolder, ILogger logger, Assembly? testAssembly)
    {
        moduleFolder = Path.GetFullPath(moduleFolder);
        Directory.CreateDirectory(moduleFolder);

        LoadedAssemblyModuleFile? testAssemblyFile = testAssembly == null
            ? null
            : new LoadedAssemblyModuleFile(SupportedTargetFramework.Both, testAssembly, hasSymbolsInRelease: true)
            {
                ModuleReferenceMode = ModuleFileReferenceMode.TestClient
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
        string? testAssemblySymbols = null;

        if (!anyFailed && testAssemblyFile != null)
        {
            anyFailed = !testAssemblyFile.TryWrite(moduleFolder, logger, out testAssemblyLocation, testAssemblyFile);
            if (testAssemblyLocation != null)
            {
                testAssemblySymbols = Path.ChangeExtension(testAssemblyLocation, ".pdb");
            }
        }

        try
        {
            foreach (string file in Directory.EnumerateFiles(moduleFolder, "*", SearchOption.TopDirectoryOnly))
            {
                string ext = Path.GetExtension(file);
                if (!ext.Equals(".dll", StringComparison.OrdinalIgnoreCase)
                    && !ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)
                    && !ext.Equals(".pdb", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string fileName = Path.GetFileName(file);
                if (FileHelper.FileNameComparer.Equals(file, testAssemblyLocation)
                    || FileHelper.FileNameComparer.Equals(file, testAssemblySymbols)
                    || IsFileExpected(fileName))
                {
                    continue;
                }

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
    internal string[]? OtherFileName { get; set; }
    internal ModuleFileReferenceMode ModuleReferenceMode { get; init; }
    internal bool LoadFromFile { get; set; } = true;

    protected ModuleFile(SupportedTargetFramework framework, string fileName)
    {
        Framework = framework;
        FileName = fileName;
    }

    public abstract bool TryWrite(string baseFolder, ILogger logger, [MaybeNullWhen(false)] out string fileName, LoadedAssemblyModuleFile? testAssembly);

    /// <returns>The exception if there was one.</returns>
    protected Exception? TryCopyIfNewer(string src, string dst, ILogger logger)
    {
        DateTime srcTime = FileHelper.GetLastWriteTimeUTCSafe(src, DateTime.MaxValue);
        DateTime existingTime = FileHelper.GetLastWriteTimeUTCSafe(dst, DateTime.MinValue);

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

public enum ModuleFileReferenceMode
{
    None,
    TestClient,
    Dummies,
    Both
}

internal sealed class LoadedAssemblyModuleFile : ModuleFile
{
    internal Assembly Assembly { get; }

    internal string? SymbolFile { get; }

    // ReSharper disable once UnusedParameter.Local
    public LoadedAssemblyModuleFile(SupportedTargetFramework framework, Assembly assembly, string? fileName = null, bool hasSymbolsInRelease = false)
        : base(framework, fileName ?? assembly.GetName().Name + ".dll")
    {
        Assembly = assembly;

#if RELEASE
        if (!hasSymbolsInRelease)
            return;
#endif
        SymbolFile = Path.ChangeExtension(FileName, ".pdb");
        OtherFileName = [ SymbolFile ];
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

        if (SymbolFile != null)
        {
            string pdbSrcPath = Path.ChangeExtension(location, ".pdb");
            string pdbDstPath = Path.Combine(baseFolder, SymbolFile);
            if (File.Exists(pdbSrcPath))
            {
                if (TryCopyIfNewer(pdbSrcPath, pdbDstPath, logger) is { } ex)
                {
                    logger.LogError(string.Format(Properties.Resources.LogErrorCopyingFile, location), ex);
                    logger.LogError(ex.Message);
                }
            }
            else
            {
                logger.LogWarning(string.Format(Properties.Resources.LogErrorFindingAssemblyLocation, pdbSrcPath));
            }
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
                logger.LogError(ex.Message);
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
#if DEBUG
    internal string? SymbolFileName { get; }
    internal string? SymbolResourceName { get; }
#endif

    // ReSharper disable once UnusedParameter.Local
    public EmbeddedModuleFile(SupportedTargetFramework framework, string embeddedResourceName, string fileName, bool hasSymbols = false, Assembly? resxAssembly = null) : base(framework, fileName)
    {
        EmbeddedResourceName = embeddedResourceName;
        Assembly = resxAssembly ?? ModuleFiles.uTestRunnerAssembly;
#if DEBUG
        if (!hasSymbols)
            return;

        SymbolFileName = Path.ChangeExtension(fileName, ".pdb");
        SymbolResourceName = Path.ChangeExtension(embeddedResourceName, ".pdb");

        OtherFileName = [ SymbolFileName ];
#endif
    }

    public override bool TryWrite(string baseFolder, ILogger logger, [MaybeNullWhen(false)] out string fileName, LoadedAssemblyModuleFile? testAssembly)
    {
        string path = Path.Combine(baseFolder, FileName);

        Stream? embeddedResourceStream = null;
#if DEBUG
        Stream? pdbEmbeddedResourceStream = null;
#endif
        try
        {
            embeddedResourceStream = Assembly.GetManifestResourceStream(EmbeddedResourceName);
            if (embeddedResourceStream == null)
            {
                logger.LogError(string.Format(Properties.Resources.LogErrorCopyingFile, FileName + $" ({EmbeddedResourceName})"));
                fileName = null;
                return false;
            }

#if DEBUG
            string? symbolPath = null;
            if (SymbolResourceName != null && SymbolFileName != null)
            {
                symbolPath = Path.Combine(baseFolder, SymbolFileName);
                pdbEmbeddedResourceStream = Assembly.GetManifestResourceStream(SymbolResourceName);
                if (pdbEmbeddedResourceStream == null)
                    logger.LogWarning(string.Format(Properties.Resources.LogErrorCopyingFile, SymbolFileName + $" ({SymbolResourceName})"));
            }
#endif

            DateTime fileCreateDate = FileHelper.GetLastWriteTimeUTCSafe(path, DateTime.MinValue);
#if DEBUG
            DateTime symbolFileCreateDate = pdbEmbeddedResourceStream == null
                ? DateTime.MinValue
                : FileHelper.GetLastWriteTimeUTCSafe(symbolPath!, DateTime.MinValue);
#endif
            DateTime assemblyCreateDate = FileHelper.GetLastWriteTimeUTCSafe(Assembly, DateTime.MaxValue);

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
#if DEBUG
            if (pdbEmbeddedResourceStream != null && assemblyCreateDate > symbolFileCreateDate)
            {
                using (FileStream fileStream = new FileStream(symbolPath, FileMode.Create, FileAccess.Write, FileShare.Read, 16384, FileOptions.SequentialScan))
                {
                    embeddedResourceStream.CopyTo(fileStream);
                }

                if (assemblyCreateDate != DateTime.MaxValue)
                {
                    try
                    {
                        File.SetLastWriteTime(symbolPath!, assemblyCreateDate);
                    }
                    catch { /* ignored */ }
                }
            }
#endif

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorCopyingFile, FileName), ex);
            logger.LogError(ex.Message);
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

            writer.WritePropertyName("Dependencies");
            writer.WriteStartArray();

            writer.WriteStartObject();

            writer.WritePropertyName("Name");
            writer.WriteValue("uTest.Bootstrapper");

            writer.WritePropertyName("Version");
            writer.WriteValue(ModuleFiles.uTestAssembly.GetName().Version.ToString());

            writer.WriteEndObject();

            writer.WriteEndArray();

            writer.WritePropertyName("Assemblies");
            writer.WriteStartArray();

            foreach (ModuleFile? file in ModuleFiles.Files.Concat(Enumerable.Repeat(testAssembly, 1)))
            {
                if (file == null || file.ModuleReferenceMode == ModuleFileReferenceMode.None)
                    continue;

                if (!ModuleFiles.IsFileApplicable(file))
                    continue;

                if (!ModuleFiles.IsServer && file.ModuleReferenceMode == ModuleFileReferenceMode.Dummies)
                    continue;

                writer.WriteStartObject();

                writer.WritePropertyName("Path");
                // nelson just appends the file name so this is correct
                writer.WriteValue("/" + file.FileName);

                writer.WritePropertyName("Role");
                if (!ModuleFiles.IsServer)
                {
                    writer.WriteValue("Client");
                }
                else
                {
                    writer.WriteValue(file.ModuleReferenceMode switch
                    {
                        ModuleFileReferenceMode.TestClient => "Server",
                        ModuleFileReferenceMode.Dummies => "Client",
                        _ => "Both_Optional"
                    });
                }

                if (!file.LoadFromFile)
                {
                    writer.WritePropertyName("Load_As_Byte_Array");
                    writer.WriteValue(true);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteEndObject();

            writer.Flush();
        }
        catch (Exception ex)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorCopyingFile, FileName), ex);
            logger.LogError(ex.Message);
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

internal sealed class BootstrapperModuleConfigFile : ModuleFile
{
    public bool IsEnabled { get; init; } = true;

    public BootstrapperModuleConfigFile() : base(SupportedTargetFramework.Both, "uTest.Bootstrapper.module") { }

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
            writer.WriteValue("uTest.Bootstrapper");

            writer.WritePropertyName("Version");
            writer.WriteValue(ModuleFiles.uTestAssembly.GetName().Version.ToString());


            writer.WritePropertyName("Assemblies");
            writer.WriteStartArray();

            writer.WriteStartObject();

            writer.WritePropertyName("Path");
            // nelson just appends the file name so this is correct
            writer.WriteValue("/uTest.Bootstrapper.dll");

            writer.WritePropertyName("Role");
            writer.WriteValue("Both_Optional");

            writer.WriteEndObject();

            writer.WriteEndArray();


            writer.WriteEndObject();

            writer.Flush();
        }
        catch (Exception ex)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorCopyingFile, FileName), ex);
            logger.LogError(ex.Message);
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