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
using System.Windows.Navigation;
using System.Windows.Shapes;
using D_IDE.Core;
using System.Collections.ObjectModel;

namespace D_IDE.D
{
	/// <summary>
	/// Interaktionslogik für Page_General.xaml
	/// </summary>
	public partial class DPrjSettingsPage :AbstractProjectSettingsPage
	{
		public DPrjSettingsPage()
		{
			InitializeComponent();
			List_Libs.ItemsSource = Libs;
		}

		public override bool ApplyChanges(Project prj)
		{
			var p = prj as DProject;

			if (p == null)
				return true;

			p.DMDVersion = Combo_DVersion.SelectedIndex == 0 ? DVersion.D1 : DVersion.D2;

			p.IsRelease = Check_Release.IsChecked.Value;

			p.LinkedLibraries.Clear();
			p.LinkedLibraries.AddRange(Libs);

			return true;
		}
		
		public override string SettingCategoryName
		{
			get { return "D Settings"; }
		}

		public override void LoadCurrent(Project prj)
		{
			var p = prj as DProject;

			if (p == null)
				return;

			if (p.DMDVersion == DVersion.D1)
				Combo_DVersion.SelectedIndex = 0;
			else Combo_DVersion.SelectedIndex = 1;

			Check_Release.IsChecked = p.IsRelease;

			Libs.Clear();
			foreach(var l in p.LinkedLibraries)
				Libs.Add(l);

			if(Libs.Count>0)
				List_Libs.SelectedIndex = 0;

		}

		ObservableCollection<string> Libs = new ObservableCollection<string>();
		private void List_Libs_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			text_CurLib.Text = List_Libs.SelectedValue as string;
		}

		private void button_AddLib_Click(object sender, RoutedEventArgs e)
		{
			if(!string.IsNullOrWhiteSpace(text_CurLib.Text))
			Libs.Add(text_CurLib.Text);
			text_CurLib.Text = "";
		}

		private void button_ApplyLib_Click(object sender, RoutedEventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(text_CurLib.Text))
				Libs[List_Libs.SelectedIndex]=text_CurLib.Text;
		}

		private void button_DeleteLib_Click(object sender, RoutedEventArgs e)
		{
			Libs.RemoveAt(List_Libs.SelectedIndex);
		}
	}
}