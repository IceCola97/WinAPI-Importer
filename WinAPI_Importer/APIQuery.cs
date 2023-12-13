using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using static WinAPI_Importer.Translate;

namespace WinAPI_Importer
{
	public static class APIQuery
	{
		/// <summary>
		/// 搜索给定WinAPI并返回可能的API列表
		/// </summary>
		/// <param name="apiName"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static async Task<QueryResult> Query(string apiName, ConfigSource config,
			CancellationToken cancellationToken)
		{
			var client = new HttpClient();

			try
			{
				var response = await client.GetAsync(config.BuildUrl(apiName),
					cancellationToken);
				var content = await response.Content.ReadAsStringAsync();

				cancellationToken.ThrowIfCancellationRequested();

				var root = JsonConvert.DeserializeObject(content) as JObject;

				if (root is null)
					return QueryResult.Fail(T(Text_SearchAPIExpired));

				var results = root["results"] as JArray;

				if (results is null)
					return QueryResult.Fail(T(Text_SearchAPIExpired));

				var titles = new List<string>();
				var names = new List<string>();
				var urls = new List<string>();

				for (int i = 0; i < results.Count; i++)
				{
					var item = results[i] as JObject;

					if (item is null)
						continue;

					var title = (item["title"] as JValue)?.Value as string;
					var url = (item["url"] as JValue)?.Value as string;

					if (title is null
						|| url is null
						|| !Uri.IsWellFormedUriString(url, UriKind.Absolute))
						continue;

					if (urls.Contains(url))
						continue;

					var (name, source) = config.ExtractTitle(title);

					if (name is null)
						continue;

					title = T(Text_APITitle, name, source);

					if (titles.Contains(title))
						continue;

					titles.Add(title);
					names.Add(name);
					urls.Add(url);
				}

				return QueryResult.Success(titles.ToArray(), names.ToArray(), urls.ToArray());
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (HttpRequestException ex) when (!Misc.DEBUG)
			{
				return QueryResult.Fail(T(Text_NetworkError, ex.Message));
			}
			catch (Exception ex) when (!Misc.DEBUG)
			{
				return QueryResult.Fail(T(Text_UnexpectedError, ex.Message));
			}
			finally
			{
				client.Dispose();
			}
		}

		public static async Task<ResolvedEntry[]> Resolve(string apiUrl, ConfigSource config,
			Func<string, bool> typeSearcher, CancellationToken cancellationToken)
		{
			var list = new List<ResolvedEntry>();
			var types = new List<string>();
			var resolveds = new List<string>();
			var function = await Resolver.ResolveFunction(apiUrl, config, cancellationToken);
			list.Add(function);

			// TODO: ResolvedEntry要给出哪些是提供的类，哪些是需要的类

			//types.AddRange(function.Select(s => s.TypeName)
			//	.Distinct().Where(t => !typeSearcher.Invoke(t)));

			//int count;

			//while ((count = types.Count) > 0)
			//{
			//	var type = types[count - 1];
			//	types.RemoveAt(count - 1);

			//	var resolved = await Resolver.ResolveType(type, cancellationToken);

			//	if (resolved.HasValue)
			//	{
			//		resolveds.Add(type);
			//		list.Add(resolved.Value);

			//		var newTypes = function.Body.Select(s => s.TypeName).Concat(types)
			//			.Distinct().Where(t => !typeSearcher.Invoke(t) && !resolveds.Contains(t));

			//		types.Clear();
			//		types.AddRange(newTypes);
			//	}
			//}

			return list.ToArray();
		}
	}

	public readonly struct QueryResult
	{
		private QueryResult(
			string failMessage,
			string[] items,
			string[] names,
			string[] urls
		)
		{
			FailMessage = failMessage;
			Items = items;
			Names = names;
			Urls = urls;
		}

		public static QueryResult Fail(string message)
		{
			if (message is null)
				throw new ArgumentNullException(nameof(message));

			return new QueryResult(message, null, null, null);
		}

		public static QueryResult Success(string[] items, string[] names, string[] urls)
		{
			if (items is null)
				throw new ArgumentNullException(nameof(items));
			if (urls is null)
				throw new ArgumentNullException(nameof(urls));
			if (urls is null)
				throw new ArgumentNullException(nameof(names));
			if (items.Length != names.Length)
				throw new ArgumentException("给定的标题与名称数量不一致");
			if (items.Length != urls.Length)
				throw new ArgumentException("给定的标题与Url数量不一致");

			return new QueryResult(null, items, names, urls);
		}

		public bool IsSuccess => FailMessage is null;

		public string FailMessage { get; }

		public string[] Items { get; }

		public string[] Names { get; }

		public string[] Urls { get; }
	}
}
