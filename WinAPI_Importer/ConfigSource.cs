using Microsoft.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static WinAPI_Importer.Translate;

namespace WinAPI_Importer
{
	public sealed class ConfigSource
	{
		private const string ConfigFileName = ".winapiconfig";

		// 为什么const, unsigned/signed, short/long, int这几个可以随意排序???
		// const unsigned short int variable; - OK
		// short const unsigned int variable; - OK ?
		// short unsigned int const variable; - OK ??
		// int const short unsigned variable; - OK ???
		private const string CppTypeName = @"(?:(?:(?:const|CONST)\s+)?(?:unsigned|signed|struct|union)\s+)?(?:\w+|(?:long|short)\s+int|long\s+long)(?:\s*\*)*";

		private const string MicrosoftLearnSearchAPI =
			"https://learn.microsoft.com/api/search?search=WinAPI+function+{0}&locale=en-us&%24top=10&expandScope=true&includeQuestion=false&partnerId=LearnSite";
		private const string DefaultTitlePattern = @"(?<name>\w+)\s+function\s*\((?<source>\w+(?:.(?:h|lib|dll))?)\)";
		private const string DefaultTypeSelector = "#main > div.content > h1";
		private const string DefaultPrototypeSelector = "#main > div.content > pre > code";
		private const string DefaultModuleSelector = "meta[name=\"req.dll\"]";
		private const string DefaultNamePattern = @"(?<name>\w+)\s+(?:[\w\s]+\w)\s*(?:\(\w+(?:.(?:h|lib|dll))?\))?";
		private const string DefaultTypePattern = @"\w+\s+(?<type>[\w\s]+\w)\s*(?:\(\w+(?:.(?:h|lib|dll))?\))?";
		private const string DefaultFunctionHeadPattern = @"(?<return>{0})\s+(?<name>\w+)(?=\()";
		private const string DefaultFunctionParamsPattern = @"\((?<param>(?:(?<=\()|,)\s*(?<ref>(?:\[[\w\s,]+\]|_[_IinOoutp]+_)\s+)?(?:\w+\s+)?(?<type>{0}\s+)(?<name>\w+)\s*)*\)";
		private const string DefaultInPattern = @"\b[Ii][Nn]\b";
		private const string DefaultOutPattern = @"\b[Oo][Uu][Tt]\b";
		private const string DefaultRefPattern = null;
		private const string DefaultOptionalPattern = @"\b[Oo][Pp][Tt][Ii][Oo][Nn][Aa][Ll]\b";

		private static readonly IDictionary<string, PrototypeKind> DefaultTypeMap;
		private static readonly IDictionary<string, string> DefaultSharpTypeMap;

		private const string Key_SearchUrl = "search_url";
		private const string Key_TitlePattern = "title_pattern";
		private const string Key_TypeSelector = "type_selector";
		private const string Key_PrototypeSelector = "prototype_selector";
		private const string Key_ModuleSelector = "module_selector";
		private const string Key_NamePattern = "name_pattern";
		private const string Key_TypePattern = "type_pattern";
		private const string Key_TypeMap = "type_map";
		private const string Key_FuncHeadPattern = "func_head_pattern";
		private const string Key_FuncParamsPattern = "func_params_pattern";
		private const string Key_RefPatterns = "ref_patterns";
		private const string Key_SharpTypeMap = "sharp_type_map";

		private static readonly ConfigSource Default;

		private readonly string m_SearchTemplate;
		private readonly Regex m_TitlePattern;
		private readonly string m_PageTypeSelector;
		private readonly string m_PagePrototypeSelector;
		private readonly string m_PageModuleSelector;
		private readonly Regex m_NamePattern;
		private readonly Regex m_TypePattern;
		private readonly Regex m_FuncHeadPattern;
		private readonly Regex m_FuncParamsPattern;
		private readonly Regex m_InPattern;
		private readonly Regex m_OutPattern;
		private readonly Regex m_RefPattern;
		private readonly Regex m_OptionalPattern;
		private readonly IDictionary<string, PrototypeKind> m_TypeMap;
		private readonly IDictionary<string, string> m_SharpTypeMap;

		static ConfigSource()
		{
			DefaultTypeMap = ImmutableDictionary.CreateRange(new Dictionary<string, PrototypeKind>()
			{
				{ "function", PrototypeKind.Function },
				{ "structure", PrototypeKind.Struct },
				{ "union", PrototypeKind.Union },
				{ "callback function", PrototypeKind.Delegate },
				{ "enumeration", PrototypeKind.Enum },
				{ "macro", PrototypeKind.Macro },
				{ "interface", PrototypeKind.Interface },
			});

			DefaultSharpTypeMap = ImmutableDictionary<string, string>.Empty;
			Default = new ConfigSource();
		}

		private ConfigSource()
		{
			m_SearchTemplate = MicrosoftLearnSearchAPI;
			m_TitlePattern = new Regex(DefaultTitlePattern);
			m_PageTypeSelector = DefaultTypeSelector;
			m_PagePrototypeSelector = DefaultPrototypeSelector;
			m_PageModuleSelector = DefaultModuleSelector;
			m_NamePattern = new Regex(DefaultNamePattern);
			m_TypePattern = new Regex(DefaultTypePattern);
			m_TypeMap = DefaultTypeMap;
			m_FuncHeadPattern = new Regex(string.Format(DefaultFunctionHeadPattern, CppTypeName));
			m_FuncParamsPattern = new Regex(string.Format(DefaultFunctionParamsPattern, CppTypeName));
			m_InPattern = new Regex(DefaultInPattern);
			m_OutPattern = new Regex(DefaultOutPattern);
			m_RefPattern = null;
			m_OptionalPattern = new Regex(DefaultOptionalPattern);
			m_SharpTypeMap = DefaultSharpTypeMap;
		}

		private ConfigSource(JToken token)
		{
			m_SearchTemplate = token.IndexValue<string>(Key_SearchUrl)
				?? MicrosoftLearnSearchAPI;
			m_TitlePattern = MakeRegex(token.IndexValue<string>(Key_TitlePattern),
				DefaultTitlePattern);
			m_PageTypeSelector = token.IndexValue<string>(Key_TypeSelector)
				?? DefaultTypeSelector;
			m_PagePrototypeSelector = token.IndexValue<string>(Key_PrototypeSelector)
				?? DefaultPrototypeSelector;
			m_PageModuleSelector = token.IndexValue<string>(Key_ModuleSelector)
				?? DefaultModuleSelector;
			m_NamePattern = MakeRegex(token.IndexValue<string>(Key_NamePattern),
				DefaultNamePattern);
			m_TypePattern = MakeRegex(token.IndexValue<string>(Key_TypePattern),
				DefaultTypePattern);
			m_TypeMap = token.IndexObject<string, PrototypeKind>(Key_TypeMap,
				s => Enum.TryParse<PrototypeKind>(s, out var pk) ? pk : PrototypeKind.None)
				?? DefaultTypeMap;
			m_FuncHeadPattern = MakeRegex(token.IndexValue<string>(Key_FuncHeadPattern),
				DefaultFunctionHeadPattern, CppTypeName);
			m_FuncParamsPattern = MakeRegex(token.IndexValue<string>(Key_FuncParamsPattern),
				DefaultFunctionParamsPattern, CppTypeName);

			var refPatterns = token.IndexArray<string>(Key_RefPatterns, true);
			m_InPattern = MakeRegex(ArrayAt(refPatterns, 0), DefaultInPattern);
			m_OutPattern = MakeRegex(ArrayAt(refPatterns, 1), DefaultOutPattern);
			m_RefPattern = MakeRegex(ArrayAt(refPatterns, 2), DefaultRefPattern);
			m_OptionalPattern = MakeRegex(ArrayAt(refPatterns, 3), DefaultOptionalPattern);

			m_SharpTypeMap = token.IndexObject<string>(Key_SharpTypeMap)
				?? DefaultSharpTypeMap;
		}

		public static async Task<ConfigSource> GetConfigSource(Solution solution)
		{
			try
			{
				var config = await solution.ReadSolutionConfig(ConfigFileName);
				return new ConfigSource(config);
			}
			catch
			{
				try
				{
					var path = ConfigReader.BuildSolutionPath(solution, ConfigFileName);
					var root = new JObject
					{
						{ Key_SearchUrl, MicrosoftLearnSearchAPI },
						{ Key_TitlePattern, DefaultTitlePattern },
						{ Key_TypeSelector, DefaultTypeSelector },
						{ Key_PrototypeSelector, DefaultPrototypeSelector },
						{ Key_ModuleSelector, DefaultModuleSelector },
						{ Key_NamePattern, DefaultNamePattern },
						{ Key_TypePattern, DefaultTypePattern },
						{ Key_TypeMap, ToJObject(DefaultTypeMap) },
						{ Key_FuncHeadPattern, DefaultFunctionHeadPattern },
						{ Key_FuncParamsPattern, DefaultFunctionParamsPattern },
						{ Key_SharpTypeMap, ToJObject(DefaultSharpTypeMap) },
					};

					File.WriteAllText(path, JsonConvert.SerializeObject(root,
						Formatting.Indented), Encoding.UTF8);
				}
				catch { }

				return Default;
			}
		}

		public string TypeSelector => m_PageTypeSelector;

		public string PrototypeSelector => m_PagePrototypeSelector;

		public string ModuleSelector => m_PageModuleSelector;

		public string BuildUrl(string keyword) => string.Format(m_SearchTemplate, keyword);

		public (string name, string source) ExtractTitle(string title)
		{
			var match = m_TitlePattern.Match(title);

			if (match.Success)
			{
				var name = match.Groups["name"].Value;
				var source = match.Groups["source"].Value;

				if (string.IsNullOrWhiteSpace(source))
					source = T(Text_MissingSource);

				if (!string.IsNullOrWhiteSpace(name))
					return (name, source);
			}

			return (null, null);
		}

		public string ExtractName(string nameText)
		{
			var match = m_NamePattern.Match(nameText);

			if (match.Success)
			{
				var name = match.Groups["name"].Value;

				if (!string.IsNullOrWhiteSpace(name))
					return name;
			}

			return null;
		}

		public string ExtractType(string typeText)
		{
			var match = m_TypePattern.Match(typeText);

			if (match.Success)
			{
				var type = match.Groups["type"].Value;

				if (!string.IsNullOrWhiteSpace(type))
					return type;
			}

			return null;
		}

		public (string name, string @return) ExtractFuncHead(string funcPrototype)
		{
			var match = m_FuncHeadPattern.Match(funcPrototype);

			if (match.Success)
			{
				var name = match.Groups["name"].Value;
				var @return = match.Groups["return"].Value;

				if (!string.IsNullOrWhiteSpace(name)
					&& !string.IsNullOrWhiteSpace(@return))
					return (name.Trim(), @return.Trim());
			}

			return (null, null);
		}

		public (string @ref, string type, string name)[] ExtractFuncParams(string funcPrototype)
		{
			var match = m_FuncParamsPattern.Match(funcPrototype);

			if (match.Success)
			{
				var list = new List<(string @ref, string type, string name)>();

				var paramGroup = match.Groups["param"];
				var refGroup = match.Groups["ref"];
				var typeGroup = match.Groups["type"];
				var nameGroup = match.Groups["name"];

				int refIndex = 0;

				for (int i = 0; i < paramGroup.Captures.Count; i++)
				{
					var paramCap = paramGroup.Captures[i];
					var typeCap = typeGroup.Captures[i];
					var nameCap = nameGroup.Captures[i];

					var refCap = refIndex < refGroup.Captures.Count
						? refGroup.Captures[refIndex] : null;

					if (refCap != null
						&& refCap.Index >= paramCap.Index
						&& refCap.Index + refCap.Length
							< paramCap.Index + paramCap.Length)
					{
						list.Add((refCap.Value, typeCap.Value, nameCap.Value));
						refIndex++;
					}
					else
						list.Add((null, typeCap.Value, nameCap.Value));
				}

				return list.ToArray();
			}

			return null;
		}

		public (RefParamType refType, bool optional) ParseRefType(string refTypeText)
		{
			var refType = RefParamType.None;

			if (m_RefPattern != null
				&& m_RefPattern.IsMatch(refTypeText))
				refType = RefParamType.Ref;
			else
			{
				if (m_InPattern.IsMatch(refTypeText))
				{
					if (m_OutPattern.IsMatch(refTypeText))
						refType = RefParamType.Ref;
					else
						refType = RefParamType.In;
				}
				else if (m_OutPattern.IsMatch(refTypeText))
					refType = RefParamType.Out;
			}

			return (refType, m_OptionalPattern.IsMatch(refTypeText));
		}

		public PrototypeKind ParseKind(string kindText)
			=> m_TypeMap.TryGetValue(kindText, out var pk) ? pk : PrototypeKind.None;

		public (ITypeSymbol type, TypeModifier modifier) ConvertType
			(string cppType, Compilation compilation)
		{
			if (m_SharpTypeMap.TryGetValue(cppType, out var sharpType))
				return DefinedType.SharpType(compilation, sharpType);

			var (type, modifier) = DefinedType.ParseType(compilation, cppType);

			if (type is null)
				return (compilation.CreateErrorTypeSymbol(null, cppType, 0), default);

			return (type, modifier);
		}

		private static Regex MakeRegex(string patternUnverified, string patternTrusted)
		{
			if (patternUnverified is null)
			{
				if (patternTrusted is null)
					return null;

				return new Regex(patternTrusted);
			}

			try
			{
				return new Regex(patternUnverified);
			}
			catch
			{
				return new Regex(patternTrusted);
			}
		}

		private static Regex MakeRegex(string patternUnverified, string patternTrusted, string arg0)
		{
			return MakeRegex(string.Format(patternUnverified, arg0),
				string.Format(patternTrusted, arg0));
		}

		private static JObject ToJObject<T>(IDictionary<string, T> map)
		{
			var result = new JObject();

			foreach (var item in map)
			{
				result.Add(item.Key, item.Value?.ToString());
			}

			return result;
		}

		private static T ArrayAt<T>(T[] array, int index)
		{
			if (array is null
				|| array.Length <= index)
				return default;

			return array[index];
		}
	}
}
