using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace WinAPI_Importer.Builders
{
	public sealed class XmlBuilder
	{
		private readonly List<XmlNodeSyntax> m_Nodes = new List<XmlNodeSyntax>();

		public XmlBuilder AddNode(XmlNodeSyntax node)
		{
			m_Nodes.Add(node);
			return this;
		}

		public XmlBuilder AddNode(string tagName, IEnumerable<XmlNodeSyntax> nodes)
		{
			return AddNode(XmlElement(tagName,
				List(nodes ?? Array.Empty<XmlNodeSyntax>())));
		}

		public XmlBuilder AddNode(string tagName, XmlBuilder nodes)
		{
			return AddNode(tagName, nodes.m_Nodes);
		}

		public XmlBuilder AddNode(string tagName, IEnumerable<XmlAttributeSyntax> attributes,
			IEnumerable<XmlNodeSyntax> nodes)
		{
			return AddNode(XmlElement(XmlElementStartTag(
				XmlName(tagName), List(attributes ?? Array.Empty<XmlAttributeSyntax>())),
				List(nodes ?? Array.Empty<XmlNodeSyntax>()), XmlElementEndTag(XmlName(tagName))));
		}

		public XmlBuilder AddSeeHrefNode(string href)
		{
			return AddNode("see", Misc.Array(XmlTextAttribute("href", href)), null);
		}

		public SyntaxTrivia Create()
		{
			return Comment(DocumentationComment(
				m_Nodes.ToArray()).ToFullString());
		}
	}
}
