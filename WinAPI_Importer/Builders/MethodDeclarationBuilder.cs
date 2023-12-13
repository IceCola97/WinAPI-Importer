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
	public class MethodDeclarationBuilder
	{
		private readonly List<AttributeSyntax> m_Attributes = new List<AttributeSyntax>();
		private readonly List<SyntaxToken> m_Modifiers = new List<SyntaxToken>();
		private TypeSyntax m_ReturnType;
		private ExplicitInterfaceSpecifierSyntax m_InterfaceSpecifier;
		private SyntaxToken m_Identifier;
		private readonly List<TypeParameterSyntax> m_TypeParameters = new List<TypeParameterSyntax>();
		private readonly List<ParameterSyntax> m_Parameters = new List<ParameterSyntax>();
		private readonly List<TypeParameterConstraintClauseSyntax> m_Constraints
			= new List<TypeParameterConstraintClauseSyntax>();
		private BlockSyntax m_Body;

		public void AddAttribute(AttributeSyntax attribute) => m_Attributes.Add(attribute);

		public void AddModifier(SyntaxToken modifier) => m_Modifiers.Add(modifier);

		public void AddModifier(SyntaxKind modifier) => m_Modifiers.Add(Token(modifier));

		public void AddTypeParameter(TypeParameterSyntax typeParameter) => m_TypeParameters.Add(typeParameter);

		public void AddParameter(ParameterSyntax parameter) => m_Parameters.Add(parameter);

		public void AddConstraint(TypeParameterConstraintClauseSyntax constraint) => m_Constraints.Add(constraint);

		public void SetReturnType(TypeSyntax type) => m_ReturnType = type;

		public void SetInterfaceSpecifier(ExplicitInterfaceSpecifierSyntax interfaceSpecifier) => m_InterfaceSpecifier = interfaceSpecifier;

		public void SetIdentifier(string name) => m_Identifier = Identifier(name);

		public void SetBody(BlockSyntax body) => m_Body = body;

		public MethodDeclarationSyntax Create()
		{
			var attributes = List(m_Attributes.Select(
				a => AttributeList(SeparatedList(Misc.Array(a)))));
			var modifiers = TokenList(m_Modifiers);
			var typeParameters = m_TypeParameters.Count > 0
				? TypeParameterList(SeparatedList(m_TypeParameters)) : null;
			var parameters = ParameterList(SeparatedList(m_Parameters));
			var constraints = List(m_Constraints);

			return MethodDeclaration(attributes, modifiers, m_ReturnType,
				m_InterfaceSpecifier, m_Identifier, typeParameters, parameters,
				constraints, m_Body, m_Body is null ? Token(SyntaxKind.SemicolonToken) : default);
		}
	}
}
