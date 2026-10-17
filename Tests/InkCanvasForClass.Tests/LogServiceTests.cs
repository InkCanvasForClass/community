using System;
using System.IO;
using Ink_Canvas.Helpers;
using Xunit;

namespace InkCanvasForClass.Tests
{
    /// <summary>
    /// <see cref="LogService"/> 的写入与滚动测试。全部用例经 internal
    /// <c>LogExceptionCore</c> 注入临时目录与小阈值，不触碰生产路径 %AppData%。
    /// </summary>
    public class LogServiceTests : IDisposable
    {
        private readonly string _tempDir;

        public LogServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "iccce-m32-logtests-" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        private string LogFilePath => Path.Combine(_tempDir, "refactor.log");

        [Fact]
        public void LogExceptionCore_WritesFileContainingMessageAndMemberName()
        {
            // Arrange
            var ex = new InvalidOperationException("m32-write-probe");

            // Act
            LogService.LogExceptionCore(ex, "ProbeMember", _tempDir, maxFileSizeBytes: 1024 * 1024);

            // Assert
            Assert.True(File.Exists(LogFilePath));
            var content = File.ReadAllText(LogFilePath);
            Assert.Contains("m32-write-probe", content);
            Assert.Contains("[ProbeMember]", content);
        }

        [Fact]
        public void LogExceptionCore_BelowThreshold_AppendsWithoutRolling()
        {
            // Arrange
            LogService.LogExceptionCore(new Exception("first"), "M1", _tempDir, maxFileSizeBytes: 1024 * 1024);

            // Act
            LogService.LogExceptionCore(new Exception("second"), "M2", _tempDir, maxFileSizeBytes: 1024 * 1024);

            // Assert：同一文件追加两条，无滚动产物
            var content = File.ReadAllText(LogFilePath);
            Assert.Contains("first", content);
            Assert.Contains("second", content);
            Assert.False(File.Exists(LogFilePath + ".1"));
        }

        [Fact]
        public void LogExceptionCore_ExceedingThreshold_RollsCurrentFileToSlot1()
        {
            // Arrange：先写满超过阈值，再触发一次写入
            Directory.CreateDirectory(_tempDir);
            File.WriteAllText(LogFilePath, new string('x', 128));

            // Act
            LogService.LogExceptionCore(new Exception("after-roll"), "M", _tempDir, maxFileSizeBytes: 64);

            // Assert：旧内容滚入 .1，当前文件重建且只含新条目
            Assert.True(File.Exists(LogFilePath + ".1"));
            Assert.Equal(128, new FileInfo(LogFilePath + ".1").Length);
            var content = File.ReadAllText(LogFilePath);
            Assert.Contains("after-roll", content);
            Assert.DoesNotContain(new string('x', 8), content);
        }

        [Fact]
        public void LogExceptionCore_RepeatedRolls_RetainsAtMostThreeBackups()
        {
            // Arrange：阈值 1 字节——每次写入都触发滚动
            const long threshold = 1;

            // Act：连续 5 次写入 → 滚动 4 次
            for (var i = 1; i <= 5; i++)
            {
                LogService.LogExceptionCore(new Exception($"entry-{i}"), "M", _tempDir, threshold);
            }

            // Assert：最多保留 .1/.2/.3 三个备份，且第 5 次写入在当前文件中
            Assert.True(File.Exists(LogFilePath + ".1"));
            Assert.True(File.Exists(LogFilePath + ".2"));
            Assert.True(File.Exists(LogFilePath + ".3"));
            Assert.False(File.Exists(LogFilePath + ".4"));
            Assert.Contains("entry-5", File.ReadAllText(LogFilePath));
        }
    }
}
