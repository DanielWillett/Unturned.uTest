using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;

namespace uTest;

/// <summary>
/// <see cref="ThreadedConsoleInputOutput"/> outputs on a different thread, so tests need to wait
/// for the pending messages to flush before they can start or stop listening for messages.
/// </summary>
internal static class CommandWindowSynchronizationHelper
{
    private static Action? _performFlush;
    private static bool _hasAttemptedToGenerate;

    internal static void FlushCommandWindow()
    {
        GameThread.Assert();

        // not dedicated server
        if (Dedicator.commandWindow == null)
            return;

        Dedicator.commandWindow.update();

        if (!_hasAttemptedToGenerate)
        {
            try
            {
                GenerateMethod();
            }
            catch (Exception ex)
            {
                CommandWindow.LogError(ex);
            }
            finally
            {
                _hasAttemptedToGenerate = true;
            }
        }

        if (_performFlush == null)
        {
            Thread.Sleep(20);
        }
        else
        {
            _performFlush.Invoke();
        }
    }

    private static void GenerateMethod()
    {
        /*
         *  // effectively generates the following code
         *
         *  foreach (ThreadedConsoleInputOutput io in Dedicator.commandWindow.ioHandlers.OfType<ThreadedConsoleInputOutput>())
         *  {
         *    while (io.pendingOutputs.Count > 0)
         *      Thread.Sleep(5);
         *  }
         *
         */

        MethodInfo? getCommandWindow = typeof(Dedicator)
            .GetProperty(nameof(Dedicator.commandWindow), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?
            .GetMethod;
        if (getCommandWindow == null)
            return;

        FieldInfo? commandWindowIoHandlers = typeof(CommandWindow)
            .GetField("ioHandlers", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (commandWindowIoHandlers == null)
            return;

        FieldInfo? threadedConsolePendingOutputs = typeof(ThreadedConsoleInputOutput)
            .GetField("pendingOutputs", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (threadedConsolePendingOutputs == null)
            return;

        Type concurrentQueueType = threadedConsolePendingOutputs.FieldType;
        MethodInfo? getConcurrentQueueCount = concurrentQueueType
            .GetProperty(nameof(ConcurrentQueue<>.Count), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .GetMethod;
        if (getConcurrentQueueCount == null)
            return;


        MethodInfo? getListCount = typeof(List<ICommandInputOutput>)
            .GetProperty(nameof(List<>.Count), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .GetMethod;
        if (getListCount == null)
            return;
        MethodInfo? getListItem = typeof(List<ICommandInputOutput>)
            .GetProperty("Item", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, typeof(ICommandInputOutput), [ typeof(int) ], null)?
            .GetMethod;
        if (getListItem == null)
            return;

        DynamicMethod method = new DynamicMethod(
            "PerformFlush",
            MethodAttributes.Public | MethodAttributes.Static,
            CallingConventions.Any,
            typeof(void),
            Type.EmptyTypes,
            typeof(CommandWindowSynchronizationHelper),
            true
        )
        {
            InitLocals = false
        };

        ILGenerator il = method.GetILGenerator();

        LocalBuilder lclIoHandlers = il.DeclareLocal(typeof(List<ICommandInputOutput>));
        LocalBuilder lclCount = il.DeclareLocal(typeof(int));

        il.Emit(OpCodes.Call, getCommandWindow);
        il.Emit(OpCodes.Ldfld, commandWindowIoHandlers);
        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Stloc, lclIoHandlers);

        // int ct = lclIoHandlers.Count
        il.Emit(OpCodes.Callvirt, getListCount);
        il.Emit(OpCodes.Stloc, lclCount);

        LocalBuilder lclIterator = il.DeclareLocal(typeof(int));
        // for (int i = 0; i < ct; ++i)
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Stloc, lclIterator);

        Label condition = il.DefineLabel();
        Label collector = il.DefineLabel();
        Label failCondition = il.DefineLabel();

        il.MarkLabel(condition);

        // i < ct
        il.Emit(OpCodes.Ldloc, lclIterator);
        il.Emit(OpCodes.Ldloc, lclCount);
        il.Emit(OpCodes.Bge, failCondition);

        // for loop body

        LocalBuilder lclCurrent = il.DeclareLocal(typeof(ICommandInputOutput));
        il.Emit(OpCodes.Ldloc, lclIoHandlers);
        il.Emit(OpCodes.Ldloc, lclIterator);
        il.Emit(OpCodes.Call, getListItem);
        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Stloc, lclCurrent);

        il.Emit(OpCodes.Isinst, typeof(ThreadedConsoleInputOutput));
        il.Emit(OpCodes.Brfalse, collector);

        // while (threadedConsole.pendingOutputs.Count > 0)
        Label whileCondition = il.DefineLabel();
        il.MarkLabel(whileCondition);
        il.Emit(OpCodes.Ldloc, lclCurrent);
        il.Emit(OpCodes.Ldfld, threadedConsolePendingOutputs);
        il.Emit(OpCodes.Callvirt, getConcurrentQueueCount);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ble, collector);

        // Thread.Sleep(5)
        il.Emit(OpCodes.Ldc_I4, 5);
        il.Emit(OpCodes.Call, new Action<int>(Thread.Sleep).Method);

        // end while
        il.Emit(OpCodes.Br, whileCondition);

        // end for loop body

        // ++i
        il.MarkLabel(collector);
        il.Emit(OpCodes.Ldloc, lclIterator);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc, lclIterator);
        il.Emit(OpCodes.Br, condition);

        il.MarkLabel(failCondition);

        il.Emit(OpCodes.Ret);

        _performFlush = (Action)method.CreateDelegate(typeof(Action));
    }
}