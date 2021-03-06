﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using DIDE.Installer;
using System.Diagnostics;
namespace TestInstallerHelper
{
    public partial class TestForm : Form
    {
		public TestForm()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string file = @".\D.config.xml";
            //if (File.Exists(file)) File.Delete(file);
            InstallerHelper.CreateConfigurationFile(file);

            InstallerHelper.Refresh();
			InstallerHelper.Initialize(Path.GetTempFileName());
            while (InstallerHelper.IsThreadActive)
            {
                Write(".");
                Application.DoEvents();
                Refresh();
            } WriteLine("DONE");

            WriteLine("Latest (online) DMD 1 Url       --> " + InstallerHelper.GetLatestDMD1Url());
            WriteLine("Latest (online) DMD 1 Version   --> " + InstallerHelper.GetLatestDMD1Version());
            WriteLine("Local (installed) DMD 1 Path    --> " + InstallerHelper.GetLocalDMD1Path());
            WriteLine("Local (installed) DMD 1 Version --> " + InstallerHelper.GetLocalDMD1Version());
            WriteLine("Latest (online) DMD 2 Url       --> " + InstallerHelper.GetLatestDMD2Url());
            WriteLine("Latest (online) DMD 2 Version   --> " + InstallerHelper.GetLatestDMD2Version());
            WriteLine("Local (installed) DMD 2 Path    --> " + InstallerHelper.GetLocalDMD2Path());
            WriteLine("Local (installed) DMD 2 Version --> " + InstallerHelper.GetLocalDMD2Version());
            WriteLine("Local Path Valid DMD 1          --> " + InstallerHelper.IsValidDMDInstallForVersion(1, InstallerHelper.GetLocalDMD1Path()));
            WriteLine("Local Path Valid DMD 2          --> " + InstallerHelper.IsValidDMDInstallForVersion(2, InstallerHelper.GetLocalDMD2Path()));
            WriteLine("Generated Config File           --> " + file);
            WriteLine("----------------------------------------------------------------------------------");
            if (File.Exists(file)) WriteLine(File.ReadAllText(file));
            WriteLine("----------------------------------------------------------------------------------");

            var env = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);
            foreach (string key in env.Keys) WriteLine(key);
            WriteLine("----------------");
            env = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process);
            foreach (string key in env.Keys) WriteLine(key);
            WriteLine("----------------");


            env = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process);
            if (env.Contains("Path"))
            {
                string pathString = env["Path"].ToString();
                //WriteLine(pathString);
                string[] folders = pathString.Split(';');
                foreach (var f in folders)
                    if (f.IndexOf("dmd", StringComparison.CurrentCultureIgnoreCase) >= 0)
                        WriteLine(f);
            }
            env = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);
            if (env.Contains("Path"))
            {
                string pathString = env["Path"].ToString();
                string[] folders = pathString.Split(';');
                foreach (var f in folders)
                    if (f.IndexOf(@"dmd\windows\bin", StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                        f.IndexOf(@"dmd2\windows\bin", StringComparison.CurrentCultureIgnoreCase) >= 0)
                        WriteLine(f);
                foreach (var f in folders)
                    if (f.IndexOf(@"dmd\windows\bin", StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                        f.IndexOf(@"dmd2\windows\bin", StringComparison.CurrentCultureIgnoreCase) >= 0)
                        WriteLine(f);
            }

			InstallerHelper.CreateConfigurationFile(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\D-IDE.config\D.config.xml");
			WriteLine(InstallerHelper.IsConfigurationValid(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\D-IDE.config\D.config.xml"));
        }

        private void TestForm_Load(object sender, EventArgs e)
        {

        }

        private void Write(string s, params object[] args)
        {
            if (args.Length > 0) textBox1.AppendText(string.Format(s, args));
            else textBox1.AppendText(s);
        }

        private void WriteLine(string s, params object[] args) 
        {
            Write(s, args);
            textBox1.AppendText(Environment.NewLine);
        }
    }
}
