using AngleSharp.Html.Parser;
using Microsoft.CodeAnalysis;
using System;
using System.Collections;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.XPath;
using static WinAPI_Importer.Translate;

namespace WinAPI_Importer
{
	public static class Resolver
	{
		/// <summary>
		/// 解析给定URL指向的函数页面
		/// </summary>
		/// <param name="url"></param>
		/// <returns></returns>
		public static async Task<ResolvedEntry> ResolveFunction(string url,
			ConfigSource config, CancellationToken cancellationToken)
		{
			var (module, prototype) = await ExtractFunction(url, config, cancellationToken);

			if (module is null || prototype is null)
				throw new InvalidDataException(T(Text_UnresolveableUrl));

			var entry = ParseFunction(prototype, config)
				?? throw new InvalidDataException(T(Text_UnresolveableUrl));

			entry.Module = module;
			entry.ReferUrl = url;
			return entry;
		}

		/// <summary>
		/// 搜索并解析第一个搜索结果指向的类型定义页面<br/>
		/// 如果搜索结果为空则返回<see langword="null"/>
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static async Task<ResolvedEntry> ResolveType(string type,
			CancellationToken cancellationToken)
		{
			var urls = await SearchType(type, cancellationToken);

			if (urls == null || urls.Length == 0)
				return null;

			var (kind, prototype) = await ExtractType(urls[0], cancellationToken);

			if (kind == PrototypeKind.None || prototype is null)
				return null;

			var entry = default(ResolvedEntry);

			switch (kind)
			{
				case PrototypeKind.Struct:
				case PrototypeKind.Union:
					entry = ParseStruct(prototype);
					break;
				case PrototypeKind.Delegate:
					entry = ParseDelegate(prototype);
					break;
				case PrototypeKind.Enum:
					entry = ParseEnum(prototype);
					break;
				case PrototypeKind.Macro: // unsupported
				case PrototypeKind.Interface: // unsupported
				default:
					return null;
			}

			return entry;
		}

		/// <summary>
		/// 从给定URL指向的函数页面提取函数模块与函数原型
		/// </summary>
		/// <param name="url"></param>
		/// <returns></returns>
		public static async Task<(string module, string prototype)> ExtractFunction(string url,
			ConfigSource config, CancellationToken cancellationToken)
		{
			var data = await Misc.HttpGet(url, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();

			var parser = new HtmlParser(default);
			var document = await parser.ParseDocumentAsync(data, cancellationToken);

			var typeElement = document.SelectElementAuto(config.TypeSelector);
			var prototypeElement = document.SelectElementAuto(config.PrototypeSelector);
			var moduleElement = document.SelectElementAuto(config.ModuleSelector);

			if (typeElement is null
				|| prototypeElement is null
				|| moduleElement is null)
				return (null, null);

			var type = config.ExtractType(typeElement.ElementContent());
			var prototype = prototypeElement.ElementContent();
			var module = moduleElement.ElementContent();

			if (string.IsNullOrWhiteSpace(type)
				|| string.IsNullOrWhiteSpace(prototype)
				|| string.IsNullOrWhiteSpace(module))
				return (null, null);

			if (config.ParseKind(type) != PrototypeKind.Function)
				return (null, null);

			return (module, prototype);
		}

		/// <summary>
		/// 将给定函数原型解析为参数列表<br/>
		/// 其中第一个参数永远是返回值
		/// </summary>
		/// <param name="prototype"></param>
		/// <returns></returns>
		public static ResolvedEntry ParseFunction(string prototype, ConfigSource config)
		{
			var (name, @return) = config.ExtractFuncHead(prototype);
			var array = config.ExtractFuncParams(prototype);

			if (array is null)
				return null;

			var entry = new ResolvedEntry(name, PrototypeKind.Function);
			entry.AddSlot(new ResolvedSlot { Name = null, TypeName = @return });

			foreach (var param in array)
			{
				var refTypeText = param.@ref?.TrimStart(',')?.Trim();
				var (refType, optional) = refTypeText is null
					? (RefParamType.None, false) : config.ParseRefType(refTypeText);

				entry.AddSlot(new ResolvedSlot
				{
					Name = param.name.Trim(),
					TypeName = param.type.TrimStart(',').Trim(),
					TypeModifier = new TypeModifier { RefParamType = refType },
					IsOptional = optional,
				});
			}

			return entry;
		}

		/// <summary>
		/// 搜索给定的类型并返回可能的URL列表<br/>
		/// 此处的搜索结果会确保指向页面的类型名称与给定的一致
		/// </summary>
		/// <param name="typeName"></param>
		/// <returns></returns>
		public static async Task<string[]> SearchType(string typeName,
			CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// 从给定URL指向的类型定义页面提取类型定义原型<br/>
		/// 并在元组第一项给出类型定义的种类<br/>
		/// 在元组第二项给出类型定义的原型
		/// </summary>
		/// <param name="url"></param>
		/// <returns></returns>
		public static async Task<(PrototypeKind, string)> ExtractType(string url,
			CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// 解析给定结构体原型
		/// </summary>
		/// <param name="prototype"></param>
		/// <returns></returns>
		public static ResolvedEntry ParseStruct(string prototype)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// 当一个结构体字段是定长数组时，尝试将字段文本解析为字段名与数组大小常量
		/// </summary>
		/// <param name="field"></param>
		/// <returns></returns>
		public static bool TryParseFixedArrayField(string field,
			out string fieldName, out string length)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// 将给定回调函数原型解析为参数列表<br/>
		/// 其中第一个参数永远是返回值
		/// </summary>
		/// <param name="prototype"></param>
		/// <returns></returns>
		public static ResolvedEntry ParseDelegate(string prototype)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// 解析给定枚举原型
		/// </summary>
		/// <param name="prototype"></param>
		/// <returns></returns>
		public static ResolvedEntry ParseEnum(string prototype)
		{
			throw new NotImplementedException();
		}
	}
}
