﻿using System;
using System.Collections.Generic;

using System.Text;
using System.Net;
using System.IO;
using System.Reflection;

namespace DIDE.Installer
{
    internal class DigitalMars
    {
        private const string ERROR_PREFIX = "[ERROR] ";
        private const string DIGITAL_MARS_FTP = "ftp://ftp.digitalmars.com/";
        private const string DIGITAL_MARS_HTTP = "http://ftp.digitalmars.com/";
        //private const string FILE_LIST = "dmd.files.${FILEDATE}.html";

        private static Dictionary<int, CompilerVersion> versions = new Dictionary<int, CompilerVersion>();

        private static FileInfo fi = null;
        private static FileInfo DataFile
        {
            get
            {
                if (fi == null)
                {
                    DateTime now = DateTime.Now;
                    DirectoryInfo d = new DirectoryInfo(Path.GetTempPath());
                    fi = new FileInfo(d.FullName + "\\DigitalMars." + now.Year + "." + now.Month + "." + now.Day + ".txt");
                }
                else fi.Refresh();

                return fi;
            }
        }
        public static string GetLatestDMDInfo(int version, out int subVersion)
        {
            GetDMDInfo();
            subVersion = (version == 1) ? versions[1].SubVersion : versions[2].SubVersion;
            return (version == 1) ? versions[1].Url : versions[2].Url;
        }

        public static void PreloadFromHtmlList(string filename)
        {
            if (versions.Count == 0)
            {
                if (File.Exists(filename))
                {
                    string[] lines = File.ReadAllLines(filename);
                    CompilerVersion ver;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        int idx1 = lines[i].IndexOf("dmd.1.");
                        int idx2 = lines[i].IndexOf("dmd.2.");
                        if (idx1 > 0 || idx2 > 0)
                        {
                            ver = new CompilerVersion();
                            if (ver.FromHtml(lines[i]))
                            {
                                if (!versions.ContainsKey(ver.Version) || versions[ver.Version].SubVersion < ver.SubVersion)
                                {
                                    versions[ver.Version] = ver;
                                }
                            }
                        }
                    }

                    if (!DataFile.Exists && versions.Count == 2)
                    {
                        StringBuilder sb = new StringBuilder();
                        foreach (CompilerVersion v in versions.Values) sb.Append(v.ToString()).Append("\r\n");
                        File.WriteAllText(DataFile.FullName, sb.ToString());
                        DataFile.Refresh();
                    }
                }
            }
        }

        public static void Preload()
        {
            GetDMDInfo();
        }

        private static void GetDMDInfo()
        {
            if (versions.Count == 0)
            {
                bool hasError = false;
                if (DataFile.Exists)
                {
                    string[] lines = File.ReadAllLines(DataFile.FullName);
                    CompilerVersion ver;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        ver = new CompilerVersion();
                        if (ver.FromString(lines[i]))
                            versions[ver.Version] = ver;
                        else
                            hasError = true;
                        if (ver.HasError) hasError = true;
                    }

                    if (hasError)
                    {
                        DataFile.CopyTo(DataFile.FullName + "." + DateTime.Now.ToBinary() + ".log");
                        DataFile.Delete();
                        DataFile.Refresh();
                    }
                }

                if (!DataFile.Exists)
                {
                    CompilerVersion ver1 = new CompilerVersion(), ver2 = new CompilerVersion();
                    int latestVer1 = 56, latestVer2 = 41, subVersion = 0;
                    string url1 = "", url2 = "";
                    ver1.Version = 1;
                    ver2.Version = 2;
                    try
                    {
                        FtpWebRequest request = WebRequest.Create(DIGITAL_MARS_FTP) as FtpWebRequest;
                        request.Method = WebRequestMethods.Ftp.ListDirectory;

                        using (FtpWebResponse response = request.GetResponse() as FtpWebResponse)
                        {
							/*
							 * Enum all files in the ftp root. While scan through all of them, extract the version number, get the highest version number, and finally download the appropriate zip
							 */
                            Console.WriteLine(response.StatusDescription);
                            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                            {
                                while (!reader.EndOfStream)
                                {
                                    string line = reader.ReadLine();
                                    if (line.StartsWith("dmd.1."))
                                    {
                                        string[] tokens = line.Split('.');
                                        if (tokens.Length > 2 && !int.TryParse(tokens[2], out subVersion)) subVersion = latestVer1;

                                        if (tokens[tokens.Length - 1].Equals("zip", StringComparison.CurrentCultureIgnoreCase))
                                        {
                                            if (latestVer1 < subVersion)
                                            {
                                                latestVer1 = subVersion;
                                                url1 = DIGITAL_MARS_HTTP + line;
                                            }
                                        }
                                    }
                                    else if (line.StartsWith("dmd.2."))
                                    {
                                        string[] tokens = line.Split('.');
                                        if (tokens.Length > 2 && !int.TryParse(tokens[2], out subVersion)) subVersion = latestVer2;

                                        if (tokens[tokens.Length - 1].Equals("zip", StringComparison.CurrentCultureIgnoreCase))
                                        {
                                            if (latestVer2 < subVersion)
                                            {
                                                latestVer2 = subVersion;
                                                url2 = DIGITAL_MARS_HTTP + line;
                                            }
                                        }
                                    }
                                }
                            }
                            response.Close();
                        }
                        ver1.SubVersion = latestVer1;
                        ver1.Url = url1;
                        ver2.SubVersion = latestVer2;
                        ver2.Url = url2;
                    }
                    catch (Exception ex)
                    {
                        ver1.Error = ERROR_PREFIX + ex.Message + Environment.NewLine + ex.StackTrace;
                        ver2.Error = ERROR_PREFIX + ex.Message + Environment.NewLine + ex.StackTrace;
                    }
                    versions[1] = ver1;
                    versions[2] = ver2;

                    StringBuilder sb = new StringBuilder();
                    foreach (CompilerVersion ver in versions.Values) sb.Append(ver.ToString()).Append("\r\n");
                    File.WriteAllText(DataFile.FullName, sb.ToString());
                }
            }
        }

        private class CompilerVersion
        {
            public string Error { get; set; }
            public string Url { get; set; }
            public int Version { get; set; }
            public int SubVersion { get; set; }
            public bool FromString(string s)
            {
                string[] items = s.Split('\t');
                if (items.Length == 4)
                {
                    Version = Convert.ToInt32(items[0]);
                    SubVersion = Convert.ToInt32(items[1]);
                    Url = items[2].Trim();
                    Error = items[3].Trim();
                    return true;
                }
                return false;
            }
            public bool FromHtml(string s)
            {
                string start = "href=\"";
                int idx1 = s.IndexOf(start) + start.Length, idx2;
                if (idx1 > 0)
                {
                    idx2 = s.IndexOf('\"', idx1 + 1);
                    if (idx2 > idx1)
                    {
                        string fname = s.Substring(idx1, idx2 - idx1);

                        string[] items = fname.Split('.');
                        if (items.Length == 4)
                        {
                            Version = Convert.ToInt32(items[1]);
                            SubVersion = Convert.ToInt32(items[2]);
                            Url = DIGITAL_MARS_HTTP + fname;
                            Error = string.Empty;
                            return true;
                        }

                    }
                }
                return false;
            }

            public bool HasError
            {
                get { return !string.IsNullOrEmpty(Error); }
            }

            public override string ToString()
            {
                return Version.ToString() + "\t" + 
                    SubVersion.ToString() + "\t" + 
                    (string.IsNullOrEmpty(Url) ? " " : Url) + "\t" + 
                    (string.IsNullOrEmpty(Error) ? " " : Error.Replace('\t', ' '));
            }
        }
    }
}
