using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ink_Canvas.Mathematics.Services
{
    public static class MathExpressionParser
    {
        public const int MaximumExpressionLength = 256;
        public const int MaximumParseDepth = 32;

        public static MathExpression Parse(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                throw new FormatException("Expression is empty.");
            if (expression.Length > MaximumExpressionLength)
                throw new FormatException($"Expression exceeds {MaximumExpressionLength} characters.");

            var text = expression.Trim();
            if (text.StartsWith("y=", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(2);

            var parser = new Parser(text);
            var evaluator = parser.ParseExpression();
            parser.ExpectEnd();
            return new MathExpression(evaluator);
        }

        private sealed class Parser
        {
            private static readonly IReadOnlyDictionary<string, Func<double, double>> Functions =
                new Dictionary<string, Func<double, double>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sin"] = Math.Sin,
                    ["cos"] = Math.Cos,
                    ["tan"] = Math.Tan,
                    ["sqrt"] = value => value < 0 ? double.NaN : Math.Sqrt(value),
                    ["abs"] = Math.Abs,
                    ["log"] = value => value <= 0 ? double.NaN : Math.Log(value),
                    ["exp"] = Math.Exp
                };

            private readonly string _text;
            private int _position;
            private int _depth;

            public Parser(string text)
            {
                _text = text;
            }

            public Func<double, double> ParseExpression()
            {
                Enter();
                try
                {
                    var left = ParseTerm();
                    while (true)
                    {
                        SkipWhitespace();
                        if (TryConsume('+'))
                        {
                            var right = ParseTerm();
                            var previous = left;
                            left = x => previous(x) + right(x);
                        }
                        else if (TryConsume('-'))
                        {
                            var right = ParseTerm();
                            var previous = left;
                            left = x => previous(x) - right(x);
                        }
                        else
                        {
                            return left;
                        }
                    }
                }
                finally
                {
                    _depth--;
                }
            }

            public void ExpectEnd()
            {
                SkipWhitespace();
                if (_position != _text.Length)
                    throw Error($"Unexpected character '{_text[_position]}'.");
            }

            private Func<double, double> ParseTerm()
            {
                var left = ParseUnary();
                while (true)
                {
                    SkipWhitespace();
                    if (TryConsume('*'))
                    {
                        var right = ParseUnary();
                        var previous = left;
                        left = x => previous(x) * right(x);
                    }
                    else if (TryConsume('/'))
                    {
                        var right = ParseUnary();
                        var previous = left;
                        left = x =>
                        {
                            var denominator = right(x);
                            return Math.Abs(denominator) <= double.Epsilon
                                ? double.NaN
                                : previous(x) / denominator;
                        };
                    }
                    else
                    {
                        return left;
                    }
                }
            }

            private Func<double, double> ParsePower()
            {
                var left = ParsePrimary();
                SkipWhitespace();
                if (!TryConsume('^')) return left;
                var right = ParseUnary();
                return x => Math.Pow(left(x), right(x));
            }

            private Func<double, double> ParseUnary()
            {
                SkipWhitespace();
                if (TryConsume('+')) return ParseUnary();
                if (TryConsume('-'))
                {
                    var inner = ParseUnary();
                    return x => -inner(x);
                }
                return ParsePower();
            }

            private Func<double, double> ParsePrimary()
            {
                SkipWhitespace();
                if (_position >= _text.Length)
                    throw Error("Expected a number, variable, function, or parenthesis.");

                if (TryConsume('('))
                {
                    var nested = ParseExpression();
                    SkipWhitespace();
                    if (!TryConsume(')')) throw Error("Missing closing parenthesis.");
                    return nested;
                }

                if (char.IsDigit(_text[_position]) || _text[_position] == '.')
                    return ParseNumber();

                if (char.IsLetter(_text[_position]))
                {
                    var identifier = ParseIdentifier();
                    if (string.Equals(identifier, "x", StringComparison.OrdinalIgnoreCase))
                        return x => x;
                    if (string.Equals(identifier, "pi", StringComparison.OrdinalIgnoreCase))
                        return _ => Math.PI;
                    if (string.Equals(identifier, "e", StringComparison.OrdinalIgnoreCase))
                        return _ => Math.E;
                    if (!Functions.TryGetValue(identifier, out var function))
                        throw Error($"Unknown identifier '{identifier}'.");

                    SkipWhitespace();
                    if (!TryConsume('(')) throw Error($"Function '{identifier}' requires parentheses.");
                    var argument = ParseExpression();
                    SkipWhitespace();
                    if (!TryConsume(')')) throw Error("Missing closing parenthesis.");
                    return x => function(argument(x));
                }

                throw Error($"Unexpected character '{_text[_position]}'.");
            }

            private Func<double, double> ParseNumber()
            {
                var start = _position;
                var hasExponent = false;
                while (_position < _text.Length)
                {
                    var current = _text[_position];
                    if (char.IsDigit(current) || current == '.')
                    {
                        _position++;
                        continue;
                    }
                    if ((current == 'e' || current == 'E') && !hasExponent)
                    {
                        hasExponent = true;
                        _position++;
                        if (_position < _text.Length && (_text[_position] == '+' || _text[_position] == '-'))
                            _position++;
                        continue;
                    }
                    break;
                }

                var token = _text.Substring(start, _position - start);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                    !double.IsFinite(value))
                    throw Error($"Invalid number '{token}'.");
                return _ => value;
            }

            private string ParseIdentifier()
            {
                var start = _position;
                while (_position < _text.Length && char.IsLetter(_text[_position]))
                    _position++;
                return _text.Substring(start, _position - start);
            }

            private bool TryConsume(char value)
            {
                if (_position >= _text.Length || _text[_position] != value) return false;
                _position++;
                return true;
            }

            private void SkipWhitespace()
            {
                while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
                    _position++;
            }

            private void Enter()
            {
                _depth++;
                if (_depth > MaximumParseDepth)
                    throw Error($"Expression nesting exceeds {MaximumParseDepth} levels.");
            }

            private FormatException Error(string message)
            {
                return new FormatException($"{message} Position: {_position}.");
            }
        }
    }
}
