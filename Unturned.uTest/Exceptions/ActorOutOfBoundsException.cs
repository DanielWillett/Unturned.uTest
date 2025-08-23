using System;

namespace uTest;

/// <summary>
/// An exception thrown when an actor is out of bounds and can't be operated on, such as a barricade not being in a valid region.
/// </summary>
public class ActorOutOfBoundsException : InvalidOperationException
{
    /// <summary>
    /// The actor in question.
    /// </summary>
    public ITestActor Actor { get; }

    /// <summary>
    /// Create a new <see cref="ActorOutOfBoundsException"/>.
    /// </summary>
    public ActorOutOfBoundsException(ITestActor actor)
        : this(actor ?? throw new ArgumentNullException(nameof(actor)), GetDefaultMessage(actor))
    { }

    /// <summary>
    /// Create a new <see cref="ActorOutOfBoundsException"/> with a custom message.
    /// </summary>
    public ActorOutOfBoundsException(ITestActor actor, string? message)
        : base(message ?? GetDefaultMessage(actor ?? throw new ArgumentNullException(nameof(actor))))
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
    }

    /// <summary>
    /// Create a new <see cref="ActorOutOfBoundsException"/> with a custom message and inner exception.
    /// </summary>
    public ActorOutOfBoundsException(ITestActor actor, string? message, Exception? inner)
        : base(message ?? GetDefaultMessage(actor ?? throw new ArgumentNullException(nameof(actor))), inner)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
    }

    /// <summary>
    /// Create a new <see cref="ActorOutOfBoundsException"/> with an inner exception.
    /// </summary>
    public ActorOutOfBoundsException(ITestActor actor, Exception? inner)
        : base(GetDefaultMessage(actor ?? throw new ArgumentNullException(nameof(actor))), inner)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
    }

    /// <summary>
    /// Gets the default message for a <see cref="ActorOutOfBoundsException"/> given an <paramref name="actor"/>.
    /// </summary>
    protected static string GetDefaultMessage(ITestActor actor)
    {
        return string.Format(Properties.Resources.ActorOutOfBoundsExceptionDefaultMessage, actor.DisplayName);
    }
}