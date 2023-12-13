using Microsoft.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WinAPI_Importer
{
	public static class ConfigReader
	{
		/// <summary>
		/// 将指定路径的文件按照给定编码(默认为UTF-8)读取为JSON
		/// </summary>
		/// <param name="path"></param>
		/// <param name="encoding"></param>
		/// <returns></returns>
		public static async Task<JToken> ReadConfig(string path, Encoding encoding = null)
		{
			using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				var reader = new StreamReader(stream, encoding ?? Encoding.UTF8, true);
				var content = await reader.ReadToEndAsync().ConfigureAwait(false);
				var maybeJToken = JsonConvert.DeserializeObject(content);

				if (maybeJToken is JToken token)
					return token;

				throw new JsonSerializationException("无法将配置读取到JToken");
			}
		}

		/// <summary>
		/// 构造指定解决方案下的配置文件路径
		/// </summary>
		/// <param name="solution"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public static string BuildSolutionPath(Solution solution, string name)
		{
			var path = solution.FilePath
				?? throw new ArgumentException("无法从给定的解决方案获得路径");
			path = Path.GetDirectoryName(path);
			return Path.Combine(path, name);
		}

		/// <summary>
		/// 将给定解决方案的配置文件按照给定编码(默认为UTF-8)读取为JSON
		/// </summary>
		/// <param name="solution"></param>
		/// <param name="name"></param>
		/// <param name="encoding"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public static async Task<JToken> ReadSolutionConfig(this Solution solution,
			string name, Encoding encoding = null)
		{
			var path = BuildSolutionPath(solution, name);
			return await ReadConfig(path, encoding);
		}

		/// <summary>
		/// 构造指定工程下的配置文件路径
		/// </summary>
		/// <param name="project"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public static string BuildProjectPath(Project project, string name)
		{
			var path = project.FilePath
				?? throw new ArgumentException("无法从给定的解决方案获得路径");
			path = Path.GetDirectoryName(path);
			return Path.Combine(path, name);
		}

		/// <summary>
		/// 将给定工程的配置文件按照给定编码(默认为UTF-8)读取为JSON
		/// </summary>
		/// <param name="project"></param>
		/// <param name="name"></param>
		/// <param name="encoding"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public static async Task<JToken> ReadProjectConfig(this Project project,
			string name, Encoding encoding = null)
		{
			var path = BuildProjectPath(project, name);
			return await ReadConfig(Path.Combine(path, name), encoding);
		}

		/// <summary>
		/// 获取给定路径下的值
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="token"></param>
		/// <param name="path"></param>
		/// <param name="throwOnError"></param>
		/// <returns></returns>
		/// <exception cref="JsonException"></exception>
		public static T IndexValue<T>(this JToken token, string path,
			bool nullable = false, bool throwOnError = false)
		{
			try
			{
				var target = token.SelectToken(path);

				if (target is JValue value)
				{
					if (value.Value is T
						|| (nullable && value.Value is null))
						return (T)value.Value;
				}
			}
			catch when (!throwOnError)
			{
				return default;
			}

			if (throwOnError)
				throw new JsonException("指定的路径下的值无法满足类型要求: " + typeof(T).FullName);

			return default;
		}

		/// <summary>
		/// 获取给定路径下的数组
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="token"></param>
		/// <param name="path"></param>
		/// <param name="throwOnError"></param>
		/// <returns></returns>
		/// <exception cref="JsonException"></exception>
		public static T[] IndexArray<T>(this JToken token, string path,
			bool nullable = false, bool throwOnError = false)
		{
			try
			{
				var target = token.SelectToken(path);

				if (target is JArray array)
				{
					var list = new List<T>();

					foreach (var item in array)
					{
						if (item is JValue value
							&& (value.Value is T
								|| (nullable && value.Value is null)))
							list.Add((T)value.Value);

						if (throwOnError)
							throw new JsonException("指定的路径下的元素值无法满足类型要求: "
								+ typeof(T).FullName);
					}

					return list.ToArray();
				}
			}
			catch when (!throwOnError)
			{
				return null;
			}

			if (throwOnError)
				throw new JsonException("指定的路径下的值不是一个数组");

			return null;
		}

		/// <summary>
		/// 获取给定路径下的对象
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="token"></param>
		/// <param name="path"></param>
		/// <param name="throwOnError"></param>
		/// <returns></returns>
		/// <exception cref="JsonException"></exception>
		public static JsonTable<T> IndexObject<T>(this JToken token, string path,
			bool nullable = false, bool throwOnError = false)
		{
			try
			{
				var target = token.SelectToken(path);

				if (target is JObject map)
				{
					var table = new JsonTable<T>();

					foreach (var item in map)
					{
						if (item.Value is JValue value
							&& (value.Value is T
								|| (nullable && value.Value is null)))
							table.Add(item.Key, (T)value.Value);

						if (throwOnError)
							throw new JsonException("指定的路径下的元素值无法满足类型要求: "
								+ typeof(T).FullName);
					}

					return table;
				}
			}
			catch when (!throwOnError)
			{
				return null;
			}

			if (throwOnError)
				throw new JsonException("指定的路径下的值不是一个对象");

			return null;
		}

		/// <summary>
		/// 获取给定路径下的对象，并自动对所有值应用转换器
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="token"></param>
		/// <param name="path"></param>
		/// <param name="throwOnError"></param>
		/// <returns></returns>
		/// <exception cref="JsonException"></exception>
		public static JsonTable<TTo> IndexObject<TFrom, TTo>(this JToken token, string path,
			Func<TFrom, TTo> converter, bool nullable = false, bool throwOnError = false)
		{
			try
			{
				var target = token.SelectToken(path);

				if (target is JObject map)
				{
					var table = new JsonTable<TTo>();

					foreach (var item in map)
					{
						if (item.Value is JValue value
							&& (value.Value is TFrom
								|| (nullable && value.Value is null)))
							table.Add(item.Key, converter.Invoke((TFrom)value.Value));

						if (throwOnError)
							throw new JsonException("指定的路径下的元素值无法满足类型要求: "
								+ typeof(TFrom).FullName);
					}

					return table;
				}
			}
			catch when (!throwOnError)
			{
				return null;
			}

			if (throwOnError)
				throw new JsonException("指定的路径下的值不是一个对象");

			return null;
		}
	}

	public sealed class JsonTable<T> : Dictionary<string, T>
	{
	}
}
