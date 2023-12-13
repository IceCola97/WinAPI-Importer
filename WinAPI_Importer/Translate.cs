using System.Threading;

namespace WinAPI_Importer
{
	public static class Translate
	{
		public const string
			Text_SearchAPITitle = "搜索 WinAPI '{0}'",
			Text_UnexpectedError = "意外的错误: {0}",
			Text_NetworkError = "网络连接错误: {0}",
			Text_SearchAPIExpired = "微软提供的搜索API已经更改或失效",
			Text_APITitle = "{0} 函数 ({1})",
			Text_NoSearchResult = "没有搜索到结果",
			Text_MissingSource = "<缺失文件来源>",
			Text_UnresolveableUrl = "指定的WinAPI页面无法解析";

		public static string T(string text)
		{
			if (Thread.CurrentThread.CurrentCulture.Name.StartsWith("zh-"))
				return text;

			switch (text)
			{
				case Text_SearchAPITitle:
					return "Search WinAPI '{0}'";
				case Text_UnexpectedError:
					return "Unexpected error: {0}";
				case Text_NetworkError:
					return "Network error: {0}";
				case Text_SearchAPIExpired:
					return "The search API provided by Microsoft has been expired";
				case Text_APITitle:
					return "{0} function ({1})";
				case Text_NoSearchResult:
					return "No search results found";
				case Text_MissingSource:
					return "<missing file>";
				case Text_UnresolveableUrl:
					return "The specified WinAPI page cannot be resolved";
			}

			return text;
		}

		public static string T(string text, object arg0)
			=> string.Format(T(text), arg0);

		public static string T(string text, object arg0, object arg1)
			=> string.Format(T(text), arg0, arg1);

		public static string T(string text, object arg0, object arg1, object arg2)
			=> string.Format(T(text), arg0, arg1, arg2);

		public static string T(string text, params object[] args)
			=> string.Format(T(text), args);
	}
}
