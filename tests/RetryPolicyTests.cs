namespace Kampute.Resilience.Test
{
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    [TestFixture]
    public class RetryPolicyTests
    {
        [Test]
        public async Task ExecuteAsync_RetriesRejectedResultsAndDiscardsOnlyIntermediateResults()
        {
            var discarded = new List<int>();
            var contexts = new List<RetryContext<int>>();
            var policy = RetryStrategies.Constant(0).WithMaxRetries(5)
                .RetryOnResult<int>(result => result < 3)
                .OnDiscarded(discarded.Add)
                .OnRetry(contexts.Add);
            var runs = 0;

            var result = await policy.ExecuteAsync(_ => Task.FromResult(++runs));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.EqualTo(3));
                Assert.That(discarded, Is.EqualTo(new[] { 1, 2 }));
                Assert.That(contexts.ConvertAll(c => c.AttemptNumber), Is.EqualTo(new uint[] { 1, 2 }));
                Assert.That(contexts.ConvertAll(c => c.Result), Is.EqualTo(new[] { 1, 2 }));
                Assert.That(contexts.TrueForAll(c => c.HasResult && c.Exception is null && c.Delay == TimeSpan.Zero), Is.True);
            }
        }

        [Test]
        public void Execute_OnResultExhaustion_ReturnsFinalResultWithoutDiscardingIt()
        {
            var discarded = new List<int>();
            var runs = 0;
            var policy = RetryStrategies.Constant(0).WithMaxRetries(2).RetryOnResult<int>(_ => true).OnDiscarded(discarded.Add);

            var result = policy.Execute(_ => ++runs);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.EqualTo(3));
                Assert.That(discarded, Is.EqualTo(new[] { 1, 2 }));
            }
        }

        [Test]
        public void Execute_DelayOverrideSeesOutcomeAndReplacesStrategyDelayBeforeNotification()
        {
            var events = new List<string>();
            var policy = RetryStrategies.Constant(TimeSpan.FromMinutes(1)).WithMaxRetries(1)
                .RetryOnResult<int>(_ => false)
                .RetryOn<IOException>()
                .OverrideDelay(context =>
                {
                    Assert.That(context.Exception, Is.TypeOf<IOException>());
                    Assert.That(context.HasResult, Is.False);
                    Assert.That(context.Delay, Is.EqualTo(TimeSpan.FromMinutes(1)));
                    events.Add("override");
                    return TimeSpan.Zero;
                })
                .OnRetry(context =>
                {
                    Assert.That(context.Delay, Is.EqualTo(TimeSpan.Zero));
                    events.Add("notify");
                });
            var runs = 0;

            var result = policy.Execute(_ => ++runs == 1 ? throw new IOException() : 42);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.EqualTo(42));
                Assert.That(events, Is.EqualTo(new[] { "override", "notify" }));
            }
        }

        [Test]
        public void Execute_RetryHandlerFailureIsNotRetriedAndDiscardedResultIsCleanedUp()
        {
            var failure = new IOException("callback failed");
            var runs = 0;
            var discarded = new List<int>();
            var policy = RetryStrategies.Constant(0)
                .RetryOnResult<int>(_ => true)
                .OnRetry(_ => throw failure)
                .OnDiscarded(discarded.Add);

            var actual = Assert.Throws<IOException>(() => policy.Execute(_ => ++runs));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(actual, Is.SameAs(failure));
                Assert.That(runs, Is.EqualTo(1));
                Assert.That(discarded, Is.EqualTo(new[] { 1 }));
            }
        }

        [Test]
        public void RetryOn_RetriesTheExceptionsThatAnyCallSelects()
        {
            var policy = RetryStrategies.Constant(0).WithMaxRetries(5)
                .RetryOn<IOException>()
                .RetryOn<InvalidOperationException>(error => error.Message == "transient")
                .RetryOn(error => error is TimeoutException);
            var failures = new Queue<Exception>(new Exception[]
            {
                new FileNotFoundException(),
                new InvalidOperationException("transient"),
                new TimeoutException(),
                new InvalidOperationException("permanent"),
            });
            var runs = 0;

            var actual = Assert.Throws<InvalidOperationException>(() => policy.Execute(_ => { ++runs; throw failures.Dequeue(); }));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(actual!.Message, Is.EqualTo("permanent"));
                Assert.That(runs, Is.EqualTo(4));
            }
        }

        [Test]
        public void RetryOn_WithoutAnyCall_RetriesEveryException()
        {
            var policy = new RetryPolicy(RetryStrategies.Constant(0).WithMaxRetries(2));
            var runs = 0;

            policy.Execute(_ =>
            {
                if (++runs < 3)
                    throw new InvalidOperationException();
            });

            Assert.That(runs, Is.EqualTo(3));
        }

        [Test]
        public void RetryOnResult_KeepsExceptionRulesDelayOverrideAndHandlersOfThePolicy()
        {
            var events = new List<string>();
            var policy = RetryStrategies.Constant(TimeSpan.FromMinutes(1)).WithMaxRetries(5)
                .RetryOn<IOException>()
                .OverrideDelay(_ => TimeSpan.Zero)
                .OnRetry(context => events.Add($"untyped:{context.AttemptNumber}:{context.Exception?.GetType().Name ?? "result"}"))
                .RetryOnResult<string?>(value => value is null)
                .OnRetry(context => events.Add($"typed:{context.AttemptNumber}"));
            var runs = 0;

            var result = policy.Execute(_ => ++runs switch
            {
                1 => throw new IOException(),
                2 => (string?)null,
                _ => "ready",
            });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.EqualTo("ready"));
                Assert.That(events, Is.EqualTo(new[] { "untyped:1:IOException", "typed:1", "untyped:2:result", "typed:2" }));
                Assert.Throws<InvalidOperationException>(() => policy.Execute(_ => throw new InvalidOperationException()));
                Assert.That(events, Has.Count.EqualTo(4));
            }
        }

        [Test]
        public void RetryOnResult_OnTypedPolicy_RetriesTheResultsThatAnyCallSelects()
        {
            var policy = RetryStrategies.Constant(0).WithMaxRetries(5)
                .RetryOnResult<int>(value => value == 1)
                .RetryOnResult(value => value == 2);
            var runs = 0;

            Assert.That(policy.Execute(_ => ++runs), Is.EqualTo(3));
        }

        [Test]
        public void ConfigurationMethods_ReturnNewPoliciesAndLeaveTheOriginalUnchanged()
        {
            var notifications = 0;
            var original = RetryStrategies.Constant(0).WithMaxRetries(1).RetryOn<IOException>();
            var derived = original.RetryOn<InvalidOperationException>().OnRetry(_ => ++notifications);

            Assert.That(derived, Is.Not.SameAs(original));
            Assert.Throws<InvalidOperationException>(() => original.Execute(_ => throw new InvalidOperationException()));
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public async Task Policy_IsSharedByConcurrentExecutionsWithIndependentRetryState()
        {
            var policy = RetryStrategies.Constant(0).WithMaxRetries(2).RetryOnResult<int>(attempt => attempt < 3);

            var results = await Task.WhenAll(Array.ConvertAll(new int[16], _ => Task.Run(() =>
            {
                var attempts = 0;
                return policy.ExecuteAsync(async ct =>
                {
                    await Task.Yield();
                    return ++attempts;
                });
            })));

            Assert.That(results, Has.All.EqualTo(3));
        }

        [Test]
        public void DisposeDiscarded_DisposesOnlyResultsThatAreNotReturned()
        {
            var resources = new List<Resource>();
            var policy = RetryStrategies.Constant(0).WithMaxRetries(5)
                .RetryOnResult<Resource?>(resource => resource is null || resources.Count < 3)
                .DisposeDiscarded();
            var runs = 0;

            var returned = policy.Execute(_ =>
            {
                if (++runs == 1)
                    return null;

                var resource = new Resource();
                resources.Add(resource);
                return resource;
            });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(returned, Is.SameAs(resources[2]));
                Assert.That(resources.ConvertAll(r => r.IsDisposed), Is.EqualTo(new[] { true, true, false }));
            }
        }

        [Test]
        public void ConfigurationMethods_WithNullArgument_ThrowArgumentNullException()
        {
            var strategy = RetryStrategies.Constant(0);
            var policy = new RetryPolicy(strategy);
            var typed = policy.RetryOnResult<int>(_ => false);

            using (Assert.EnterMultipleScope())
            {
                Assert.Throws<ArgumentNullException>(() => new RetryPolicy(null!));
                Assert.Throws<ArgumentNullException>(() => ((IRetryStrategy)null!).RetryOn<IOException>());
                Assert.Throws<ArgumentNullException>(() => strategy.RetryOn<IOException>(null!));
                Assert.Throws<ArgumentNullException>(() => strategy.RetryOn((Func<Exception, bool>)null!));
                Assert.Throws<ArgumentNullException>(() => strategy.RetryOnResult<int>(null!));
                Assert.Throws<ArgumentNullException>(() => strategy.OverrideDelay(null!));
                Assert.Throws<ArgumentNullException>(() => strategy.OnRetry(null!));
                Assert.Throws<ArgumentNullException>(() => typed.RetryOn<IOException>(null!));
                Assert.Throws<ArgumentNullException>(() => typed.RetryOn((Func<Exception, bool>)null!));
                Assert.Throws<ArgumentNullException>(() => typed.RetryOnResult(null!));
                Assert.Throws<ArgumentNullException>(() => typed.OverrideDelay(null!));
                Assert.Throws<ArgumentNullException>(() => typed.OnRetry(null!));
                Assert.Throws<ArgumentNullException>(() => typed.OnDiscarded(null!));
                Assert.Throws<ArgumentNullException>(() => RetryPolicyExtensions.DisposeDiscarded<Resource>(null!));
            }
        }

        private sealed class Resource : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose() => IsDisposed = true;
        }
    }
}
