using System;

namespace uTest;

/// <summary>
/// An exception thrown when the client is missing authority to perform an operation on an actor.
/// </summary>
public class ActorMissingAuthorityException : InvalidOperationException
{
    /// <summary>
    /// The actor in question.
    /// </summary>
    public ITestActor Actor { get; }

    /// <summary>
    /// Create a new <see cref="ActorMissingAuthorityException"/>.
    /// </summary>
    public ActorMissingAuthorityException(ITestActor actor)
        : this(actor ?? throw new ArgumentNullException(nameof(actor)), GetDefaultMessage(actor))
    { }

    /// <summary>
    /// Create a new <see cref="ActorMissingAuthorityException"/> with a custom message.
    /// </summary>
    public ActorMissingAuthorityException(ITestActor actor, string? message)
        : base(message ?? GetDefaultMessage(actor ?? throw new ArgumentNullException(nameof(actor))))
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
    }

    /// <summary>
    /// Create a new <see cref="ActorMissingAuthorityException"/> with a custom message and inner exception.
    /// </summary>
    public ActorMissingAuthorityException(ITestActor actor, string? message, Exception? inner)
        : base(message ?? GetDefaultMessage(actor ?? throw new ArgumentNullException(nameof(actor))), inner)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
    }

    /// <summary>
    /// Create a new <see cref="ActorMissingAuthorityException"/> with an inner exception.
    /// </summary>
    public ActorMissingAuthorityException(ITestActor actor, Exception? inner)
        : base(GetDefaultMessage(actor ?? throw new ArgumentNullException(nameof(actor))), inner)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
    }

    /// <summary>
    /// Gets the default message for a <see cref="ActorMissingAuthorityException"/> given an <paramref name="actor"/>.
    /// </summary>
    protected static string GetDefaultMessage(ITestActor actor)
    {
        return string.Format(Properties.Resources.ActorMissingAuthorityExceptionDefaultMessage, actor.DisplayName);
    }
}