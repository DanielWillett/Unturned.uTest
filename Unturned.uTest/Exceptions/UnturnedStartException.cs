using System;
using System.Collections.Generic;
using System.Text;

namespace uTest;


public class UnturnedStartException : Exception
{
    public UnturnedStartException() : this(Properties.Resources.UnturnedStartExceptionDefaultMessage) { }

    public UnturnedStartException(string message) : base(message) { }

    public UnturnedStartException(string message, Exception inner) : base(message, inner) { }

    public UnturnedStartException(Exception inner) : base(Properties.Resources.UnturnedStartExceptionDefaultMessage, inner) { }
}