﻿using System;
using System.Collections.Generic;
using System.Text;
using D_Parser.Core;
using System.IO;
using System.Collections;
using System.Collections.ObjectModel;

namespace D_Parser.Resolver
{
	/// <summary>
	/// Generic class for resolve module relations and/or declarations
	/// </summary>
	public class DCodeResolver
	{
		#region Util
		public static string ReverseString(string s)
		{
			if (s.Length < 1) return s;
			char[] ret = new char[s.Length];
			for (int i = s.Length; i > 0; i--)
			{
				ret[s.Length - i] = s[i - 1];
			}
			return new string(ret);
		}

		#endregion

		/// <summary>
		/// Returns a list of all items that can be accessed in the current scope.
		/// </summary>
		/// <param name="ScopedBlock"></param>
		/// <param name="ImportCache"></param>
		/// <returns></returns>
		public static IEnumerable<INode> EnumAllAvailableMembers(IBlockNode ScopedBlock, IEnumerable<IAbstractSyntaxTree> CodeCache)
		{
			/* First walk through the current scope.
			 * Walk up the node hierarchy and add all their items (private as well as public members).
			 * Resolve base classes and add their non-private|static members.
			 * 
			 * Then add public members of the imported modules 
			 */
			var ret = new List<INode>();
			var ImportCache = ResolveImports(ScopedBlock.NodeRoot as IAbstractSyntaxTree, CodeCache);

			#region Current module/scope related members
			var curScope = ScopedBlock;

			while (curScope != null)
			{
				// Walk up inheritance hierarchy
				if (curScope is DClassLike)
				{
					var curWatchedClass = curScope as DClassLike;
					// MyClass > BaseA > BaseB > Object
					while (curWatchedClass != null)
					{
						if (curWatchedClass.TemplateParameters != null)
							ret.AddRange(curWatchedClass.TemplateParameterNodes as IEnumerable<INode>);

						foreach (var m in curWatchedClass)
						{
							var dm2 = m as DNode;
							var dm3 = m as DMethod; // Only show normal & delegate methods
							if (dm2 == null ||
								(dm3 != null && !(dm3.SpecialType == DMethod.MethodType.Normal || dm3.SpecialType == DMethod.MethodType.Delegate))
								)
								continue;

							// Add static and non-private members of all base classes; add everything if we're still handling the currently scoped class
							if (curWatchedClass == curScope || dm2.IsStatic || !dm2.ContainsAttribute(DTokens.Private))
								ret.Add(m);
						}

						// Stop adding if Object class level got reached
						if (!string.IsNullOrEmpty(curWatchedClass.Name) && curWatchedClass.Name.ToLower() == "object")
							break;

						curWatchedClass = ResolveBaseClass(curWatchedClass, ImportCache);
					}
				}
				else if (curScope is DMethod)
				{
					var dm = curScope as DMethod;
					ret.AddRange(dm.Parameters);

					if (dm.TemplateParameters != null)
						ret.AddRange(dm.TemplateParameterNodes as IEnumerable<INode>);

					foreach (var n in dm)
						if (!(n is DStatementBlock))
							ret.Add(n);
				}
				else foreach (var n in curScope)
						if (!(n is DStatementBlock))
						{
							var dm3 = n as DMethod; // Only show normal & delegate methods
							if ((dm3 != null && !(dm3.SpecialType == DMethod.MethodType.Normal || dm3.SpecialType == DMethod.MethodType.Delegate)))
								continue;

							//TODO: (More parser-related!) Add anonymous blocks (e.g. delegates) to the syntax tree
							ret.Add(n);
						}

				curScope = curScope.Parent as IBlockNode;
			}
			#endregion

			#region Global members
			// Add all non-private and non-package-only nodes
			foreach (var mod in ImportCache)
			{
				if (mod.FileName == (ScopedBlock.NodeRoot as IAbstractSyntaxTree).FileName)
					continue;

				foreach (var i in mod)
				{
					var dn = i as DNode;
					if (dn != null)
					{
						if (dn.IsPublic && !dn.ContainsAttribute(DTokens.Package))
							ret.Add(dn);
					}
					else ret.Add(i);
				}
			}
			#endregion

			if (ret.Count < 1)
				return null;
			return ret;
		}

		public static ITypeDeclaration BuildIdentifierList(string Text, int CaretOffset, /*bool BackwardOnly,*/ out DToken OptionalInitToken)
		{
			OptionalInitToken = null;

			#region Step 1: Walk along the code to find the declaration's beginning
			int IdentListStart = ReverseParsing.SearchExpressionStart(Text, CaretOffset);
			#endregion

			#region 2: Init the parser
			if (IdentListStart < 0)
				return null;

			// If code e.g. starts with a bracket, increment IdentListStart
			var ch = Text[IdentListStart];
			while (IdentListStart < Text.Length - 1 && !Char.IsLetterOrDigit(ch) && ch != '_' && ch != '.')
			{
				IdentListStart++;
				ch = Text[IdentListStart];
			}

			//if (BackwardOnly && IdentListStart >= CaretOffset)return null;

			var psr = DParser.ParseBasicType(Text.Substring(IdentListStart), out OptionalInitToken);
			#endregion

			return psr;
		}

		public static IBlockNode SearchBlockAt(IBlockNode Parent, CodeLocation Where)
		{
			foreach (var n in Parent)
			{
				if (!(n is IBlockNode)) continue;

				var b = n as IBlockNode;
				if (Where > b.StartLocation && Where < b.EndLocation)
					return SearchBlockAt(b, Where);
			}

			return Parent;
		}

		public static IBlockNode SearchClassLikeAt(IBlockNode Parent, CodeLocation Where)
		{
			foreach (var n in Parent)
			{
				if (!(n is DClassLike)) continue;

				var b = n as IBlockNode;
				if (Where > b.BlockStartLocation && Where < b.EndLocation)
					return SearchClassLikeAt(b, Where);
			}

			return Parent;
		}

		#region Import path resolving
		/// <summary>
		/// Returns all imports of a module and those public ones of the imported modules
		/// </summary>
		/// <param name="cc"></param>
		/// <param name="ActualModule"></param>
		/// <returns></returns>
		public static IEnumerable<IAbstractSyntaxTree> ResolveImports(IAbstractSyntaxTree ActualModule, IEnumerable<IAbstractSyntaxTree> CodeCache)
		{
			var ret = new List<IAbstractSyntaxTree>();
			if (CodeCache == null || ActualModule == null) return ret;

			// First add all local imports
			var localImps = new List<string>();
			foreach (var kv in ActualModule.Imports)
				localImps.Add(kv.Key.ToString());

			// Then try to add the 'object' module
			var objmod = SearchModuleInCache(CodeCache, "object");
			if (objmod != null && !ret.Contains(objmod))
				ret.Add(objmod);

			foreach (var m in CodeCache)
				if (localImps.Contains(m.Name) && !ret.Contains(m))
				{
					ret.Add(m);
					/* 
					 * dmd-feature: public imports only affect the directly superior module
					 *
					 * Module A:
					 * import B;
					 * 
					 * foo(); // Will fail, because foo wasn't found
					 * 
					 * Module B:
					 * import C;
					 * 
					 * Module C:
					 * public import D;
					 * 
					 * Module D:
					 * void foo() {}
					 * 
					 * 
					 * Whereas
					 * Module B:
					 * public import C;
					 * 
					 * will succeed because we have a closed import hierarchy in which all imports are public.
					 * 
					 */

					// So if there aren't any public imports in our import, continue without doing anything
					ResolveImports(ret, m, CodeCache);
				}

			return ret;
		}

		public static void ResolveImports(List<IAbstractSyntaxTree> ImportModules,
			IAbstractSyntaxTree ActualModule, IEnumerable<IAbstractSyntaxTree> CodeCache)
		{
			var localImps = new List<string>();
			foreach (var kv in ActualModule.Imports)
				if (kv.Value)
					localImps.Add(kv.Key.ToString());

			foreach (var m in CodeCache)
				if (localImps.Contains(m.Name) && !ImportModules.Contains(m))
				{
					ImportModules.Add(m);
					ResolveImports(ImportModules, m, CodeCache);
				}
		}
		#endregion

		#region Declaration resolving
		public enum NodeFilter
		{
			/// <summary>
			/// Returns all static and public children of a node
			/// </summary>
			PublicStaticOnly,
			/// <summary>
			/// Returns all public members e.g. of an object
			/// </summary>
			PublicOnly,

			/// <summary>
			/// e.g. in class hierarchies: Returns all public and protected members
			/// </summary>
			NonPrivate,
			/// <summary>
			/// Returns all members
			/// </summary>
			All
		}

		public static bool MatchesFilter(NodeFilter Filter, INode n)
		{
			switch (Filter)
			{
				case NodeFilter.All:
					return true;
				case NodeFilter.PublicOnly:
					return (n as DNode).IsPublic;
				case NodeFilter.NonPrivate:
					return !(n as DNode).ContainsAttribute(DTokens.Private);
				case NodeFilter.PublicStaticOnly:
					return (n as DNode).IsPublic && (n as DNode).IsStatic;
			}
			return false;
		}

		public static IAbstractSyntaxTree SearchModuleInCache(IEnumerable<IAbstractSyntaxTree> HayStack, string ModuleName)
		{
			foreach (var m in HayStack)
			{
				if (m.Name == ModuleName) return m;
			}
			return null;
		}

		public static ITypeDeclaration GetDNodeType(INode VariableOrMethod)
		{
			var ret = VariableOrMethod.Type;

			// If our node contains an auto attribute, expect that there is an initializer that implies its type
			if (VariableOrMethod is DVariable && (VariableOrMethod as DNode).ContainsAttribute(DTokens.Auto))
			{
				var init = (VariableOrMethod as DVariable).Initializer;
				if (init is NewExpression)
				{
					return (init as NewExpression).Type;
					/*var tex = init as NewExpression;
					while (tex != null)
					{
						if (tex is TypeDeclarationExpression)
						{
							ret = (tex as TypeDeclarationExpression).Declaration;

							// If variable initializer contains template arguments, skip those and pass the raw name only
							if (ret is TemplateDecl)
								ret = (ret as TemplateDecl).Base;
							break;
						}

						tex = tex.Base;
					}*/
				}
			}


			return ret;
		}

		/// <summary>
		/// Finds the location (module node) where a type (TypeExpression) has been declared.
		/// Note: This function only searches within 1 module only!
		/// </summary>
		/// <param name="Module"></param>
		/// <param name="IdentifierList"></param>
		/// <returns>When a type was found, the declaration entry will be returned. Otherwise, it'll return null.</returns>
		public static INode[] ResolveTypeDeclarations_ModuleOnly(IBlockNode BlockNode, ITypeDeclaration IdentifierList, NodeFilter Filter, IEnumerable<IAbstractSyntaxTree> ImportCache)
		{
			var ret = new List<INode>();

			if (BlockNode == null || IdentifierList == null) return ret.ToArray();

			// Modules    Declaration
			// |---------|-----|
			// std.stdio.writeln();
			/*if (IdentifierList is IdentifierList)
			{
				var il = IdentifierList as IdentifierList;
				var skippedIds = 0;

				// Now search the entire block
				var istr = il.ToString();

				var mod = BlockNode is DModule ? BlockNode as DModule : BlockNode.NodeRoot as DModule;
				// If the id list start with the name of BlockNode's root module, 
				// skip those identifiers first to proceed seeking the rest of the list
				if (mod != null && !string.IsNullOrEmpty(mod.ModuleName) && istr.StartsWith(mod.ModuleName))
				{
					skippedIds += mod.ModuleName.Split('.').Length;
					istr = il.ToString(skippedIds);
				}

				// Now move stepwise deeper calling ResolveTypeDeclaration recursively
				ret.Add(BlockNode); // Temporarily add the block node to the return array - it gets proceeded in the next while loop

				var tFilter = Filter;
				while (skippedIds < il.Parts.Count && ret.Count > 0)
				{
					var DeeperLevel = new List<INode>();
					// As long as our node(s) can contain other nodes, scan it
					foreach (var n in ret)
						DeeperLevel.AddRange(ResolveTypeDeclarations_ModuleOnly(n as IBlockNode, il[skippedIds], tFilter, ImportCache));

					// If a variable is given and if it's not the last identifier, return it's definition type
					// If a method is given, search for its return type
					if (DeeperLevel.Count > 0 && skippedIds < il.Parts.Count - 1)
					{
						if (DeeperLevel[0] is DVariable || DeeperLevel[0] is DMethod)
						{
							// If we retrieve deeper levels, we are only allowed to scan for public members
							tFilter = NodeFilter.PublicOnly;
							var v = DeeperLevel[0];
							DeeperLevel.Clear();

							DeeperLevel.AddRange(ResolveTypeDeclarations_ModuleOnly(v.Parent as IBlockNode, GetDNodeType(v), Filter, ImportCache));
						}
					}

					skippedIds++;
					ret = DeeperLevel;
				}

				return ret.ToArray();
			}*/

			//HACK: Scan the type declaration list for any NormalDeclarations
			var td = IdentifierList;
			while (td != null && !(td is IdentifierDeclaration))
				td = td.InnerDeclaration;

			var baseTypes = new List<INode>();

			if (td is IdentifierDeclaration)
			{
				var nameIdent = td as IdentifierDeclaration;

				// Scan from the inner to the outer level
				var currentParent = BlockNode;
				while (currentParent != null)
				{
					// Scan the node's children for a match - return if we found one
					foreach (var ch in currentParent)
					{
						if (nameIdent == ch.Name && !ret.Contains(ch) && MatchesFilter(Filter, ch))
							baseTypes.Add(ch);
					}

					// If our current Level node is a class-like, also attempt to search in its baseclass!
					if (currentParent is DClassLike)
					{
						var baseClass = ResolveBaseClass(currentParent as DClassLike, ImportCache);
						if (baseClass != null)
							baseTypes.AddRange(ResolveTypeDeclarations_ModuleOnly(baseClass, nameIdent, NodeFilter.NonPrivate, ImportCache));
					}

					// Check parameters
					if (currentParent is DMethod)
						foreach (var ch in (currentParent as DMethod).Parameters)
						{
							if (nameIdent == ch.Name)
								baseTypes.Add(ch);
						}

					// and template parameters
					if (currentParent is DNode && (currentParent as DNode).TemplateParameters != null)
						foreach (var ch in (currentParent as DNode).TemplateParameters)
						{
							if (nameIdent == ch.Name)
								baseTypes.Add(new TemplateParameterNode(ch));
						}

					// Move root-ward
					currentParent = currentParent.Parent as IBlockNode;
				}

				if (td == IdentifierList)
					ret.AddRange(baseTypes);
				else
				{

				}
			}

			//TODO: Here a lot of additional checks and more detailed type evaluations are missing!

			return ret.ToArray();
		}



		/// <summary>
		/// Search a type within an entire Module Cache.
		/// </summary>
		/// <param name="ImportCache"></param>
		/// <param name="CurrentlyScopedBlock"></param>
		/// <param name="IdentifierList"></param>
		/// <returns></returns>
		public static INode[] ResolveTypeDeclarations(IBlockNode CurrentlyScopedBlock, ITypeDeclaration IdentifierList, IEnumerable<IAbstractSyntaxTree> ImportCache,
			bool IsCompleteIdentifier)
		{
			var ret = new List<INode>();

			var ThisModule = CurrentlyScopedBlock.NodeRoot as DModule;

			// Of course it's needed to scan our own module at first
			if (ThisModule != null)
				ret.AddRange(ResolveTypeDeclarations_ModuleOnly(CurrentlyScopedBlock, IdentifierList, NodeFilter.All, ImportCache));

			// Then search within the imports for our IdentifierList
			if (ImportCache != null)
				foreach (var m in ImportCache)
				{
					// Add the module itself to the returned list if its name starts with the identifierlist
					if (IsCompleteIdentifier ? (m.Name.StartsWith(IdentifierList.ToString() + ".") || m.Name == IdentifierList.ToString()) // If entire identifer was typed, check if the entire id is part of the module name
						: m.Name.StartsWith(IdentifierList.ToString()))
						ret.Add(m);

					else if (m.FileName != ThisModule.FileName) // We already parsed this module
						ret.AddRange(ResolveTypeDeclarations_ModuleOnly(m, IdentifierList, NodeFilter.PublicOnly, ImportCache));
				}

			return ret.ToArray();
		}

		/// <summary>
		/// Resolves all base classes of a class, struct, template or interface
		/// </summary>
		/// <param name="ModuleCache"></param>
		/// <param name="ActualClass"></param>
		/// <returns></returns>
		public static DClassLike ResolveBaseClass(DClassLike ActualClass, IEnumerable<IAbstractSyntaxTree> ModuleCache)
		{
			// Implicitly set the object class to the inherited class if no explicit one was done
			if (ActualClass.BaseClasses.Count < 1)
			{
				var ObjectClass = ResolveTypeDeclarations(ActualClass.NodeRoot as IBlockNode, new IdentifierDeclaration("Object"), ModuleCache, true);
				if (ObjectClass.Length > 0 && ObjectClass[0] != ActualClass) // Yes, it can be null - like the Object class which can't inherit itself
					return ObjectClass[0] as DClassLike;
			}
			else // Take the first only (since D forces single inheritance)
			{
				var ClassMatches = ResolveTypeDeclarations(ActualClass.NodeRoot as IBlockNode, ActualClass.BaseClasses[0], ModuleCache, true);
				if (ClassMatches.Length > 0)
					return ClassMatches[0] as DClassLike;
			}

			return null;
		}

		#endregion

		/// <summary>
		/// Trivial class which cares about locating Comments and other non-code blocks within a code file
		/// </summary>
		public class Commenting
		{
			public static int IndexOf(string HayStack, bool Nested, int Start)
			{
				string Needle = Nested ? "+/" : "*/";
				char cur = '\0';
				int off = Start;
				bool IsInString = false;
				int block = 0, nested = 0;

				while (off < HayStack.Length - 1)
				{
					cur = HayStack[off];

					// String check
					if (cur == '\"' && (off < 1 || HayStack[off - 1] != '\\'))
					{
						IsInString = !IsInString;
					}

					if (!IsInString && (cur == '/') && (HayStack[off + 1] == '*' || HayStack[off + 1] == '+'))
					{
						if (HayStack[off + 1] == '*')
							block++;
						else
							nested++;

						off += 2;
						continue;
					}

					if (!IsInString && cur == Needle[0])
					{
						if (off + Needle.Length - 1 >= HayStack.Length)
							return -1;

						if (HayStack.Substring(off, Needle.Length) == Needle)
						{
							if (Nested) nested--; else block--;

							if ((Nested ? nested : block) < 0) // that value has to be -1 because we started to count at 0
								return off;

							off++; // Skip + or *
						}

						if (HayStack.Substring(off, 2) == (Nested ? "*/" : "+/"))
						{
							if (Nested) block--; else nested--;
							off++;
						}
					}

					off++;
				}
				return -1;
			}

			public static int LastIndexOf(string HayStack, bool Nested, int Start)
			{
				string Needle = Nested ? "/+" : "/*";
				char cur = '\0', prev = '\0';
				int off = Start;
				bool IsInString = false;
				int block = 0, nested = 0;

				while (off >= 0)
				{
					cur = HayStack[off];
					if (off > 0) prev = HayStack[off - 1];

					// String check
					if (cur == '\"' && (off < 1 || HayStack[off - 1] != '\\'))
					{
						IsInString = !IsInString;
					}

					if (!IsInString && (cur == '+' || cur == '*') && HayStack[off + 1] == '/')
					{
						if (cur == '*')
							block--;
						else
							nested--;

						off -= 2;
						continue;
					}

					if (!IsInString && cur == '/')
					{
						if (HayStack.Substring(off, Needle.Length) == Needle)
						{
							if (Nested) nested++; else block++;

							if ((Nested ? nested : block) >= 1)
								return off;
						}

						if (HayStack.Substring(off, 2) == (Nested ? "/*" : "/+"))
						{
							if (Nested) block++; else nested++;
							off--;
						}
					}

					off--;
				}
				return -1;
			}

			public static void IsInCommentAreaOrString(string Text, int Offset, out bool IsInString, out bool IsInLineComment, out bool IsInBlockComment, out bool IsInNestedBlockComment)
			{
				char cur = '\0', peekChar = '\0';
				int off = 0;
				IsInString = IsInLineComment = IsInBlockComment = IsInNestedBlockComment = false;

				while (off < Offset - 1)
				{
					cur = Text[off];
					if (off < Text.Length - 1) peekChar = Text[off + 1];

					// String check
					if (!IsInLineComment && !IsInBlockComment && !IsInNestedBlockComment && cur == '\"' && (off < 1 || Text[off - 1] != '\\'))
						IsInString = !IsInString;

					if (!IsInString)
					{
						// Line comment check
						if (!IsInBlockComment && !IsInNestedBlockComment)
						{
							if (cur == '/' && peekChar == '/')
								IsInLineComment = true;
							if (IsInLineComment && cur == '\n')
								IsInLineComment = false;
						}

						// Block comment check
						if (cur == '/' && peekChar == '*')
							IsInBlockComment = true;
						if (IsInBlockComment && cur == '*' && peekChar == '/')
							IsInBlockComment = false;

						// Nested comment check
						if (!IsInString && cur == '/' && peekChar == '+')
							IsInNestedBlockComment = true;
						if (IsInNestedBlockComment && cur == '+' && peekChar == '/')
							IsInNestedBlockComment = false;
					}

					off++;
				}
			}

			public static bool IsInCommentAreaOrString(string Text, int Offset)
			{
				bool a, b, c, d;
				IsInCommentAreaOrString(Text, Offset, out a, out b, out c, out d);

				return a || b || c || d;
			}
		}
	}

	/// <summary>
	/// Code completion rules:
	///
	/// - If a letter has been typed:
	///		- Show the popup if:
	///			- there's a "*" in front of the identifier, what makes us assume (TODO: ensure it!) that it is meant to be an expression, not a type
	///			- there is no type, show the popup
	///			- a preceding () belong to:
	///				if while for foreach foreach_reverse with try catch finally cast
	///		- Do not show the popup if:
	///			- "]" or an other identifier (includes keywords) is located (after at least one whitespace) in front of the identifier
	///			- If the caret is already located within an identifier
	///		-> When a new identifier has begun to be typed, the after-space completion data gets shown
	///	
	/// - If a dot has been typed:
	///		- resolve the type of the expression in front of the dot
	///			- if the type is a class-like, show all its static public members
	///			- if it's an enum, show all its items
	///			- if it's a variable or method, resolve its base type and show all its public members (also those of the type's base classes)
	///				- if the variable is declared within the base type itself, show all items
	/// </summary>
	public class DResolver
	{
		public static ResolveResult[] ResolveType(string code, int caret, CodeLocation caretLocation, IAbstractSyntaxTree codeAST, IEnumerable<IAbstractSyntaxTree> parseCache,
			bool alsoParseBeyondCaret = false,
			bool onlyAssumeIdentifierList = false)
		{
			var start = ReverseParsing.SearchExpressionStart(code, caret);

			if (start < 0)
				return null;

			var expressionCode = code.Substring(start, alsoParseBeyondCaret ? code.Length - start : caret - start);

			var parser = DParser.Create(new StringReader(expressionCode));
			parser.Lexer.NextToken();

			if (onlyAssumeIdentifierList)
				return ResolveType(parser.IdentifierList(), codeAST, parseCache);
			else if (parser.IsAssignExpression())
				return ResolveType(parser.AssignExpression().ExpressionTypeRepresentation, codeAST, parseCache);
			else
				return ResolveType(parser.Type(), codeAST, parseCache);
		}

		public static ResolveResult[] ResolveType(ITypeDeclaration declaration, IBlockNode currentlyScopedNode, IEnumerable<IAbstractSyntaxTree> parseCache)
		{
			if (declaration == null)
				return null;

			var returnedResults = new List<ResolveResult>();

			// Walk down recursively to resolve everything from the very first to declaration's base type declaration.
			ResolveResult[] rbases = null;
			if (declaration.InnerDeclaration != null)
				rbases = ResolveType(declaration.InnerDeclaration, currentlyScopedNode, parseCache);

			/* 
			 * If there is no parent resolve context (what usually means we are searching the type named like the first identifier in the entire declaration),
			 * search the very first type declaration by walking along the current block scope hierarchy.
			 * If there wasn't any item found in that procedure, search in the global parse cache
			 */
			#region Search initial member/type/module/whatever
			if (rbases == null)
			{
				if (declaration is IdentifierDeclaration)
				{
					string searchIdentifier = (declaration as IdentifierDeclaration).Value as string;
					var matches = new List<INode>();

					// First search along the hierarchy in the current module
					var curScope = currentlyScopedNode;
					while (curScope != null)
					{
						matches.AddRange(ScanNodeForIdentifier(curScope, searchIdentifier, parseCache));

						if (curScope is IAbstractSyntaxTree && (curScope as IAbstractSyntaxTree).ModuleName.StartsWith(searchIdentifier))
							matches.Add(curScope);

						curScope = curScope.Parent as IBlockNode;
					}

					// Then go on searching in the global scope
					var ThisModule = currentlyScopedNode is IAbstractSyntaxTree ? currentlyScopedNode as IAbstractSyntaxTree : currentlyScopedNode.NodeRoot as IAbstractSyntaxTree;
					foreach (var mod in parseCache)
					{
						if (mod == ThisModule)
							continue;

						if (mod.ModuleName.StartsWith(searchIdentifier))
							matches.Add(mod);

						matches.AddRange(ScanNodeForIdentifier(mod, searchIdentifier, null));
					}

					var results= HandleNodeMatches(matches, currentlyScopedNode, parseCache, searchIdentifier);
					if (results != null)
						returnedResults.AddRange(results);
				}
			}
			#endregion

			#region Search in further, deeper levels
			else foreach(var rbase in rbases){
					if (declaration is IdentifierDeclaration)
					{
						string searchIdentifier = (declaration as IdentifierDeclaration).Value as string;

						var scanResults = new List<ResolveResult>();
						scanResults.Add(rbase);
						var nextResults=new List<ResolveResult>();

						while (scanResults.Count > 0)
						{
							foreach (var scanResult in scanResults)
							{
								// First filter out all alias and member results..so that there will be only (Static-)Type or Module results left..
								if (scanResult is MemberResult)
								{
									var _m = (scanResult as MemberResult).MemberBaseTypes;
									if(_m!=null)nextResults.AddRange(_m);
								}
								else if (scanResult is AliasResult)
								{
									var _m = (scanResult as AliasResult).AliasDefinition;
									if(_m!=null)nextResults.AddRange(_m);
								}


								else if (scanResult is TypeResult)
								{
									var results = HandleNodeMatches(
										ScanNodeForIdentifier((scanResult as TypeResult).ResolvedTypeDefinition, searchIdentifier, parseCache),
										currentlyScopedNode, parseCache, searchIdentifier, rbase);
									if (results != null)
										returnedResults.AddRange(results);
								}
								else if (scanResult is ModuleResult)
								{
									var modRes = (scanResult as ModuleResult);

									if (modRes.IsOnlyModuleNamePartTyped())
									{
										var modNameParts = modRes.ResolvedModule.ModuleName.Split('.');

										if (modNameParts[modRes.AlreadyTypedModuleNameParts] == searchIdentifier)
										{
											returnedResults.Add(new ModuleResult()
											{
												ResolvedModule = modRes.ResolvedModule,
												AlreadyTypedModuleNameParts = modRes.AlreadyTypedModuleNameParts + 1,
												ResultBase = modRes
											});
										}
									}
									else
									{
										var results = HandleNodeMatches(
										ScanNodeForIdentifier((scanResult as ModuleResult).ResolvedModule, searchIdentifier, parseCache),
										currentlyScopedNode, parseCache, searchIdentifier, rbase);
										if (results != null)
											returnedResults.AddRange(results);
									}
								}
								else if (scanResult is StaticTypeResult)
								{

								}
							}

							scanResults = nextResults;
							nextResults = new List<ResolveResult>();
						}
					}
			}
			#endregion

			return returnedResults.Count > 0 ? returnedResults.ToArray() : null;
		}

		public static TypeResult[] ResolveBaseClass(DClassLike ActualClass, IEnumerable<IAbstractSyntaxTree> ModuleCache)
		{
			if (ActualClass == null || (ActualClass.BaseClasses.Count < 1 && ActualClass.Name.ToLower() == "object"))
				return null;

			var ret = new List<TypeResult>();
			// Implicitly set the object class to the inherited class if no explicit one was done
			if (ActualClass.BaseClasses.Count < 1)
			{
				foreach (var i in ResolveType(new IdentifierDeclaration("Object"), ActualClass.NodeRoot as IBlockNode, ModuleCache))
					if (i is TypeResult)
						ret.Add(i as TypeResult);
			}
			else // Take the first only (since D enforces single inheritance)
			{
				foreach (var i in ResolveType(ActualClass.BaseClasses[0], ActualClass.NodeRoot as IBlockNode, ModuleCache))
					if (i is TypeResult)
						ret.Add(i as TypeResult);
			}
			return ret.Count > 0 ? ret.ToArray() : null;
		}

		/// <summary>
		/// Scans through the node. Also checks if n is a DClassLike or an other kind of type node and checks their specific child and/or base class nodes.
		/// </summary>
		/// <param name="n"></param>
		/// <param name="name"></param>
		/// <param name="parseCache">Needed when trying to search base classes</param>
		/// <returns></returns>
		public static IEnumerable<INode> ScanNodeForIdentifier(IBlockNode curScope, string name, IEnumerable<IAbstractSyntaxTree> parseCache)
		{
			var matches = new List<INode>();
			foreach (var n in curScope)
			{
				if (n.Name == name)
					matches.Add(n);
			}

			// If our current Level node is a class-like, also attempt to search in its baseclass!
			if (curScope is DClassLike)
			{
				var baseClasses = ResolveBaseClass(curScope as DClassLike, parseCache);
				if (baseClasses != null)
					foreach (var i in baseClasses)
					{
						var baseClass = i as TypeResult;
						if (baseClass == null)
							continue;
						// Search for items called name in the base class(es)
						var r = ScanNodeForIdentifier(baseClass.ResolvedTypeDefinition, name, parseCache);

						if (r != null)
							matches.AddRange(r);
					}
			}

			// Check parameters
			if (curScope is DMethod)
				foreach (var ch in (curScope as DMethod).Parameters)
				{
					if (name == ch.Name)
						matches.Add(ch);
				}

			// and template parameters
			if (curScope is DNode && (curScope as DNode).TemplateParameters != null)
				foreach (var ch in (curScope as DNode).TemplateParameters)
				{
					if (name == ch.Name)
						matches.Add(new TemplateParameterNode(ch));
				}

			return matches;
		}

		static IEnumerable<ResolveResult> HandleNodeMatches(IEnumerable<INode> matches, 
			IBlockNode currentlyScopedNode, 
			IEnumerable<IAbstractSyntaxTree> parseCache, 
			string searchIdentifier="",
			ResolveResult resultBase = null)
		{
			foreach (var m in matches)
			{
				if (m is DVariable)
				{
					var v = m as DVariable;

					if (v.IsAlias)
						yield return new AliasResult()
						{
							AliasDefinition = ResolveType(v.Type, currentlyScopedNode, parseCache),
							ResultBase=resultBase
						};
					else
						yield return new MemberResult()
						{
							ResolvedMember = m,
							MemberBaseTypes = ResolveType(v.Type, currentlyScopedNode, parseCache),
							ResultBase = resultBase
						};
				}
				else if (m is DMethod)
				{
					var method = m as DMethod;

					yield return new MemberResult()
					{
						ResolvedMember = m,
						MemberBaseTypes = ResolveType(method.Type, currentlyScopedNode, parseCache),
						ResultBase = resultBase
					};
				}
				else if (m is DClassLike)
				{
					var Class = m as DClassLike;

					yield return new TypeResult()
					{
						ResolvedTypeDefinition = Class,
						BaseClass = ResolveBaseClass(Class, parseCache),
						ResultBase = resultBase
					};
				}
				else if (m is IAbstractSyntaxTree)
					yield return new ModuleResult()
					{
						ResolvedModule = m as IAbstractSyntaxTree,
						AlreadyTypedModuleNameParts=1,
						ResultBase = resultBase
					};
			}
		}

		static readonly BitArray sigTokens = DTokens.NewSet(
			DTokens.If,
			DTokens.Foreach,
			DTokens.Foreach_Reverse,
			DTokens.With,
			DTokens.Try,
			DTokens.Catch,
			DTokens.Finally,

			DTokens.Cast // cast(...) myType << Show cc popup after a cast
			);

		/// <summary>
		/// Checks if an identifier is about to be typed. Therefore, we assume that this identifier hasn't been typed yet. 
		/// So, we also will assume that the caret location is the start of the identifier;
		/// </summary>
		public static bool IsTypeIdentifier(string code, int caret)
		{
			//try{
				if (caret < 1)
					return false;

				code = code.Insert(caret, " "); // To ensure correct behaviour, insert a phantom ws after the caret

				// Check for preceding letters
				if (char.IsLetter(code[caret]))
					return true;

				int precedingExpressionOrTypeStartOffset = ReverseParsing.SearchExpressionStart(code, caret);

				if (precedingExpressionOrTypeStartOffset >= caret)
					return false;

				var expressionCode = code.Substring(precedingExpressionOrTypeStartOffset, caret - precedingExpressionOrTypeStartOffset);

				if (string.IsNullOrEmpty(expressionCode) || expressionCode.Trim() == string.Empty)
					return false;

				var lx = new DLexer(new StringReader(expressionCode));

				var firstToken = lx.NextToken();

				if (DTokens.ClassLike[firstToken.Kind])
					return true;

				while (lx.LookAhead.Kind != DTokens.EOF)
					lx.NextToken();

				var lastToken = lx.CurrentToken;

				if (lastToken.Kind == DTokens.Times)
					return false; // TODO: Check if it's an expression or not

				if (lastToken.Kind == DTokens.CloseSquareBracket || lastToken.Kind == DTokens.Identifier)
					return true;

				if (lastToken.Kind == DTokens.CloseParenthesis)
				{
					lx.CurrentToken = firstToken;

					while (lx.LookAhead.Kind != DTokens.OpenParenthesis && lx.LookAhead.Kind != DTokens.EOF)
						lx.NextToken();

					if (sigTokens[lx.CurrentToken.Kind])
						return false;
					else
						return true;
				}

			//}catch(Exception ex) { }
			return false;
		}
	}

	/// <summary>
	/// Helper class for e.g. finding the initial offset of a statement.
	/// </summary>
	public class ReverseParsing
	{
		static IList<string> preParenthesisBreakTokens = new List<string> { "if", "while", "for", "foreach", "foreach_reverse", "with", "try", "catch", "finally", "synchronized", "pragma" };

		public static int SearchExpressionStart(string Text, int CaretOffset)
		{
			if (CaretOffset > Text.Length)
				throw new ArgumentOutOfRangeException("CaretOffset", "Caret offset must be smaller than text length");
			else if (CaretOffset == Text.Length)
				Text += ' ';

			// At first we only want to find the beginning of our identifier list
			// later we will pass the text beyond the beginning to the parser - there we parse all needed expressions from it
			int IdentListStart = -1;

			/*
			T!(...)>.<
			 */

			int isComment = 0;
			bool isString = false, expectDot = false, hadDot = true;
			var bracketStack = new Stack<char>();

			var identBuffer = "";
			bool hadBraceOpener = false;
			int lastBraceOpenerOffset = 0;

			bool stopSeeking = false;

			// Step backward
			for (int i = CaretOffset; i >= 0 && !stopSeeking; i--)
			{
				IdentListStart = i;
				var c = Text[i];
				var str = Text.Substring(i);
				char p = ' ';
				if (i > 0) p = Text[i - 1];

				// Primitive comment check
				if (!isString && c == '/' && (p == '*' || p == '+'))
					isComment++;
				if (!isString && isComment > 0 && (c == '+' || c == '*') && p == '/')
					isComment--;

				// Primitive string check
				//TODO: "blah">.<
				if (isComment < 1 && c == '"' && p != '\\')
					isString = !isString;

				// If string or comment, just continue
				if (isString || isComment > 0)
					continue;

				// If between brackets, skip
				if (bracketStack.Count > 0 && c != bracketStack.Peek())
					continue;

				// Bracket check
				if (hadDot)
					switch (c)
					{
						case ']':
							bracketStack.Push('[');
							continue;
						case ')':
							bracketStack.Push('(');
							continue;
						case '}':
							if (bracketStack.Count < 1)
							{
								IdentListStart++;
								stopSeeking = true;
								continue;
							}
							bracketStack.Push('{');
							continue;

						case '[':
						case '(':
						case '{':
							if (bracketStack.Count > 0 && bracketStack.Peek() == c)
							{
								bracketStack.Pop();
								if (p == '!') // Skip template stuff
									i--;
							}
							else if (c == '{')
							{
								stopSeeking = true;
								IdentListStart++;
							}
							else
							{
								lastBraceOpenerOffset = IdentListStart;
								// e.g. foo>(< bar| )
								hadBraceOpener = true;
								identBuffer = "";
							}
							continue;
					}

				// whitespace check
				if (Char.IsWhiteSpace(c)) { if (hadDot) expectDot = false; else expectDot = true; continue; }

				if (c == '.')
				{
					hadBraceOpener = false;
					identBuffer = "";
					expectDot = false;
					hadDot = true;
					continue;
				}

				/*
				 * abc
				 * abc . abc
				 * T!().abc[]
				 * def abc.T
				 */
				if (Char.IsLetterOrDigit(c) || c == '_')
				{
					hadDot = false;

					if (!expectDot)
					{
						identBuffer += c;

						if (!hadBraceOpener)
							continue;
						else if (!preParenthesisBreakTokens.Contains(identBuffer))
							continue;
						else
							IdentListStart = lastBraceOpenerOffset;
					}
				}

				IdentListStart++;
				stopSeeking = true;
			}

			return IdentListStart;
		}
	}
}
