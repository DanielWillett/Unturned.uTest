using NUnit.Framework;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using uTest;
using Assert = NUnit.Framework.Assert;
using TestAttribute = NUnit.Framework.TestAttribute;
// ReSharper disable AccessToDisposedClosure
#pragma warning disable CS9107 // Parameter is captured into the state of the enclosing type and its value is also passed to the base constructor. The value might be captured by the base class as well.

namespace uTest_Test;

public class TaskAwaitableHelperTests
{
    [Test]
    public async Task TestBasicTaskNotCompleted()
    {
        using SemaphoreSlim sem = new SemaphoreSlim(0, 1);
        Task<object?> task = TaskAwaitableHelper.CreateTaskFromReturnValue(TestTask());

        Assert.That(task.IsCompleted, Is.False);

        sem.Release();
        await task;

        Assert.That(task.IsCompleted, Is.True);

        async Task TestTask()
        {
            await sem.WaitAsync();
        }
    }

    [Test]
    public async Task TestBasicTaskCompleted()
    {
        Task<object?> task = TaskAwaitableHelper.CreateTaskFromReturnValue(Task.CompletedTask);

        Assert.That(task.IsCompleted, Is.True);

        await task;
        Assert.That(task.IsCompleted, Is.True);
    }

    [Test]
    public async Task TestBasicValueTaskNotCompleted()
    {
        using SemaphoreSlim sem = new SemaphoreSlim(0, 1);
        Task<object?> task = TaskAwaitableHelper.CreateTaskFromReturnValue(TestTask());

        Assert.That(task.IsCompleted, Is.False);

        sem.Release();
        await task;

        Assert.That(task.IsCompleted, Is.True);

        async ValueTask TestTask()
        {
            await sem.WaitAsync();
        }
    }

    [Test]
    public async Task TestBasicValueTaskCompleted()
    {
        Task<object?> task = TaskAwaitableHelper.CreateTaskFromReturnValue(default(ValueTask));

        Assert.That(task.IsCompleted, Is.True);

        await task;
        Assert.That(task.IsCompleted, Is.True);
    }

    [Test]
    public async Task TestCustomTask()
    {
        using SemaphoreSlim sem = new SemaphoreSlim(0, 1);
        Task<object?> task = TaskAwaitableHelper.CreateTaskFromReturnValue(new TestAwaitable(sem));

        Assert.That(task.IsCompleted, Is.False);

        sem.Release();
        await task;
        Assert.That(task.IsCompleted, Is.True);
    }

    [Test]
    public async Task TestCustomTaskVoidConfigurable()
    {
        using SemaphoreSlim sem = new SemaphoreSlim(0, 1);
        TestAwaitableConfigurable c = new TestAwaitableConfigurable(sem);
        Task<object?> task = TaskAwaitableHelper.CreateTaskFromReturnValue(c);

        Assert.That(task.IsCompleted, Is.False);
        Assert.That(c.Configuration, Is.False);

        sem.Release();
        await task;
        Assert.That(task.IsCompleted, Is.True);
    }

    [Test]
    public async Task TestCustomTaskVoidNewConfigurable()
    {
        using SemaphoreSlim sem = new SemaphoreSlim(0, 1);
        TestAwaitableConfigurableNew c = new TestAwaitableConfigurableNew(sem);
        Task<object?> task = TaskAwaitableHelper.CreateTaskFromReturnValue(c);

        Assert.That(task.IsCompleted, Is.False);
        Assert.That(c.NewConfiguration, Is.False);

        sem.Release();
        await task;
        Assert.That(task.IsCompleted, Is.True);
    }


    private class TestAwaitableConfigurable(SemaphoreSlim semaphore) : TestAwaitable(semaphore)
    {
        public bool? Configuration { get; set; }

        public void ConfigureAwait(bool c)
        {
            Configuration = c;
        }
    }

    private class TestAwaitableConfigurableNew(SemaphoreSlim semaphore) : TestAwaitable(semaphore)
    {
        public bool? NewConfiguration { get; set; }
        
        public TestAwaitable ConfigureAwait(bool c)
        {
            NewConfiguration = c;
            return new TestAwaitable(semaphore);
        }
    }

    private class TestAwaitable
    {
        private readonly SemaphoreSlim _semaphore;

        public TestAwaitable(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public TestAwaiter GetAwaiter()
        {
            TestAwaiter awaiter = new TestAwaiter(_semaphore);
            return awaiter;
        }

        public class TestAwaiter(SemaphoreSlim semaphore) : INotifyCompletion
        {
            /// <inheritdoc />
            public void OnCompleted(Action continuation)
            {
                Task.Run(async () =>
                {
                    if (!await semaphore.WaitAsync(1000))
                        throw new TimeoutException();
                    IsCompleted = true;
                    continuation();
                });
            }

            public bool IsCompleted { get; set; }
            public void GetResult()
            {

            }
        }
    }
}