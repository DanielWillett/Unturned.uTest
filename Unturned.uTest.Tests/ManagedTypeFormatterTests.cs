using Mono.Cecil;
using Mono.Cecil.Rocks;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using uTest.Adapter;

namespace uTest.Test;

public class ManagedTypeFormatterTests
{
    private const string TestDllVersion = "1";
    
#nullable disable
    private string _file;
#nullable restore

    [SetUp]
    public void Setup()
    {
        string dir = Path.GetFullPath("Temp");
        Directory.CreateDirectory(dir);
        _file = Path.Combine(dir, $"testdll_{TestDllVersion}.dll");
        bool hasVersion = false;
        foreach (string otherFile in Directory.GetFiles(dir, "*.dll", SearchOption.TopDirectoryOnly))
        {
            if (otherFile.Equals(_file, StringComparison.Ordinal))
                hasVersion = true;
            else if (otherFile.StartsWith("testdll"))
                File.Delete(otherFile);
        }

        if (hasVersion)
        {
            return;
        }

        using ModuleDefinition def = ModuleDefinition.CreateModule("TestDll", new ModuleParameters
        {
            Kind = ModuleKind.Dll,
            Runtime = TargetRuntime.Net_4_0
        });

        TypeReference voidType = def.ImportReference(typeof(void));
        TypeReference intType = def.ImportReference(typeof(int));

        const TypeAttributes publicClass = TypeAttributes.Class | TypeAttributes.Public;

        TypeDefinition methodsType = new TypeDefinition(null, "M", publicClass);
        TypeDefinition methodsGenericType = new TypeDefinition(null, "M`1", publicClass);
        methodsGenericType.GenericParameters.Add(new GenericParameter("P0", methodsGenericType));

        TypeDefinition naNbC = new TypeDefinition("NamespaceA.NamespaceB", "Class", publicClass);
        def.Types.Add(naNbC);

        // note: some of these test examples are from https://github.com/microsoft/vstest/blob/main/docs/RFCs/0017-Managed-TestCase-Properties.md

        // NamespaceA.NamespaceB.ClassName`1+InnerClass`2
        {
            TypeDefinition u1 = new TypeDefinition("NamespaceA.NamespaceB", "ClassName`1", publicClass);
            u1.GenericParameters.Add(new GenericParameter("T1", u1));

            TypeDefinition u2 = new TypeDefinition("NamespaceA.NamespaceB", "InnerClass`2", publicClass | TypeAttributes.NestedPrivate);

            u2.GenericParameters.Add(new GenericParameter("T1", u2));
            u2.GenericParameters.Add(new GenericParameter("TT1", u2));
            u2.GenericParameters.Add(new GenericParameter("TT2", u2));

            u1.NestedTypes.Add(u2);

            def.Types.Add(u1);
        }

        // Method(NamespaceA.NamespaceB.Class)
        {
            MethodDefinition mtd = new MethodDefinition("Method", MethodAttributes.Public | MethodAttributes.Static, voidType);
            mtd.Parameters.Add(new ParameterDefinition("p1", ParameterAttributes.None, naNbC));
            methodsType.Methods.Add(mtd);
        }

        // '𝐌𝐲 𝗮𝘄𝗲𝘀𝗼𝗺𝗲 method w\\𝘢𝘯 𝒊𝒏𝒂𝒄𝒄𝒆𝒔𝒔𝒊𝒃𝒍𝒆 𝙣𝙖𝙢𝙚 🤦‍♂️'(NamespaceA.NamespaceB.Class)
        {
            MethodDefinition mtd = new MethodDefinition(@"𝐌𝐲 𝗮𝘄𝗲𝘀𝗼𝗺𝗲 method w\𝘢𝘯 𝒊𝒏𝒂𝒄𝒄𝒆𝒔𝒔𝒊𝒃𝒍𝒆 𝙣𝙖𝙢𝙚 🤦‍♂️", MethodAttributes.Public | MethodAttributes.Static, voidType);
            mtd.Parameters.Add(new ParameterDefinition("p1", ParameterAttributes.None, naNbC));
            methodsType.Methods.Add(mtd);
        }

        // Method(System.String,System.Int32)
        {
            MethodDefinition mtd = new MethodDefinition("Method", MethodAttributes.Public | MethodAttributes.Static, voidType);
            mtd.Parameters.Add(new ParameterDefinition("p1", ParameterAttributes.None, def.ImportReference(typeof(string))));
            mtd.Parameters.Add(new ParameterDefinition("p2", ParameterAttributes.None, intType));
            methodsType.Methods.Add(mtd);
        }

        // Method(System.String[])
        {
            MethodDefinition mtd = new MethodDefinition("Method", MethodAttributes.Public | MethodAttributes.Static, voidType);
            mtd.Parameters.Add(new ParameterDefinition("p1", ParameterAttributes.None, def.ImportReference(typeof(string)).MakeArrayType()));
            methodsType.Methods.Add(mtd);
        }

        // Method(System.Collections.Generic.List`1<System.String>)
        {
            MethodDefinition mtd = new MethodDefinition("Method", MethodAttributes.Public | MethodAttributes.Static, voidType);
            mtd.Parameters.Add(new ParameterDefinition("p1", ParameterAttributes.None,
                def.ImportReference(typeof(List<>)).MakeGenericInstanceType(def.ImportReference(typeof(string)))
            ));
            methodsType.Methods.Add(mtd);
        }

        // 'Method Name'(System.Collections.Generic.List`1<System.String>)
        {
            MethodDefinition mtd = new MethodDefinition("Method Name", MethodAttributes.Public | MethodAttributes.Static, voidType);
            mtd.Parameters.Add(new ParameterDefinition("p1", ParameterAttributes.None,
                def.ImportReference(typeof(List<>)).MakeGenericInstanceType(def.ImportReference(typeof(string)))
            ));
            methodsType.Methods.Add(mtd);
        }

        // Method(!0)
        {
            MethodDefinition mtd = new MethodDefinition("Method", MethodAttributes.Public | MethodAttributes.Static, voidType);
            mtd.Parameters.Add(new ParameterDefinition("p1", ParameterAttributes.None,
                methodsGenericType.GenericParameters[0]
            ));
            methodsGenericType.Methods.Add(mtd);
        }

        // Method(!!0)
        {
            MethodDefinition mtd = new MethodDefinition("Method", MethodAttributes.Public | MethodAttributes.Static, voidType);
            mtd.GenericParameters.Add(new GenericParameter("M0", mtd));
            mtd.Parameters.Add(new ParameterDefinition("p1", ParameterAttributes.None,
                mtd.GenericParameters[0]
            ));
            methodsType.Methods.Add(mtd);
        }

        // Method(System.Collections.Generic.List`1<!0>)
        {
            MethodDefinition mtd = new MethodDefinition("Method", MethodAttributes.Public | MethodAttributes.Static, voidType);
            mtd.Parameters.Add(new ParameterDefinition("p1", ParameterAttributes.None,
                def.ImportReference(typeof(List<>)).MakeGenericInstanceType(methodsGenericType.GenericParameters[0])
            ));
            methodsGenericType.Methods.Add(mtd);
        }

        // A`1+B`1
        {
            TypeDefinition u1 = new TypeDefinition(null, "A`1", publicClass);
            u1.GenericParameters.Add(new GenericParameter("T", u1));

            TypeDefinition u2 = new TypeDefinition(null, "B`1", TypeAttributes.Class | TypeAttributes.NestedPublic);

            u2.GenericParameters.Add(new GenericParameter("T", u2));
            u2.GenericParameters.Add(new GenericParameter("X", u2));

            u1.NestedTypes.Add(u2);

            // Method(!0, !1)
            {
                MethodDefinition mtd = new MethodDefinition("Method", MethodAttributes.Public | MethodAttributes.Static, voidType);
                mtd.Parameters.Add(new ParameterDefinition("t", ParameterAttributes.None,
                    u2.GenericParameters[0]
                ));
                mtd.Parameters.Add(new ParameterDefinition("x", ParameterAttributes.None,
                    u2.GenericParameters[1]
                ));
                u2.Methods.Add(mtd);
            }

            // Method(!1)
            {
                MethodDefinition mtd = new MethodDefinition("Method", MethodAttributes.Public | MethodAttributes.Static, voidType);
                mtd.Parameters.Add(new ParameterDefinition("x", ParameterAttributes.None,
                    u2.GenericParameters[1]
                ));
                u2.Methods.Add(mtd);
            }

            // Method(!0, !1, !!0)
            {
                MethodDefinition mtd = new MethodDefinition("Method", MethodAttributes.Public | MethodAttributes.Static, voidType);
                mtd.GenericParameters.Add(new GenericParameter("U", mtd));
                mtd.Parameters.Add(new ParameterDefinition("t", ParameterAttributes.None,
                    u2.GenericParameters[0]
                ));
                mtd.Parameters.Add(new ParameterDefinition("x", ParameterAttributes.None,
                    u2.GenericParameters[1]
                ));
                mtd.Parameters.Add(new ParameterDefinition("u", ParameterAttributes.None,
                    mtd.GenericParameters[0]
                ));
                u2.Methods.Add(mtd);
            }

            def.Types.Add(u1);
        }

        // CleanNamespaceName.SecondLevel.'𝐌𝐲 𝘤𝘭𝘢𝘴𝘴 with 𝘢𝘯 𝒊𝒏𝒂𝒄𝒄𝒆𝒔𝒔𝒊𝒃𝒍𝒆 𝙣𝙖𝙢𝙚 🤷‍♀️' | Sum(System.Int32,System.Int32)
        {
            TypeDefinition u1 = new TypeDefinition("CleanNamespaceName.SecondLevel", "𝐌𝐲 𝘤𝘭𝘢𝘴𝘴 with 𝘢𝘯 𝒊𝒏𝒂𝒄𝒄𝒆𝒔𝒔𝒊𝒃𝒍𝒆 𝙣𝙖𝙢𝙚 🤷‍♀️", publicClass);

            MethodDefinition mtd = new MethodDefinition("Sum", MethodAttributes.Public, intType);
            mtd.Parameters.Add(new ParameterDefinition("x", ParameterAttributes.None, intType));
            mtd.Parameters.Add(new ParameterDefinition("y", ParameterAttributes.None, intType));

            u1.Methods.Add(mtd);
            def.Types.Add(u1);
        }

        // CleanNamespaceName.SecondLevel.'Deeply wrong '.'namespace name'.NamespaceA.Class1 | 'Method with . in it'(System.Int32,System.Int32)
        {
            TypeDefinition u1 = new TypeDefinition("CleanNamespaceName.SecondLevel.Deeply wrong .namespace name.NamespaceA", "Class1", publicClass);

            MethodDefinition mtd = new MethodDefinition("Method with . in it", MethodAttributes.Public, intType);
            mtd.Parameters.Add(new ParameterDefinition("x", ParameterAttributes.None, intType));
            mtd.Parameters.Add(new ParameterDefinition("y", ParameterAttributes.None, intType));

            u1.Methods.Add(mtd);
            def.Types.Add(u1);
        }

        // CleanNamespaceName.ClassName\\Continues | MethodName(System.Int32,System.Int32)
        {
            TypeDefinition u1 = new TypeDefinition("CleanNamespaceName", @"ClassName\Continues", publicClass);

            MethodDefinition mtd = new MethodDefinition("MethodName", MethodAttributes.Public, voidType);
            mtd.Parameters.Add(new ParameterDefinition("x", ParameterAttributes.None, intType));
            mtd.Parameters.Add(new ParameterDefinition("y", ParameterAttributes.None, intType));

            u1.Methods.Add(mtd);
            def.Types.Add(u1);
        }

        TypeDefinition cnnClassName = new TypeDefinition("CleanNamespaceName", "ClassName", publicClass);

        // CleanNamespaceName.ClassName | 'MethodName`1'(System.Int32,System.Int32)
        {
            MethodDefinition mtd = new MethodDefinition("MethodName`1", MethodAttributes.Public, voidType);
            mtd.Parameters.Add(new ParameterDefinition("x", ParameterAttributes.None, intType));
            mtd.Parameters.Add(new ParameterDefinition("y", ParameterAttributes.None, intType));

            cnnClassName.Methods.Add(mtd);
        }

        // CleanNamespaceName.ClassName | MethodName`1(System.Int32,System.Int32)
        {
            MethodDefinition mtd = new MethodDefinition("MethodName`1", MethodAttributes.Public, voidType);
            mtd.GenericParameters.Add(new GenericParameter("M0", mtd));
            mtd.Parameters.Add(new ParameterDefinition("x", ParameterAttributes.None, intType));
            mtd.Parameters.Add(new ParameterDefinition("y", ParameterAttributes.None, intType));

            cnnClassName.Methods.Add(mtd);
        }

        // custom tests

        // Method   (no params)
        {
            MethodDefinition mtd = new MethodDefinition("Method", MethodAttributes.Public | MethodAttributes.Static, voidType);
            methodsType.Methods.Add(mtd);
        }

        // Method(System.Int32*)
        {
            MethodDefinition mtd = new MethodDefinition("Method_1", MethodAttributes.Public | MethodAttributes.Static, voidType);
            mtd.Parameters.Add(new ParameterDefinition("p1", ParameterAttributes.None,
                intType.MakePointerType()
            ));
            methodsType.Methods.Add(mtd);
        }

        // Method(System.Int32&)
        {
            MethodDefinition mtd = new MethodDefinition("Method_2", MethodAttributes.Public | MethodAttributes.Static, voidType);
            mtd.Parameters.Add(new ParameterDefinition("p1", ParameterAttributes.None,
                intType.MakeByReferenceType()
            ));
            methodsType.Methods.Add(mtd);
        }

        // Method(System.Int32[,])
        {
            MethodDefinition mtd = new MethodDefinition("Method_3", MethodAttributes.Public | MethodAttributes.Static, voidType);
            mtd.Parameters.Add(new ParameterDefinition("p1", ParameterAttributes.None,
                intType.MakeArrayType(2)
            ));
            methodsType.Methods.Add(mtd);
        }

        // Method(System.Int32[*])
        {
            MethodDefinition mtd = new MethodDefinition("Method_4", MethodAttributes.Public | MethodAttributes.Static, voidType);
            ArrayType oneDimNonSzArrayType = intType.MakeArrayType();
            oneDimNonSzArrayType.Dimensions[0] = new ArrayDimension(1, 8);
            mtd.Parameters.Add(new ParameterDefinition("p1", ParameterAttributes.None,
                oneDimNonSzArrayType
            ));
            methodsType.Methods.Add(mtd);
        }

        // Method(System.Int32[]&)
        {
            MethodDefinition mtd = new MethodDefinition("Method_5", MethodAttributes.Public | MethodAttributes.Static, voidType);
            mtd.Parameters.Add(new ParameterDefinition("p1", ParameterAttributes.None,
                intType.MakeArrayType().MakeByReferenceType()
            ));
            methodsType.Methods.Add(mtd);
        }

        // Method(System.Int32[][])
        {
            MethodDefinition mtd = new MethodDefinition("Method_6", MethodAttributes.Public | MethodAttributes.Static, voidType);
            mtd.Parameters.Add(new ParameterDefinition("p1", ParameterAttributes.None,
                intType.MakeArrayType().MakeArrayType()
            ));
            methodsType.Methods.Add(mtd);
        }

        // Method(System.Int32[][,])
        {
            MethodDefinition mtd = new MethodDefinition("Method_7", MethodAttributes.Public | MethodAttributes.Static, voidType);
            mtd.Parameters.Add(new ParameterDefinition("p1", ParameterAttributes.None,
                intType.MakeArrayType().MakeArrayType(2)
            ));
            methodsType.Methods.Add(mtd);
        }

        // Method(System.Int32[,][])
        {
            MethodDefinition mtd = new MethodDefinition("Method_8", MethodAttributes.Public | MethodAttributes.Static, voidType);
            mtd.Parameters.Add(new ParameterDefinition("p1", ParameterAttributes.None,
                intType.MakeArrayType(2).MakeArrayType()
            ));
            methodsType.Methods.Add(mtd);
        }

        // Method(System.Int32*[,][])
        {
            MethodDefinition mtd = new MethodDefinition("Method_9", MethodAttributes.Public | MethodAttributes.Static, voidType);
            mtd.Parameters.Add(new ParameterDefinition("p1", ParameterAttributes.None,
                intType.MakePointerType().MakeArrayType(2).MakeArrayType()
            ));
            methodsType.Methods.Add(mtd);
        }

        // Method(System.Int32[,]*[])
        {
            MethodDefinition mtd = new MethodDefinition("Method_10", MethodAttributes.Public | MethodAttributes.Static, voidType);
            mtd.Parameters.Add(new ParameterDefinition("p1", ParameterAttributes.None,
                intType.MakeArrayType(2).MakePointerType().MakeArrayType()
            ));
            methodsType.Methods.Add(mtd);
        }

        // Method(System.Int32[,][]*)
        {
            MethodDefinition mtd = new MethodDefinition("Method_11", MethodAttributes.Public | MethodAttributes.Static, voidType);
            mtd.Parameters.Add(new ParameterDefinition("p1", ParameterAttributes.None,
                intType.MakeArrayType(2).MakeArrayType().MakePointerType()
            ));
            methodsType.Methods.Add(mtd);
        }

        def.Types.Add(cnnClassName);

        def.Types.Add(methodsType);
        def.Types.Add(methodsGenericType);

        def.Write(_file);
    }

    [NUnit.Framework.Test]
    public void ManagedTypeAndMethodNames()
    {
        using ModuleDefinition def = ModuleDefinition.ReadModule(_file);

        AssertManagedType(
            def,
            "NamespaceA.NamespaceB.ClassName`1/NamespaceA.NamespaceB.InnerClass`2",
            "NamespaceA.NamespaceB.ClassName`1+InnerClass`2"
        );

        AssertManagedMethod(
            def,
            "M", "Method",
            "Method(NamespaceA.NamespaceB.Class)",
            m => m.Parameters.Count == 1 && TypeNameIs(m.Parameters[0].ParameterType, "NamespaceA.NamespaceB.Class")
        );
        AssertManagedMethod(
            def,
            "M", @"𝐌𝐲 𝗮𝘄𝗲𝘀𝗼𝗺𝗲 method w\𝘢𝘯 𝒊𝒏𝒂𝒄𝒄𝒆𝒔𝒔𝒊𝒃𝒍𝒆 𝙣𝙖𝙢𝙚 🤦‍♂️",
            @"'𝐌𝐲 𝗮𝘄𝗲𝘀𝗼𝗺𝗲 method w\\𝘢𝘯 𝒊𝒏𝒂𝒄𝒄𝒆𝒔𝒔𝒊𝒃𝒍𝒆 𝙣𝙖𝙢𝙚 🤦‍♂️'(NamespaceA.NamespaceB.Class)",
            m => m.Parameters.Count == 1 && TypeNameIs(m.Parameters[0].ParameterType, "NamespaceA.NamespaceB.Class")
        );

        AssertManagedMethod(
            def,
            "M", "Method",
            "Method(System.String,System.Int32)",
            m => m.Parameters.Count == 2
                 && TypeNameIs(m.Parameters[0].ParameterType, "System.String")
                 && TypeNameIs(m.Parameters[1].ParameterType, "System.Int32")
        );

        AssertManagedMethod(
            def,
            "M", "Method",
            "Method(System.String[])",
            m => m.Parameters.Count == 1
                 && TypeNameIs(m.Parameters[0].ParameterType, "System.String[]")
        );

        AssertManagedMethod(
            def,
            "M", "Method",
            "Method(System.Collections.Generic.List`1<System.String>)",
            m => m.Parameters.Count == 1
                 && TypeNameIs(m.Parameters[0].ParameterType, "System.Collections.Generic.List`1<System.String>")
        );
        AssertManagedMethod(
            def,
            "M", "Method Name",
            "'Method Name'(System.Collections.Generic.List`1<System.String>)",
            m => m.Parameters.Count == 1
                 && TypeNameIs(m.Parameters[0].ParameterType, "System.Collections.Generic.List`1<System.String>")
        );

        AssertManagedMethod(
            def,
            "M`1", "Method",
            "Method(!0)",
            m => m.Parameters.Count == 1
                 && m.Parameters[0].ParameterType.IsGenericParameter
                 && !m.HasGenericParameters
        );

        AssertManagedMethod(
            def,
            "M", "Method",
            "Method(!!0)",
            m => m.Parameters.Count == 1
                 && m.Parameters[0].ParameterType.IsGenericParameter
                 && m.HasGenericParameters
        );

        AssertManagedMethod(
            def,
            "M`1", "Method",
            "Method(System.Collections.Generic.List`1<!0>)",
            m => m.Parameters.Count == 1
                 && !m.Parameters[0].ParameterType.IsGenericParameter
                 && !m.HasGenericParameters
                 && m.Parameters[0].ParameterType.IsGenericInstance
        );

        AssertManagedType(def, "A`1", "A`1");
        AssertManagedType(def, "A`1/B`1", "A`1+B`1");

        AssertManagedMethod(
            def,
            "A`1/B`1", "Method",
            "Method(!0,!1)",
            m => m.Parameters.Count == 2
        );
        AssertManagedMethod(
            def,
            "A`1/B`1", "Method",
            "Method(!1)",
            m => m.Parameters.Count == 1
        );
        AssertManagedMethod(
            def,
            "A`1/B`1", "Method",
            "Method(!0,!1,!!0)",
            m => m.Parameters.Count == 3
        );

        AssertManagedType(
            def,
            "CleanNamespaceName.SecondLevel.𝐌𝐲 𝘤𝘭𝘢𝘴𝘴 with 𝘢𝘯 𝒊𝒏𝒂𝒄𝒄𝒆𝒔𝒔𝒊𝒃𝒍𝒆 𝙣𝙖𝙢𝙚 🤷‍♀️",
            "CleanNamespaceName.SecondLevel.'𝐌𝐲 𝘤𝘭𝘢𝘴𝘴 with 𝘢𝘯 𝒊𝒏𝒂𝒄𝒄𝒆𝒔𝒔𝒊𝒃𝒍𝒆 𝙣𝙖𝙢𝙚 🤷‍♀️'"
        );
        AssertManagedMethod(
            def,
            "CleanNamespaceName.SecondLevel.𝐌𝐲 𝘤𝘭𝘢𝘴𝘴 with 𝘢𝘯 𝒊𝒏𝒂𝒄𝒄𝒆𝒔𝒔𝒊𝒃𝒍𝒆 𝙣𝙖𝙢𝙚 🤷‍♀️", "Sum",
            "Sum(System.Int32,System.Int32)"
        );

        AssertManagedType(
            def,
            "CleanNamespaceName.SecondLevel.Deeply wrong .namespace name.NamespaceA.Class1",
            "CleanNamespaceName.SecondLevel.'Deeply wrong '.'namespace name'.NamespaceA.Class1"
        );
        AssertManagedMethod(
            def,
            "CleanNamespaceName.SecondLevel.Deeply wrong .namespace name.NamespaceA.Class1", "Method with . in it",
            "'Method with . in it'(System.Int32,System.Int32)"
        );

        AssertManagedType(
            def,
            @"CleanNamespaceName.ClassName\Continues",
            @"CleanNamespaceName.'ClassName\\Continues'"
        );
        AssertManagedMethod(
            def,
            @"CleanNamespaceName.ClassName\Continues", "MethodName",
            "MethodName(System.Int32,System.Int32)"
        );

        AssertManagedType(
            def,
            "CleanNamespaceName.ClassName",
            "CleanNamespaceName.ClassName"
        );
        AssertManagedMethod(
            def,
            "CleanNamespaceName.ClassName", "MethodName`1",
            "'MethodName`1'(System.Int32,System.Int32)",
            x => !x.HasGenericParameters
        );

        AssertManagedType(
            def,
            "CleanNamespaceName.ClassName",
            "CleanNamespaceName.ClassName"
        );
        AssertManagedMethod(
            def,
            "CleanNamespaceName.ClassName", "MethodName`1",
            "MethodName`1(System.Int32,System.Int32)",
            x => x.HasGenericParameters
        );


        AssertManagedMethod(
            def,
            "M", "Method",
            "Method",
            m => m.Parameters.Count == 0
        );

        AssertManagedMethod(
            def,
            "M", "Method_1",
            "Method_1(System.Int32*)"
        );
        AssertManagedMethod(
            def,
            "M", "Method_2",
            "Method_2(System.Int32&)"
        );
        AssertManagedMethod(
            def,
            "M", "Method_3",
            "Method_3(System.Int32[,])"
        );
        AssertManagedMethod(
            def,
            "M", "Method_4",
            "Method_4(System.Int32[*])"
        );
        AssertManagedMethod(
            def,
            "M", "Method_5",
            "Method_5(System.Int32[]&)"
        );
        AssertManagedMethod(
            def,
            "M", "Method_6",
            "Method_6(System.Int32[][])"
        );
        AssertManagedMethod(
            def,
            "M", "Method_7",
            "Method_7(System.Int32[][,])"
        );
        AssertManagedMethod(
            def,
            "M", "Method_8",
            "Method_8(System.Int32[,][])"
        );
        AssertManagedMethod(
            def,
            "M", "Method_9",
            "Method_9(System.Int32*[,][])"
        );
        AssertManagedMethod(
            def,
            "M", "Method_10",
            "Method_10(System.Int32[,]*[])"
        );
        AssertManagedMethod(
            def,
            "M", "Method_11",
            "Method_11(System.Int32[,][]*)"
        );
    }

    private static bool TypeNameIs(TypeReference tr, string n)
    {
        return ManagedTypeFormatter.GetManagedType(tr).Equals(n, StringComparison.Ordinal);
    }

    private static void AssertManagedType(ModuleDefinition def, string path, string expected)
    {
        TypeDefinition? type = def.GetType(path);
        NUnit.Framework.Assert.NotNull(type);

        string managedType = ManagedTypeFormatter.GetManagedType(type);

        NUnit.Framework.Assert.That(managedType, Is.EqualTo(expected));
    }

    private static void AssertManagedMethod(ModuleDefinition def, string typePath, string methodName, string expected, Func<MethodDefinition, bool>? selector = null)
    {
        TypeDefinition? type = def.GetType(typePath);
        NUnit.Framework.Assert.NotNull(type);

        MethodDefinition method = type.Methods.Single(x => x.Name.Equals(methodName, StringComparison.Ordinal) && (selector == null || selector(x)));

        string managedMethod = ManagedTypeFormatter.GetManagedMethod(method);

        NUnit.Framework.Assert.That(managedMethod, Is.EqualTo(expected));
    }
}