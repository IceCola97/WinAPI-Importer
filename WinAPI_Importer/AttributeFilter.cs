using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WinAPI_Importer
{
	public static class AttributeFilter
	{
		private const string WindowsAPIAttribute = nameof(WindowsAPIAttribute);
		private const string DllImportAttribute = nameof(DllImportAttribute);
		private const string LibraryImportAttribute = nameof(LibraryImportAttribute);

		public static bool IsWindowsAPIClass(this ITypeSymbol symbol)
		{
			return symbol.GetAttributes()
				.Any(a => a.AttributeClass.Name == WindowsAPIAttribute);
		}
	}
}
