﻿using Vivian.CodeAnalysis.Symbols;
using Vivian.CodeAnalysis.Syntax;

namespace Vivian.CodeAnalysis.Binding
{
    internal sealed class BoundLocalFieldAccessExpression : BoundExpression
    {
        public BoundLocalFieldAccessExpression(SyntaxNode syntax, StructSymbol instance, VariableSymbol structMember)
            : base(syntax)
        {
            Instance = instance;
            StructMember = structMember;
        }

        public override TypeSymbol Type => StructMember.Type;
        public override BoundNodeKind Kind => BoundNodeKind.ThisExpression;
        public StructSymbol Instance { get; }
        public VariableSymbol StructMember { get; }
    }
}