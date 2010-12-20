﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.ComponentModel;
using Parser.Core;
using D_IDE.Core;
using System.Collections;
using System.Reflection;
using Microsoft.Win32;
using System.IO;

namespace D_IDE.Dialogs
{
	/// <summary>
	/// Interaktionslogik für NewProjectDlg.xaml
	/// </summary>
	public partial class NewSrcDlg : Window, INotifyPropertyChanged
	{
		#region Properties
		string _FileName;
		SourceFileType _FileType;

		public string FileName 
		{ 
			get { return _FileName; }
			set	{
				_FileName = value;
				PropChanged("CreationAllowed");
			}
		}

		public ILanguageBinding SelectedLanguageBinding		{get {return List_Languages.SelectedItem as ILanguageBinding;}	}
		public SourceFileType SelectedFileType		{			
			get { 
				return _FileType; 
			}
			set
			{
				if (SelectedFileType != null)
				{
					string defExt = (SelectedFileType.Extensions!=null&& SelectedFileType.Extensions.Length>0)? SelectedFileType.Extensions[0]:"";
					string DummyName = SelectedFileType.DefaultFilePrefix + "1"+defExt;

					if (String.IsNullOrEmpty(FileName) || FileName == DummyName)
					{
						FileName = value.DefaultFilePrefix + "1"+
							((value.Extensions != null && value.Extensions.Length > 0) ? value.Extensions[0] : "");
						PropChanged("FileName");
					}
				}
				_FileType = value;
			}
		}



		public object Languages		{			get			{				return LanguageLoader.Bindings;			}		}
		public object FileTypes
		{
			get
			{
				var o = SelectedLanguageBinding;
				if (o != null)
					return o.ModuleTypes;
				return null;
			}
		}
		
		#endregion

		public NewSrcDlg()
		{
			DataContext = this;
			InitializeComponent();

			if (List_Languages.Items.Count > 0)
				List_Languages.SelectedIndex = 0;

			if (List_FileTypes.Items.Count > 0)
			{
				_FileType = List_FileTypes.Items[0] as SourceFileType;
				List_FileTypes.SelectedIndex = 0;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
		public void PropChanged(string name)
		{
			if (PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(name));
		}

		private void View_Languages_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			PropChanged("FileTypes");

			if (List_FileTypes.Items.Count > 0)
			{
				SelectedFileType = List_FileTypes.Items[0] as SourceFileType;
				//List_FileTypes.SelectedIndex = 0;
			}
		}

		/// <summary>
		/// Validates all input fields and returns true if everything's ok
		/// </summary>
		public bool CreationAllowed
		{
			get { 
				return 
					SelectedLanguageBinding!=null &&
					SelectedFileType!=null&&
					!String.IsNullOrEmpty(FileName) &&
					// Primitive file name validation
					FileName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars())<0;
			}
		}

		private void OK_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
		}
	}
}
