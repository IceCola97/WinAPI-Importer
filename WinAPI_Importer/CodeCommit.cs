using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinAPI_Importer.Builders;

using static WinAPI_Importer.Translate;

namespace WinAPI_Importer
{
	public static class CodeCommit
	{
		public static Func<string, bool> MakeTypeSearcher(ITypeSymbol symbol, Document document)
		{
			//throw new NotImplementedException();

			return _ => true;
		}

		public static async Task<Solution> CommitItems(
			ResolvedEntry[] resolved,
			ConfigSource config,
			IdentifierNameSyntax identifier,
			ITypeSymbol symbol,
			Document document,
			CancellationToken cancellationToken
		)
		{
			var entry = resolved[0];
			var syntax = await symbol.DeclaringSyntaxReferences
				.First().GetSyntaxAsync(cancellationToken);

			document = document.Project.Solution.GetDocument(syntax.SyntaxTree);
			var currentId = document.Id;

			var renamed = await RenameOnly(entry.Name, identifier,
				document, cancellationToken);

			document = renamed.GetDocument(currentId);

			var semantic = await document.GetSemanticModelAsync(cancellationToken);
			var compilation = semantic.Compilation;

			var symbolName = symbol.ToDisplayString(
				new SymbolDisplayFormat(typeQualificationStyle:
				SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces));
			symbol = compilation.GetTypeByMetadataName(symbolName);
			syntax = await symbol.DeclaringSyntaxReferences
				.First().GetSyntaxAsync(cancellationToken);

			var last = syntax.ChildNodes()
				.Where(node => node is MethodDeclarationSyntax)
				.LastOrDefault() ?? syntax.ChildNodes().LastOrDefault();
			var position = syntax.ChildTokens().Last().SpanStart - 1;

			var methodBuilder = new MethodDeclarationBuilder();

			// DllImport
			var attrBuilder = new AttributeBuilder();
			attrBuilder.SetClass(typeof(DllImportAttribute), semantic, position);
			attrBuilder.AddArgument(StringLiteral(entry.Module.ToLower()));
			attrBuilder.AddProperty(nameof(DllImportAttribute.SetLastError),
				SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression));
			methodBuilder.AddAttribute(attrBuilder.Create());

			// Modifiers
			if (symbol.DeclaredAccessibility == Accessibility.Public)
				methodBuilder.AddModifier(SyntaxKind.PublicKeyword);
			else
				methodBuilder.AddModifier(SyntaxKind.InternalKeyword);

			methodBuilder.AddModifier(SyntaxKind.StaticKeyword);
			methodBuilder.AddModifier(SyntaxKind.ExternKeyword);

			// ReturnType
			var returnSlot = entry.SlotAt(0);
			var (returnType, inner) = config.ConvertType(returnSlot.TypeName, compilation);
			var modifier = TypeModifier.Combine(inner, returnSlot.TypeModifier);
			methodBuilder.SetReturnType(modifier.ApplyType(semantic, position, returnType));

			// Identifier
			methodBuilder.SetIdentifier(entry.Name);

			// Parameters
			for (int i = 1; i < entry.SlotCount; i++)
			{
				var paramSlot = entry.SlotAt(i);
				var (paramType, paramInner) = config.ConvertType(paramSlot.TypeName, compilation);
				var paramModifier = TypeModifier.Combine(paramInner, paramSlot.TypeModifier);

				methodBuilder.AddParameter(paramModifier.ApplyParameter(semantic, position,
					paramType, paramSlot.Name, paramSlot.IsOptional, paramSlot.TypeModifier.RefParamType));
			}

			var newLineTrivia = SyntaxFactory.SyntaxTrivia(
				SyntaxKind.EndOfLineTrivia, Environment.NewLine);
			var xmlComment = new XmlBuilder()
				.AddNode("summary", new XmlBuilder()
					.AddSeeHrefNode(entry.ReferUrl))
				.Create();
			var newMethod = methodBuilder.Create()
				.WithLeadingTrivia(newLineTrivia, xmlComment, newLineTrivia);
			var newSyntax = syntax.InsertNodesAfter(last, Misc.Array(newMethod));
			var root = await document.GetSyntaxRootAsync(cancellationToken);

			root = root.ReplaceNode(syntax, newSyntax);

			var inserted = document.Project.Solution
				.WithDocumentSyntaxRoot(document.Id, root);

			return inserted;
			//return await RenameOnly(entry.Name, identifier,
			//	inserted.GetDocument(originalId), cancellationToken);
		}

		public static async Task<Solution> RenameOnly(
			string newName,
			IdentifierNameSyntax identifier,
			Document document,
			CancellationToken cancellationToken
		)
		{
			var targetId = document.Id;

			var newText = identifier.WithIdentifier(SyntaxFactory.Identifier(newName));
			newText = newText.WithTriviaFrom(identifier);

			var root = await identifier.SyntaxTree.GetRootAsync(cancellationToken);
			root = root.ReplaceNode(identifier, newText);

			return document.Project.Solution
				.WithDocumentSyntaxRoot(targetId, root);
		}

		private static LiteralExpressionSyntax StringLiteral(string literal)
		{
			return SyntaxFactory.LiteralExpression(
				SyntaxKind.StringLiteralExpression,
				SyntaxFactory.Literal(literal)
			);
		}
	}
}
