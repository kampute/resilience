namespace Kampute.Resilience.Test
{
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    [TestFixture]
    public class StatefulExecutionTests
    {
        private static readonly IRetryStrategy ImmediateRetries = RetryStrategies.Constant(TimeSpan.Zero).WithMaxRetries(3);

        private sealed class Counter
        {
            public int Runs;
        }

        [Test]
        public void Strategy_Execute_PassesTheStateToEveryAttempt()
        {
            var counter = new Counter();

            var result = ImmediateRetries.Execute(counter, static (state, _) => ++state.Runs < 3 ? throw new IOException() : state.Runs);

            Assert.That(result, Is.EqualTo(3));
        }

        [Test]
        public void Strategy_ExecuteVoid_PassesTheStateToEveryAttempt()
        {
            var counter = new Counter();

            ImmediateRetries.Execute(counter, static (state, _) =>
            {
                if (++state.Runs < 3)
                    throw new IOException();
            });

            Assert.That(counter.Runs, Is.EqualTo(3));
        }

        [Test]
        public async Task Strategy_ExecuteAsync_PassesTheStateToEveryAttempt()
        {
            var counter = new Counter();

            var result = await ImmediateRetries.ExecuteAsync(counter, static (state, _) =>
                ++state.Runs < 3 ? throw new IOException() : Task.FromResult(state.Runs));

            Assert.That(result, Is.EqualTo(3));
        }

        [Test]
        public async Task Strategy_ExecuteAsyncVoid_PassesTheStateToEveryAttempt()
        {
            var counter = new Counter();

            await ImmediateRetries.ExecuteAsync(counter, static (state, _) =>
                ++state.Runs < 3 ? throw new IOException() : Task.CompletedTask);

            Assert.That(counter.Runs, Is.EqualTo(3));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task Policy_StatefulExecution_AppliesExceptionRulesAndHandlers(bool asynchronous)
        {
            var notifications = new List<uint>();
            var policy = ImmediateRetries.RetryOn<IOException>().OnRetry(context => notifications.Add(context.AttemptNumber));
            var failures = new Queue<Exception>(new Exception[] { new IOException(), new InvalidOperationException() });

            if (asynchronous)
                Assert.ThrowsAsync<InvalidOperationException>(() => policy.ExecuteAsync(failures, static (queue, _) => Task.FromException<int>(queue.Dequeue())));
            else
                Assert.Throws<InvalidOperationException>(() => policy.Execute(failures, static (queue, _) => (int)Throw(queue.Dequeue())));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(failures, Is.Empty);
                Assert.That(notifications, Is.EqualTo(new uint[] { 1 }));
            }

            await Task.CompletedTask;
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task Policy_StatefulVoidExecution_AppliesExceptionRules(bool asynchronous)
        {
            var policy = ImmediateRetries.RetryOn<IOException>();
            var counter = new Counter();

            if (asynchronous)
                await policy.ExecuteAsync(counter, static (state, _) => ++state.Runs < 2 ? Task.FromException(new IOException()) : Task.CompletedTask);
            else
                policy.Execute(counter, static (state, _) =>
                {
                    if (++state.Runs < 2)
                        throw new IOException();
                });

            Assert.That(counter.Runs, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ResultPolicy_StatefulExecution_RetriesSelectedResults(bool asynchronous)
        {
            var discarded = new List<int>();
            var policy = ImmediateRetries.RetryOnResult<int>(value => value < 3).OnDiscarded(discarded.Add);
            var counter = new Counter();

            var result = asynchronous
                ? await policy.ExecuteAsync(counter, static (state, _) => Task.FromResult(++state.Runs))
                : policy.Execute(counter, static (state, _) => ++state.Runs);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.EqualTo(3));
                Assert.That(discarded, Is.EqualTo(new[] { 1, 2 }));
            }
        }

        [Test]
        public void StatefulExecution_AcceptsNullState()
        {
            var result = ImmediateRetries.Execute<string?, bool>(null, static (state, _) => state is null);

            Assert.That(result, Is.True);
        }

        [Test]
        public void StatefulExecution_WithNullArguments_ThrowsArgumentNullExceptionBeforeReturningTask()
        {
            var policy = ImmediateRetries.RetryOn<IOException>();
            var typed = policy.RetryOnResult<int>(_ => false);

            using (Assert.EnterMultipleScope())
            {
                Assert.Throws<ArgumentNullException>(() => ((IRetryStrategy)null!).Execute(0, static (_, _) => 0));
                Assert.Throws<ArgumentNullException>(() => ((IRetryStrategy)null!).ExecuteAsync(0, static (_, _) => Task.CompletedTask));
                Assert.Throws<ArgumentNullException>(() => ImmediateRetries.Execute(0, (Action<int, CancellationToken>)null!));
                Assert.Throws<ArgumentNullException>(() => ImmediateRetries.Execute(0, (Func<int, CancellationToken, int>)null!));
                Assert.Throws<ArgumentNullException>(() => ImmediateRetries.ExecuteAsync(0, (Func<int, CancellationToken, Task>)null!));
                Assert.Throws<ArgumentNullException>(() => ImmediateRetries.ExecuteAsync(0, (Func<int, CancellationToken, Task<int>>)null!));
                Assert.Throws<ArgumentNullException>(() => policy.Execute(0, (Action<int, CancellationToken>)null!));
                Assert.Throws<ArgumentNullException>(() => policy.Execute(0, (Func<int, CancellationToken, int>)null!));
                Assert.Throws<ArgumentNullException>(() => policy.ExecuteAsync(0, (Func<int, CancellationToken, Task>)null!));
                Assert.Throws<ArgumentNullException>(() => policy.ExecuteAsync(0, (Func<int, CancellationToken, Task<int>>)null!));
                Assert.Throws<ArgumentNullException>(() => typed.Execute(0, null!));
                Assert.Throws<ArgumentNullException>(() => typed.ExecuteAsync(0, null!));
            }
        }

        private static object Throw(Exception exception) => throw exception;
    }
}
