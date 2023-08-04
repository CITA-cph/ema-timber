using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Rhino;

namespace EmaTimber
{
    public class LaserMeasureEventArgs : EventArgs
    {
        public LaserMeasureEventArgs(double value)
        {
            this.Distance = value;
        }
        public readonly double Distance;
    }

    public class OD2Interface
    {
        public delegate void LaserChangeHandler(object clock, LaserMeasureEventArgs laserInformation);

        // The event we publish
        public event LaserChangeHandler LaserChange;

        // The method which fires the Event
        protected void OnLaserValueChange(object clock, LaserMeasureEventArgs laserInformation)
        {
            // Check if there are any Subscribers
            if (LaserChange != null)
            {
                // Call the Event
                LaserChange(clock, laserInformation);
            }
        }

        public enum Command
        {
            MEASURE,
            START_MEASURE,
            STOP_MEASURE,
            LASER_ON,
            LASER_OFF,
            Q2
        }

        SerialPort port;
        Dictionary<Command, string> commands;
        public double Min { get; private set; }
        public double Max { get; private set; }
        public bool ResetMinMax;

        static TimeSpan CommandTimeout = TimeSpan.FromMilliseconds(100);

        // Recording vars
        public bool IsRecording = false;
        private bool RecordedThisSample = false;
        public int NumHoldingSamples = 20;
        private double RecordedValue = 0.0;
        public double RecordedTolerance = 0.5;
        private int RecordingCounter = 0;
        private List<double> RecordedValues = new List<double>();

        public bool IsOpen { get; private set; }
        public string PortName { get; private set; }
        volatile bool CommandReturn = false;

        public double MinRange = 85.1;
        public double MaxRange = 414.9;

        private double measurevalue;
        public double MeasureValue
        {
            get
            {
                return this.measurevalue;
            }
            private set
            {
                if (value < MinRange || value > MaxRange)
                {
                    LaserMeasureEventArgs li = new LaserMeasureEventArgs(double.NaN);
                    OnLaserValueChange(this, li);
                    this.measurevalue = double.NaN;
                    return;
                }

                this.measurevalue = value;

                if (ResetMinMax)
                {
                    Min = double.PositiveInfinity;
                    Max = 0;
                    ResetMinMax = false;
                }
                else
                {
                    Min = Math.Min(Min, value);
                    if (value < MaxRange)
                        Max = Math.Max(Max, value);
                }

                if (IsRecording)
                {
                    if (Math.Abs(value - RecordedValue) < RecordedTolerance)
                        RecordingCounter++;
                    else
                    {
                        RecordingCounter = 1;
                        RecordedThisSample = false;
                        RecordedValue = value;
                    }

                    if (RecordingCounter % NumHoldingSamples == 0 && !RecordedThisSample)
                    {
                        RhinoApp.WriteLine("RECORDED: " + value.ToString());
                        RecordedValues.Add(value);
                        RecordedThisSample = true;
                    }
                }
                LaserMeasureEventArgs laserInfo = new LaserMeasureEventArgs(value);
                OnLaserValueChange(this, laserInfo);
            }
        }

        public bool ContinuousMeasure = false;

        private string Reply = "";
        private bool Receiving = false;

        private void Error(string message)
        {
            RhinoApp.WriteLine("Error: " + message);
            //Console.ReadKey();
            //throw new Exception(message);
        }

        public OD2Interface(string port_name)
        {
            string[] available_ports = System.IO.Ports.SerialPort.GetPortNames();
            if (!available_ports.Any(port_name.Contains))
            {
                Error("Specified port not found.");
                return;
            }

            PortName = port_name;

            port = new SerialPort(port_name, 9600, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One);
            port.DataReceived += new SerialDataReceivedEventHandler(laser_DataReceived);
            ResetMinMax = true;

            port.Open();
            IsOpen = port.IsOpen;

            if (!port.IsOpen)
            {
                Error("Port failed to open. Check connection.");
                return;
            }

            // Add commands to dictionary so that we can translate between easy key commands and laser-specific commands.
            commands = new Dictionary<Command, string>();
            commands.Add(Command.MEASURE, "\u0002MEASURE\u0003");
            commands.Add(Command.START_MEASURE, "\u0002START_MEASURE\u0003");
            commands.Add(Command.STOP_MEASURE, "\u0002STOP_MEASURE\u0003");
            commands.Add(Command.LASER_ON, "\u0002OFF\u0003");
            commands.Add(Command.LASER_OFF, "\u0002ON\u0003");
            commands.Add(Command.Q2, "\u0002Q2\u0003");

        }

        ~OD2Interface()
        {
            if (port != null && port.IsOpen)
                port.Close();
            port = null;
        }

        public void ClearRecordedValues()
        {
            RecordedValues.Clear();
        }

        /// <summary>
        /// Send command to laser. The 'commands' dictionary translates key commands (i.e. 'm') into 
        /// laser commands (i.e.<STX>MEASURE</STX>) if the right pair is found.
        /// </summary>
        /// <param name="c">Command to send. This must be in the 'commands' dictionary, otherwise nothing will
        /// be sent.</param>
        /// <returns>True if sending was successful. False if the command was not found.</returns>
        public bool SendCommand(string c)
        {
            if (port == null || !port.IsOpen) return false;
            Command cmd;
            if (Enum.TryParse(c, out cmd))
                if (commands.ContainsKey(cmd))
                {
                    //Console.WriteLine("Command: " + commands[c]);
                    port.Write(commands[cmd]);
                    return true;
                }
            RhinoApp.WriteLine("Command not found.");
            return false;
        }

        public double GetMeasure()
        {
            if (!IsOpen) return double.NaN;
            CommandReturn = false;
            port.Write(commands[Command.MEASURE]);
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < CommandTimeout)
            {
                if (CommandReturn)
                    return MeasureValue;
            }
            RhinoApp.WriteLine("Command timed out...");
            return double.NaN;
            //throw new Exception("Command timed out.");
            //return MeasureValue;
        }

        /// <summary>
        /// Stop communicating with laser and close port.
        /// </summary>
        /// <returns>True if closing the port was successful.</returns>
        public bool End()
        {
            IsOpen = false;
            port.Close();
            if (!port.IsOpen)
                return true;
            return false;
        }

        private void ProcessReply()
        {
            if (Reply.Length < 1) return;
            if (Reply == "?")
                RhinoApp.WriteLine("LASER: Catastrophic failure.");
            else if (Reply == ">")
                RhinoApp.WriteLine("LASER: Success.");
            else
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                double res;
                if (double.TryParse(Reply, out res))
                    MeasureValue = res;
            }
            CommandReturn = true;
        }

        /// <summary>
        /// Event handler for handling received data.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void laser_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (ContinuousMeasure)
            {
                string recv = port.ReadExisting();
                if (recv.Length > 0)
                {
                    for (int i = 0; i < recv.Length; ++i)
                    {
                        if (recv[i] == '\u000D')
                        {
                            ProcessReply();
                            Reply = "";
                        }
                        else
                            Reply += recv[i];
                    }
                }
            }
            else
            {
                string recv = port.ReadExisting();

                if (recv.Length < 1) return;
                for (int i = 0; i < recv.Length; ++i)
                {
                    switch(recv[i])
                    {
                        case ('\u0002'):
                            Reply = "";
                            Receiving = true;

                            break;
                        case ('\u0003'):
                            Receiving = false;
                            ProcessReply();
                            break;
                        default:
                            Reply += recv[i];
                            break;
                    }
                }

            }
        }
    }
}
