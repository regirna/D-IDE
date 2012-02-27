﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using D_Parser.Dom;
using D_Parser.Parser;

namespace D_Parser.Misc
{
	/// <summary>
	/// Stores syntax trees while regarding their package hierarchy.
	/// </summary>
	public class ParseCache
	{
		#region Properties
		public RootPackage Root=new RootPackage();

		public string[] ParsedDirectories
		{
			get;
			private set;
		}

		public Exception LastParseException { get; private set; }
		#endregion

		#region Parsing management
		public ParsePerformanceData[] Parse()
		{
			return Parse(ParsedDirectories);
		}

		/// <summary>
		/// Parses all directories and updates the cache contents
		/// </summary>
		public ParsePerformanceData[] Parse(IEnumerable<string> directoriesToParse)
		{
			var performanceLogs = new List<ParsePerformanceData>();

			if (directoriesToParse == null)
				return null;

			var parsedDirs = new List<string>();
			var newRoot=new RootPackage();
			foreach (var dir in directoriesToParse)
			{
				parsedDirs.Add(dir);

				var ppd = new ParsePerformanceData { BaseDirectory = dir };
				performanceLogs.Add(ppd);

				Parse(dir, newRoot, ppd);
			}

			ParsedDirectories = parsedDirs.ToArray();

			return performanceLogs.ToArray();
		}

		void Parse(string dir, ModulePackage Parent, ParsePerformanceData ppd)
		{
			// wild card character ? seems to behave differently across platforms
			// msdn: -> Exactly zero or one character.
			// monodocs: -> Exactly one character.
			string[] dFiles = Directory.GetFiles(dir, "*.d", SearchOption.TopDirectoryOnly);
			string[] diFiles = Directory.GetFiles(dir, "*.di", SearchOption.TopDirectoryOnly);
			string[] files = new string[dFiles.Length + diFiles.Length];
			Array.Copy(dFiles, 0, files, 0, dFiles.Length);
			Array.Copy(diFiles, 0, files, dFiles.Length, diFiles.Length);

			ModulePackage package = null;
			var packageName = Path.GetFileName(dir);

			if (Parent is RootPackage)
				package = Parent;
			else if (!Parent.Packages.TryGetValue(packageName, out package))
				package = Parent.Packages[packageName] = new ModulePackage
				{
					Name = packageName,
					Parent = Parent
				};

			bool isPhobosRoot = dir.EndsWith(Path.DirectorySeparatorChar+"phobos");

			var sw = new Stopwatch();

			foreach (var file in files)
			{
				// Skip index.d (D2) || phobos.d (D2|D1)
				if (isPhobosRoot && (file.EndsWith("index.d") || file.EndsWith("phobos.d")))
					continue;

				sw.Start();

				try
				{
					// If no debugger attached, save time + memory by skipping function bodies
					var ast = DParser.ParseFile(file, !Debugger.IsAttached);

					ast.ModuleName = DModule.GetModuleName(dir, ast);
					ast.FileName = file;

					package.Modules[ExtractModuleName(ast.ModuleName)] = ast;
				}
				catch (Exception ex)
				{
					LastParseException = ex;
				}

				ppd.AmountFiles++;
				sw.Stop();
			}

			ppd.TotalDuration += sw.Elapsed.TotalSeconds;

			// Parse further subdirectories
			foreach (var subDir in Directory.EnumerateDirectories(dir))
				Parse(subDir, package, ppd);
		}

		public void Clear(bool parseDirectories=false)
		{
			Root = null;
			if (parseDirectories)
				ParsedDirectories = null;

			Root = new RootPackage();
		}
		#endregion

		#region Tree management
		/// <summary>
		/// Use this method to add a syntax tree to the parse cache.
		/// Equally-named trees will be overwritten. 
		/// </summary>
		public void AddOrUpdate(IAbstractSyntaxTree ast)
		{
			if (ast == null)
				return;

			var packName = ExtractPackageName(ast.ModuleName);

			if (string.IsNullOrEmpty(packName))
			{
				Root.Modules[ast.ModuleName] = ast;
				return;
			}

			var pack = GetOrCreatePackage(packName,true);

			pack.Modules[ExtractModuleName(ast.ModuleName)] = ast;
		}

		ModulePackage GetOrCreatePackage(string package, bool create=false)
		{
			if (string.IsNullOrEmpty(package))
				return Root;

			var currentPackage = (ModulePackage)Root;

			foreach (var p in SplitModuleName(package))
			{
				ModulePackage returnValue = null;

				if (!currentPackage.Packages.TryGetValue(p, out returnValue))
				{
					if (create)
						returnValue = currentPackage.Packages[p] =
							new ModulePackage
							{
								Name = p,
								Parent = currentPackage
							};
					else
						return null;
				}

				currentPackage = returnValue;
			}

			return currentPackage;
		}

		/// <summary>
		/// Returns null if no module was found.
		/// </summary>
		public IAbstractSyntaxTree GetModule(string moduleName)
		{
			var packName = ExtractPackageName(moduleName);

			var pack = GetOrCreatePackage(packName, false);

			if (pack != null)
			{
				IAbstractSyntaxTree ret = null;
				if(pack.Modules.TryGetValue(ExtractModuleName(moduleName),out ret))
					return ret;
			}

			return null;
		}
		#endregion

		#region Module name splitting helpers
		/// <summary>
		/// a.b.c.d => a.b.c
		/// </summary>
		public static string ExtractPackageName(string ModuleName)
		{
			if (string.IsNullOrEmpty(ModuleName))
				return "";

			var i = ModuleName.LastIndexOf('.');

			return i == -1 ? ModuleName : ModuleName.Substring(0,i);
		}

		/// <summary>
		/// a.b.c.d => d
		/// </summary>
		public static string ExtractModuleName(string ModuleName)
		{
			if (string.IsNullOrEmpty(ModuleName))
				return "";

			var i = ModuleName.LastIndexOf('.');

			return i == -1 ? ModuleName : ModuleName.Substring(i + 1);
		}

		public static string[] SplitModuleName(string ModuleName)
		{
			return ModuleName.Split('.');
		}
		#endregion
	}

	public class RootPackage : ModulePackage { }

	public class ModulePackage
	{
		public ModulePackage Parent { get; internal set; }
		public string Name { get; internal set; }

		public Dictionary<string, ModulePackage> Packages = new Dictionary<string, ModulePackage>();
		public Dictionary<string, IAbstractSyntaxTree> Modules = new Dictionary<string, IAbstractSyntaxTree>();

		public string Path
		{
			get{
				return (Parent==null ? (Parent.Path+"."):"") + Name;
			}
		}
	}

	public class ParsePerformanceData
	{
		public string BaseDirectory;
		public int AmountFiles = 0;

		/// <summary>
		/// Duration (in seconds)
		/// </summary>
		public double TotalDuration = 0.0;
		public double FileDuration
		{
			get
			{
				if (AmountFiles > 0)
					return TotalDuration / AmountFiles;
				return 0;
			}
		}
	}
}