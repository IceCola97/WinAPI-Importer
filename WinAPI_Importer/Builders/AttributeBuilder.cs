using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace WinAPI_Importer.Builders
{
	public sealed class AttributeBuilder
	{
		private readonly List<AttributeArgumentSyntax> m_Argumnets = new List<AttributeArgumentSyntax>();
		private NameSyntax m_AttributeClass;

		public void AddArgument(AttributeArgumentSyntax argument) => m_Argumnets.Add(argument);

		/// <summary>
		/// [DemoAttribute(expression)]
		/// </summary>
		/// <param name="argument"></param>
		public void AddArgument(ExpressionSyntax argument) => AddArgument(AttributeArgument(argument));

		/// <summary>
		/// [DemoAttribute(argument: expression)]
		/// </summary>
		/// <param name="name"></param>
		/// <param name="argument"></param>
		public void AddNamedArgument(string name, ExpressionSyntax argument)
			=> AddArgument(AttributeArgument(null, NameColon(name), argument));

		/// <summary>
		/// [DemoAttribute(Property = expression)]
		/// </summary>
		/// <param name="property"></param>
		/// <param name="init"></param>
		public void AddProperty(string property, ExpressionSyntax init)
			=> AddArgument(AttributeArgument(NameEquals(property), null, init));

		public void SetClass(NameSyntax name) => m_AttributeClass = name;

		public void SetClass(INamedTypeSymbol name, SemanticModel semantic, int position)
		{
			SetClass(semantic.CreateNameSyntax(name, position,
				new SymbolDisplayFormat(miscellaneousOptions:
					SymbolDisplayMiscellaneousOptions.RemoveAttributeSuffix)));
		}

		public void SetClass(Type directType, SemanticModel semantic, int position)
		{
			SetClass(semantic.CreateNameSyntax(directType, position, true,
				new SymbolDisplayFormat(miscellaneousOptions:
					SymbolDisplayMiscellaneousOptions.RemoveAttributeSuffix)));
		}

		public AttributeSyntax Create()
		{
			var arguments = m_Argumnets.Count > 0
				? AttributeArgumentList(SeparatedList(m_Argumnets)) : null;

			return Attribute(m_AttributeClass, arguments);
		}
	}
}
