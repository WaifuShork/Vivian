﻿using System.Collections.Generic;

namespace Vivian.CodeAnalysis.Syntax
{
    public sealed class AssignmentExpressionSyntax : ExpressionSyntax
    {
        internal AssignmentExpressionSyntax(SyntaxTree syntaxTree, SyntaxToken identifierToken, SyntaxToken assignmentToken, ExpressionSyntax expression)
            : base(syntaxTree)
        {
            IdentifierToken = identifierToken;
            AssignmentToken = assignmentToken;
            Expression = expression;
        }

        public override SyntaxKind Kind => SyntaxKind.AssignmentExpression;
        
        public SyntaxToken IdentifierToken { get; }
        public SyntaxToken AssignmentToken { get; }
        public ExpressionSyntax Expression { get; }
        
        public override IEnumerable<SyntaxNode> GetChildren()
        {
            yield return IdentifierToken;
            yield return AssignmentToken;
            yield return Expression;
        }
    }
}