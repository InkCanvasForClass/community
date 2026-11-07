using System;
using Ink_Canvas.Models;
using Xunit;

namespace InkCanvasForClass.Tests
{
    /// <summary>
    /// <see cref="ToolModeMapping"/> 的 characterization 测试：
    /// 锁定字符串字面量 ↔ 枚举双向映射现状。这些字面量是 tool/state URI 的
    /// 持久化锚点，永不可改名，故此处做逐字节往返断言。
    /// </summary>
    public class ToolModeMappingTests
    {
        // 已知字面量全集（与 ToolModeMapping.Entries 一一对应）。
        // ToolMode 为 internal 枚举，不能出现在 public 测试方法签名里（CS0051），
        // 故 TheoryData 携带期望枚举名（字符串），断言时按名比较。
        public static readonly TheoryData<string, string> KnownLiterals = new TheoryData<string, string>
        {
            { "cursor", "Cursor" },
            { "pen", "Pen" },
            { "color", "Color" },
            { "eraser", "Eraser" },
            { "eraserByStrokes", "EraserByStrokes" },
            { "select", "Select" },
            { "shape", "Shape" },
            { "roaming", "Roaming" },
        };

        private static string ModeName(ToolMode mode) => Enum.GetName(typeof(ToolMode), mode);

        [Theory]
        [MemberData(nameof(KnownLiterals))]
        public void FromInternalString_KnownLiteral_ReturnsMatchingMode(string literal, string expectedModeName)
        {
            // Arrange（数据行即 Arrange）

            // Act
            var mode = ToolModeMapping.FromInternalString(literal);

            // Assert
            Assert.Equal(expectedModeName, ModeName(mode));
        }

        [Theory]
        [MemberData(nameof(KnownLiterals))]
        public void RoundTrip_KnownLiteral_ReturnsByteIdenticalString(string literal, string expectedModeName)
        {
            // Arrange（数据行即 Arrange）

            // Act
            var mode = ToolModeMapping.FromInternalString(literal);
            var roundTripped = ToolModeMapping.ToInternalString(mode);

            // Assert：往返必须逐字节相等（持久化锚点）
            Assert.Equal(expectedModeName, ModeName(mode));
            Assert.Equal(literal, roundTripped);
        }

        [Fact]
        public void FromInternalString_UnknownString_ReturnsUnknownWithoutThrowing()
        {
            // Arrange
            const string unknown = "laserPointer";

            // Act
            var mode = ToolModeMapping.FromInternalString(unknown);

            // Assert
            Assert.Equal("Unknown", ModeName(mode));
        }

        [Fact]
        public void FromInternalString_Null_ReturnsUnknownWithoutThrowing()
        {
            // Arrange
            const string nullLiteral = null;

            // Act
            var mode = ToolModeMapping.FromInternalString(nullLiteral);

            // Assert
            Assert.Equal("Unknown", ModeName(mode));
        }

        [Fact]
        public void FromInternalString_EmptyString_ReturnsUnknown()
        {
            // Arrange
            const string empty = "";

            // Act
            var mode = ToolModeMapping.FromInternalString(empty);

            // Assert
            Assert.Equal("Unknown", ModeName(mode));
        }

        [Fact]
        public void FromInternalString_DifferentCase_ReturnsUnknown_OrdinalComparisonIsCaseSensitive()
        {
            // Arrange：现状为 StringComparer.Ordinal（大小写敏感），本测试锁定该行为而非期望行为
            const string wrongCase = "PEN";

            // Act
            var mode = ToolModeMapping.FromInternalString(wrongCase);

            // Assert
            Assert.Equal("Unknown", ModeName(mode));
        }

        [Fact]
        public void FromInternalString_LeadingWhitespace_ReturnsUnknown()
        {
            // Arrange：序数比较不做任何修剪
            const string padded = " pen";

            // Act
            var mode = ToolModeMapping.FromInternalString(padded);

            // Assert
            Assert.Equal("Unknown", ModeName(mode));
        }

        [Fact]
        public void ToInternalString_Unknown_ReturnsEmptyString()
        {
            // Arrange（单输入，无额外准备）

            // Act
            var literal = ToolModeMapping.ToInternalString(ToolMode.Unknown);

            // Assert：现状契约——Unknown 映射为空串而非 null
            Assert.Equal(string.Empty, literal);
        }

        [Fact]
        public void ToInternalString_OutOfRangeEnumValue_ReturnsEmptyString()
        {
            // Arrange：未注册的枚举数值不抛异常，现状与 Unknown 一致
            const ToolMode outOfRange = (ToolMode)999;

            // Act
            var literal = ToolModeMapping.ToInternalString(outOfRange);

            // Assert
            Assert.Equal(string.Empty, literal);
        }
    }
}
