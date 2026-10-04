#if NET
namespace Kampute.Resilience.Test
{
    using NUnit.Framework;
    using System;
    using System.Threading;

    [TestFixture]
    public class RetryAllocationTests
    {
        private static readonly Func<CancellationToken, int> Operation = static _ => 1;

        [Test]
        public void Execute_WithResultRetries_AllocatesTheSameForOneRetryAsForMany()
        {
            static RetryPolicy<int> Policy(uint maxRetries) => RetryStrategies.Constant(0).WithMaxRetries(maxRetries)
                .RetryOnResult<int>(static _ => true)
                .OverrideDelay(static context => context.Delay)
                .OnRetry(static _ => { })
                .OnDiscarded(static _ => { });

            var oneRetry = Policy(1);
            var manyRetries = Policy(50);
            oneRetry.Execute(Operation);
            manyRetries.Execute(Operation);

            var oneRetryBytes = AllocatedBytes(oneRetry);
            var manyRetriesBytes = AllocatedBytes(manyRetries);

            Assert.That(manyRetriesBytes, Is.EqualTo(oneRetryBytes));
        }

        [Test]
        public void Execute_WithPrebuiltPolicyAndStaticOperation_AllocatesNothing()
        {
            var strategy = RetryStrategies.Constant(0).WithMaxRetries(3);
            var policy = strategy.RetryOn<TimeoutException>().OnRetry(static _ => { });
            strategy.Execute(Operation);
            policy.Execute(Operation);

            var before = GC.GetAllocatedBytesForCurrentThread();
            strategy.Execute(Operation);
            policy.Execute(Operation);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void Execute_WithStateAndStaticOperation_AllocatesNothing()
        {
            var strategy = RetryStrategies.Constant(0).WithMaxRetries(3);
            var policy = strategy.RetryOnResult<int>(static value => value < 0);
            var offset = 41;
            var results = 0;
            var allocated = 0L;

            // The first pass creates and caches each call site's delegate; the second pass is measured.
            for (var pass = 0; pass < 2; ++pass)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                results = strategy.Execute(offset, static (state, _) => state + 1) + policy.Execute(offset, static (state, _) => state + 1);
                allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(results, Is.EqualTo(84));
                Assert.That(allocated, Is.Zero);
            }
        }

        private static long AllocatedBytes(RetryPolicy<int> policy)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            policy.Execute(Operation);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
    }
}
#endif
