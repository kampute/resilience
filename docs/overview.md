---
title: User Guide
summary: Choose a retry policy and apply it to asynchronous operations, results, and manual retry loops.
---

# User Guide

Kampute.Resilience retries operations after failures that may be temporary. You choose which failures to retry, how long to wait between attempts, and when to stop.

Start with a retry policy. Add result handling or callbacks when you need them, and use a manual loop when your workflow needs more control.

## Getting Started

The examples assume a C# console project with `Kampute.Resilience` referenced. `LoadAsync` represents your application's asynchronous operation: it accepts a `CancellationToken`, returns a `Task<string>`, and may throw `TimeoutException`. Replace it with your own operation.

A strategy decides how long to wait and when to stop. [`RetryOn`](~/api/Kampute.Resilience.RetryStrategyExtensions.html#Kampute_Resilience_RetryStrategyExtensions_RetryOn__1_Kampute_Resilience_IRetryStrategy_) turns it into a retry policy that also decides which failures to retry, and [`ExecuteAsync`](~/api/Kampute.Resilience.RetryPolicy.html#Kampute_Resilience_RetryPolicy_ExecuteAsync__1_System_Func{System_Threading_CancellationToken_System_Threading_Tasks_Task{__0}}_System_Threading_CancellationToken_) runs the operation under that policy:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Kampute.Resilience;

var retry = RetryStrategies
    .Constant(TimeSpan.FromSeconds(1))
    .WithMaxRetries(3)
    .RetryOn<TimeoutException>();

var value = await retry.ExecuteAsync(ct => LoadAsync(ct));
```

This policy waits one second between attempts and allows three retries after the initial attempt: **at most four attempts**. A `TimeoutException` requests a retry; other exceptions reach the caller immediately. If the retry budget is exhausted, awaiting `ExecuteAsync` throws the last exception. On success, it returns the operation's value.

The later examples reuse this `retry` policy and the same `LoadAsync` operation.

Choose failures that your application expects to clear, and operations that are safe to repeat. Retrying can repeat work that an earlier attempt already performed.

## Retry Strategies

A **strategy**, represented by [`IRetryStrategy`](~/api/Kampute.Resilience.IRetryStrategy.html), defines the waiting pattern and retry limits. A **session**, represented by [`IRetrySession`](~/api/Kampute.Resilience.IRetrySession.html), tracks the progress of one operation in a retry loop that you write yourself. Policies keep the progress of each execution themselves.

Built-in strategies, and the policies built from them, can be shared across operations: each execution has its own retry budget.

### Built-in Strategies

[`RetryStrategies`](~/api/Kampute.Resilience.RetryStrategies.html) provides these waiting patterns. Increasing the delay as failures continue is called *backoff*.

| Strategy | Waiting pattern | Typical use |
| --- | --- | --- |
| [`None`](~/api/Kampute.Resilience.RetryStrategies.html#Kampute_Resilience_RetryStrategies_None) | No retries. | Report the first failure to the caller. |
| [`Once`](~/api/Kampute.Resilience.RetryStrategies.html#Kampute_Resilience_RetryStrategies_Once_System_TimeSpan_) | One retry after the specified delay. | Give a brief interruption one chance to clear. |
| [`Constant`](~/api/Kampute.Resilience.RetryStrategies.html#Kampute_Resilience_RetryStrategies_Constant_System_TimeSpan_) | The same delay before each retry. | Check for recovery at a steady interval. |
| [`Linear`](~/api/Kampute.Resilience.RetryStrategies.html#Kampute_Resilience_RetryStrategies_Linear_System_TimeSpan_) | Add a fixed amount to each subsequent delay. | Reduce retry frequency gradually. |
| [`Fibonacci`](~/api/Kampute.Resilience.RetryStrategies.html#Kampute_Resilience_RetryStrategies_Fibonacci_System_TimeSpan_) | Grow delays using the Fibonacci sequence. | Increase waits more moderately than doubling them. |
| [`Exponential`](~/api/Kampute.Resilience.RetryStrategies.html#Kampute_Resilience_RetryStrategies_Exponential_System_TimeSpan_System_Double_) | Multiply each subsequent delay; the default factor is two. | Increase recovery time quickly after repeated failures. |

Specify delays as a nonnegative `TimeSpan` or a number of milliseconds. Zero allows an immediate retry. Except for [`None`](~/api/Kampute.Resilience.RetryStrategies.html#Kampute_Resilience_RetryStrategies_None) and [`Once`](~/api/Kampute.Resilience.RetryStrategies.html#Kampute_Resilience_RetryStrategies_Once_System_TimeSpan_), these strategies allow retries without a count or elapsed-time limit; add the limits your operation needs.

### Strategy Modifiers

Modifiers adjust a strategy. For example, this strategy doubles the delay after each failure but caps each wait at ten seconds:

```csharp
var backoff = RetryStrategies
    .Exponential(TimeSpan.FromSeconds(1))
    .WithMaxDelay(TimeSpan.FromSeconds(10))
    .WithMaxRetries(3);
```

| Modifier | Use it to |
| --- | --- |
| [`WithMaxRetries`](~/api/Kampute.Resilience.RetryStrategyExtensions.html#Kampute_Resilience_RetryStrategyExtensions_WithMaxRetries_Kampute_Resilience_IRetryStrategy_System_UInt32_) | Limit retries after the initial attempt. |
| [`WithMaxDelay`](~/api/Kampute.Resilience.RetryStrategyExtensions.html#Kampute_Resilience_RetryStrategyExtensions_WithMaxDelay_Kampute_Resilience_IRetryStrategy_System_TimeSpan_) | Cap each wait without limiting the retry count. |
| [`WithMaxElapsedTime`](~/api/Kampute.Resilience.RetryStrategyExtensions.html#Kampute_Resilience_RetryStrategyExtensions_WithMaxElapsedTime_Kampute_Resilience_IRetryStrategy_System_TimeSpan_) | Stop scheduling retries after a time budget, shortening a wait that would extend beyond it. |
| [`WithJitter`](~/api/Kampute.Resilience.RetryStrategyExtensions.html#Kampute_Resilience_RetryStrategyExtensions_WithJitter_Kampute_Resilience_IRetryStrategy_System_Double_) | Randomly vary delays so operations that fail together can retry at different times. |

Order matters when modifiers change delays. Apply jitter before the delay cap to keep waits within that cap; jitter applied after the cap can lengthen a capped wait.

An elapsed-time limit does not interrupt an attempt already running. Use caller cancellation when you also need to stop the operation.

### Custom Strategies

Implement [`IRetryStrategy`](~/api/Kampute.Resilience.IRetryStrategy.html) when you need a different waiting pattern. Its [`TryGetRetryDelay`](~/api/Kampute.Resilience.IRetryStrategy.html#Kampute_Resilience_IRetryStrategy_TryGetRetryDelay_System_TimeSpan_System_UInt32_System_TimeSpan@_) method receives the elapsed time and the number of retries already made, then returns whether another retry is allowed and how long to wait. Custom strategies can use the same modifiers and policies.

## Retry Policies

A **policy**, represented by [`RetryPolicy`](~/api/Kampute.Resilience.RetryPolicy.html), combines a strategy with the failures to retry and callbacks for each retry. Start one from a strategy with a `RetryOn` method, then run operations with [`ExecuteAsync`](~/api/Kampute.Resilience.RetryPolicy.html#Kampute_Resilience_RetryPolicy_ExecuteAsync__1_System_Func{System_Threading_CancellationToken_System_Threading_Tasks_Task{__0}}_System_Threading_CancellationToken_) or [`Execute`](~/api/Kampute.Resilience.RetryPolicy.html#Kampute_Resilience_RetryPolicy_Execute__1_System_Func{System_Threading_CancellationToken___0}_System_Threading_CancellationToken_). `ExecuteAsync` awaits both the operation and the waits between retries; `Execute` blocks the calling thread during the waits.

Policies are immutable: each method returns a new policy and leaves the original unchanged. Build a policy once, store it, and share it between concurrent operations.

### Choosing Exceptions

[`RetryOn<TException>`](~/api/Kampute.Resilience.RetryPolicy.html#Kampute_Resilience_RetryPolicy_RetryOn__1) retries the exceptions of a type, optionally only those that satisfy a predicate, and [`RetryOn`](~/api/Kampute.Resilience.RetryPolicy.html#Kampute_Resilience_RetryPolicy_RetryOn_System_Func{System_Exception_System_Boolean}_) with a `Func<Exception, bool>` accepts any condition. Each call adds to the exceptions already selected:

```csharp
var transient = RetryStrategies
    .Constant(TimeSpan.FromSeconds(1))
    .WithMaxRetries(3)
    .RetryOn<TimeoutException>()
    .RetryOn<IOException>(error => error is not FileNotFoundException);
```

**A policy without a `RetryOn` call retries every exception except caller cancellation**, and so do the [`ExecuteAsync`](~/api/Kampute.Resilience.RetryStrategyExtensions.html#Kampute_Resilience_RetryStrategyExtensions_ExecuteAsync__1_Kampute_Resilience_IRetryStrategy_System_Func{System_Threading_CancellationToken_System_Threading_Tasks_Task{__0}}_System_Threading_CancellationToken_) and [`Execute`](~/api/Kampute.Resilience.RetryStrategyExtensions.html#Kampute_Resilience_RetryStrategyExtensions_Execute__1_Kampute_Resilience_IRetryStrategy_System_Func{System_Threading_CancellationToken___0}_System_Threading_CancellationToken_) methods that you call on a strategy directly. Select exceptions when only particular failures are recoverable.

### Result Retries

[`RetryOnResult`](~/api/Kampute.Resilience.RetryPolicy.html#Kampute_Resilience_RetryPolicy_RetryOnResult__1_System_Func{__0_System_Boolean}_) retries a returned value that another attempt might improve. It returns a [`RetryPolicy<T>`](~/api/Kampute.Resilience.RetryPolicy_1.html) for operations returning that type, which keeps the exceptions, delay override, and retry handlers already configured. In this example, an empty string requests a retry:

```csharp
var policy = retry.RetryOnResult<string>(value => string.IsNullOrEmpty(value));

var value = await policy.ExecuteAsync(ct => LoadAsync(ct));
```

`TimeoutException` is still retried, while the result predicate handles returned values. **When result retries are exhausted, execution returns the last result.** Here, the caller must handle an empty string if the operation never produces a useful value.

### Retry Notifications

[`OnRetry`](~/api/Kampute.Resilience.RetryPolicy_1.html#Kampute_Resilience_RetryPolicy_1_OnRetry_System_Action{Kampute_Resilience_RetryContext{_0}}_) observes approved retries. The handler runs before the wait and receives a [`RetryContext<T>`](~/api/Kampute.Resilience.RetryContext_1.html) that describes the failed attempt; on a policy that retries only exceptions, it receives a [`RetryContext`](~/api/Kampute.Resilience.RetryContext.html). This policy logs each retry of the result-retry example:

```csharp
var logged = policy.OnRetry(context =>
    Console.WriteLine($"Attempt {context.AttemptNumber}: retry in {context.Delay}."));
```

[`AttemptNumber`](~/api/Kampute.Resilience.RetryContext_1.html#Kampute_Resilience_RetryContext_1_AttemptNumber) starts at one, so a notification for attempt one means the initial attempt needs a retry. [`Delay`](~/api/Kampute.Resilience.RetryContext_1.html#Kampute_Resilience_RetryContext_1_Delay) is the wait before the next attempt. Each `OnRetry` call adds a handler, and handlers run in the order they were added. They do not run when no retry follows.

### Result Cleanup

If results own resources, such as HTTP responses or streams, use [`OnDiscarded`](~/api/Kampute.Resilience.RetryPolicy_1.html#Kampute_Resilience_RetryPolicy_1_OnDiscarded_System_Action{_0}_) to release the results that execution does not return, or [`DisposeDiscarded`](~/api/Kampute.Resilience.RetryPolicyExtensions.html#Kampute_Resilience_RetryPolicyExtensions_DisposeDiscarded__1_Kampute_Resilience_RetryPolicy{__0}_) to dispose them:

```csharp
var send = RetryStrategies
    .Exponential(TimeSpan.FromSeconds(1))
    .WithMaxRetries(3)
    .RetryOnResult<HttpResponseMessage>(response => (int)response.StatusCode >= 500)
    .DisposeDiscarded();
```

Execution does not dispose results otherwise. Accepted results and the last result returned on exhaustion remain the caller's responsibility. The string example needs no cleanup.

Keep callbacks short. Result predicates, delay overrides, retry handlers, and discard handlers stop execution if they throw; the [`RetryPolicy<T>`](~/api/Kampute.Resilience.RetryPolicy_1.html) reference describes their exception behavior.

### Delay Override

Use [`OverrideDelay`](~/api/Kampute.Resilience.RetryPolicy_1.html#Kampute_Resilience_RetryPolicy_1_OverrideDelay_System_Func{Kampute_Resilience_RetryContext{_0}_System_TimeSpan}_) when a result or exception should change the next wait. Its function receives the failed attempt, whose `Delay` is the delay that the strategy proposes, and returns the delay to wait. This override waits five seconds after an empty result and keeps the strategy's delay for exceptions:

```csharp
var slower = policy.OverrideDelay(context =>
    context.HasResult ? TimeSpan.FromSeconds(5) : context.Delay);
```

[`HasResult`](~/api/Kampute.Resilience.RetryContext_1.html#Kampute_Resilience_RetryContext_1_HasResult) distinguishes a returned value from an exception. The override runs only after the strategy allows a retry, and a later `OverrideDelay` call replaces an earlier one. The override also suits a retry time that the failure itself suggests, such as one carried by an exception.

An overriding delay replaces the proposed delay **after modifiers have run**, so it can exceed a delay cap or the wait remaining under an elapsed-time limit. Apply any required limits in the override or through caller cancellation.

### Caller Cancellation

Pass the caller's token to `ExecuteAsync` or `Execute` and pass the operation's `ct` parameter to its asynchronous APIs. The token cancels retry waits; the operation must also observe it to stop its own work.

This example gives the operation and its retries a 30-second cancellation deadline:

```csharp
using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

var value = await retry.ExecuteAsync(ct => LoadAsync(ct), cancellation.Token);
```

An `OperationCanceledException` from the operation is not retried when that token is canceled. In an application, use the token supplied by the calling request or background job, or link it with a deadline token.

### Passing State

Each `Execute` and `ExecuteAsync` method has an [overload](~/api/Kampute.Resilience.RetryPolicy.html#Kampute_Resilience_RetryPolicy_ExecuteAsync__2___0_System_Func{__0_System_Threading_CancellationToken_System_Threading_Tasks_Task{__1}}_System_Threading_CancellationToken_) that passes a state value to every attempt. A lambda that uses local variables captures them, which allocates a closure on each call. Passing those values as state to a `static` lambda avoids that allocation, which can matter on frequently called paths. Use a tuple to pass several values:

```csharp
var page = await retry.ExecuteAsync(
    (client, uri),
    static (state, ct) => state.client.GetStringAsync(state.uri, ct));
```

Here, `client` is an `HttpClient` and `uri` is the address to read.

## Retry Sessions

Use a session directly when you need to control the workflow between attempts or supply your own scheduling.

### Manual Retry Loops

[`StartSession`](~/api/Kampute.Resilience.RetryStrategyExtensions.html#Kampute_Resilience_RetryStrategyExtensions_StartSession_Kampute_Resilience_IRetryStrategy_) creates a [`RetrySession`](~/api/Kampute.Resilience.RetrySession.html) for one operation. [`WaitToRetryAsync`](~/api/Kampute.Resilience.RetrySession.html#Kampute_Resilience_RetrySession_WaitToRetryAsync_System_Threading_CancellationToken_) returns `true` after an approved wait, or `false` when the strategy allows no further retry.

This loop retries timeouts from `LoadAsync` with the strategy of the `retry` policy. It assumes `cancellationToken` is supplied by the caller:

```csharp
var session = retry.Strategy.StartSession();

while (true)
{
    try
    {
        await LoadAsync(cancellationToken);
        break;
    }
    catch (TimeoutException)
    {
        if (!await session.WaitToRetryAsync(cancellationToken))
            throw; // Preserve the last failure when no retry remains.
    }
}
```

Create the session before the loop. Creating it inside the loop would restart the retry budget on every failure. Start a new session for each independent operation, and do not share a session between concurrent operations.

### Custom Sessions

Implement [`IRetrySession`](~/api/Kampute.Resilience.IRetrySession.html) when your application owns the retry decisions and waits of a loop that you write. To change only the decision or the delay, such as to wait for a retry time that the failure suggests, derive from [`RetrySession`](~/api/Kampute.Resilience.RetrySession.html) and override [`TryGetRetryDelay`](~/api/Kampute.Resilience.RetrySession.html#Kampute_Resilience_RetrySession_TryGetRetryDelay_System_TimeSpan@_); the session still counts the retries, applies its delay limit, and waits. Policies do not use sessions; to base a policy's delay on the failure, use [`OverrideDelay`](~/api/Kampute.Resilience.RetryPolicy_1.html#Kampute_Resilience_RetryPolicy_1_OverrideDelay_System_Func{Kampute_Resilience_RetryContext{_0}_System_TimeSpan}_).
