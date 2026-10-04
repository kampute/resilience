# Kampute.Resilience

A lightweight .NET library for retrying operations that fail transiently. It provides composable retry strategies, fluent retry policies that run synchronous or asynchronous operations with retries, and retry sessions for loops that you write yourself, with no package dependencies.

[Documentation](https://kampute.github.io/resilience/) · [User guide](https://kampute.github.io/resilience/overview.html) · [API reference](https://kampute.github.io/resilience/api/)

## Features

- Constant, linear, Fibonacci and exponential backoff strategies.
- Modifiers that limit retries by count or elapsed time, cap delays, and add jitter, combined in any order.
- Fluent, immutable retry policies that select the exceptions and results to retry, and that you build once and share.
- Retry notifications with attempt, outcome, elapsed time, and delay; outcome-based delays and cleanup of discarded results.
- `Execute` and `ExecuteAsync` methods that preserve exception stack traces and, when run synchronously, add no allocations of their own for each retry.
- Retry sessions for operations that run their own retry loop.

## Installation

```shell
dotnet add package Kampute.Resilience
```

The package targets `netstandard2.0` and `net10.0`.

## Usage

This example runs an asynchronous operation and retries it when it throws `TimeoutException`. `RunOperationAsync` stands for your own operation.

```csharp
using System;
using Kampute.Resilience;

var retry = RetryStrategies
    .Exponential(TimeSpan.FromSeconds(1))
    .WithMaxDelay(TimeSpan.FromSeconds(30))
    .WithJitter(0.2)
    .WithMaxRetries(5)
    .RetryOn<TimeoutException>();

var result = await retry.ExecuteAsync(ct => RunOperationAsync(ct));
```

## Building

```shell
dotnet build -c Release
dotnet test --verbosity minimal
kampose build
```

On Windows, the tests also run on .NET Framework 4.8. `kampose build` generates the documentation site in `.site` from the Release build and the topics in `docs`.

## License

This project is licensed under the [MIT License](LICENSE).
