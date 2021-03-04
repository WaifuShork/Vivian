using System;
using System.Collections.Generic;
using System.Linq;
using Vivian.CodeAnalysis.Syntax;
using Vivian.CodeAnalysis.Text;
using Xunit;

namespace Vivian.Tests.CodeAnalysis.Syntax
{
    public class LexerTests
        {
            [Fact]
            public void Lexer_Lexes_UnterminatedString()
            {
                const string? text = "\"text";
                var tokens = SyntaxTree.ParseTokens(text, out var diagnostics);
    
                var token = Assert.Single(tokens);
                Assert.Equal(SyntaxKind.StringToken, token!.Kind);
                Assert.Equal(text, token.Text);
    
                var diagnostic = Assert.Single(diagnostics);
                Assert.Equal(new TextSpan(0, 1), diagnostic!.Location.Span);
                Assert.Equal("Unterminated string literal.", diagnostic.Message);
            }
    
            [Fact]
            public void Lexer_Covers_AllTokens()
            {
                var tokenKinds = Enum.GetValues(typeof(SyntaxKind))
                                     .Cast<SyntaxKind>()
                                     .Where(k => k.IsToken());
    
                var testedTokenKinds = GetTokens().Concat(GetSeparators()).Select(t => t.kind);
    
                var untestedTokenKinds = new SortedSet<SyntaxKind>(tokenKinds);
                untestedTokenKinds.Remove(SyntaxKind.BadToken);
                untestedTokenKinds.Remove(SyntaxKind.EndOfFileToken);
                untestedTokenKinds.ExceptWith(testedTokenKinds);
    
                Assert.Empty(untestedTokenKinds);
            }
    
            [Theory]
            [MemberData(nameof(GetTokensData))]
            public void Lexer_Lexes_Token(SyntaxKind kind, string text)
            {
                var tokens = SyntaxTree.ParseTokens(text);
    
                var token = Assert.Single(tokens);
                Assert.Equal(kind, token!.Kind);
                Assert.Equal(text, token.Text);
            }
    
            [Theory]
            [MemberData(nameof(GetSeparatorsData))]
            public void Lexer_Lexes_Separator(SyntaxKind kind, string text)
            {
                var tokens = SyntaxTree.ParseTokens(text, includeEndOfFile: true);
    
                var token = Assert.Single(tokens);
                var trivia = Assert.Single(token!.LeadingTrivia);
                Assert.Equal(kind, trivia!.Kind);
                Assert.Equal(text, trivia.Text);
            }
    
            [Theory]
            [MemberData(nameof(GetTokenPairsData))]
            public void Lexer_Lexes_TokenPairs(SyntaxKind t1Kind, string t1Text,
                                               SyntaxKind t2Kind, string t2Text)
            {
                var text = t1Text + t2Text;
                var tokens = SyntaxTree.ParseTokens(text).ToArray();
    
                Assert.Equal(2, tokens.Length);
                Assert.Equal(t1Kind, tokens[0].Kind);
                Assert.Equal(t1Text, tokens[0].Text);
                Assert.Equal(t2Kind, tokens[1].Kind);
                Assert.Equal(t2Text, tokens[1].Text);
            }
    
            [Theory]
            [MemberData(nameof(GetTokenPairsWithSeparatorData))]
            public void Lexer_Lexes_TokenPairs_WithSeparators(SyntaxKind t1Kind, string t1Text,
                                                              SyntaxKind separatorKind, string separatorText,
                                                              SyntaxKind t2Kind, string t2Text)
            {
                var text = t1Text + separatorText + t2Text;
                var tokens = SyntaxTree.ParseTokens(text).ToArray();
    
                Assert.Equal(2, tokens.Length);
                Assert.Equal(t1Kind, tokens[0].Kind);
                Assert.Equal(t1Text, tokens[0].Text);
    
                var separator = Assert.Single(tokens[0].TrailingTrivia);
                Assert.Equal(separatorKind, separator!.Kind);
                Assert.Equal(separatorText, separator.Text);
    
                Assert.Equal(t2Kind, tokens[1].Kind);
                Assert.Equal(t2Text, tokens[1].Text);
            }
    
            [Theory]
            [InlineData("foo")]
            [InlineData("foo42")]
            [InlineData("foo_42")]
            [InlineData("_foo")]
            public void Lexer_Lexes_Identifiers(string name)
            {
                var tokens = SyntaxTree.ParseTokens(name).ToArray();
    
                Assert.Single(tokens);
    
                var token = tokens[0];
                Assert.Equal(SyntaxKind.IdentifierToken, token.Kind);
                Assert.Equal(name, token.Text);
            }
    
            [Theory]
            [InlineData("42", 42)]
            [InlineData("42.01", 42.01)]
            [InlineData("1_000_000", 1_000_000)]
            [InlineData("1_000_000.001", 1_000_000)]
            public void Lexer_Lexes_NumberLiterals(string text, float value)
            {
                var tokens = SyntaxTree.ParseTokens(text).ToArray();
    
                Assert.Single(tokens);
    
                var token = tokens[0];
                Assert.Equal(SyntaxKind.NumberToken, token.Kind);
                Assert.Equal(text, token.Text);
                Assert.Equal(float.Parse(text.Replace("_", "")), value);
            }
    
            public static IEnumerable<object[]> GetTokensData()
            {
                foreach (var (kind, text) in GetTokens())
                {
                    yield return new object[] { kind, text };
                }
            }
    
            public static IEnumerable<object[]> GetSeparatorsData()
            {
                foreach (var (kind, text) in GetSeparators())
                {
                    yield return new object[] { kind, text };
                }
            }
    
            public static IEnumerable<object[]> GetTokenPairsData()
            {
                foreach (var (t1Kind, t1Text, t2Kind, t2Text) in GetTokenPairs())
                {
                    yield return new object[] { t1Kind, t1Text, t2Kind, t2Text };
                }
            }
    
            public static IEnumerable<object[]> GetTokenPairsWithSeparatorData()
            {
                foreach (var (t1Kind, t1Text, separatorKind, separatorText, t2Kind, t2Text) in GetTokenPairsWithSeparator())
                {
                    yield return new object[] { t1Kind, t1Text, separatorKind, separatorText, t2Kind, t2Text };
                }
            }
    
            private static IEnumerable<(SyntaxKind kind, string text)> GetTokens()
            {
                var fixedTokens = Enum.GetValues(typeof(SyntaxKind))
                                      .Cast<SyntaxKind>()
                                      .Select(k => (k, text: SyntaxFacts.GetText(k)))
                                      .Where(t => t.text != null)
                                      .Cast<(SyntaxKind, string)>();
    
                var dynamicTokens = new[]
                {
                    (SyntaxKind.NumberToken, "1"),
                    (SyntaxKind.NumberToken, "123"),
                    (SyntaxKind.NumberToken, "1.0"),
                    (SyntaxKind.NumberToken, "123.3"),
                    (SyntaxKind.IdentifierToken, "a"),
                    (SyntaxKind.IdentifierToken, "abc"),
                    (SyntaxKind.StringToken, "\"Test\""),
                    (SyntaxKind.StringToken, "\"Te\"\"st\""),
                    (SyntaxKind.CharToken, "\'T\'"),
                    (SyntaxKind.CharToken, "\'\'\'\'"),
                };
    
                return fixedTokens.Concat(dynamicTokens);
            }
    
            private static IEnumerable<(SyntaxKind kind, string text)> GetSeparators()
            {
                return new[]
                {
                    (SyntaxKind.WhitespaceTrivia, " "),
                    (SyntaxKind.WhitespaceTrivia, "  "),
                    (SyntaxKind.LineBreakTrivia, "\r"),
                    (SyntaxKind.LineBreakTrivia, "\n"),
                    (SyntaxKind.LineBreakTrivia, "\r\n"),
                    (SyntaxKind.MultiLineCommentTrivia, "/**/"),
                };
            }
    
            private static bool RequiresSeparator(SyntaxKind t1Kind, SyntaxKind t2Kind)
            {
                var t1IsKeyword = t1Kind.IsKeyword();
                var t2IsKeyword = t2Kind.IsKeyword();

                if (t1Kind == SyntaxKind.IdentifierToken && t2Kind == SyntaxKind.IdentifierToken)
                {
                    return true;
                }

                if (t1IsKeyword && t2IsKeyword)
                {
                    return true;
                }

                if (t1IsKeyword && t2Kind == SyntaxKind.IdentifierToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.IdentifierToken && t2IsKeyword)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.IdentifierToken && t2Kind == SyntaxKind.NumberToken)
                {
                    return true;
                }

                if (t1IsKeyword && t2Kind == SyntaxKind.NumberToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.NumberToken && t2Kind == SyntaxKind.NumberToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.StringToken && t2Kind == SyntaxKind.StringToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.CharToken && t2Kind == SyntaxKind.CharToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.BangToken && t2Kind == SyntaxKind.EqualsToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.BangToken && t2Kind == SyntaxKind.EqualsEqualsToken)
                {
                    return true;
                }
    
                if (t1Kind == SyntaxKind.EqualsToken && t2Kind == SyntaxKind.EqualsToken)
                {
                    return true; 
                }

                if (t1Kind == SyntaxKind.EqualsToken && t2Kind == SyntaxKind.EqualsEqualsToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.PlusToken && t2Kind == SyntaxKind.EqualsToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.PlusToken && t2Kind == SyntaxKind.EqualsEqualsToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.MinusToken && t2Kind == SyntaxKind.EqualsToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.MinusToken && t2Kind == SyntaxKind.EqualsEqualsToken)
                {
                    return true;
                }
    
                if (t1Kind == SyntaxKind.StarToken && t2Kind == SyntaxKind.EqualsToken)
                    return true;

                if (t1Kind == SyntaxKind.StarToken && t2Kind == SyntaxKind.EqualsEqualsToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.SlashToken && t2Kind == SyntaxKind.EqualsToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.SlashToken && t2Kind == SyntaxKind.EqualsEqualsToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.LessToken && t2Kind == SyntaxKind.EqualsToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.LessToken && t2Kind == SyntaxKind.EqualsEqualsToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.GreaterToken && t2Kind == SyntaxKind.EqualsToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.GreaterToken && t2Kind == SyntaxKind.EqualsEqualsToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.AmpersandToken && t2Kind == SyntaxKind.AmpersandToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.AmpersandToken && t2Kind == SyntaxKind.AmpersandAmpersandToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.AmpersandToken && t2Kind == SyntaxKind.EqualsToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.AmpersandToken && t2Kind == SyntaxKind.EqualsEqualsToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.AmpersandToken && t2Kind == SyntaxKind.AmpersandEqualsToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.PipeToken && t2Kind == SyntaxKind.PipeToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.PipeToken && t2Kind == SyntaxKind.PipePipeToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.PipeToken && t2Kind == SyntaxKind.EqualsToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.PipeToken && t2Kind == SyntaxKind.EqualsEqualsToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.PipeToken && t2Kind == SyntaxKind.PipeEqualsToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.HatToken && t2Kind == SyntaxKind.EqualsToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.HatToken && t2Kind == SyntaxKind.EqualsEqualsToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.SlashToken && t2Kind == SyntaxKind.SlashToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.SlashToken && t2Kind == SyntaxKind.StarToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.SlashToken && t2Kind == SyntaxKind.SlashEqualsToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.SlashToken && t2Kind == SyntaxKind.StarEqualsToken)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.SlashToken && t2Kind == SyntaxKind.SingleLineCommentTrivia)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.SlashToken && t2Kind == SyntaxKind.MultiLineCommentTrivia)
                {
                    return true;
                }

                if (t1Kind == SyntaxKind.EqualsGreaterThanToken && t2Kind == SyntaxKind.GreaterToken)
                {
                    return true;
                }


                return false;
            }
    
            private static IEnumerable<(SyntaxKind t1Kind, string t1Text, SyntaxKind t2Kind, string t2Text)> GetTokenPairs()
            {
                foreach (var (kind, text) in GetTokens())
                {
                    foreach (var (syntaxKind, str) in GetTokens())
                    {
                        if (!RequiresSeparator(kind, syntaxKind))
                        {
                            yield return (kind, text, syntaxKind, str);
                        }
                    }
                }
            }
    
            private static IEnumerable<(SyntaxKind t1Kind, string t1Text,
                                        SyntaxKind separatorKind, string separatorText,
                                        SyntaxKind t2Kind, string t2Text)> GetTokenPairsWithSeparator()
            {
                foreach (var (kind, text) in GetTokens())
                {
                    foreach (var (syntaxKind, text1) in GetTokens())
                    {
                        if (RequiresSeparator(kind, syntaxKind))
                        {
                            foreach (var (syntaxKind1, str) in GetSeparators())
                            {
                                if (!RequiresSeparator(kind, syntaxKind1) && !RequiresSeparator(syntaxKind1, syntaxKind))
                                {
                                    yield return (kind, text, syntaxKind1, str, syntaxKind, text1);
                                }
                            }
                        }
                    }
                }
            }
        }
}