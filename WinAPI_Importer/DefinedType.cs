using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace WinAPI_Importer
{
	public static class DefinedType
	{
		private static readonly Regex m_SharpTypePattern =
			new Regex(@"(?<ref>in|out|ref)\s+(?<name>[\w\.]+)(?:<(?<tp>[\w\.])>)?(?<ptr>\s*\*)*");

		public static (INamedTypeSymbol type, TypeModifier modifier) ParseType
			(Compilation context, string type)
		{
			SpecialType specialType;
			INamedTypeSymbol typeSymbol;
			TypeModifier typeModifier;

			(specialType, typeModifier) = ParseSpecialType(type);

			if (specialType != SpecialType.None)
				return (context.GetSpecialType(specialType), typeModifier);

			(specialType, typeModifier) = ParsePlatformRelatedType(type);

			if (specialType != SpecialType.None)
				return (context.GetSpecialType(specialType), typeModifier);

			(typeSymbol, typeModifier) = ParsePointerType(context, type);

			if (typeSymbol != null)
				return (typeSymbol, typeModifier);

			(typeSymbol, typeModifier) = ParseRefType(context, type);

			if (typeSymbol != null)
				return (typeSymbol, typeModifier);

			// HANDLE, HMODULE, HINSTANCE, HWND, HDC, ...
			//if (type.StartsWith('H') && type.IsUpperFully())
			//	return (context.GetSpecialType(SpecialType.System_IntPtr),
			//		new TypeModifier(StringType.None, RefParamType.None, 0));

			return (default, default);
		}

		private static SpecialType PlatformRelated(
			SpecialType typeOn32bits,
			SpecialType typeOn64bits,
			SpecialType typeOn128bits = SpecialType.None
		)
		{
			switch (IntPtr.Size)
			{
				case 4:
					return typeOn32bits;
				case 8:
					return typeOn64bits;
				case 16:
					return typeOn128bits;
			}

			return SpecialType.None;
		}

		private static (SpecialType type, TypeModifier modifier) ParsePlatformRelatedType(string type)
		{
			var specialType = SpecialType.None;
			var refParamType = RefParamType.None;
			var stringType = StringType.None;

			switch (type)
			{
				case "HALF_PTR":
					specialType = PlatformRelated(
						SpecialType.System_Int16,
						SpecialType.System_Int32,
						SpecialType.System_Int64
					);
					break;
				case "UHALF_PTR":
					specialType = PlatformRelated(
						SpecialType.System_UInt16,
						SpecialType.System_UInt32,
						SpecialType.System_UInt64
					);
					break;
				case "PHALF_PTR":
					specialType = PlatformRelated(
						SpecialType.System_Int16,
						SpecialType.System_Int32,
						SpecialType.System_Int64
					);
					refParamType = RefParamType.Ref;
					break;
				case "PUHALF_PTR":
					specialType = PlatformRelated(
						SpecialType.System_UInt16,
						SpecialType.System_UInt32,
						SpecialType.System_UInt64
					);
					refParamType = RefParamType.Ref;
					break;
			}

			return (specialType, new TypeModifier(stringType, refParamType, 0));
		}

		private static (INamedTypeSymbol type, TypeModifier modifier) ParseRefType
			(Compilation context, string type)
		{
			var specialType = SpecialType.None;
			var refParamType = RefParamType.None;
			var stringType = StringType.None;

			switch (type)
			{
				case "PBOOL":
				case "LPINT":
				case "PLONG":
				case "LPBOOL":
				case "LPLONG":
				case "PINT":
				case "PINT32":
				case "PLONG32":
					specialType = SpecialType.System_Int32;
					refParamType = RefParamType.Ref;
					break;
				case "PDWORD":
				case "PULONG":
				case "LPDWORD":
				case "LPCOLORREF":
				case "PDWORD32":
				case "PLCID":
				case "PUINT":
				case "PUINT32":
				case "PULONG32":
					specialType = SpecialType.System_UInt32;
					refParamType = RefParamType.Ref;
					break;
				case "PINT16":
				case "PSHORT":
					specialType = SpecialType.System_Int16;
					refParamType = RefParamType.Ref;
					break;
				case "PWORD":
				case "LPWORD":
				case "PUINT16":
				case "PUSHORT":
					specialType = SpecialType.System_UInt16;
					refParamType = RefParamType.Ref;
					break;
				case "PBOOLEAN":
					specialType = SpecialType.System_Boolean;
					refParamType = RefParamType.Ref;
					break;
				case "PBYTE":
				case "PCHAR":
				case "PUINT8":
					specialType = SpecialType.System_Byte;
					refParamType = RefParamType.Ref;
					break;
				case "PINT8":
					specialType = SpecialType.System_SByte;
					refParamType = RefParamType.Ref;
					break;
				case "PWCHAR":
					specialType = SpecialType.System_Char;
					refParamType = RefParamType.Ref;
					break;
				case "PINT64":
				case "PLONG64":
				case "PLONGLONG":
					specialType = SpecialType.System_Int64;
					refParamType = RefParamType.Ref;
					break;
				case "PDWORD64":
				case "PDWORDLONG":
				case "PUINT64":
				case "PULONGLONG":
				case "PULONG64":
					specialType = SpecialType.System_UInt64;
					refParamType = RefParamType.Ref;
					break;
				case "PINT_PTR":
				case "PLONG_PTR":
				case "PSSIZE_T":
					specialType = SpecialType.System_IntPtr;
					refParamType = RefParamType.Ref;
					break;
				case "PDWORD_PTR":
				case "PSIZE_T":
				case "PUINT_PTR":
				case "PULONG_PTR":
					specialType = SpecialType.System_UIntPtr;
					refParamType = RefParamType.Ref;
					break;
				case "PFLOAT":
					specialType = SpecialType.System_Single;
					refParamType = RefParamType.Ref;
					break;
				case "PCSTR":
				case "LPCSTR":
					specialType = SpecialType.System_String;
					refParamType = RefParamType.In;
					stringType = StringType.Ansi;
					break;
				case "PSTR":
				case "LPSTR":
					specialType = SpecialType.System_String;
					refParamType = RefParamType.Ref;
					stringType = StringType.Ansi;
					break;
				case "PCWSTR":
				case "PCTSTR":
				case "LPCWSTR":
				case "LPCTSTR": // C# char == C++ WCHAR
					specialType = SpecialType.System_String;
					refParamType = RefParamType.In;
					stringType = StringType.Unicode;
					break;
				case "PWSTR":
				case "PTSTR":
				case "LPWSTR":
				case "LPTSTR":
					specialType = SpecialType.System_String;
					refParamType = RefParamType.Ref;
					stringType = StringType.Unicode;
					break;
				default:
					if ((type.StartsWith("P")
						&& IsHandleType(type.Substring(1)))
						|| (type.StartsWith("LP")
						&& IsHandleType(type.Substring(2))))
					{
						specialType = SpecialType.System_IntPtr;
						refParamType = RefParamType.Ref;
					}
					else
						return (default, default);

					break;
			}

			return (context.GetSpecialType(specialType),
				new TypeModifier(stringType, refParamType, 0));
		}

		private static (INamedTypeSymbol type, TypeModifier modifier) ParsePointerType
			(Compilation context, string type)
		{
			var specialType = SpecialType.None;
			int pointerLevel = 0;
			var stringType = StringType.None;

			switch (type)
			{
				case "PUCHAR":
				case "STRING":
				case "UNC":
				case "LPBYTE":
					specialType = SpecialType.System_Byte;
					pointerLevel = 1;
					break;
				case "LMCSTR":
				case "BSTR":
				case "LMSTR":
				case "PTBYTE":
				case "PTCHAR":
					specialType = SpecialType.System_Char;
					pointerLevel = 1;
					break;
				default:
					return (default, default);
			}

			var baseType = context.GetSpecialType(specialType);

			return (baseType, new TypeModifier
			{
				StringType = stringType,
				PointerLevel = pointerLevel,
			});
		}

		private static bool IsHandleType(string type)
		{
			switch (type)
			{
				case "HANDLE":
				case "HACCEL":
				case "HBITMAP":
				case "HBRUSH":
				case "HCOLORSPACE":
				case "HCONV":
				case "HCONVLIST":
				case "HCURSOR":
				case "HDC":
				case "HDDEDATA":
				case "HDESK":
				case "HDROP":
				case "HDWP":
				case "HENHMETAFILE":
				case "HFONT":
				case "HGDIOBJ":
				case "HGLOBAL":
				case "HHOOK":
				case "HICON":
				case "HINSTANCE":
				case "HKEY":
				case "HKL":
				case "HLOCAL":
				case "HMENU":
				case "HMETAFILE":
				case "HMODULE":
				case "HMONITOR":
				case "HPALETTE":
				case "HPEN":
				case "HRGN":
				case "HRSRC":
				case "HSZ":
				case "HWINSTA":
				case "HWND":
				case "SC_HANDLE":
				case "SERVICE_STATUS_HANDLE":
				case "ADCONNECTION_HANDLE":
				case "LDAP_UDP_HANDLE":
				case "PCONTEXT_HANDLE":
				case "RPC_BINDING_HANDLE":
					return true;
			}

			return false;
		}

		private static (SpecialType type, TypeModifier modifier) ParseSpecialType(string type)
		{
			var specialType = SpecialType.None;
			var stringType = StringType.None;

			switch (type)
			{
				case "bool":
				case "BOOLEAN":
					specialType = SpecialType.System_Boolean;
					break;
				case "char":
				case "__int8":
				case "signed char":
				case "signed __int8":
				case "INT8":
					specialType = SpecialType.System_SByte;
					break;
				case "unsigned char":
				case "unsigned __int8":
				case "UINT8":
				case "BYTE":
				case "CHAR":
				case "UCHAR":
				case "CCHAR":
					specialType = SpecialType.System_Byte;
					break;
				case "short":
				case "short int":
				case "__int16":
				case "signed short":
				case "signed short int":
				case "signed __int16":
				case "INT16":
				case "SHORT":
					specialType = SpecialType.System_Int16;
					break;
				case "unsigned short":
				case "unsigned short int":
				case "unsigned __int16":
				case "USHORT":
				case "UINT16":
				case "WORD":
				case "ATOM":
				case "LANGID":
					specialType = SpecialType.System_UInt16;
					break;
				case "int":
				case "long":
				case "long int":
				case "__int32":
				case "signed int":
				case "signed long":
				case "signed long int":
				case "signed __int32":
				case "BOOL":
				case "INT":
				case "INT32":
				case "LONG":
				case "LONG32":
				case "HRESULT":
				case "HFILE":
				case "NTSTATUS":
					specialType = SpecialType.System_Int32;
					break;
				case "unsigned int":
				case "unsigned long":
				case "unsigned long int":
				case "unsigned __int32":
				case "error_status_t":
				case "DWORD":
				case "DWORD32":
				case "UINT":
				case "UINT32":
				case "ULONG":
				case "ULONG32":
				case "HCALL":
				case "COLORREF":
				case "NET_API_STATUS":
				case "LCID":
				case "LCTYPE":
				case "LGRPID":
					specialType = SpecialType.System_UInt32;
					break;
				case "long long":
				case "long long int":
				case "__int64":
				case "hyper":
				case "signed long long":
				case "signed long long int":
				case "signed __int64":
				case "signed hyper":
				case "INT64":
				case "LONG64":
				case "LONGLONG":
				case "USN":
					specialType = SpecialType.System_Int64;
					break;
				case "unsigned long long":
				case "unsigned long long int":
				case "unsigned __int64":
				case "unsigned hyper":
				case "UINT64":
				case "ULONG64":
				case "DWORD64":
				case "DWORDLONG":
				case "ULONGLONG":
				case "QWORD":
					specialType = SpecialType.System_UInt64;
					break;
				case "wchar_t":
				case "WCHAR":
				case "UNICODE":
				case "TBYTE":
				case "TCHAR":
					specialType = SpecialType.System_Char;
					break;
				case "float":
				case "FLOAT":
					specialType = SpecialType.System_Single;
					break;
				case "double":
				case "DOUBLE":
					specialType = SpecialType.System_Double;
					break;
				case "__int3264":
				case "LONG_PTR":
				case "PVOID":
				case "LPVOID":
				case "LPCVOID":
				case "INT_PTR":
				case "WPARAM":
				case "LPARAM":
				case "LRESULT":
				case "SSIZE_T":
				case "SC_LOCK":
					specialType = SpecialType.System_IntPtr;
					break;
				case "unsigned __int3264":
				case "size_t":
				case "ULONG_PTR":
				case "DWORD_PTR":
				case "SIZE_T":
				case "UINT_PTR":
					specialType = SpecialType.System_UIntPtr;
					break;
				case "void":
				case "VOID":
					specialType = SpecialType.System_Void;
					break;
				default:
					if (IsHandleType(type))
						specialType = SpecialType.System_IntPtr;

					break;
			}

			return (specialType, new TypeModifier { StringType = stringType });
		}

		public static (ITypeSymbol type, TypeModifier modifier) SharpType
			(Compilation context, string type)
		{
			var match = m_SharpTypePattern.Match(type);

			if (!match.Success)
				return (context.CreateErrorTypeSymbol(null, type, 0), default);

			var specialType = SpecialType.None;
			var stringType = StringType.None;
			var refParamType = RefParamType.None;
			var pointerLevel = match.Groups["ptr"].Captures.Count;

			switch (match.Groups["ref"].Value)
			{
				case "in":
					refParamType = RefParamType.In;
					break;
				case "out":
					refParamType = RefParamType.Out;
					break;
				case "ref":
					refParamType = RefParamType.Ref;
					break;
			}

			var typeArg = match.Groups["tp"].Success
				? match.Groups["tp"].Value : null;
			type = match.Groups["name"].Value;

			if (typeArg == null)
			{
				switch (type)
				{
					case "bool":
					case "Boolean":
						specialType = SpecialType.System_Boolean;
						break;
					case "byte":
					case "Byte":
						specialType = SpecialType.System_Byte;
						break;
					case "sbyte":
					case "SByte":
						specialType = SpecialType.System_SByte;
						break;
					case "char":
					case "Char":
						specialType = SpecialType.System_Char;
						break;
					case "short":
					case "Int16":
						specialType = SpecialType.System_Int16;
						break;
					case "ushort":
					case "UInt16":
						specialType = SpecialType.System_UInt16;
						break;
					case "int":
					case "Int32":
						specialType = SpecialType.System_Int32;
						break;
					case "uint":
					case "UInt32":
						specialType = SpecialType.System_UInt32;
						break;
					case "long":
					case "Int64":
						specialType = SpecialType.System_Int64;
						break;
					case "ulong":
					case "UInt64":
						specialType = SpecialType.System_UInt64;
						break;
					case "float":
					case "Single":
						specialType = SpecialType.System_Single;
						break;
					case "double":
					case "Double":
						specialType = SpecialType.System_Double;
						break;
					case "nint":
					case "IntPtr":
						specialType = SpecialType.System_IntPtr;
						break;
					case "nuint":
					case "UIntPtr":
						specialType = SpecialType.System_UIntPtr;
						break;
					case "bytes":
					case "Utf8":
					case "Ansi":
						specialType = SpecialType.System_String;
						stringType = StringType.Ansi;
						break;
					case "string":
					case "String":
					case "Unicode":
						specialType = SpecialType.System_String;
						stringType = StringType.Unicode;
						break;
					case "void":
					case "Void":
						specialType = SpecialType.System_Void;
						break;
				}

				if (specialType != SpecialType.None)
					return (context.GetSpecialType(specialType),
						new TypeModifier(stringType, refParamType, pointerLevel));

				var typeSymbol = context.GetTypeByMetadataName(type);

				if (typeSymbol != null)
					return (typeSymbol,
						new TypeModifier(stringType, refParamType, pointerLevel));
			}
			else
			{
				INamedTypeSymbol wrapped = null;

				switch (type)
				{
					case "Span":
						wrapped = context.GetTypeByMetadataName(
							typeof(Span<>).FullName);
						break;
					case "Memory":
						wrapped = context.GetTypeByMetadataName(
							typeof(Memory<>).FullName);
						break;
					case "ReadOnlySpan":
						wrapped = context.GetTypeByMetadataName(
							typeof(ReadOnlySpan<>).FullName);
						break;
					case "ReadOnlyMemory":
						wrapped = context.GetTypeByMetadataName(
							typeof(ReadOnlyMemory<>).FullName);
						break;
					case "Array":
						wrapped = context.GetSpecialType(SpecialType.System_Array);
						break;
					default:
						wrapped = context.GetTypeByMetadataName(type);

						if (wrapped != null && wrapped.Arity != 1)
							wrapped = null;

						break;
				}

				if (wrapped != null)
				{
					switch (typeArg)
					{
						case "bool":
						case "Boolean":
							specialType = SpecialType.System_Boolean;
							break;
						case "byte":
						case "Byte":
							specialType = SpecialType.System_Byte;
							break;
						case "sbyte":
						case "SByte":
							specialType = SpecialType.System_SByte;
							break;
						case "char":
						case "Char":
							specialType = SpecialType.System_Char;
							break;
						case "short":
						case "Int16":
							specialType = SpecialType.System_Int16;
							break;
						case "ushort":
						case "UInt16":
							specialType = SpecialType.System_UInt16;
							break;
						case "int":
						case "Int32":
							specialType = SpecialType.System_Int32;
							break;
						case "uint":
						case "UInt32":
							specialType = SpecialType.System_UInt32;
							break;
						case "long":
						case "Int64":
							specialType = SpecialType.System_Int64;
							break;
						case "ulong":
						case "UInt64":
							specialType = SpecialType.System_UInt64;
							break;
						case "float":
						case "Single":
							specialType = SpecialType.System_Single;
							break;
						case "double":
						case "Double":
							specialType = SpecialType.System_Double;
							break;
						case "nint":
						case "IntPtr":
							specialType = SpecialType.System_IntPtr;
							break;
						case "nuint":
						case "UIntPtr":
							specialType = SpecialType.System_UIntPtr;
							break;
					}

					ITypeSymbol typeSymbol = null;

					if (specialType != SpecialType.None)
					{
						if (type == "Array")
							typeSymbol = context.CreateArrayTypeSymbol(
								context.GetSpecialType(specialType));
						else
							typeSymbol = wrapped.Construct(
								context.GetSpecialType(specialType));
					}

					if (typeSymbol != null)
						return (typeSymbol,
							new TypeModifier(stringType, refParamType, pointerLevel));
				}
			}

			return (context.CreateErrorTypeSymbol(null, type, 0),
				new TypeModifier(stringType, refParamType, pointerLevel));
		}
	}
}
