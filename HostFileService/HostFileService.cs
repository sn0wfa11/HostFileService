using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Timers;
using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Win32;

namespace HostFileService
{
    public partial class HostFileService : ServiceBase
    {
        EventLog eventLog1;
        private int eventId = 1;
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(System.IntPtr handle, ref ServiceStatus serviceStatus);

        private string service_registry_path = @"System\CurrentControlSet\Services\HostFileService";
        private string host_registry_path = @"System\CurrentControlSet\Services\HostFileService\hosts";
        private string host_file_path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers/etc/hosts");
        private double default_interval = 1; //interval in minutes
        private Timer timer;

        /// <summary>
        /// Constructor
        /// Setup Logging
        /// </summary>
        public HostFileService()
        {
            InitializeComponent();
            eventLog1 = new EventLog();
            if (!EventLog.SourceExists("HostFileService"))
            {
                EventLog.CreateEventSource(
                    "HostFileService", "Application");
            }
            eventLog1.Source = "HostFileService";
            eventLog1.Log = "Application";
        }

        /// <summary>
        /// OnStart
        /// Set Service status
        /// Initialize Timer
        /// RunUpdate for the first time.
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
            // Update the service state to Start Pending.
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_START_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            // Set up a timer that triggers every minute.
            timer = new Timer();
            timer.Interval = GetTimerInterval();
            timer.Elapsed += new ElapsedEventHandler(this.OnTimer);
            timer.Start();
            RunUpdate();

            // Update the service state to Running.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
        }

        double GetTimerInterval()
        {
            RegistryKey key = Registry.LocalMachine.CreateSubKey(service_registry_path, true);
            if (key.GetValue("Interval") == null)
            {
                key.SetValue("Interval", default_interval, RegistryValueKind.DWord);
                return (default_interval * 60000);
            }
            else
                return (Convert.ToDouble(key.GetValue("Interval", default_interval)) * 60000);
        }

        /// <summary>
        /// Update Event ID and RunUpdate on timer trigger
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        public void OnTimer(object sender, ElapsedEventArgs args)
        {
            eventId++;
            RunUpdate();
            double new_interval = GetTimerInterval();
            if (timer.Interval != new_interval)
                timer.Interval = new_interval;
        }

        /// <summary>
        /// RunUpdate Function
        /// Merges host file entries and registry entries if this is the fist time running the service.
        /// Updates the host file with registry entries if needed when service has been configured already.
        /// </summary>
        private void RunUpdate()
        {
            try
            {
                RegistryKey key = Registry.LocalMachine.CreateSubKey(service_registry_path, true);
                if (Convert.ToBoolean(Convert.ToUInt16(key.GetValue("Configured", 0))))
                    Update();
                else
                {
                    if (Merge())
                        key.SetValue("Configured", 1, RegistryValueKind.DWord);
                    else
                        eventLog1.WriteEntry("Merge Failed", EventLogEntryType.Error, eventId);
                }
            }
            catch (Exception e)
            {
                eventLog1.WriteEntry("Unable to Update: " + e, EventLogEntryType.Error, eventId);
            }
        }

        /// <summary>
        /// Returns a string list of host file entries
        /// </summary>
        /// <returns>String List</returns>
        private List<string> Get_Hosts_Hostfile()
        {
            string line;
            List<string> raw_lines = new List<string>();
            List<string> hosts = new List<string>();
            List<string> line_entries;
            string entry;
            string output_line;

            try
            {
                StreamReader file = File.OpenText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers/etc/hosts"));

                while ((line = file.ReadLine()) != null)
                    if ((!line.StartsWith("#")) && !(line.Trim().Length == 0))
                        raw_lines.Add(line);
                file.Close();
            }
            catch (IOException e)
            {
                eventLog1.WriteEntry("The file could not be opened: " + e, EventLogEntryType.Error, eventId);
            }

            foreach (string raw_line in raw_lines)
            {
                output_line = "";
                line_entries = raw_line.Split(null).ToList();
                foreach (string line_entry in line_entries)
                {
                    entry = line_entry.Trim();
                    if (entry != "")
                    {
                        if (output_line == "")
                            output_line = entry;
                        else
                            output_line += "," + entry;
                    }
                }
                hosts.Add(output_line);
            }
            return hosts;
        }

        /// <summary>
        /// Returns a string list of the host file comments
        /// </summary>
        /// <returns>String List</returns>
        private List<string> Get_Hostsfile_Comments()
        {
            List<string> lines = new List<string>();
            string line;
            try
            {
                StreamReader file = File.OpenText(host_file_path);

                while ((line = file.ReadLine()) != null)
                    if (line.StartsWith("#"))
                        lines.Add(line);
                file.Close();
            }
            catch (Exception e)
            {
                eventLog1.WriteEntry("The file could not be opened: " + e, EventLogEntryType.Error, eventId);
            }
            return lines;
        }

        /// <summary>
        /// Writes host entries to the host file.
        /// </summary>
        /// <param name="hosts"></param>
        /// <returns>True if success, false if not</returns>
        private bool Write_Hosts_Hostfile(List<string> hosts)
        {
            bool is_read_only = false;
            List<string> output = Get_Hostsfile_Comments();
            
            if (!output.Contains("# THIS HOST FILE IS MANAGED BY HostFileService!"))
            {
                output.Add("# *+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*");
                output.Add("# THIS HOST FILE IS MANAGED BY HostFileService!");
                output.Add("# Using Registry Values in \\HKEY_LOCAL_MACHINE\\System\\CurrentControlSet\\Services\\HostFileService\\hosts");
                output.Add("# To add a host, make a new string entry with Name: <IP>,<hostname>. No data is needed.");
                output.Add("# Host file will be updated within set interval, at service restart, or reboot.");
                output.Add("# See https://github.com/sn0wfa11/HostFileService for more information.");
                output.Add("# *+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*+*");
            }
            
            foreach (string host in hosts)
                output.Add(String.Join("\t", host.Split(',')));
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
                foreach (string line in output)
                    writer.WriteLine(line);
                writer.Close();

                // If host file was read only, set it back to that.
                if (is_read_only)
                {
                    attributes = File.GetAttributes(host_file_path);
                    attributes = AddAttribute(attributes, FileAttributes.ReadOnly);
                    File.SetAttributes(host_file_path, attributes);
                }
                return true;
            }
            catch (Exception e)
            {
                eventLog1.WriteEntry("The file could not be opened: " + e, EventLogEntryType.Error, eventId);
                return false;
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

        /// <summary>
        /// Gets the host entries from the registry as a string list
        /// </summary>
        /// <returns>String List</returns>
        private List<string> Get_Hosts_Registry()
        {
            try
            {
                RegistryKey key = Registry.LocalMachine.OpenSubKey(host_registry_path);
                return (key.GetValueNames().ToList());
            }
            catch (Exception)
            {
                return (new List<string>());
            }
        }

        /// <summary>
        /// Writes the host entries to the registry
        /// </summary>
        /// <param name="hosts"></param>
        /// <returns>True if success, false if not</returns>
        private bool Write_Hosts_Registry(List<string> hosts)
        {
            try
            {
                RegistryKey key = Registry.LocalMachine.CreateSubKey(host_registry_path, true);
                foreach (string host in hosts)
                    key.SetValue(host, "");
                return true;
            }
            catch (Exception e)
            {
                eventLog1.WriteEntry("Error:" + e, EventLogEntryType.Error, eventId);
                return false;
            }
        }

        /// <summary>
        /// Builds a master list of host entries for the merge function
        /// </summary>
        /// <param name="hostfile_hosts"></param>
        /// <param name="registry_hosts"></param>
        /// <returns>String List</returns>
        private List<string> Build_Master_Hosts(List<string> hostfile_hosts, List<string> registry_hosts)
        {
            List<string> output = hostfile_hosts;
            foreach (string entry in registry_hosts)
                if (!output.Contains(entry))
                    output.Add(entry);
            return output;
        }

        /// <summary>
        /// Merges the host entries and writes master list to both host file and registry
        /// </summary>
        /// <returns>true if success, false if not</returns>
        private bool Merge()
        {
            List<string> master_hosts = Build_Master_Hosts(Get_Hosts_Hostfile(), Get_Hosts_Registry());
            return (Write_Hosts_Registry(master_hosts) && Write_Hosts_Hostfile(master_hosts));
        }

        /// <summary>
        /// Checks if there are updates to do.
        /// If so it updates the host file with the entries from the registry.
        /// </summary>
        /// <returns>true if success, false if not</returns>
        private bool Update()
        {
            List<string> registry_hosts = Get_Hosts_Registry();
            List<string> hostfile_hosts = Get_Hosts_Hostfile();

            if (!ListSame(registry_hosts, hostfile_hosts))
            {
                if (Write_Hosts_Hostfile(registry_hosts))
                    return true;
                else
                    return false;                
            }
            else
                return true;
        }

        /// <summary>
        /// Checks to see if two string lists are the same or not.
        /// </summary>
        /// <param name="list1"></param>
        /// <param name="list2"></param>
        /// <returns>true if same, false if not</returns>
        private bool ListSame(List<string> list1, List<string> list2)
        {
            if (list1.Count != list2.Count)
                return false;

            foreach (string item in list1)
                if (!list2.Contains(item))
                    return false;

            return true;
        }

        /// <summary>
        /// What to do when the service stops
        /// </summary>
        protected override void OnStop()
        {
            //empty
        }

        /// <summary>
        /// Service State Settings
        /// </summary>
        public enum ServiceState
        {
            SERVICE_STOPPED = 0x00000001,
            SERVICE_START_PENDING = 0x00000002,
            SERVICE_STOP_PENDING = 0x00000003,
            SERVICE_RUNNING = 0x00000004,
            SERVICE_CONTINUE_PENDING = 0x00000005,
            SERVICE_PAUSE_PENDING = 0x00000006,
            SERVICE_PAUSED = 0x00000007,
        }

        /// <summary>
        /// Service Status Settings
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ServiceStatus
        {
            public int dwServiceType;
            public ServiceState dwCurrentState;
            public int dwControlsAccepted;
            public int dwWin32ExitCode;
            public int dwServiceSpecificExitCode;
            public int dwCheckPoint;
            public int dwWaitHint;
        };
    }
}
