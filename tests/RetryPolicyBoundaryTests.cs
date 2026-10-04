namespace Kampute.Resilience.Test
{
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    [TestFixture]
    public class RetryPolicyBoundaryTests
    {
        /// <summary>
        /// Runs the same behavioral scenario through either synchronous or asynchronous execution.
        /// </summary>
        /// <typeparam name="T">
        /// The scenario result type.
        /// </typeparam>
        /// <param name="asynchronous">
        /// Whether to use asynchronous execution.
        /// </param>
        /// <param name="policy">
        /// The policy being exercised.
        /// </param>
        /// <param name="operation">
        /// The operation producing the scenario's result or failure.
        /// </param>
        /// <param name="token">
        /// The caller token supplied to execution.
        /// </param>
        /// <returns>
        /// The asynchronous result, or a completed task containing the synchronous result.
        /// </returns>
        private static Task<T> Run<T>(bool asynchronous, RetryPolicy<T> policy, Func<CancellationToken, T> operation, CancellationToken token = default)
            => asynchronous ? policy.ExecuteAsync(ct => Task.FromResult(operation(ct)), token)
                : Task.FromResult(policy.Execute(operation, token));

        [TestCase(false)]
        [TestCase(true)]
        public void CanceledResultWait_CleansUpOnceAndDoesNotRunAgain(bool asynchronous)
        {
            using var source = new CancellationTokenSource();
            var discarded = new List<int>();
            var runs = 0;
            var notifications = 0;
            var policy = RetryStrategies.Constant(TimeSpan.FromMinutes(1))
                .RetryOnResult<int>(_ => true)
                .OnRetry(context =>
                {
                    ++notifications;
                    Assert.That(context.Delay, Is.EqualTo(TimeSpan.FromMinutes(1)));
                    source.Cancel();
                })
                .OnDiscarded(discarded.Add);

            Assert.CatchAsync<OperationCanceledException>(() => Run(asynchronous, policy, _ => ++runs, source.Token));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(runs, Is.EqualTo(1));
                Assert.That(notifications, Is.EqualTo(1));
                Assert.That(discarded, Is.EqualTo(new[] { 1 }));
            }
        }

        [TestCase(false, "predicate")]
        [TestCase(true, "predicate")]
        [TestCase(false, "override")]
        [TestCase(true, "override")]
        [TestCase(false, "notification")]
        [TestCase(true, "notification")]
        [TestCase(false, "cleanup")]
        [TestCase(true, "cleanup")]
        public void ResultHookFailure_PropagatesWithoutRetryingAndCleansUpOnce(bool asynchronous, string hook)
        {
            var failure = new IOException(hook);
            var runs = 0;
            var cleanupCalls = 0;
            var policy = RetryStrategies.Constant(0)
                .RetryOnResult<int>(_ => hook == "predicate" ? throw failure : true)
                .OverrideDelay(context => hook == "override" ? throw failure : context.Delay)
                .OnRetry(_ => { if (hook == "notification") throw failure; })
                .OnDiscarded(_ => { ++cleanupCalls; if (hook == "cleanup") throw failure; });

            var actual = Assert.ThrowsAsync<IOException>(() => Run(asynchronous, policy, _ => ++runs));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(actual, Is.SameAs(failure));
                Assert.That(runs, Is.EqualTo(1));
                Assert.That(cleanupCalls, Is.EqualTo(1));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ThrowingExceptionPredicate_PreservesOriginalException(bool asynchronous)
        {
            var original = new IOException("operation");
            var policy = RetryStrategies.Constant(0)
                .RetryOn(_ => throw new InvalidOperationException("predicate"))
                .RetryOnResult<int>(_ => false);

            var actual = Assert.ThrowsAsync<IOException>(() => Run<int>(asynchronous, policy, _ => throw original));

            Assert.That(actual, Is.SameAs(original));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task OverrideReturningProposedDelay_KeepsStrategyDelayAndCleansUpBeforeNextAttempt(bool asynchronous)
        {
            var events = new List<string>();
            var policy = RetryStrategies.Constant(TimeSpan.FromMilliseconds(7))
                .RetryOnResult<int>(result => result == 1)
                .OverrideDelay(context =>
                {
                    Assert.That(context.Result, Is.EqualTo(1));
                    Assert.That(context.Delay, Is.EqualTo(TimeSpan.FromMilliseconds(7)));
                    events.Add("override");
                    return context.Delay;
                })
                .OnRetry(context =>
                {
                    Assert.That(context.Delay, Is.EqualTo(TimeSpan.FromMilliseconds(7)));
                    Assert.That(context.AttemptNumber, Is.EqualTo(1));
                    events.Add("notify");
                })
                .OnDiscarded(_ => events.Add("discard"));
            var runs = 0;

            var result = await Run(asynchronous, policy, _ =>
            {
                events.Add("run");
                return ++runs;
            });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.EqualTo(2));
                Assert.That(events, Is.EqualTo(new[] { "run", "override", "notify", "discard", "run" }));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NegativeOverriddenDelay_IsRejectedBeforeNotification(bool asynchronous)
        {
            var notifications = 0;
            var discarded = 0;
            var policy = RetryStrategies.Constant(0)
                .RetryOnResult<int>(_ => true)
                .OverrideDelay(_ => TimeSpan.FromTicks(-1))
                .OnRetry(_ => ++notifications)
                .OnDiscarded(_ => ++discarded);

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Run(asynchronous, policy, _ => 1));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(notifications, Is.Zero);
                Assert.That(discarded, Is.EqualTo(1));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task UnsupportedOverriddenDelay_StopsAndReturnsUndiscardedResult(bool asynchronous)
        {
            var calls = 0;
            var policy = RetryStrategies.Constant(0)
                .RetryOnResult<int>(_ => true)
                .OverrideDelay(_ => TimeSpan.FromMilliseconds((double)int.MaxValue + 1))
                .OnRetry(_ => ++calls)
                .OnDiscarded(_ => ++calls);

            var result = await Run(asynchronous, policy, _ => 42);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.EqualTo(42));
                Assert.That(calls, Is.Zero);
            }
        }

        [Test]
        public void StrategyRefusesRetry_DoesNotOverrideDelayNotifyOrDiscard()
        {
            var calls = 0;
            var policy = RetryStrategies.None
                .RetryOnResult<int>(_ => true)
                .OverrideDelay(_ => { ++calls; return TimeSpan.Zero; })
                .OnRetry(_ => ++calls)
                .OnDiscarded(_ => ++calls);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(policy.Execute(_ => 42), Is.EqualTo(42));
                Assert.That(calls, Is.Zero);
            }
        }

        [Test]
        public void OverriddenDelay_IsAppliedAfterDelayCap()
        {
            var selected = TimeSpan.FromSeconds(30);
            TimeSpan? proposed = null;
            TimeSpan? notified = null;
            using var source = new CancellationTokenSource();
            var policy = RetryStrategies.Constant(TimeSpan.FromMinutes(1)).WithMaxDelay(1)
                .OverrideDelay(context => { proposed = context.Delay; return selected; })
                .OnRetry(context => { notified = context.Delay; source.Cancel(); });

            Assert.Catch<OperationCanceledException>(() => policy.Execute(_ => throw new IOException(), source.Token));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(proposed, Is.EqualTo(TimeSpan.FromMilliseconds(1)));
                Assert.That(notified, Is.EqualTo(selected));
            }
        }

        [Test]
        public void OverriddenDelay_IsAppliedAfterElapsedTimeClipping()
        {
            var selected = TimeSpan.FromMinutes(30);
            TimeSpan? proposed = null;
            TimeSpan? notified = null;
            using var source = new CancellationTokenSource();
            var policy = RetryStrategies.Constant(TimeSpan.FromHours(1)).WithMaxElapsedTime(TimeSpan.FromMinutes(1))
                .OverrideDelay(context => { proposed = context.Delay; return selected; })
                .OnRetry(context => { notified = context.Delay; source.Cancel(); });

            Assert.Catch<OperationCanceledException>(() => policy.Execute(_ => throw new IOException(), source.Token));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(proposed, Is.GreaterThan(TimeSpan.Zero).And.LessThanOrEqualTo(TimeSpan.FromMinutes(1)));
                Assert.That(notified, Is.EqualTo(selected));
            }
        }

        [Test]
        public void NullArguments_AreRejectedBeforeReturningTask()
        {
            var policy = RetryStrategies.Constant(0).RetryOn<IOException>();
            var typed = policy.RetryOnResult<int>(_ => false);

            using (Assert.EnterMultipleScope())
            {
                Assert.Throws<ArgumentNullException>(() => policy.ExecuteAsync((Func<CancellationToken, Task>)null!));
                Assert.Throws<ArgumentNullException>(() => policy.ExecuteAsync((Func<CancellationToken, Task<int>>)null!));
                Assert.Throws<ArgumentNullException>(() => policy.Execute((Action<CancellationToken>)null!));
                Assert.Throws<ArgumentNullException>(() => policy.Execute((Func<CancellationToken, int>)null!));
                Assert.Throws<ArgumentNullException>(() => typed.ExecuteAsync(null!));
                Assert.Throws<ArgumentNullException>(() => typed.Execute(null!));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task VoidOperation_SupportsHooksAndDelayOverride(bool asynchronous)
        {
            var runs = 0;
            var calls = 0;
            var policy = RetryStrategies.Constant(TimeSpan.FromMinutes(1)).WithMaxRetries(1)
                .OverrideDelay(_ => TimeSpan.Zero)
                .OnRetry(context => { ++calls; Assert.That(context.Exception, Is.TypeOf<IOException>()); });
            Action<CancellationToken> operation = _ =>
            {
                if (++runs == 1)
                    throw new IOException();
            };

            if (asynchronous)
                await policy.ExecuteAsync(ct =>
                {
                    operation(ct);
                    return Task.CompletedTask;
                });
            else
                policy.Execute(operation);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(runs, Is.EqualTo(2));
                Assert.That(calls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task NullResult_IsDistinctFromExceptionOutcome()
        {
            var contexts = new List<RetryContext<string?>>();
            var policy = RetryStrategies.Constant(0).WithMaxRetries(2)
                .RetryOnResult<string?>(value => value is null)
                .OnRetry(contexts.Add);
            var runs = 0;

            var result = await policy.ExecuteAsync(_ => ++runs == 1 ? throw new IOException() : Task.FromResult(runs == 2 ? null : "ready"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.EqualTo("ready"));
                Assert.That(contexts[0].HasResult, Is.False);
                Assert.That(contexts[0].Exception, Is.TypeOf<IOException>());
                Assert.That(contexts[1].HasResult, Is.True);
                Assert.That(contexts[1].Result, Is.Null);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CallerCancellation_IsNotRetriedOrNotified(bool asynchronous)
        {
            using var source = new CancellationTokenSource();
            var notifications = 0;
            var policy = RetryStrategies.Constant(0)
                .RetryOn(_ => true)
                .RetryOnResult<int>(_ => false)
                .OnRetry(_ => ++notifications);

            Assert.CatchAsync<OperationCanceledException>(() => Run<int>(asynchronous, policy, ct =>
            {
                source.Cancel();
                ct.ThrowIfCancellationRequested();

                return 1;
            }, source.Token));

            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public async Task UserGuide_ReadinessExample_ReturnsTrueAfterTwoNotifications()
        {
            var attempts = 0;
            var notifications = new List<uint>();
            var retry = RetryStrategies.Constant(0).WithMaxRetries(3)
                .RetryOnResult<bool>(ready => !ready)
                .OnRetry(context =>
                {
                    Console.WriteLine($"Attempt {context.AttemptNumber}: retry in {context.Delay}");
                    notifications.Add(context.AttemptNumber);
                });

            var ready = await retry.ExecuteAsync(_ => Task.FromResult(++attempts >= 3));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ready, Is.True);
                Assert.That(notifications, Is.EqualTo(new uint[] { 1, 2 }));
            }
        }
    }
}
