---
title: Home
summary: A lightweight .NET library for retrying operations that fail transiently.
---

[![Version](https://img.shields.io/github/v/release/kampute/resilience?label=Version&color=darkred)](https://github.com/kampute/resilience/releases)

# Welcome to Kampute.Resilience

Network calls time out, databases drop connections, and services ask clients to slow down. Many of these failures clear on their own, and the operation succeeds when it is tried again a moment later. [`Kampute.Resilience`](~/api/Kampute.Resilience.html) retries such operations for you: you describe how long to wait between attempts, when to give up, and which failures are worth another try, and it runs your operation under those rules.

The package targets `netstandard2.0` and `net10.0` and has no package dependencies.

## Highlights

- **Backoff strategies:** constant, linear, Fibonacci, and exponential delays, plus single-retry and no-retry strategies.
- **Composable limits:** cap the number of retries, the total time spent retrying, and the length of each wait, and add jitter so that clients that fail together do not retry together.
- **Fluent policies:** state which exceptions and which returned values to retry. Policies are immutable, so you build one once and share it across your application.
- **Hooks for each retry:** log or measure retries, compute the next delay from the failure, and release results that are thrown away, such as HTTP responses.
- **Synchronous and asynchronous execution:** the caller's cancellation token reaches your operation and ends the waits between attempts, caller cancellation is never retried, and the final exception keeps its original stack trace.
- **Low overhead:** a policy adds no allocations of its own for each retry of a synchronous operation, and overloads that pass a state value to the operation let you avoid closure allocations.

## Install

```shell
dotnet add package Kampute.Resilience
```

## First Retry

This example retries an asynchronous operation up to five times when it throws [`TimeoutException`](https://learn.microsoft.com/dotnet/api/system.timeoutexception). It waits about one second before the first retry and doubles the delay after that, varying each delay by up to 20 percent. `RunOperationAsync` stands for your own operation.

```csharp
using System;
using Kampute.Resilience;

var retry = RetryStrategies
    .Exponential(TimeSpan.FromSeconds(1))
    .WithJitter(0.2)
    .WithMaxRetries(5)
    .RetryOn<TimeoutException>();

var result = await retry.ExecuteAsync(ct => RunOperationAsync(ct));
```

Other exceptions reach the caller at once. If every retry fails, the last `TimeoutException` is rethrown with its original stack trace. The `retry` policy can be stored and reused for any number of operations, including concurrent ones.

## How It Fits Together

| Building block | Answers | Start with |
| --- | --- | --- |
| [Strategy](~/overview.html#retry-strategies) | How long to wait before each retry, and when to stop. | [`RetryStrategies`](~/api/Kampute.Resilience.RetryStrategies.html) |
| [Policy](~/overview.html#retry-policies) | Which exceptions and results to retry, and what to do at each retry. | [`RetryPolicy`](~/api/Kampute.Resilience.RetryPolicy.html) |
| [Session](~/overview.html#retry-sessions) | Whether and when to retry in a retry loop that you write yourself. | [`RetrySession`](~/api/Kampute.Resilience.RetrySession.html) |

Most applications need only a strategy and a policy. A strategy can also run an operation directly, retrying every exception except caller cancellation.

## Learn More

- The [User Guide](~/overview.html) walks through choosing a strategy, retrying returned values, logging retries, cleaning up discarded results, and cancellation.
- The [API Reference](~/api/index.html) documents every type and member.
- The source code, issues, and releases are on [GitHub](https://github.com/kampute/resilience).
