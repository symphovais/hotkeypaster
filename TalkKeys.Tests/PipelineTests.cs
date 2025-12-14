using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TalkKeys.Services;
using TalkKeys.Services.Pipeline;
using Xunit;

namespace TalkKeys.Tests
{
    /// <summary>
    /// Integration tests for Pipeline execution.
    /// These tests verify the pipeline framework works correctly.
    /// </summary>
    public class PipelineTests
    {
        [Fact]
        public async Task Pipeline_WithNoStages_ReturnsSuccessResult()
        {
            var pipeline = new Pipeline("Empty", new List<IPipelineStage>());

            var context = new PipelineContext();
            context.SetData("AudioData", new byte[100]);

            var result = await pipeline.ExecuteAsync(context);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task Pipeline_ExecutesStagesInOrder()
        {
            var executionOrder = new List<string>();

            var stages = new List<IPipelineStage>
            {
                new TestStage("Stage1", () => executionOrder.Add("Stage1")),
                new TestStage("Stage2", () => executionOrder.Add("Stage2")),
                new TestStage("Stage3", () => executionOrder.Add("Stage3"))
            };

            var pipeline = new Pipeline("Test", stages);

            var context = new PipelineContext();
            context.SetData("AudioData", new byte[100]);

            var result = await pipeline.ExecuteAsync(context);

            Assert.True(result.IsSuccess);
            Assert.Equal(3, executionOrder.Count);
            Assert.Equal("Stage1", executionOrder[0]);
            Assert.Equal("Stage2", executionOrder[1]);
            Assert.Equal("Stage3", executionOrder[2]);
        }

        [Fact]
        public async Task Pipeline_StopsOnFailure()
        {
            var executionOrder = new List<string>();

            var stages = new List<IPipelineStage>
            {
                new TestStage("Stage1", () => executionOrder.Add("Stage1")),
                new TestStage("Stage2", () =>
                {
                    executionOrder.Add("Stage2");
                    throw new Exception("Stage 2 failed");
                }),
                new TestStage("Stage3", () => executionOrder.Add("Stage3"))
            };

            var pipeline = new Pipeline("Test", stages);

            var context = new PipelineContext();
            context.SetData("AudioData", new byte[100]);

            var result = await pipeline.ExecuteAsync(context);

            Assert.False(result.IsSuccess);
            Assert.Equal(2, executionOrder.Count); // Stage3 should not run
            Assert.DoesNotContain("Stage3", executionOrder);
            Assert.Contains("Stage 2 failed", result.ErrorMessage);
        }

        [Fact]
        public async Task Pipeline_SupportsCancellation()
        {
            var cts = new CancellationTokenSource();
            var stage2Executed = false;

            var stages = new List<IPipelineStage>
            {
                new TestStage("Stage1", () =>
                {
                    // Cancel during first stage - second stage should not run
                    cts.Cancel();
                }),
                new TestStage("Stage2", () =>
                {
                    stage2Executed = true;
                })
            };

            var pipeline = new Pipeline("Test", stages);

            var context = new PipelineContext
            {
                CancellationToken = cts.Token
            };
            context.SetData("AudioData", new byte[100]);

            var result = await pipeline.ExecuteAsync(context);

            // Pipeline should detect cancellation and not run Stage2
            Assert.False(result.IsSuccess);
            Assert.False(stage2Executed);
            Assert.Contains("cancelled", result.ErrorMessage?.ToLower() ?? "");
        }

        [Fact]
        public async Task Pipeline_ReportsProgress()
        {
            var progressReports = new List<string>();
            var progress = new Progress<ProgressEventArgs>(p =>
            {
                progressReports.Add(p.Message);
            });

            var stages = new List<IPipelineStage>
            {
                new TestStage("Stage1", () => { })
            };

            var pipeline = new Pipeline("Test", stages);

            var context = new PipelineContext
            {
                Progress = progress
            };
            context.SetData("AudioData", new byte[100]);

            var result = await pipeline.ExecuteAsync(context);

            Assert.True(result.IsSuccess);
            // Progress should have been reported
            Assert.NotEmpty(progressReports);
        }

        [Fact]
        public async Task Pipeline_ContextDataPassesBetweenStages()
        {
            string? receivedData = null;

            var stages = new List<IPipelineStage>
            {
                new TestStage("Stage1", ctx =>
                {
                    ctx.SetData("TestKey", "TestValue");
                }),
                new TestStage("Stage2", ctx =>
                {
                    receivedData = ctx.GetData<string>("TestKey");
                })
            };

            var pipeline = new Pipeline("Test", stages);

            var context = new PipelineContext();
            context.SetData("AudioData", new byte[100]);

            var result = await pipeline.ExecuteAsync(context);

            Assert.True(result.IsSuccess);
            Assert.Equal("TestValue", receivedData);
        }

        [Fact]
        public void PipelineContext_GetData_ReturnsNullForMissingKey()
        {
            var context = new PipelineContext();

            var result = context.GetData<string>("NonExistentKey");

            Assert.Null(result);
        }

        [Fact]
        public void PipelineContext_SetAndGetData_WorksCorrectly()
        {
            var context = new PipelineContext();
            var testBytes = new byte[] { 1, 2, 3, 4, 5 };

            context.SetData("TestBytes", testBytes);
            var retrieved = context.GetData<byte[]>("TestBytes");

            Assert.NotNull(retrieved);
            Assert.Equal(testBytes, retrieved);
        }

        [Fact]
        public void PipelineMetrics_TracksStageMetrics()
        {
            var metrics = new PipelineMetrics();
            var stageMetrics = new StageMetrics
            {
                StageName = "Test",
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMilliseconds(100)
            };

            metrics.AddStageMetrics(stageMetrics);

            Assert.Single(metrics.StageMetrics);
            Assert.Equal("Test", metrics.StageMetrics[0].StageName);
            Assert.True(metrics.StageMetrics[0].DurationMs >= 0);
        }

        [Fact]
        public void StageMetrics_DurationMs_CalculatesCorrectly()
        {
            var startTime = DateTime.UtcNow;
            var endTime = startTime.AddMilliseconds(250);

            var metrics = new StageMetrics
            {
                StageName = "Test",
                StartTime = startTime,
                EndTime = endTime
            };

            Assert.Equal(250, metrics.DurationMs, 1); // 1ms tolerance
        }

        [Fact]
        public void StageMetrics_CustomMetrics_WorkCorrectly()
        {
            var metrics = new StageMetrics
            {
                StageName = "Test"
            };

            metrics.AddMetric("WordCount", 150);
            metrics.AddMetric("ModelUsed", "test-model");

            Assert.Equal(150, metrics.GetMetric<int>("WordCount"));
            Assert.Equal("test-model", metrics.GetMetric<string>("ModelUsed"));
            Assert.Null(metrics.GetMetric<string>("NonExistent"));
        }

        [Fact]
        public void StageResult_Success_CreatesCorrectResult()
        {
            var metrics = new StageMetrics { StageName = "Test" };
            var result = StageResult.Success(metrics);

            Assert.True(result.IsSuccess);
            Assert.Null(result.ErrorMessage);
            Assert.Equal("Test", result.Metrics.StageName);
        }

        [Fact]
        public void StageResult_Failure_CreatesCorrectResult()
        {
            var metrics = new StageMetrics { StageName = "Test" };
            var result = StageResult.Failure("Test error", metrics);

            Assert.False(result.IsSuccess);
            Assert.Equal("Test error", result.ErrorMessage);
            Assert.Equal("Test", result.Metrics.StageName);
        }

        /// <summary>
        /// Helper test stage for pipeline testing
        /// </summary>
        private class TestStage : IPipelineStage
        {
            private readonly Action? _syncAction;
            private readonly Action<PipelineContext>? _contextAction;
            private readonly Func<Task>? _asyncAction;

            public string Name { get; }
            public string StageType => "Test";
            public int RetryCount => 0;
            public TimeSpan RetryDelay => TimeSpan.Zero;

            public TestStage(string name, Action action)
            {
                Name = name;
                _syncAction = action;
            }

            public TestStage(string name, Action<PipelineContext> action)
            {
                Name = name;
                _contextAction = action;
            }

            public TestStage(string name, Func<Task> asyncAction)
            {
                Name = name;
                _asyncAction = asyncAction;
            }

            public async Task<StageResult> ExecuteAsync(PipelineContext context)
            {
                var startTime = DateTime.UtcNow;

                if (_asyncAction != null)
                {
                    await _asyncAction();
                }
                else if (_contextAction != null)
                {
                    _contextAction(context);
                }
                else
                {
                    _syncAction?.Invoke();
                }

                var metrics = new StageMetrics
                {
                    StageName = Name,
                    StartTime = startTime,
                    EndTime = DateTime.UtcNow
                };

                return StageResult.Success(metrics);
            }
        }
    }
}
