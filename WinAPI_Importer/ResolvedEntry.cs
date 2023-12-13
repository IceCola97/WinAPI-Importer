using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using WinAPI_Importer.Builders;

namespace WinAPI_Importer
{
	public sealed class ResolvedEntry
	{
		private readonly List<ResolvedSlot> m_Slots = new List<ResolvedSlot>();
		private readonly List<ResolvedSlot> m_Extends = new List<ResolvedSlot>();
		private readonly List<ResolvedEntry> m_Nests = new List<ResolvedEntry>();

		public ResolvedEntry(string name, PrototypeKind kind)
		{
			Name = name;
			Kind = kind;
		}

		public string Name { get; }

		public PrototypeKind Kind { get; }

		public string Module { get; set; }

		public string ReferUrl { get; set; }

		public int SlotCount => m_Slots.Count;

		public int ExtendsCount => m_Extends.Count;

		public int NestCount => m_Slots.Count;

		public ResolvedSlot SlotAt(int index) => m_Slots[index];

		public ResolvedSlot ExtendAt(int index) => m_Extends[index];

		public ResolvedEntry NestAt(int index) => m_Nests[index];

		public void AddSlot(ResolvedSlot slot) => m_Slots.Add(slot);

		/// <summary>
		/// 扩展类型指的是下方<c>PDEMOSTRUCT</c>类型，是给定原型中包含的副类型
		/// <code>
		/// struct tagDEMOSTRUCT {
		///     DWORD field1;
		///     SIZE_T field2;
		/// } DEMOSTRUCT, *PDEMOSTRUCT
		/// </code>
		/// </summary>
		/// <param name="extend"></param>
		public void AddExtend(ResolvedSlot extend) => m_Extends.Add(extend);

		/// <summary>
		/// 内含类型指的是结构体或联合体中的内嵌类型，如下方的NESTUNION
		/// <code>
		/// struct tagDEMOSTRUCT {
		///     DWORD field1;
		///     union NESTUNION {
		///         DWORD field1;
		///         HANDLE field2;
		///         BYTE field3[4];
		///     } field2;
		/// }
		/// </code>
		/// </summary>
		/// <param name="nest"></param>
		public void AddNest(ResolvedEntry nest) => m_Nests.Add(nest);
	}

	public sealed class ResolvedSlot
	{
		/// <summary>
		/// 指示参数、枚举项、字段等的名称
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// 指示参数、字段等的类型名称
		/// </summary>
		public string TypeName { get; set; }

		/// <summary>
		/// 当枚举项被明确定义枚举值时指示枚举值的常量
		/// </summary>
		public string Value { get; set; }

		/// <summary>
		/// 指示一个结构体或联合体的字段是定长数组时数组的长度常量
		/// </summary>
		public string FixedArrayLength { get; set; }

		/// <summary>
		/// 指示指针层级、引用类型、字符串封送方式
		/// </summary>
		public TypeModifier TypeModifier { get; set; }

		/// <summary>
		/// 指示函数参数是否是可选的
		/// </summary>
		public bool IsOptional { get; set; }
	}

	public struct TypeModifier
	{
		public TypeModifier(StringType stringType, RefParamType refParamType, int pointerLevel)
		{
			StringType = stringType;
			RefParamType = refParamType;
			PointerLevel = pointerLevel;
		}

		public StringType StringType { get; set; }

		public RefParamType RefParamType { get; set; }

		public int PointerLevel { get; set; }

		private TypeSyntax ApplyTypeCore(SemanticModel semantic, int position,
			ITypeSymbol baseType, bool makeRefType, out RefParamType refParamType)
		{
			int pointerLevel = PointerLevel;

			refParamType = RefParamType;

			if (baseType.SpecialType == SpecialType.System_String)
			{
				switch (refParamType)
				{
					case RefParamType.Out:
					case RefParamType.Ref:
						baseType = semantic.Compilation
							.GetTypeByMetadataName(typeof(StringBuilder).FullName);
						break;
				}
			}

			if (!baseType.IsValueType)
			{
				if ((refParamType & RefParamType.Ref) != 0)
					refParamType = RefParamType.None;
				else
					pointerLevel--;
			}

			TypeSyntax baseName = SyntaxFactory.ParseTypeName(
				baseType.ToMinimalDisplayString(semantic, position));

			for (int i = pointerLevel - 1; i >= 0; i--)
				baseName = SyntaxFactory.PointerType(baseName);

			if (makeRefType && (refParamType & RefParamType.Ref) != 0)
				baseName = SyntaxFactory.RefType(baseName);

			return baseName;
		}

		public TypeSyntax ApplyType(SemanticModel semantic, int position, ITypeSymbol baseType)
		{
			return ApplyTypeCore(semantic, position, baseType, true, out _);
		}

		public ParameterSyntax ApplyParameter(
			SemanticModel semantic, int position,
			ITypeSymbol baseType, string paramName,
			bool isOptional = false,
			RefParamType attrType = RefParamType.None,
			ExpressionSyntax defaultValue = null
		)
		{
			TypeSyntax baseName = ApplyTypeCore(semantic, position,
				baseType, false, out var refParamType);

			var parameterBuilder = new ParameterBuilder();

			if (attrType.HasFlag(RefParamType.In))
			{
				var attrBuilder = new AttributeBuilder();
				attrBuilder.SetClass(typeof(InAttribute), semantic, position);
				parameterBuilder.AddAttribute(attrBuilder.Create());
			}

			if (attrType.HasFlag(RefParamType.Out))
			{
				var attrBuilder = new AttributeBuilder();
				attrBuilder.SetClass(typeof(OutAttribute), semantic, position);
				parameterBuilder.AddAttribute(attrBuilder.Create());
			}

			if (isOptional)
			{
				var attrBuilder = new AttributeBuilder();
				attrBuilder.SetClass(typeof(OptionalAttribute), semantic, position);
				parameterBuilder.AddAttribute(attrBuilder.Create());
			}

			if (StringType != StringType.None)
			{
				var attrBuilder = new AttributeBuilder();
				var unmanagedType = semantic.CreateNameSyntax(typeof(UnmanagedType), position, true);
				var enumItem = StringType == StringType.Ansi
					? nameof(UnmanagedType.LPStr) : nameof(UnmanagedType.LPWStr);
				var argument = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
					unmanagedType, SyntaxFactory.IdentifierName(enumItem));

				attrBuilder.AddArgument(argument);
				attrBuilder.SetClass(typeof(MarshalAsAttribute), semantic, position);
				parameterBuilder.AddAttribute(attrBuilder.Create());
			}

			parameterBuilder.AddModifier(refParamType);
			parameterBuilder.SetParameterType(baseName);
			parameterBuilder.SetIdentifier(paramName);
			parameterBuilder.SetDefaultValue(defaultValue);

			return parameterBuilder.Create();
		}

		public static TypeModifier Combine(TypeModifier inner, TypeModifier outer)
		{
			RefParamType finalRefType;
			int totalLevel = inner.PointerLevel + outer.PointerLevel;

			if (outer.PointerLevel > 0)
			{
				finalRefType = outer.RefParamType;

				if ((inner.RefParamType & RefParamType.Ref) == 0)
					totalLevel--;
			}
			else if ((inner.RefParamType & RefParamType.Ref) != 0)
				finalRefType = outer.RefParamType;
			else
				finalRefType = RefParamType.None;

			return new TypeModifier
			{
				StringType = inner.StringType,
				RefParamType = finalRefType,
				PointerLevel = totalLevel,
			};
		}
	}

	public enum StringType
	{
		None,
		Ansi,
		Unicode,
	}

	public enum RefParamType
	{
		None,
		In,
		Out,
		Ref,
	}

	public enum PrototypeKind
	{
		None,
		Function,
		Struct,
		Union,
		Delegate,
		Enum,
		Macro, // unsupported
		Interface, // unsupported
	}
}
