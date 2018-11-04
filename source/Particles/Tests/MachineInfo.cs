using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;

namespace Tests
{
    public enum OperatingSystem
    {
        Windows,
        Linux,
        MacOS,
        Other,
        Unknown
    }

    /// <summary>
    /// Information about a computer.
    /// </summary>
    public struct MachineInfo : IEquatable<MachineInfo>
    {
        private readonly string label;
        private readonly string hostname;
        private readonly double clockFrequency;
        private readonly int numHardwareThreads;
        private readonly long memory;
        private readonly OperatingSystem os;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Tests.MachineInfo"/> struct.
        /// </summary>
        /// <param name="label">The label representing the machine.</param>
        /// <param name="hostname">The hostname by which this machine is identified.</param>
        /// <param name="clockFrequency">The clock frequence of the processor, in Hz.</param>
        /// <param name="numHardwareThreads">The number of hardware threads.</param>
        /// <param name="memory">The size of the working memory of the machine, in bytes.</param>
        /// <param name="os">The operating system</param>
        public MachineInfo(string label, string hostname, double clockFrequency, int numHardwareThreads, long memory, OperatingSystem os)
        {
            this.clockFrequency = clockFrequency;
            this.numHardwareThreads = numHardwareThreads;
            this.memory = memory;
            this.label = label;
            this.hostname = hostname;
            this.os = os;
        }

        /// <summary>
        /// The clock frequence of the processor, in Hz.
        /// </summary>
        public double ClockFrequency
        {
            get { return clockFrequency; }
        }
        /// <summary>
        /// The number of hardware threads.
        /// </summary>
        public int HardwareThreads
        {
            get { return numHardwareThreads; }
        }
        /// <summary>
        /// The size of the working memory of the machine, in bytes.
        /// </summary>
        public long MemorySize
        {
            get { return memory; }
        }

        /// <summary>
        /// The operating system of the machine.
        /// </summary>
        public OperatingSystem OperatingSystem
        {
            get{
                return this.os;
            }
        }

        /// <summary>
        /// The label representing the machine.
        /// </summary>
        public string Label
        {
            get { return label; }
        }

        /// <summary>
        /// The hostname by which this machine is identified.
        /// </summary>
        public string Hostname
        {
            get { return hostname; }
        }

        /// <summary>
        /// Determines the operating system executing this call.
        /// </summary>
        private static OperatingSystem GetOperatingSystem()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return OperatingSystem.Windows;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return OperatingSystem.MacOS;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return OperatingSystem.Linux;

            return OperatingSystem.Other;
        }


        /// <summary>
        /// Describes the machine that is executing this call.
        /// </summary>
        /// <param name="label">The label to be used for the running machine.</param>
        /// <returns>Information about the machine that is executing this call.</returns>
        public static MachineInfo GetRunning(string label="[Running]")
        {
            var os = GetOperatingSystem();

            int hwc = 0;
            string name = "";
            long ms = 0;
            double clockSpeed = 0.0;
            switch (os)
            {
                case OperatingSystem.Linux:
                    // Hostname:
                    var hostnameInfo = new ProcessStartInfo("hostname");
                    hostnameInfo.RedirectStandardOutput = true;
                    using (var p = Process.Start(hostnameInfo))
                        name = p.StandardOutput.ReadToEnd();

                    // Memory size:
                    using (var sr = new StreamReader("/proc/meminfo"))
                        while (!sr.EndOfStream){
                            var line = sr.ReadLine();
                            if (line.StartsWith("MemTotal:", StringComparison.InvariantCulture))
                            {
                                var cells = line.Split();
                                Debug.Assert(cells.Length == 3);
                                Debug.Assert(cells[2] == "kB");
                                ms = long.Parse(cells[1]);
                                break;
                            }
                        }

                    // Max. CPU speed:
                    using (var sr = new StreamReader("cat /sys/devices/system/cpu/cpu*/cpufreq/scaling_max_freq"))
                        while (!sr.EndOfStream)
                        {
                            clockSpeed = double.Parse(sr.ReadLine());
                            hwc++;
                        }
                    break;
                default:
                    throw new NotImplementedException(string.Format("Retrieval of machine information has not been implemented for {0}!", os));
            }

            return new MachineInfo(label, name, clockSpeed, hwc, ms, os);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is MachineInfo))
                return false;

            var other = (MachineInfo)obj;
            return this.Equals(other);
        }

        public bool Equals(MachineInfo other)
        {
            return this.label.Equals(other.label)
                       && this.hostname.Equals(other.hostname) 
                       && this.clockFrequency.Equals(other.clockFrequency) 
                       && this.numHardwareThreads.Equals(other.numHardwareThreads)
                       && this.memory.Equals(other.memory)
                       && this.os.Equals(other.os);
        }

        public override int GetHashCode()
        {
            return hostname.GetHashCode();
        }

        public override string ToString()
        {
            return label;
        }

    }
}
