using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

using static WinAPI_Importer.Translate;

namespace WinAPI_Importer
{
	[ExportCodeRefactoringProvider(LanguageNames.CSharp,
		Name = nameof(WinAPI_ImporterCodeRefactoringProvider)), Shared]
	internal class WinAPI_ImporterCodeRefactoringProvider : CodeRefactoringProvider
	{
		static WinAPI_ImporterCodeRefactoringProvider()
		{
			AssemblyLoader.Initialize();
		}

		public sealed override async Task ComputeRefactoringsAsync
			(CodeRefactoringContext context)
		{
			if (!context.Span.IsEmpty)
				return;

			var root = await context.Document
				.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

			if (root.Language != LanguageNames.CSharp)
				return;

			var memberNameSyntax = root.FindNode(context.Span)
				as IdentifierNameSyntax;

			if (memberNameSyntax is null)
				return;

			var memberAccessSyntax = memberNameSyntax.Parent
				as MemberAccessExpressionSyntax;

			if (memberAccessSyntax is null)
				return;

			var children = memberAccessSyntax.ChildNodesAndTokens().ToArray();

			if (!(children.Length == 3
				&& children[0].IsNode
				&& children[0].AsNode() is ExpressionSyntax beforeDot
				&& children[1].IsToken
				&& children[1].Kind() == SyntaxKind.DotToken
				&& children[2].IsNode
				&& children[2].AsNode() is IdentifierNameSyntax afterDot))
				return;

			var semanticModel = await context.Document
				.GetSemanticModelAsync(context.CancellationToken);
			var symbol = semanticModel.GetTypeInfo(beforeDot,
				context.CancellationToken).ConvertedType;

			if (symbol is null
				|| symbol.TypeKind != TypeKind.Class
				|| !symbol.IsWindowsAPIClass())
				return;

			var apiName = afterDot.Identifier.Text;

			if (symbol.HasWinAPI(apiName))
				return;

			var config = await ConfigSource.GetConfigSource(
				context.Document.Project.Solution);

			var subActions = await CreateActions(apiName, config, symbol, afterDot,
				context.Document, context.CancellationToken);
			var codeAction = CodeAction.Create(T(Text_SearchAPITitle, apiName),
				ImmutableArray.Create(subActions), false);

			context.RegisterRefactoring(codeAction);
		}

		private static async Task<CodeAction[]> CreateActions(
			string apiName,
			ConfigSource config,
			ITypeSymbol target,
			IdentifierNameSyntax afterDot,
			Document document,
			CancellationToken cancellationToken
		)
		{
			var result = await APIQuery.Query(apiName, config, cancellationToken);
			int count = 0;

			if (result.IsSuccess)
			{
				count = result.Items.Length;

				if (count == 0)
					result = QueryResult.Fail(T(Text_NoSearchResult));
			}

			if (!result.IsSuccess)
				return Misc.Array(new EmptyCodeAction(result.FailMessage));

			var list = new List<CodeAction>();

			for (int i = 0; i < count; i++)
			{
				list.Add(new ApplyCodeAction(result.Items[i], result.Names[i],
					result.Urls[i], config, target, afterDot, document));
			}

			return list.ToArray();
		}

		private sealed class TextCodeActionOperation : CodeActionOperation
		{
			public TextCodeActionOperation(string title)
			{
				Title = title;
			}

			public override string Title { get; }

			public static IEnumerable<CodeActionOperation> CreateSingle(string title)
			{
				return ImmutableArray.Create(new TextCodeActionOperation(title));
			}
		}

		private sealed class ApplyCodeAction : CodeAction
		{
			private readonly string m_NewName;
			private readonly string m_Url;
			private readonly ConfigSource m_Config;
			private readonly ITypeSymbol m_Target;
			private readonly IdentifierNameSyntax m_AfterDot;
			private readonly Document m_Document;

			private volatile ResolvedEntry[] m_Resolveds;
			private volatile string m_ErrorMessage = null;

			public ApplyCodeAction(
				string title,
				string newName,
				string url,
				ConfigSource config,
				ITypeSymbol target,
				IdentifierNameSyntax afterDot,
				Document document
			)
			{
				Title = title;

				m_NewName = newName;
				m_Url = url;
				m_Config = config;
				m_Target = target;
				m_AfterDot = afterDot;
				m_Document = document;
			}

			public override string Title { get; }

			public override string EquivalenceKey => nameof(WinAPI_Importer) + ".Apply." + m_NewName;

			private async Task EnsureResolved(CancellationToken cancellationToken = default)
			{
				var resolveds = m_Resolveds;

				if (resolveds is null)
				{
					try
					{
						resolveds = await APIQuery.Resolve(m_Url, m_Config,
							CodeCommit.MakeTypeSearcher(m_Target, m_Document), cancellationToken);
					}
					catch (OperationCanceledException)
					{
						throw;
					}
					catch (HttpRequestException ex) when (!Misc.DEBUG)
					{
						m_ErrorMessage = T(Text_NetworkError, ex.Message);
					}
					catch (Exception ex) when (!Misc.DEBUG)
					{
						m_ErrorMessage = T(Text_UnexpectedError, ex.Message);
					}

					m_Resolveds = resolveds;
				}
			}

			protected override async Task<IEnumerable<CodeActionOperation>> ComputePreviewOperationsAsync(CancellationToken cancellationToken)
			{
				await EnsureResolved(cancellationToken);

				if (m_ErrorMessage != null)
					return TextCodeActionOperation.CreateSingle(m_ErrorMessage);

				return await base.ComputePreviewOperationsAsync(cancellationToken);
			}

			protected override async Task<Solution> GetChangedSolutionAsync(CancellationToken cancellationToken)
			{
				await EnsureResolved(cancellationToken);

				if (m_ErrorMessage != null)
					return null;

				if (m_Target.HasWinAPI(m_NewName))
				{
					return await CodeCommit.RenameOnly(m_NewName,
						m_AfterDot, m_Document, cancellationToken);
				}

				return await CodeCommit.CommitItems(
					m_Resolveds,
					m_Config,
					m_AfterDot,
					m_Target,
					m_Document,
					cancellationToken
				);
			}
		}

		private sealed class EmptyCodeAction : CodeAction
		{
			public EmptyCodeAction(string title) => Title = title;

			public override string Title { get; }

			public override string EquivalenceKey => nameof(WinAPI_Importer) + ".Empty";

			protected override Task<Document> GetChangedDocumentAsync(CancellationToken cancellationToken)
			{
				return Task.FromResult(default(Document));
			}
		}
	}
}
