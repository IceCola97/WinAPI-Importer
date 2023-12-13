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
		private static Assembly m_Assembly_AngleSharp = null;
		private static Assembly m_Assembly_AngleSharp_XPath = null;
		private static Assembly m_Assembly_Newtonsoft_Json = null;

		static AssemblyLoader()
		{
			AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
			AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
		}

		private static void CurrentDomain_AssemblyLoad(object sender, AssemblyLoadEventArgs args)
		{
			var name = args.LoadedAssembly.FullName;

			if (name.StartsWith("AngleSharp,"))
				m_Assembly_AngleSharp = args.LoadedAssembly;
			else if (name.StartsWith("AngleSharp.XPath,"))
				m_Assembly_AngleSharp_XPath = args.LoadedAssembly;
			else if (name.StartsWith("Newtonsoft.Json,"))
				m_Assembly_Newtonsoft_Json = args.LoadedAssembly;
		}

		private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
		{
			var name = args.Name;

			if (name.StartsWith("AngleSharp,"))
				return m_Assembly_AngleSharp
					?? AppDomain.CurrentDomain.Load(Resource.AngleSharp);
			else if (name.StartsWith("AngleSharp.XPath,"))
				return m_Assembly_AngleSharp_XPath
					?? AppDomain.CurrentDomain.Load(Resource.AngleSharp_XPath);
			else if (name.StartsWith("Newtonsoft.Json,"))
				return m_Assembly_Newtonsoft_Json
					?? AppDomain.CurrentDomain.Load(Resource.Newtonsoft_Json);

			return null;
		}

		public static void Initialize() { }
	}
}
