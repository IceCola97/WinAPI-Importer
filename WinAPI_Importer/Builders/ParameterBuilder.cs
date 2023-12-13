using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace WinAPI_Importer.Builders
{
	public sealed class ParameterBuilder
	{
		private readonly List<AttributeSyntax> m_Attributes = new List<AttributeSyntax>();
		private readonly List<SyntaxToken> m_Modifiers = new List<SyntaxToken>();
		private TypeSyntax m_ParameterType;
		private SyntaxToken m_Identifier;
		private ExpressionSyntax m_DefaultValue;

		public void AddAttribute(AttributeSyntax attribute) => m_Attributes.Add(attribute);

		public void AddModifier(SyntaxToken modifier) => m_Modifiers.Add(modifier);

		public void AddModifier(SyntaxKind modifier) => m_Modifiers.Add(Token(modifier));

		public void AddModifier(RefParamType refParamType)
		{
			switch (refParamType)
			{
				case RefParamType.In:
					AddModifier(SyntaxKind.InKeyword);
					break;
				case RefParamType.Out:
					AddModifier(SyntaxKind.OutKeyword);
					break;
				case RefParamType.Ref:
					AddModifier(SyntaxKind.RefKeyword);
					break;
			}
		}

		public void SetParameterType(TypeSyntax type) => m_ParameterType = type;

		public void SetParameterType(string type) => m_ParameterType = ParseTypeName(type);

		public void SetIdentifier(string name) => m_Identifier = Identifier(name);

		public void SetDefaultValue(ExpressionSyntax literal) => m_DefaultValue = literal;

		public void SetDefaultValue(SyntaxKind kind) => m_DefaultValue = LiteralExpression(kind);

		public void SetDefaultValue(SyntaxKind kind, SyntaxToken token)
			=> m_DefaultValue = LiteralExpression(kind, token);

		public ParameterSyntax Create()
		{
			// [Attribute1, Attribute2]
			// [Attribute1]\n[Attribute2] // I prefer this
			var attributes = List(m_Attributes.Select(
				a => AttributeList(SeparatedList(Misc.Array(a)))));

			return Parameter(attributes, TokenList(m_Modifiers),
				m_ParameterType, m_Identifier, m_DefaultValue is null
					? null : EqualsValueClause(m_DefaultValue));
		}
	}
}
