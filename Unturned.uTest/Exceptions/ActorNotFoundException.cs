using System;

namespace uTest;

/// <summary>
/// An exception thrown when an actor can't be found by some kind of unique ID.
/// </summary>
public class ActorNotFoundException : InvalidOperationException
{
    /// <summary>
    /// Create a new <see cref="ActorNotFoundException"/>.
    /// </summary>
    public ActorNotFoundException() : base(GetDefaultMessage()) { }

    /// <summary>
    /// Create a new <see cref="ActorNotFoundException"/> given an ID.
    /// </summary>
    public ActorNotFoundException(string? id) : base(id == null ? GetDefaultMessage() : GetDefaultMessage(id)) { }

    /// <summary>
    /// Gets the default message for a <see cref="ActorNotFoundException"/>.
    /// </summary>
    protected static string GetDefaultMessage()
    {
        return Properties.Resources.ActorNotFoundExceptionDefaultMessage;
    }

    /// <summary>
    /// Gets the default message for a <see cref="ActorNotFoundException"/> given an ID.
    /// </summary>
    protected static string GetDefaultMessage(string idToString)
    {
        return string.Format(Properties.Resources.ActorNotFoundExceptionDefaultMessage, idToString);
    }
}