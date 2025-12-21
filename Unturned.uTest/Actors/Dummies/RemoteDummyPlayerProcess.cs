using System;
using System.Collections.Generic;
using System.Text;

namespace uTest.Dummies;

internal class RemoteDummyPlayerProcess : IDummyPlayerController
{
    public RemoteDummyPlayerActor Actor { get; }

    internal RemoteDummyPlayerProcess(RemoteDummyPlayerActor actor)
    {
        Actor = actor;
    }
}
