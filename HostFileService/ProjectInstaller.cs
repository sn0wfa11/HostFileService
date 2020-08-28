using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.Threading.Tasks;
using System.ServiceProcess;
using System.IO;

namespace HostFileService
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        string host_file_path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers/etc/hosts");

        public ProjectInstaller()
        {
            InitializeComponent();
        }

        private void serviceInstaller1_AfterInstall(object sender, InstallEventArgs e)
        {
            ServiceController sc = new ServiceController(serviceInstaller1.ServiceName);
            sc.Start();
        }

        /// <summary>
        /// At uninstall, this function is used to remove the comments the service adds to the host file.
        /// Nicer Cleanup.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void serviceInstaller1_AfterUninstall(object sender, InstallEventArgs e)
        {
            WriteHostFile(GetHostFileLines());
        }

        /// <summary>
        /// Reads the host file and removes the comments that were put in by the service.
        /// </summary>
        /// <returns>lines of hostfile</returns>
        List<string> GetHostFileLines()
        {
            List<string> service_comments = new List<string>();
            service_comments.Add("# *+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*");
            service_comments.Add("# THIS HOST FILE IS MANAGED BY HostFileService!");
            service_comments.Add("# Using Registry Values in \\HKEY_LOCAL_MACHINE\\System\\CurrentControlSet\\Services\\HostFileService\\hosts");
            service_comments.Add("# To add a host, make a new string entry with Name: <IP>,<hostname>. No data is needed.");
            service_comments.Add("# Host file will be updated within set interval, at service restart, or reboot.");
            service_comments.Add("# See https://github.com/sn0wfa11/HostFileService for more information.");
            service_comments.Add("# *+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*");

            List<string> lines = new List<string>();
            string line;
            try
            {
                StreamReader file = File.OpenText(host_file_path);

                while ((line = file.ReadLine()) != null)
                    if (!service_comments.Contains(line.Trim()))
                        lines.Add(line);
                file.Close();
            }
            catch (Exception error)
            {
                // Do nothing here
            }
            return lines;
        }

        /// <summary>
        /// Writes the host file
        /// </summary>
        /// <param name="lines"></param>
        void WriteHostFile(List<string> lines)
        {
            bool is_read_only = false;
            try
            {
                // Check if host file is read only.  If it is, remove the attibute to make it writable.
                FileAttributes attributes = File.GetAttributes(host_file_path);
                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    is_read_only = true;
                    attributes = RemoveAttribute(attributes, FileAttributes.ReadOnly);
                    File.SetAttributes(host_file_path, attributes);
                }

                StreamWriter writer = new StreamWriter(host_file_path, false);
                foreach (string line in lines)
                    writer.WriteLine(line);
                writer.Close();

                // If host file was read only, set it back to that.
                if (is_read_only)
                {
                    attributes = File.GetAttributes(host_file_path);
                    attributes = AddAttribute(attributes, FileAttributes.ReadOnly);
                    File.SetAttributes(host_file_path, attributes);
                }
            }
            catch (Exception error)
            {
                // Do nothing here
            }
        }

        /// <summary>
        /// Removes a file attribute
        /// </summary>
        /// <param name="attributes"></param>
        /// <param name="attributesToRemove"></param>
        /// <returns>FileAttributes</returns>
        private FileAttributes RemoveAttribute(FileAttributes attributes, FileAttributes attributesToRemove)
        {
            return attributes & ~attributesToRemove;
        }

        /// <summary>
        /// Adds a file attribute
        /// </summary>
        /// <param name="attributes"></param>
        /// <param name="attributesToAdd"></param>
        /// <returns>FileAttributes</returns>
        private FileAttributes AddAttribute(FileAttributes attributes, FileAttributes attributesToAdd)
        {
            return attributes | attributesToAdd;
        }

    }
}
