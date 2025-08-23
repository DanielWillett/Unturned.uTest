using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace uTest.Adapter;

[FileExtension(".dll")]
[DefaultExecutorUri(TestExecuter.Uri)]
public class TestDiscoverer : BaseTestInterface, ITestDiscoverer
{
    private const string TestAssemblyName = "Unturned.uTest";
    private const string TestClassInterfaceFullName = "uTest.ITestClass";
    private const string TestMethodAttributeFullName = "uTest.TestAttribute";

    private static readonly Uri ExecutorUriObj = new Uri(TestExecuter.Uri);

    private static readonly TestProperty HierarchyProperty = TestProperty.Register(
        "TestCase.Hierarchy",
        "Hierarchy",
        string.Empty,
        string.Empty,
        typeof(string[]),
        o => o is string[] { Length: 4 },
        TestPropertyAttributes.Hidden,
        typeof(TestCase)
    );

    /// <inheritdoc />
    public void DiscoverTests(IEnumerable<string> sources, IDiscoveryContext discoveryContext, IMessageLogger logger, ITestCaseDiscoverySink discoverySink)
    {
        Init(logger);

        List<string> sourceList = sources as List<string> ?? new List<string>(sources);

        string thisAsmPath = Assembly.GetExecutingAssembly().Location;

        if (!string.IsNullOrEmpty(thisAsmPath))
            sourceList.Remove(thisAsmPath);

        if (sourceList.Count == 0)
            return;

        Stopwatch sw = Stopwatch.StartNew();
        Info(Properties.Resources.LogDiscoveringTests);
        foreach (string source in sourceList)
        {
            Info($" * {source}");
        }

        RunConfiguration? configuration = null;
        string? settings = discoveryContext.RunSettings?.SettingsXml;
        if (!string.IsNullOrEmpty(settings))
            configuration = XmlRunSettingsUtilities.GetRunConfigurationNode(settings);

        bool collectSourceInfo = configuration is not { ShouldCollectSourceInformation: false };
        Info($" Collect source info: {collectSourceInfo}");

        RuntimeHelpers.RunClassConstructor(typeof(TestCase).TypeHandle);

        TestProperty? managedTypeProperty = TestProperty.Find("TestCase.ManagedType");
        TestProperty? managedMethodProperty = TestProperty.Find("TestCase.ManagedMethod");

        if (managedTypeProperty == null)
            Warn("Failed to find property TestCase.ManagedType.");
        if (managedMethodProperty == null)
            Warn("Failed to find property TestCase.ManagedMethod.");

        int testCt = 0;

        foreach (string file in sourceList)
        {
            string fullFilePath;

            try
            {
                fullFilePath = Path.GetFullPath(file);
            }
            catch
            {
                fullFilePath = file;
            }

            if (!File.Exists(fullFilePath))
            {
                Warn(string.Format(Properties.Resources.LogSkippingSourceFileNotFound, file));
                continue;
            }

            ModuleDefinition? mod = null;
            try
            {
                mod = ModuleDefinition.ReadModule(file, new ReaderParameters(ReadingMode.Immediate)
                {
                    ReadSymbols = collectSourceInfo
                });

                if (!mod.AssemblyReferences.Any(x => string.Equals(x.Name, TestAssemblyName)))
                {
                    mod.Dispose();
                    Info(string.Format(Properties.Resources.LogSkippingSourceDoesntReferenceUTest, file, TestAssemblyName));
                    continue;
                }
            }
            catch (FileNotFoundException)
            {
                Warn(string.Format(Properties.Resources.LogSkippingSourceFileNotFound, file));
            }
            catch (DirectoryNotFoundException)
            {
                Warn(string.Format(Properties.Resources.LogSkippingSourceFileNotFound, file));
            }
            catch (Exception ex)
            {
                Warn(string.Format(Properties.Resources.LogSkippingSourceFailedToReadFile, file) + System.Environment.NewLine + ex);
            }

            if (mod == null)
            {
                continue;
            }

            bool symbolsHasFailed = false;
            try
            {
                foreach (TypeDefinition testClassType in mod.Types)
                {
                    if (!testClassType.Interfaces.Any(x => string.Equals(x.InterfaceType.FullName, TestClassInterfaceFullName)))
                        continue;

                    string? managedType = null;
                    foreach (MethodDefinition testMethod in testClassType.Methods)
                    {
                        if (!testMethod.CustomAttributes.Any(x => string.Equals(x.AttributeType.FullName, TestMethodAttributeFullName)))
                            continue;

                        managedType ??= ManagedTypeFormatter.GetManagedType(testClassType);
                        string managedMethod = ManagedTypeFormatter.GetManagedMethod(testMethod);

                        TestCase testCase = new TestCase(managedType + "." + managedMethod, ExecutorUriObj, fullFilePath);

                        if (managedTypeProperty != null)
                        {
                            testCase.SetPropertyValue(managedTypeProperty, managedType);
                        }
                        if (managedMethodProperty != null)
                        {
                            testCase.SetPropertyValue(managedMethodProperty, managedMethod);
                        }

                        if (mod.HasSymbols && !symbolsHasFailed)
                        {
                            try
                            {
                                MethodDebugInformation debugInfo = mod.SymbolReader.Read(testMethod);
                                if (debugInfo.HasSequencePoints)
                                {
                                    SequencePoint? sp = debugInfo.SequencePoints.FirstOrDefault();
                                    if (sp != null)
                                    {
                                        testCase.LineNumber = sp.StartLine;
                                        testCase.CodeFilePath = sp.Document.Url;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                symbolsHasFailed = true;
                                Warn(string.Format(Properties.Resources.LogExceptionReadingSymbols, file) + System.Environment.NewLine + ex);
                            }
                        }

                        string?[] hierarchy = new string?[4];

                        hierarchy[1] = testClassType.Namespace;
                        hierarchy[2] = testClassType.Name;
                        hierarchy[3] = testMethod.Name;

                        testCase.SetPropertyValue(HierarchyProperty, hierarchy);

                        discoverySink.SendTestCase(testCase);
                        ++testCt;
                    }
                }
            }
            catch (Exception ex)
            {
                Warn(string.Format(Properties.Resources.LogExceptionSearchingModule, file) + System.Environment.NewLine + ex);
            }
            finally
            {
                mod.Dispose();
            }
        }

        Info(string.Format(Properties.Resources.LogTestDiscoveryFinished, testCt, sw.ElapsedTicks / (double)Stopwatch.Frequency * 1000d));
    }
}