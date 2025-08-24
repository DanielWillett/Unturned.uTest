using JetBrains.Annotations;
using System;
using System.Runtime.CompilerServices;
using uTest.Messages;

#pragma warning disable IDE0130

namespace uTest.Assertions.NUnit;

#pragma warning restore IDE0130

public static partial class AssertExtensions
{
    extension(Assert)
    {
        [OverloadResolutionPriority(2)]
        public static void That(
            bool condition,
            TestMessageBuilder message,
            [CallerArgumentExpression(nameof(condition))] string? expr = null)
        {
            That(condition, Is.True, message, expr);
        }

        [OverloadResolutionPriority(2)]
        public static void That(
            bool condition,
            string? message = null,
            [CallerArgumentExpression(nameof(condition))] string? expr = null)
        {
            That(condition, Is.True, message, expr);
        }
        
        public static void That(
            [InstantHandle] Func<bool> operation,
            TestMessageBuilder message,
            [CallerArgumentExpression(nameof(operation))] string? expr = null)
        {
            That(operation, Is.True, message, expr);
        }

        public static void That(
            [InstantHandle] Func<bool> operation,
            string? message = null,
            [CallerArgumentExpression(nameof(operation))] string? expr = null)
        {
            That(operation, Is.True, message, expr);
        }

        [OverloadResolutionPriority(1)]
        public static void That<TValue>(
            TValue value,
            ITerminalAssertionExpression<TValue, bool> expression,
            string? message = null,
            [CallerArgumentExpression(nameof(value))] string? expr = null)
        {
            expression.Value = value;
            bool result = expression.Solve();
        }

        [OverloadResolutionPriority(1)]
        public static void That<TValue>(
            TValue value,
            ITerminalAssertionExpression<TValue, bool> expression,
            TestMessageBuilder message,
            [CallerArgumentExpression(nameof(value))] string? expr = null)
        {
            expression.Value = value;
            bool result = expression.Solve();
        }
        
        public static void That<TValue>(
            [InstantHandle] Func<TValue> operation,
            ITerminalAssertionExpression<TValue, bool> expression,
            string? message = null,
            [CallerArgumentExpression(nameof(operation))] string? expr = null)
        {
            expression.Value = operation();
            bool result = expression.Solve();
        }
        
        public static void That<TValue>(
            [InstantHandle] Func<TValue> operation,
            ITerminalAssertionExpression<TValue, bool> expression,
            TestMessageBuilder message,
            [CallerArgumentExpression(nameof(operation))] string? expr = null)
        {
            expression.Value = operation();
            bool result = expression.Solve();
        }

        public static void That(
            ReadOnlySpan<char> value,
            ITerminalAssertionExpression<string, bool> expression,
            string? message = null,
            [CallerArgumentExpression(nameof(value))] string? expr = null)
        {
            expression.Value = value.ToString();
            bool result = expression.Solve();
        }

        public static void That(
            ReadOnlySpan<char> value,
            ITerminalAssertionExpression<string, bool> expression,
            TestMessageBuilder message,
            [CallerArgumentExpression(nameof(value))] string? expr = null)
        {
            expression.Value = value.ToString();
            bool result = expression.Solve();
        }

        public static void That<TElementType>(
            ReadOnlySpan<TElementType> value,
            ITerminalAssertionExpression<TElementType[], bool> expression,
            string? message = null,
            [CallerArgumentExpression(nameof(value))] string? expr = null)
        {
            expression.Value = value.ToArray();
            bool result = expression.Solve();
        }

        public static void That<TElementType>(
            ReadOnlySpan<TElementType> value,
            ITerminalAssertionExpression<TElementType[], bool> expression,
            TestMessageBuilder message,
            [CallerArgumentExpression(nameof(value))] string? expr = null)
        {
            expression.Value = value.ToArray();
            bool result = expression.Solve();
        }

    }
}