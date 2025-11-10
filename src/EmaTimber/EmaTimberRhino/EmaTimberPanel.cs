using EmaTimber;
using Eto.Drawing;
using Eto.Forms;
using Rhino.UI;
using Rhino.UI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace EmaTimberRhino
{
    /// <summary>
    /// Required class GUID, used as the panel Id
    /// </summary>
    [System.Runtime.InteropServices.Guid("819f6787-5222-454b-9baa-56434be2ed3b")]
    public class EmaTimberPanel : Panel, IPanel
    {
        readonly uint m_document_sn = 0;

        /// <summary>
        /// Provide easy access to the SampleCsEtoPanel.GUID
        /// </summary>
        public static System.Guid PanelId => typeof(EmaTimberPanel).GUID;

        /// <summary>
        /// Required public constructor with NO parameters
        /// </summary>
        public EmaTimberPanel(uint documentSerialNumber)
        {
            m_document_sn = documentSerialNumber;

            Title = GetType().Name;

            var hello_button = new Button { Text = "Hello..." };
            hello_button.Click += (sender, e) => OnHelloButton();

            var deviceStartButton = new Button { Text = "Start" };
            var deviceStopButton = new Button { Text = "Stop" };

            var child_button = new Button { Text = "Child Dialog..." };
            child_button.Click += (sender, e) => OnChildButton();

            var LaserPortDropDown = new DropDown { Tag = "Port" };
            LaserPortDropDown.DropDownOpening += LaserPortDropDown_DropDownOpening;
            LaserPortDropDown.SelectedKeyChanged += LaserPortDropDown_SelectedKeyChanged;

            var CncDropDown = new DropDown();
            CncDropDown.DropDownOpening += CncDropDown_DropDownOpening;
            CncDropDown.SelectedIndexChanged += CncDropDown_SelectedIndexChanged;


            var document_sn_label = new Label() { Text = $"Document serial number: {documentSerialNumber}" };

            var layout = new DynamicLayout { DefaultSpacing = new Size(5, 5), Padding = new Padding(10) };
            layout.BeginVertical();
            layout.AddRow(new Label { Text = "Laser port" }, LaserPortDropDown);
            layout.AddRow(new Label { Text = "CNC device" }, CncDropDown);
            layout.EndVertical();
            layout.AddSeparateRow(document_sn_label, null);
            layout.AddSeparateRow(hello_button, null);
            layout.AddRow(new Divider());
            layout.AddSeparateRow(new Label { Text = "Device" });
            layout.AddSeparateRow(deviceStartButton, deviceStopButton, null);
            layout.Add(null);
            Content = layout;
        }

        private void CncDropDown_SelectedIndexChanged(object sender, EventArgs e)
        {
            var dropdown = (DropDown)sender;
            if (string.IsNullOrEmpty(dropdown.SelectedKey))
                dropdown.SelectedIndex = 0;

            if (dropdown.SelectedIndex >= 0)
            {
                ListItem item = dropdown.Items[dropdown.SelectedIndex] as ListItem;
                if (item == null) return;

                ETContext.CncAddress = (PhysicalAddress)item.Tag;
                ETContext.CncFriendlyName = ((DropDown)sender).SelectedKey;
            }
        }

        private void CncDropDown_DropDownOpening(object sender, EventArgs e)
        {
            var dropdown = (DropDown)sender;

            dropdown.Items.Clear();
            var deviceNames = ETContext.GetAvailablePcapDeviceNames();

            foreach (var deviceName in deviceNames)
            {
                dropdown.Items.Add(new ListItem { Key = deviceName.Item1, Text = string.Format("{0} ({1})", deviceName.Item1, deviceName.Item2.ToString()), Tag = deviceName.Item2 });
            }
        }

        private void LaserPortDropDown_SelectedKeyChanged(object sender, EventArgs e)
        {
            var dropdown = (DropDown)sender;
            if (string.IsNullOrEmpty(dropdown.SelectedKey))
                dropdown.SelectedIndex = 0;

            ETContext.LaserPort = ((DropDown)sender).SelectedKey;
        }

        private void LaserPortDropDown_DropDownOpening(object sender, EventArgs e)
        {
            var dropdown = (DropDown)sender;
            dropdown.Items.Clear();
            var port_names = System.IO.Ports.SerialPort.GetPortNames();

            foreach (var port_name in port_names)
            {
                dropdown.Items.Add(new ListItem { Key = port_name, Text = port_name });
            }
        }

        public string Title { get; }

        /// <summary>
        /// Example of proper way to display a message box
        /// </summary>
        protected void OnHelloButton()
        {
            // Use the Rhino common message box and NOT the Eto MessageBox,
            // the Eto version expects a top level Eto Window as the owner for
            // the MessageBox and will cause problems when running on the Mac.
            // Since this panel is a child of some Rhino container it does not
            // have a top level Eto Window.
            Dialogs.ShowMessage("Hello Rhino!", Title);
        }

        /// <summary>
        /// Sample of how to display a child Eto dialog
        /// </summary>
        protected void OnChildButton()
        {
            //var dialog = new SampleCsEtoHelloWorld();
            //dialog.ShowModal(this);
        }

        #region IPanel methods
        public void PanelShown(uint documentSerialNumber, ShowPanelReason reason)
        {
            // Called when the panel tab is made visible, in Mac Rhino this will happen
            // for a document panel when a new document becomes active, the previous
            // documents panel will get hidden and the new current panel will get shown.
            //Rhino.RhinoApp.WriteLine($"Panel shown for document {documentSerialNumber}, this serial number {m_document_sn} should be the same");
        }

        public void PanelHidden(uint documentSerialNumber, ShowPanelReason reason)
        {
            // Called when the panel tab is hidden, in Mac Rhino this will happen
            // for a document panel when a new document becomes active, the previous
            // documents panel will get hidden and the new current panel will get shown.
            Rhino.RhinoApp.WriteLine($"Panel hidden for document {documentSerialNumber}, this serial number {m_document_sn} should be the same");
        }

        public void PanelClosing(uint documentSerialNumber, bool onCloseDocument)
        {
            // Called when the document or panel container is closed/destroyed
            Rhino.RhinoApp.WriteLine($"Panel closing for document {documentSerialNumber}, this serial number {m_document_sn} should be the same");
        }
        #endregion IPanel methods
    }
}

