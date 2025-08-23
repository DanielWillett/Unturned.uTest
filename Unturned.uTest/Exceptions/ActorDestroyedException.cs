using System;

namespace uTest;

/// <summary>
/// An exception thrown when an actor has been destroyed and can't be operated on.
/// </summary>
public class ActorDestroyedException : InvalidOperationException
{
    /// <summary>
    /// The actor in question.
    /// </summary>
    public ITestActor Actor { get; }

    /// <summary>
    /// Create a new <see cref="ActorDestroyedException"/>.
    /// </summary>
    public ActorDestroyedException(ITestActor actor)
        : this(actor ?? throw new ArgumentNullException(nameof(actor)), GetDefaultMessage(actor))
    { }

    /// <summary>
    /// Create a new <see cref="ActorDestroyedException"/> with a custom message.
    /// </summary>
    public ActorDestroyedException(ITestActor actor, string? message)
        : base(message ?? GetDefaultMessage(actor ?? throw new ArgumentNullException(nameof(actor))))
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
    }

    /// <summary>
    /// Create a new <see cref="ActorDestroyedException"/> with a custom message and inner exception.
    /// </summary>
    public ActorDestroyedException(ITestActor actor, string? message, Exception? inner)
        : base(message ?? GetDefaultMessage(actor ?? throw new ArgumentNullException(nameof(actor))), inner)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
    }

    /// <summary>
    /// Create a new <see cref="ActorDestroyedException"/> with an inner exception.
    /// </summary>
    public ActorDestroyedException(ITestActor actor, Exception? inner)
        : base(GetDefaultMessage(actor ?? throw new ArgumentNullException(nameof(actor))), inner)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
    }

    /// <summary>
    /// Gets the default message for a <see cref="ActorDestroyedException"/> given an <paramref name="actor"/>.
    /// </summary>
    protected static string GetDefaultMessage(ITestActor actor)
    {
        return string.Format(Properties.Resources.ActorDestroyedExceptionDefaultMessage, actor.DisplayName);
    }
}