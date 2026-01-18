using DanielWillett.ModularRpcs.Exceptions;
using StackCleaner;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace uTest;

/// <summary>
/// <see cref="IExceptionFormatter"/> implementation that utilizes the <c>DanielWillett.StackCleaner</c> package to format stack traces.
/// </summary>
public class StackCleanerExceptionFormatter : IExceptionFormatter
{
    protected readonly object StackCleaner;

    public static IExceptionFormatter? GetStackCleanerFormatterIfInstalled(bool colored)
    {
        try
        {
            Assembly loadedAssembly = Assembly.Load("StackCleaner, Version=1.5.1.0, Culture=neutral, PublicKeyToken=2363f4e901d396ce");
            return loadedAssembly != null ? new StackCleanerExceptionFormatter(colored) : null;
        }
        catch
        {
            return null;
        }
    }

    public StackCleanerExceptionFormatter(bool colored)
    {
        StackCleaner = new StackTraceCleaner(new StackCleanerConfiguration
        {
            ColorFormatting = colored ? StackColorFormatType.ExtendedANSIColor : StackColorFormatType.None,
            Colors = Color32Config.Default,
            IncludeNamespaces = false,
            PutSourceDataOnNewLine = false
        });
    }

    public StackCleanerExceptionFormatter(object stackCleanerConfig)
    {
        if (stackCleanerConfig is not StackCleanerConfiguration config)
            throw new ArgumentException("Expected config of type StackCleanerConfiguration.");

        StackCleaner = new StackTraceCleaner(config);
    }

    protected virtual IEnumerable<Exception> EnumerateInnerExceptions(Exception ex)
    {
        return ex switch
        {
            AggregateException aggr               => aggr.InnerExceptions,

            ReflectionTypeLoadException rflTypeLd => rflTypeLd.LoaderExceptions.Where(x => x != null),

            _                                     => ex.InnerException != null
                                                        ? Enumerable.Repeat(ex.InnerException, 1)
                                                        : Enumerable.Empty<Exception>()
        };
    }

    /// <summary>
    /// Writes only the stack trace to the <paramref name="writer"/>.
    /// </summary>
    protected void FormatExceptionStackTrace(TextWriter writer, Exception ex)
    {
        StackTraceCleaner stc = (StackTraceCleaner)StackCleaner;

        stc.WriteToTextWriter(ex, writer);
    }

    public string FormatException(Exception ex)
    {
        StringBuilder sb = new StringBuilder();
        StringWriter writer = new StringWriter(sb);

        FormatException(writer, ex, 0);
        writer.Flush();
        return sb.ToString();
    }

    public virtual void FormatException(TextWriter writer, Exception ex, int indent)
    {
        StackTraceCleaner stc = (StackTraceCleaner)StackCleaner;

        string indention = indent == 0 ? string.Empty : new string(' ', indent * 2);

        StackColorFormatType colorFormatting = stc.Configuration.ColorFormatting;

        stc.WriteToTextWriter(ex.GetType(), writer);

        if (ex.Message != null)
        {
            writer.WriteLine();
            writer.Write(indention);
            string? clrSeq = colorFormatting switch
            {
                StackColorFormatType.ExtendedANSIColor => TerminalColorHelper.GetTerminalColorSequence(255, 102, 102),
                StackColorFormatType.ANSIColor => TerminalColorHelper.GetTerminalColorSequence(ConsoleColor.Red),
                StackColorFormatType.ANSIColorNoBright => TerminalColorHelper.GetTerminalColorSequence(ConsoleColor.DarkRed),
                _ => null
            };

            if (clrSeq != null)
            {
                writer.Write(ex.Message.Replace(TerminalColorHelper.ForegroundResetSequence, clrSeq));
                writer.Write(TerminalColorHelper.ForegroundResetSequence);
            }
            else
            {
                writer.Write(ex.Message);
            }
        }

        StringBuilder sb = ((StringWriter)writer).GetStringBuilder();
        writer.WriteLine();
        writer.Write(indention);
        int startPos = sb.Length;
        FormatExceptionStackTrace(writer, ex);
        writer.Flush();
        
        if (startPos == sb.Length)
        {
            writer.Write("[ ");
            writer.Write(Properties.Resources.Exception_Formatter_NoStackTrace);
            writer.Write(" ]");
        }
        
        if (ex is RpcInvocationException { RemoteStackTrace: { } remoteStackTrace })
        {
            writer.Write(Properties.Resources.Exception_Formatter_RPCRemoteException);
            writer.Write(':');
            writer.WriteLine();
            writer.Write(indention);
            writer.Write(remoteStackTrace);
        }

        using (IEnumerator<Exception>? innerExceptions = EnumerateInnerExceptions(ex)?.GetEnumerator())
        {
            if (innerExceptions != null && innerExceptions.MoveNext())
            {
                writer.Write(Properties.Resources.Exception_Formatter_InnerExceptions);
                writer.Write(':');
                writer.WriteLine();
                writer.Write(indention);
                int index = 1;
                do
                {
                    writer.WriteLine(Properties.Resources.Exception_Formatter_InnerExceptionNum, index);
                    ++index;
                    writer.Write(indention);
                    FormatException(writer, innerExceptions.Current!, indent + 1);
                    writer.WriteLine();
                    writer.WriteLine();
                    writer.Write(indention);
                } while (innerExceptions.MoveNext());
            }
        }
    }
}
