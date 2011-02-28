﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_IDE.Core;
using ICSharpCode.AvalonEdit.CodeCompletion;
using Parser.Core;
using D_Parser;
using D_IDE.D.CodeCompletion;

namespace D_IDE.D
{
	public class DCodeCompletionSupport
	{
		public static DCodeCompletionSupport Instance = new DCodeCompletionSupport();

		public bool IsIdentifierChar(char key)
		{
			return char.IsLetterOrDigit(key) || key == '_';
		}

		public bool CanShowCompletionWindow(DEditorDocument EditorDocument)
		{
			return false; // While cc isn't available, disable cc functionality
		}

		public void BuildCompletionData(DEditorDocument EditorDocument, IList<ICSharpCode.AvalonEdit.CodeCompletion.ICompletionData> l, string EnteredText)
		{
			if (EnteredText == ".")
			{
				l.Add(new DCompletionData( new DVariable() { Name="myVar", Description="A description for myVar"}));
			}
		}

		public void BuildToolTip(DEditorDocument EditorDocument, ToolTipRequestArgs ToolTipRequest)
		{
			int offset = EditorDocument.Editor.Document.GetOffset(ToolTipRequest.Line, ToolTipRequest.Column);

			if (!ToolTipRequest.InDocument||
					DCodeResolver.Commenting.IsInCommentAreaOrString(EditorDocument.Editor.Text,offset)) 
				return;

			DToken tk=null;
			var id=DCodeResolver.BuildIdentifierList(EditorDocument.Editor.Text,
				offset, false, out tk);

			if (id == null)
				return;

			var types=DCodeResolver.ResolveTypeDeclarations(
				DCodeResolver.SearchBlockAt(EditorDocument.SyntaxTree,new CodeLocation(ToolTipRequest.Column,ToolTipRequest.Line)),
				id,ResolveImportedModules(EditorDocument));

			string tt = "";

			//TODO: Build well-formatted tool tip string
			if(types!=null)
				foreach(var n in types)
					tt+=n.Type.ToString() +" "+ n.Name+"\r\n";

			ToolTipRequest.ToolTipContent = tt.Trim();
		}

		public bool IsInsightWindowTrigger(char key)
		{
			return key == '(' || key==',';
		}

		public IEnumerable<IAbstractSyntaxTree> ResolveImportedModules(DEditorDocument Editor)
		{
			var availableNodes = EnumAvailableModules(Editor).ToArray();
			return DCodeResolver.ResolveImports(Editor.SyntaxTree, availableNodes);
		}

		public IEnumerable<IAbstractSyntaxTree> EnumAvailableModules(DEditorDocument Editor)
		{
			return EnumAvailableModules(Editor.HasProject ? Editor.Project as DProject : null);
		}

		public IEnumerable<IAbstractSyntaxTree> EnumAvailableModules(DProject Project)
		{
			var ret =new List<IAbstractSyntaxTree>();

			if (Project != null)
			{
				// Add all parsed global modules that belong to the project's compiler configuration
				foreach (var astColl in Project.CompilerConfiguration.ASTCache)
					ret.AddRange(astColl);

				// Add all modules that exist in the current solution.
				if (Project.Solution != null)
				{
					foreach (var prj in Project.Solution)
						if (prj is DProject)
							ret.AddRange((prj as DProject).ParsedModules);
				}
				else // If no solution present, add scanned modules of our current project only
					ret.AddRange(Project.ParsedModules);
			}
			else // If no project present, only add the modules of the default compiler configuration
				foreach (var astColl in DSettings.Instance.DMDConfig().ASTCache)
					ret.AddRange(astColl);

			return ret;
		}
	}

	public class DCompletionData : ICompletionData
	{
		public DCompletionData(INode n)
		{
			Node = n;
		}

		public INode Node { get; protected set; }

		public void Complete(ICSharpCode.AvalonEdit.Editing.TextArea textArea, ICSharpCode.AvalonEdit.Document.ISegment completionSegment, EventArgs insertionRequestEventArgs)
		{
			textArea.Document.Replace(completionSegment, Text);
		}

		public object Content
		{
			get { return Text; }
		}

		public object Description
		{
			get { return Node.Description; }
		}

		public System.Windows.Media.ImageSource Image
		{
			get { return null; }
		}

		public double Priority
		{
			get { return 1; }
		}

		public string Text
		{
			get { return Node.Name; }
			protected set { }
		}
	}
}
