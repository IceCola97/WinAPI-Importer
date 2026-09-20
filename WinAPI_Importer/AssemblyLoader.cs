using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Linq;
using System.Diagnostics;

namespace WinAPI_Importer
{
	internal static class AssemblyLoader
	{
		private static readonly object m_Lock = new object();

		private static volatile Assembly m_Assembly_AngleSharp = null;
		private static volatile Assembly m_Assembly_AngleSharp_XPath = null;
		private static volatile Assembly m_Assembly_Newtonsoft_Json = null;

		static AssemblyLoader()
		{
			AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
			AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
		}

		private static Assembly FindAssembly(string name)
		{
			var assemblies = AppDomain.CurrentDomain.GetAssemblies()
				.Where(a => a.FullName.StartsWith(name + ","))
				.ToArray();

			if (assemblies.Length == 0)
				return null;
			if (assemblies.Length == 1)
				return assemblies[0];

			throw new AmbiguousMatchException($"有多个名为 '{name}' 的程序集存在");
		}

		private static void CurrentDomain_AssemblyLoad(object sender, AssemblyLoadEventArgs args)
		{
			lock (m_Lock)
			{
				var name = args.LoadedAssembly.FullName.FromFullName();

				switch (name)
				{
					case "AngleSharp":
						m_Assembly_AngleSharp = args.LoadedAssembly;
						break;
					case "AngleSharp.XPath":
						m_Assembly_AngleSharp_XPath = args.LoadedAssembly;
						break;
					case "Newtonsoft.Json":
						m_Assembly_Newtonsoft_Json = args.LoadedAssembly;
						break;
				}
			}
		}

		private static string FromFullName(this string fullName)
		{
			var items = fullName.Split(Misc.Array(','), 2);

			if (items.Length > 0)
				return items[0];

			return fullName;
		}

		private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
		{
			lock (m_Lock)
			{
				var name = args.Name.FromFullName();

				switch (name)
				{
					case "AngleSharp":
						return m_Assembly_AngleSharp
							?? FindAssembly(name)
							?? AppDomain.CurrentDomain.Load(Resource.AngleSharp);
					case "AngleSharp.XPath":
						return m_Assembly_AngleSharp_XPath
							?? FindAssembly(name)
							?? AppDomain.CurrentDomain.Load(Resource.AngleSharp_XPath);
					case "Newtonsoft.Json":
						return m_Assembly_Newtonsoft_Json
							?? FindAssembly(name)
							?? AppDomain.CurrentDomain.Load(Resource.Newtonsoft_Json);
				}

				return null;
			}
		}

		public static void Initialize() { }
	}
}
