using System;
using System.Collections.Generic;
using System.Management;
using System.Windows.Forms;
using CoreAudioApi;

namespace AudioSwitch
{
    public partial class Form1 : Form
    {
        public bool firstShow = true;
        public bool onBattery = false;
        public bool dragResize = false;
        public AudioDevice onlineAudioDevice = new AudioDevice("", "");
        public AudioDevice offlineAudioDevice = new AudioDevice("", "");
        public ManagementEventWatcher managementEventWatcher;
        public readonly Dictionary<string, string> powerValues = new Dictionary<string, string>
                         {
                             {"4", "Entering Suspend"},
                             {"7", "Resume from Suspend"},
                             {"10", "Power Status Change"},
                             {"11", "OEM Event"},
                             {"18", "Resume Automatic"}
                         };

        public Form1()
        {
            InitializeComponent();
            // Start power event watcher
            InitPowerEvents();
            logView.AppendText(DateTime.Now.ToString() + " : App Launch\n");
            // Check current power status
            PowerLineStatus status = SystemInformation.PowerStatus.PowerLineStatus;
            if (status == PowerLineStatus.Offline)
            {
                statusLabel.Text = "Power status : On Battery";
                logEvent("Power status : On Battery\n", false);
                onBattery = true;
            }
            else
            {
                statusLabel.Text = "Power status : Plugged In";
                logEvent("Power status : Plugged In\n", false);
                onBattery = false;
            }
            string selectedOnlineDevice = Properties.Settings.Default.onlineDevice;
            string selectedOfflineDevice = Properties.Settings.Default.offlineDevice;
            // Get list of all audio devices
            GetAllAudioDevices(true);
            restoreSelection(selectedOfflineDevice, selectedOnlineDevice);
        }

        private void logEvent(string text, bool printTime)
        {
            logView.Invoke((MethodInvoker)delegate
            {
                if (printTime)
                {
                    logView.AppendText($"\n{DateTime.Now} : {text}");
                }
                else
                {
                    logView.AppendText(text);
                }

            });
        }

        private void InitPowerEvents()
        {
            var q = new WqlEventQuery();
            var scope = new ManagementScope("root\\CIMV2");

            q.EventClassName = "Win32_PowerManagementEvent";
            managementEventWatcher = new ManagementEventWatcher(scope, q);
            managementEventWatcher.EventArrived += PowerEventArrive;
            managementEventWatcher.Start();
        }

        private void PowerEventArrive(object sender, EventArrivedEventArgs e)
        {
            foreach (PropertyData pd in e.NewEvent.Properties)
            {
                if (pd == null || pd.Value == null) continue;
                var name = powerValues.ContainsKey(pd.Value.ToString())
                               ? powerValues[pd.Value.ToString()]
                               : pd.Value.ToString();
                if (name == powerValues["10"])
                {
                    getPowerInfo();
                }
            }
        }

        public void getPowerInfo()
        {
            PowerLineStatus status = SystemInformation.PowerStatus.PowerLineStatus;
            if (status == PowerLineStatus.Offline && !onBattery)
            {
                statusLabel.Invoke((MethodInvoker)delegate
                {
                    statusLabel.Text = "Power status : On Battery";
                });
                logEvent("Power Status Change -> On Battery\n", true);
                onBattery = true;
                if (string.Compare(onlineAudioDevice.ID, offlineAudioDevice.ID, System.StringComparison.CurrentCultureIgnoreCase) != 0)
                {
                    SetAudioDevice(offlineAudioDevice);
                    GetAllAudioDevices();
                    if (notifycheck.Checked)
                    {
                        notifyIcon1.ShowBalloonTip(3000, "On Battery", $"Audio Device Change -> {offlineAudioDevice.Name}", ToolTipIcon.Info);
                    }
                }
                else
                {
                    logEvent("No Audio Device Change\n", true);
                }
            }
            else if (status == PowerLineStatus.Online && onBattery)
            {
                statusLabel.Invoke((MethodInvoker)delegate
                {
                    statusLabel.Text = "Power status : Plugged In";
                });
                logEvent("Power Status Change -> Plugged In\n", true);
                onBattery = false;
                if (string.Compare(onlineAudioDevice.ID, offlineAudioDevice.ID, System.StringComparison.CurrentCultureIgnoreCase) != 0)
                {
                    SetAudioDevice(onlineAudioDevice);
                    GetAllAudioDevices();
                    if (notifycheck.Checked)
                    {
                        notifyIcon1.ShowBalloonTip(3000, "Plugged In", $"Audio Device Change -> {onlineAudioDevice.Name}", ToolTipIcon.Info);
                    }
                }
                else
                {
                    logEvent("No Audio Device Change\n", true);
                }
            }
        }

        public void scrollToEnd()
        {
            if (logView.TextLength > 0)
            {
                logView.SelectionStart = logView.TextLength - 1;
                logView.SelectionLength = 0;
                logView.ScrollToCaret();
            }
        }

        private void bootcheck_CheckedChanged(object sender, EventArgs e)
        {
            Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (bootcheck.Checked)
            {
                rk.SetValue("AudioSwitch", Application.ExecutablePath);
            }
            else
            {
                rk.DeleteValue("AudioSwitch", false);
            }
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Show();
            this.WindowState = Properties.Settings.Default.wState;
            this.Location = Properties.Settings.Default.wLoc;
            this.Size = Properties.Settings.Default.wSize;
            notifyIcon1.Visible = false;
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            if (firstShow)
            {
                this.WindowState = FormWindowState.Minimized;
                Hide();
                notifyIcon1.ShowBalloonTip(4000);
            }
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                Properties.Settings.Default.wSize = this.RestoreBounds.Size;
                Properties.Settings.Default.wLoc = this.RestoreBounds.Location;
                Hide();
                notifyIcon1.Visible = true;
            }
            else if (this.WindowState == FormWindowState.Maximized)
            {
                Properties.Settings.Default.wState = FormWindowState.Maximized;
                scrollToEnd();
            }
            else if (this.WindowState == FormWindowState.Normal && !dragResize)
            {
                Properties.Settings.Default.wState = FormWindowState.Normal;
                Properties.Settings.Default.wSize = this.Size;
                Properties.Settings.Default.wLoc = this.Location;
                scrollToEnd();
            }
        }

        private void Form1_ResizeBegin(object sender, EventArgs e)
        {
            dragResize = true;
        }

        private void Form1_ResizeEnd(object sender, EventArgs e)
        {
            dragResize = false;
            if (this.WindowState == FormWindowState.Normal)
            {
                Properties.Settings.Default.wState = FormWindowState.Normal;
                Properties.Settings.Default.wSize = this.Size;
                Properties.Settings.Default.wLoc = this.Location;
                scrollToEnd();
            }
        }

        private void Form1_LocationChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.wLoc = this.Location;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (rk.GetValue("AudioSwitch") != null)
            {
                bootcheck.Checked = true;
            }
            notifycheck.Checked = Properties.Settings.Default.notifyOnChange;
            if (Properties.Settings.Default.wState == FormWindowState.Maximized)
            {
                this.WindowState = Properties.Settings.Default.wState;
            }
            else
            {
                this.Size = Properties.Settings.Default.wSize;
                this.Location = Properties.Settings.Default.wLoc;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            managementEventWatcher.Stop();
            Properties.Settings.Default.offlineDevice = offlineAudioDevice.Name + " | " + offlineAudioDevice.ID;
            Properties.Settings.Default.onlineDevice = onlineAudioDevice.Name + " | " + onlineAudioDevice.ID;
            Properties.Settings.Default.Save();
        }

        private string getID(string device)
        {
            return device.Substring(device.IndexOf("|") + 1).Trim();
        }

        private string getName(string device)
        {
            return device.Substring(0, device.IndexOf("|")).Trim();
        }

        private void restoreSelection(string selectedOfflineDevice, string selectedOnlineDevice)
        {
            bool offlineFound = false;
            bool onlineFound = false;
            string offlineID = getID(selectedOfflineDevice);
            string offlineName = getName(selectedOfflineDevice);
            string onlineID = getID(selectedOnlineDevice);
            string onlineName = getName(selectedOnlineDevice);
            for (int i = 0; i < offlineSelect.Items.Count; i++)
            {
                string ID = getID(offlineSelect.Items[i].ToString());
                string Name = getName(offlineSelect.Items[i].ToString());
                if (ID == offlineID && Name == offlineName)
                {
                    offlineSelect.SelectedItem = selectedOfflineDevice;
                    offlineFound = true;
                }
                if (ID == onlineID && Name == onlineName)
                {
                    onlineSelect.SelectedItem = selectedOnlineDevice;
                    onlineFound = true;
                }
            }
            if (offlineFound == false || onlineFound == false)
            {
                firstShow = false;
            }
        }

        private void GetAllAudioDevices(bool update = false)
        {
            // Create a new MMDeviceEnumerator
            MMDeviceEnumerator DevEnum = new MMDeviceEnumerator();
            // Create a MMDeviceCollection of every devices that are enabled
            MMDeviceCollection DeviceCollection = DevEnum.EnumerateAudioEndPoints(EDataFlow.eRender, EDeviceState.DEVICE_STATE_ACTIVE);

            logEvent("\n----------------------------\nAudio Devices\n----------------------------\n", false);

            // For every MMDevice in DeviceCollection
            for (int i = 0; i < DeviceCollection.Count; i++)
            {
                // If this MMDevice's ID is either the same the default playback device's ID, or the same as the default recording device's ID
                if (DeviceCollection[i].ID == DevEnum.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia).ID)
                {
                    // Log device details
                    logEvent($"Index : {i + 1}\nDefault : True\nType : Playback\nName : {DeviceCollection[i].FriendlyName}\nID : {DeviceCollection[i].ID}\n\n", false);
                }
                else
                {
                    // Log device details
                    logEvent($"Index : {i + 1}\nDefault : False\nType : Playback\nName : {DeviceCollection[i].FriendlyName}\nID : {DeviceCollection[i].ID}\n\n", false);
                }
                if (update)
                {
                    offlineSelect.Items.Add(DeviceCollection[i].FriendlyName + " | " + DeviceCollection[i].ID);
                    onlineSelect.Items.Add(DeviceCollection[i].FriendlyName + " | " + DeviceCollection[i].ID);
                }
            }
        }

        private void SetAudioDevice(AudioDevice device)
        {
            bool found = false;
            // Create a new MMDeviceEnumerator
            MMDeviceEnumerator DevEnum = new MMDeviceEnumerator();
            // Create a MMDeviceCollection of every devices that are enabled
            MMDeviceCollection DeviceCollection = DevEnum.EnumerateAudioEndPoints(EDataFlow.eRender, EDeviceState.DEVICE_STATE_ACTIVE);

            for (int i = 0; i < DeviceCollection.Count; i++)
            {
                // If this MMDevice's ID is the same as the string received by the ID parameter
                if (string.Compare(DeviceCollection[i].ID, device.ID, System.StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    // Create a new audio PolicyConfigClient
                    PolicyConfigClient client = new PolicyConfigClient();
                    // Using PolicyConfigClient, set the given device as the default communication device (for its type)
                    client.SetDefaultEndpoint(DeviceCollection[i].ID, ERole.eCommunications);
                    // Using PolicyConfigClient, set the given device as the default device (for its type)
                    client.SetDefaultEndpoint(DeviceCollection[i].ID, ERole.eMultimedia);

                    // Log the result of changing the audio device
                    logEvent($"Audio Device Change -> {DeviceCollection[i].FriendlyName} [{DeviceCollection[i].ID}]\n", true);
                    found = true;
                }
            }
            if (!found)
            {
                // Show an error message about the received ID not being found
                logEvent("No matching AudioDevice found\n", true);
            }
        }

        private void pauseButton_Click(object sender, EventArgs e)
        {
            if (pauseButton.Text == "Pause")
            {
                pauseButton.Text = "Resume";
                managementEventWatcher.Stop();
                logEvent("AudioSwitch paused\n", true);
            }
            else
            {
                pauseButton.Text = "Pause";
                managementEventWatcher.Start();
                logEvent("AudioSwitch resumed\n", true);
            }
        }

        private void refreshButton_Click(object sender, EventArgs e)
        {
            GetAllAudioDevices(true);
        }

        private void notifycheck_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.notifyOnChange = notifycheck.Checked;
        }

        private void offlineSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            string val = offlineSelect.SelectedItem.ToString();
            offlineAudioDevice.ID = getID(val);
            offlineAudioDevice.Name = getName(val);
        }

        private void onlineSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            string val = onlineSelect.SelectedItem.ToString();
            onlineAudioDevice.ID = getID(val);
            onlineAudioDevice.Name = getName(val);
        }
    }

    public class AudioDevice
    {
        // ID of the MMDevice ex: "{0.0.0.00000000}.{c4aadd95-74c7-4b3b-9508-b0ef36ff71ba}"
        public string ID;
        // Name of the MMDevice ex: "Speakers (Realtek High Definition Audio)"
        public string Name;

        public AudioDevice(string ID, string Name)
        {
            // Set this object's ID to that of the received MMDevice's ID
            this.ID = ID;

            // Set this object's Name to that of the received MMDevice's FriendlyName
            this.Name = Name;
        }
    }

    /*public class AudioDevice
    {
        // Order in which this MMDevice appeared from MMDeviceEnumerator
        public int Index;
        // Default (for its Type) is either true or false
        public bool Default;
        // Type is either "Playback" or "Recording"
        public string Type;
        // Name of the MMDevice ex: "Speakers (Realtek High Definition Audio)"
        public string Name;
        // ID of the MMDevice ex: "{0.0.0.00000000}.{c4aadd95-74c7-4b3b-9508-b0ef36ff71ba}"
        public string ID;
        // The MMDevice itself
        public MMDevice Device;

        // To be created, a new AudioDevice needs an Index, and the MMDevice it will communicate with
        public AudioDevice(int Index, MMDevice BaseDevice, bool Default = false)
        {
            // Set this object's Index to the received integer
            this.Index = Index;

            // Set this object's Default to the received boolean
            this.Default = Default;

            // If the received MMDevice is a playback device
            if (BaseDevice.DataFlow == EDataFlow.eRender)
            {
                // Set this object's Type to "Playback"
                this.Type = "Playback";
            }
            // If not, if the received MMDevice is a recording device
            else if (BaseDevice.DataFlow == EDataFlow.eCapture)
            {
                // Set this object's Type to "Recording"
                this.Type = "Recording";
            }

            // Set this object's Name to that of the received MMDevice's FriendlyName
            this.Name = BaseDevice.FriendlyName;

            // Set this object's Device to the received MMDevice
            this.Device = BaseDevice;

            // Set this object's ID to that of the received MMDevice's ID
            this.ID = BaseDevice.ID;
        }
    }*/
}
