using System;
using System.Collections.Generic;

namespace uTest;

internal struct ElementTypeState
{
    public Type? FirstElement;
    public Stack<Type>? Stack;

    public Type ReduceElementType(Type type)
    {
        Type elementType = type;
        this = default;
        while (true)
        {
            Type nextElementType;
            if (elementType.HasElementType)
            {
                nextElementType = elementType.GetElementType()!;
            }
            else
            {
                if (Stack != null)
                    FirstElement = null;
                return elementType;
            }

            if (FirstElement == null)
                FirstElement = elementType;
            else if (Stack == null)
            {
                Stack = StackPool<Type>.Rent();
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

    public delegate void AddElementType<TState>(ref TState state, Type type);

    public readonly void AppendElementType<TState>(ref TState state, AddElementType<TState> callback)
    {
        if (Stack != null)
        {
            while (Stack.Count > 0)
            {
                callback(ref state, Stack.Pop());
            }

            StackPool<Type>.Return(Stack);
        }
        else if (FirstElement != null)
        {
            callback(ref state, FirstElement);
        }
    }
}