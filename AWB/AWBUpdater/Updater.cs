/*
AWBUpdater
Copyright (C) 2007 Sam Reed

This program is free software; you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation; either version 2 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using ICSharpCode.SharpZipLib.Zip;

using System.Text.RegularExpressions;
using System.Net;

namespace AwbUpdater
{
    internal sealed partial class Updater : Form
    {
        string AWBdirectory = "";
        string tempDirectory = "";

        string AWBZipName = "";
        string AWBWebAddress = "";

        string UpdaterZipName = "";
        string UpdaterWebAddress = "";

        IWebProxy proxy;

        bool updaterUpdate;
        bool awbUpdate;
        bool badUpdate;

        public Updater()
        {
            InitializeComponent();

            AWBdirectory = Path.GetDirectoryName(Application.ExecutablePath) + "\\";
            tempDirectory = AWBdirectory + "temp\\";
        }

        /// <summary>
        /// Version of the Updater
        /// </summary>
        public static string AssemblyVersion
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
        }

        private void Updater_Load(object sender, EventArgs e)
        {
            tmrTimer.Enabled = true;
        }

        /// <summary>
        /// Main program function
        /// </summary>
        private void UpdateAwb()
        {
            try
            {
                proxy = WebRequest.GetSystemWebProxy();

                if (proxy.IsBypassed(new Uri("http://en.wikipedia.org")))
                    proxy = null;

                updateUI("Getting Current AWB and Updater Versions");
                AWBversion();

                if ((!updaterUpdate && !awbUpdate) && string.IsNullOrEmpty(AWBWebAddress))
                    ExitEarly();
                else
                {
                    updateUI("Creating a Temporary Directory");
                    CreateTempDir();

                    updateUI("Downloading AWB");
                    GetAwbFromInternet();

                    updateUI("Unzipping AWB to the Temporary Directory");
                    UnzipAwb();

                    updateUI("Making Sure AWB is Closed");
                    CloseAwb();

                    updateUI("Copying AWB Files from temp to AWB Directory");
                    CopyFiles();
                    MessageBox.Show("AWB Update Successful", "Update Successful");

                    updateUI("Starting AWB");
                    StartAwb();

                    updateUI("Cleaning up from Update");
                    KillTempDir();

                    Application.Exit();
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.Handle(ex);
            }
        }

        /// <summary>
        /// Multiple use function to update the GUI items
        /// </summary>
        /// <param name="currentStatus">What the updater is currently doing</param>
        private void updateUI(string currentStatus)
        {
            lblCurrentTask.Text = currentStatus;
            Application.DoEvents();
        }

        /// <summary>
        /// Close the updater early due to lack of updates
        /// </summary>
        private void ExitEarly()
        {
            MessageBox.Show("Nothing to Update. The Updater will now close");
            StartAwb();
            Application.Exit();
        }

        /// <summary>
        /// Checks and compares the current AWB version with the version listed on the checkpage
        /// </summary>
        private void AWBversion()
        {
            string text = "";

            try
            {
                HttpWebRequest rq = (HttpWebRequest)WebRequest.Create("http://en.wikipedia.org/w/index.php?title=Wikipedia:AutoWikiBrowser/CheckPage/Version&action=raw");

                rq.Proxy = proxy;

                rq.UserAgent = "AWBUpdater " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

                HttpWebResponse response = (HttpWebResponse)rq.GetResponse();

                Stream stream = response.GetResponseStream();
                StreamReader sr = new StreamReader(stream, Encoding.UTF8);

                text = sr.ReadToEnd();

                sr.Close();
                stream.Close();
                response.Close();
            }
            catch
            {
                MessageBox.Show("Error fetching current version number.");
                Application.Exit();
            }

            Match m_awbversion = Regex.Match(text, @"<!-- Current version: (.*?) -->");
            Match m_awbnewest = Regex.Match(text, @"<!-- Newest version: (.*?) -->");
            Match m_updversion = Regex.Match(text, @"<!-- Updater version: (.*?) -->");

            if ((m_awbversion.Success && m_awbversion.Groups[1].Value.Length == 4) || (m_awbnewest.Success && m_awbnewest.Groups[1].Value.Length == 4))
            {
                try
                {
                    awbUpdate = updaterUpdate = false;
                    FileVersionInfo versionAWB = FileVersionInfo.GetVersionInfo(AWBdirectory + "AutoWikiBrowser.exe");

                    if ((Convert.ToInt32(versionAWB.FileVersion.Replace(".", ""))) < (Convert.ToInt32(m_awbversion.Groups[1].Value)))
                        awbUpdate = true;
                    else if ((Convert.ToInt32(versionAWB.FileVersion.Replace(".", "")) >= (Convert.ToInt32(m_awbversion.Groups[1].Value))) &&
                        ((Convert.ToInt32(versionAWB.FileVersion.Replace(".", "")) < (Convert.ToInt32(m_awbnewest.Groups[1].Value)))) &&
                        MessageBox.Show("There is an Optional Update to AutoWikiBrowser. Would you like to Upgrade?", "Optional Update", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        awbUpdate = true;

                    if (awbUpdate)
                    {
                        AWBZipName = "AutoWikiBrowser" + m_awbnewest.Groups[1].Value.Replace(".", "") + ".zip";
                        AWBWebAddress = "http://downloads.sourceforge.net/autowikibrowser/" + AWBZipName;
                    }
                    else if (m_updversion.Success && m_updversion.Groups[1].Value.Length == 4 &&
                        Convert.ToInt32(m_updversion.Groups[1].Value) > Convert.ToInt32(AssemblyVersion.Replace(".", "")))
                    {
                        UpdaterZipName = "AWBUpdater" + m_updversion.Groups[1].Value.Replace(".", "") + ".zip";
                        UpdaterWebAddress = "http://downloads.sourceforge.net/autowikibrowser/" + UpdaterZipName;
                        updaterUpdate = true;
                    }
                }
                catch
                { MessageBox.Show("Unable to find AutoWikiBrowser.exe to query Version No."); }

                progressUpdate.Value = 30;
            }
        }

        /// <summary>
        /// Creates the temporary folder if it doesnt already exist
        /// </summary>
        private void CreateTempDir()
        {
            if (!Directory.Exists(tempDirectory))
                Directory.CreateDirectory(tempDirectory);

            progressUpdate.Value = 35;
        }

        /// <summary>
        /// Check the addresses for the files are valid (not null or empty), and downloads the files from the internet
        /// </summary>
        private void GetAwbFromInternet()
        {
            System.Net.WebClient client = new System.Net.WebClient();
            client.Proxy = proxy;

            if (!string.IsNullOrEmpty(AWBWebAddress))
                client.DownloadFile(AWBWebAddress, tempDirectory + AWBZipName);
            else if (!string.IsNullOrEmpty(UpdaterWebAddress))
                client.DownloadFile(UpdaterWebAddress, tempDirectory + UpdaterZipName);

            client.Dispose();

            progressUpdate.Value = 50;
        }

        /// <summary>
        /// Checks the zip files exist and calls the functions to unzip them
        /// </summary>
        private void UnzipAwb()
        {
            if (!string.IsNullOrEmpty(AWBZipName) && File.Exists(tempDirectory + AWBZipName))
                Extract(AWBZipName);

            if (!string.IsNullOrEmpty(UpdaterZipName) && File.Exists(tempDirectory + UpdaterZipName))
                Extract(UpdaterZipName);

            if (badUpdate)
            {
                MessageBox.Show(@"Something has gone wrong with the downloading of the files. Please restart the updater to try again.

AWBUpdater will now close!");
                Application.Exit();
            }

            progressUpdate.Value = 70;
        }

        /// <summary>
        /// Code used to unzip the zip files to the temporary directory
        /// </summary>
        /// <param name="File"></param>
        private void Extract(string File)
        {
            try
            {
                FastZip zip = new FastZip();

                zip.ExtractZip(File, tempDirectory, null);
            }
            catch { badUpdate = true; }
        }

        /// <summary>
        /// Looks if AWB or IRCM are open. If they are, tell the user
        /// </summary>
        private void CloseAwb()
        {
            bool awbOpen = false;

            do
            {
                foreach (Process p in Process.GetProcesses())
                {
                    awbOpen = (p.ProcessName == "AutoWikiBrowser" || p.ProcessName == "IRCMonitor");
                    if (awbOpen)
                    {
                        MessageBox.Show("Please save your settings (if you wish) and close " + p.ProcessName + " completely before pressing OK.");
                        break;
                    }
                }
            }
            while (awbOpen);

            progressUpdate.Value = 75;
        }

        /// <summary>
        /// Copies files from the temporary to the working directory
        /// </summary>
        private void CopyFiles()
        {
            if (File.Exists(tempDirectory + "AWBUpdater.exe"))
                File.Copy(tempDirectory + "AWBUpdater.exe", AWBdirectory + "AWBUpdater.exe.new", true);

            if (awbUpdate)
            {
                File.Copy(tempDirectory + "AutoWikiBrowser.exe", AWBdirectory + "AutoWikiBrowser.exe", true);
                File.Copy(tempDirectory + "AutoWikiBrowser.exe.config", AWBdirectory + "AutoWikiBrowser.exe.config", true);

                File.Copy(tempDirectory + "WikiFunctions.dll", AWBdirectory + "WikiFunctions.dll", true);
                File.Copy(tempDirectory + "IRCMonitor.exe", AWBdirectory + "IRCMonitor.exe", true);

                if (File.Exists(tempDirectory + "Diff.dll"))
                    File.Copy(tempDirectory + "Diff.dll", AWBdirectory + "Diff.dll", true);

                if (File.Exists(AWBdirectory + "Wikidiff2.dll"))
                    File.Delete(AWBdirectory + "Wikidiff2.dll");

                if (File.Exists(AWBdirectory + "gpl-2.0.txt"))
                    File.Delete(AWBdirectory + "gpl-2.0.txt");

                if (File.Exists(AWBdirectory + "gpl-3.0.txt"))
                    File.Delete(AWBdirectory + "gpl-3.0.txt");

                if (File.Exists(tempDirectory + "COPYING"))
                    File.Copy(tempDirectory + "COPYING", AWBdirectory + "COPYING", true);

                if (File.Exists(AWBdirectory + "CFD.dll"))
                    File.Copy(tempDirectory + "Plugins\\CFD\\CFD.dll", AWBdirectory + "CFD.dll", true);

                if (!Directory.Exists(AWBdirectory + "\\Plugins\\CFD"))
                    Directory.CreateDirectory(AWBdirectory + "\\Plugins\\CFD");

                File.Copy(tempDirectory + "Plugins\\CFD\\CFD.dll", AWBdirectory + "Plugins\\CFD\\CFD.dll", true);

                if (File.Exists(AWBdirectory + "IFD.dll"))
                    File.Copy(tempDirectory + "Plugins\\IFD\\IFD.dll", AWBdirectory + "IFD.dll", true);

                if (!Directory.Exists(AWBdirectory + "\\Plugins\\IFD"))
                    Directory.CreateDirectory(AWBdirectory + "\\Plugins\\IFD");

                File.Copy(tempDirectory + "Plugins\\IFD\\IFD.dll", AWBdirectory + "Plugins\\IFD\\IFD.dll", true);

                if (File.Exists(AWBdirectory + "Kingbotk AWB Plugin.dll"))
                    File.Copy(tempDirectory + "Plugins\\Kingbotk\\Kingbotk AWB Plugin.dll", AWBdirectory + "Kingbotk AWB Plugin.dll", true);

                if (!Directory.Exists(AWBdirectory + "Plugins\\Kingbotk"))
                    Directory.CreateDirectory(AWBdirectory + "Plugins\\Kingbotk");

                File.Copy(tempDirectory + "Plugins\\Kingbotk\\Kingbotk AWB Plugin.dll", AWBdirectory + "Plugins\\Kingbotk\\Kingbotk AWB Plugin.dll", true);

                if (File.Exists(AWBdirectory + "WikiFunctions2.dll"))
                    File.Delete(AWBdirectory + "WikiFunctions2.dll");

                if (File.Exists(AWBdirectory + "WPAssessmentsCatCreator.dll"))
                    File.Copy(tempDirectory + "Plugins\\WPAssessmentsCatCreator\\WPAssessmentsCatCreator.dll", AWBdirectory + "WPAssessmentsCatCreator.dll", true);

                if (!Directory.Exists(AWBdirectory + "Plugins\\WPAssessmentsCatCreator"))
                    Directory.CreateDirectory(AWBdirectory + "Plugins\\WPAssessmentsCatCreator");

                File.Copy(tempDirectory + "Plugins\\WPAssessmentsCatCreator\\WPAssessmentsCatCreator.dll", AWBdirectory + "Plugins\\WPAssessmentsCatCreator\\WPAssessmentsCatCreator.dll", true);
            }
            progressUpdate.Value = 90;
        }

        /// <summary>
        /// Starts AWB if exists and is not already running
        /// </summary>
        private void StartAwb()
        {
            bool awbOpen = false;
            foreach (Process p in Process.GetProcesses())
            {
                awbOpen = (p.ProcessName == "AutoWikiBrowser");

                if (awbOpen)
                    break;
            }

            if (!awbOpen && System.IO.File.Exists(AWBdirectory + "AutoWikiBrowser.exe") && MessageBox.Show("Would you like to Start AWB?", "Start AWB?", MessageBoxButtons.YesNo) == DialogResult.Yes)
                System.Diagnostics.Process.Start(AWBdirectory + "AutoWikiBrowser.exe");

            progressUpdate.Value = 95;
        }

        /// <summary>
        /// Deletes the temporary directory
        /// </summary>
        private void KillTempDir()
        {
            Directory.Delete(tempDirectory, true);
            progressUpdate.Value = 100;
        }

        private void tmrTimer_Tick(object sender, EventArgs e)
        {
            tmrTimer.Enabled = false;
            UpdateAwb();
        }
    }
}