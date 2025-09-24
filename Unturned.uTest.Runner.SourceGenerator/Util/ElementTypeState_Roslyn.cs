using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace uTest.Util;


internal struct ElementRoslynTypeState
{
    public ITypeSymbol? FirstElement;
    public Stack<ITypeSymbol>? Stack;

    public ITypeSymbol ReduceElementType(ITypeSymbol type)
    {
        ITypeSymbol elementType = type;
        this = default;
        while (true)
        {
            ITypeSymbol nextElementType;
            switch (elementType)
            {
                case IArrayTypeSymbol arr:
                    nextElementType = arr.ElementType;
                    break;

                case IPointerTypeSymbol ptr:
                    nextElementType = ptr.PointedAtType;
                    break;

                default:
                    if (Stack != null)
                        FirstElement = null;
                    return elementType;
            }

            if (FirstElement == null)
                FirstElement = elementType;
            else if (Stack == null)
            {
                Stack = StackPool<ITypeSymbol>.Rent();
                Stack.Push(FirstElement);
                Stack.Push(elementType);
            }
            else
            {
                Stack.Push(elementType);
            }

            elementType = nextElementType;
        }
    }

    public delegate void AddElementType<TState>(ref TState state, ITypeSymbol type, bool isByRef);

    public readonly void AppendElementType<TState>(ref TState state, bool isByRef, AddElementType<TState> callback)
    {
        if (Stack != null)
        {
            while (Stack.Count > 0)
            {
                callback(ref state, Stack.Pop(), isByRef && Stack.Count == 0);
            }

            StackPool<ITypeSymbol>.Return(Stack);
        }
        else if (FirstElement != null)
        {
            callback(ref state, FirstElement, isByRef);
        }
    }
}