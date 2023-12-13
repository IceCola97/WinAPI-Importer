using AngleSharp.Dom;
using AngleSharp.XPath;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace WinAPI_Importer
{
	public static class Misc
	{
		public const bool DEBUG =
#if DEBUG
			true;
#else
			false;
#endif

		public static bool IsUpperString(this string s) => s.All(c => !char.IsLower(c));

		public static bool IsUpperFully(this string s) => s.All(c => char.IsUpper(c));

		public static bool StartsWith(this string s, char first) => s.Length > 0 && s[0] == first;

		public static async Task<string> HttpGet(string url, CancellationToken token = default)
		{
			using (var client = new HttpClient())
			{
				var response = await client.GetAsync(url, token);
				return await response.Content.ReadAsStringAsync();
			}
		}

		public static bool HasWinAPI(this ITypeSymbol symbol, string apiName)
		{
			return symbol.GetMembers(apiName)
				.Any(m => m is IMethodSymbol method
					&& method.MethodKind == MethodKind.Ordinary
					&& method.IsStatic);
		}

		/// <summary>
		/// missing my .NET 8, missing the collection expression
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="array"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T[] Array<T>(params T[] array) => array;

		public static TypeSyntax CreateTypeSyntax(this SemanticModel semantic,
			ITypeSymbol symbol, int position)
		{
			return SyntaxFactory.ParseTypeName(symbol
				.ToMinimalDisplayString(semantic, position));
		}

		public static NameSyntax CreateNameSyntax(this SemanticModel semantic,
			INamedTypeSymbol symbol, int position, SymbolDisplayFormat format = null)
		{
			return SyntaxFactory.ParseName(symbol
				.ToMinimalDisplayString(semantic, position, format));
		}

		public static NameSyntax CreateNameSyntax(this SemanticModel semantic,
			string typeName, int position, bool forced = false, SymbolDisplayFormat format = null)
		{
			var symbol = semantic.Compilation.GetTypeByMetadataName(typeName);

			if (symbol is null && forced)
				symbol = semantic.Compilation.CreateErrorTypeSymbol(null, typeName, 0);

			return semantic.CreateNameSyntax(symbol, position, format);
		}

		public static NameSyntax CreateNameSyntax(this SemanticModel semantic,
			Type directType, int position, bool forced = false, SymbolDisplayFormat format = null)
		{
			return semantic.CreateNameSyntax(
				directType.FullName, position, forced, format);
		}

		public static IElement SelectElementAuto(this IDocument document, string selectorOrXPath)
		{
			const string XPathMark = "!xpath ";
			const string SelectorMark = "!selector ";

			var isXPath = selectorOrXPath.Contains('/');

			if (selectorOrXPath.StartsWith('!'))
			{
				if (selectorOrXPath.StartsWith(XPathMark))
				{
					isXPath = true;
					selectorOrXPath = selectorOrXPath
						.Substring(XPathMark.Length);
				}
				else if (selectorOrXPath.StartsWith(SelectorMark))
				{
					isXPath = false;
					selectorOrXPath = selectorOrXPath
						.Substring(SelectorMark.Length);
				}
			}

			if (isXPath)
				return document.DocumentElement
					.SelectSingleNode(selectorOrXPath) as IElement;
			else
				return document.QuerySelector(selectorOrXPath);
		}

		public static string ElementContent(this IElement element)
		{
			if (element.TagName == "META")
				return element.GetAttribute("content") ?? element.TextContent;

			return element.TextContent;
		}
	}
}
