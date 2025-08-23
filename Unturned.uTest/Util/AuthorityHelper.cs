namespace uTest;

internal static class AuthorityHelper
{
    /// <summary>
    /// Throws an error if not <see cref="Provider.isServer"/>.
    /// </summary>
    /// <exception cref="ActorMissingAuthorityException"/>
    public static void AssertServer(ITestActor actor)
    {
        if (!Provider.isServer)
            throw new ActorMissingAuthorityException(actor);
    }
}
