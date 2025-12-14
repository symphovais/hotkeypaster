using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;
using TalkKeys.Logging;

namespace TalkKeys.Services.Resilience
{
    /// <summary>
    /// Provides pre-configured resilience pipelines for HTTP operations.
    /// Uses Polly for retry with exponential backoff and jitter.
    /// </summary>
    public static class HttpResilience
    {
        /// <summary>
        /// Standard retry pipeline for API calls.
        /// 3 retries with exponential backoff (1s, 2s, 4s) + jitter.
        /// Retries on transient HTTP errors (5xx, 408, 429).
        /// </summary>
        public static ResiliencePipeline<HttpResponseMessage> CreateApiPipeline(ILogger? logger = null)
        {
            return new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromSeconds(1),
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
                        .HandleResult(r => IsTransientError(r)),
                    OnRetry = args =>
                    {
                        var statusCode = args.Outcome.Result?.StatusCode;
                        var exception = args.Outcome.Exception;
                        var reason = statusCode?.ToString() ?? exception?.Message ?? "Unknown";
                        logger?.Log($"[Resilience] Retry {args.AttemptNumber} after {args.RetryDelay.TotalSeconds:F1}s - Reason: {reason}");
                        return ValueTask.CompletedTask;
                    }
                })
                .Build();
        }

        /// <summary>
        /// Extended retry pipeline for transcription/upload operations.
        /// 2 retries with longer delays (2s, 4s) for large uploads.
        /// </summary>
        public static ResiliencePipeline<HttpResponseMessage> CreateTranscriptionPipeline(ILogger? logger = null)
        {
            return new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = 2,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromSeconds(2),
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
                        .HandleResult(r => IsTransientError(r)),
                    OnRetry = args =>
                    {
                        var statusCode = args.Outcome.Result?.StatusCode;
                        var exception = args.Outcome.Exception;
                        var reason = statusCode?.ToString() ?? exception?.Message ?? "Unknown";
                        logger?.Log($"[Resilience] Transcription retry {args.AttemptNumber} after {args.RetryDelay.TotalSeconds:F1}s - Reason: {reason}");
                        return ValueTask.CompletedTask;
                    }
                })
                .Build();
        }

        /// <summary>
        /// Simple retry pipeline for general operations (non-HTTP).
        /// 3 retries with exponential backoff.
        /// </summary>
        public static ResiliencePipeline CreateGeneralPipeline(ILogger? logger = null)
        {
            return new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromMilliseconds(200),
                    OnRetry = args =>
                    {
                        logger?.Log($"[Resilience] General retry {args.AttemptNumber} after {args.RetryDelay.TotalMilliseconds:F0}ms - {args.Outcome.Exception?.Message}");
                        return ValueTask.CompletedTask;
                    }
                })
                .Build();
        }

        /// <summary>
        /// Execute an HTTP request with the API resilience pipeline.
        /// </summary>
        public static async Task<HttpResponseMessage> ExecuteWithRetryAsync(
            Func<CancellationToken, Task<HttpResponseMessage>> operation,
            ILogger? logger = null,
            CancellationToken cancellationToken = default)
        {
            var pipeline = CreateApiPipeline(logger);
            return await pipeline.ExecuteAsync(
                async ct => await operation(ct),
                cancellationToken);
        }

        /// <summary>
        /// Execute an HTTP request with the transcription resilience pipeline.
        /// </summary>
        public static async Task<HttpResponseMessage> ExecuteTranscriptionWithRetryAsync(
            Func<CancellationToken, Task<HttpResponseMessage>> operation,
            ILogger? logger = null,
            CancellationToken cancellationToken = default)
        {
            var pipeline = CreateTranscriptionPipeline(logger);
            return await pipeline.ExecuteAsync(
                async ct => await operation(ct),
                cancellationToken);
        }

        /// <summary>
        /// Determines if an HTTP response indicates a transient error that should be retried.
        /// </summary>
        private static bool IsTransientError(HttpResponseMessage response)
        {
            // Server errors (5xx)
            if ((int)response.StatusCode >= 500)
                return true;

            // Rate limiting
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return true;

            // Request timeout
            if (response.StatusCode == HttpStatusCode.RequestTimeout)
                return true;

            return false;
        }
    }
}
