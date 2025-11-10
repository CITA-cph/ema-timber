using EmaTimber;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
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
    [System.Runtime.InteropServices.Guid("b6c553d4-fcea-413c-ab53-a10ce397b360")]
    public class SimulationPanel : Panel, IPanel
    {
        readonly uint m_document_sn = 0;
        private TreeGridView treeGridView = null;

        public static System.Guid PanelId => typeof(SimulationPanel).GUID;

        public void Update()
        {
            if (treeGridView == null) return;

            var treeCollection = new TreeGridItemCollection();
            var root = new TreeGridItem { Values = new string[] { "Model" }, Expanded = true };
            var parts = new TreeGridItem { Values = new string[] { "Parts" }, Expanded = true };

            foreach (var kvp in ETContext.SimulationContext.Parts)
            {
                parts.Children.Add(new TreeGridItem { Values = new string[] { kvp.Key.ToString() }, Expanded = true });
            }

            var materials = new TreeGridItem { Values = new string[] { "Materials" }, Expanded = true };
            var steps = new TreeGridItem { Values = new string[] { "Steps" }, Expanded = true };


            root.Children.Add(parts);
            root.Children.Add(materials);
            root.Children.Add(steps);

            treeCollection.Add(root);

            treeGridView.DataStore = treeCollection;
        }

        public SimulationPanel(uint documentSerialNumber)
        {
            m_document_sn = documentSerialNumber;

            Title = GetType().Name;



            treeGridView = new TreeGridView { BackgroundColor = Eto.Drawing.Colors.White};

            treeGridView.Columns.Add(new GridColumn
            {
                HeaderText = "Model",
                DataCell = new TextBoxCell(0),
                AutoSize = true,
                Editable = false
                
            });

            Update();

            var physicalGroupList = new ListBox();
            //physicalGroupList.Load += PhysicalGroupList_Load;
            physicalGroupList.Shown += PhysicalGroupList_Load;
            physicalGroupList.MouseDoubleClick += PhysicalGroupList_MouseDoubleClick;

            var maxElementSizeStepper = new Eto.Forms.NumericStepper();
            maxElementSizeStepper.MinValue = 0;
            maxElementSizeStepper.Value = 100;
            maxElementSizeStepper.Increment = 1;
            maxElementSizeStepper.ValueChanged += MaxElementSizeStepper_ValueChanged;

            var minElementSizeStepper = new Eto.Forms.NumericStepper();
            minElementSizeStepper.MinValue = 0;
            minElementSizeStepper.Increment = 1;
            minElementSizeStepper.ValueChanged += MinElementSizeStepper_ValueChanged;


            var document_sn_label = new Label() { Text = $"Document serial number: {documentSerialNumber}" };

            var layout = new DynamicLayout { DefaultSpacing = new Size(5, 5), Padding = new Padding(10) };
            layout.BeginVertical();

            layout.AddRow(new Label { Text = "Max element size" }, maxElementSizeStepper, null);
            layout.AddRow(new Label { Text = "Min element size" }, minElementSizeStepper, null);
            layout.EndVertical();

            layout.BeginVertical();
            layout.AddSeparateRow(new Label { Text = "Element groups" });
            layout.AddRow(physicalGroupList);
            layout.EndVertical();
            layout.AddRow(treeGridView);

            layout.AddSeparateRow(document_sn_label, null);

            layout.Add(null);
            Content = layout;
        }

        private void MaxElementSizeStepper_ValueChanged(object sender, EventArgs e)
        {
            var stepper = sender as NumericStepper;
            ETContext.SimulationContext.MaxElementSize = stepper.Value;
        }

        private void MinElementSizeStepper_ValueChanged(object sender, EventArgs e)
        {
            var stepper = sender as NumericStepper;
            ETContext.SimulationContext.MinElementSize = stepper.Value;
        }

        private void PhysicalGroupList_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var listbox = sender as ListBox;
            if (Guid.TryParse(listbox.SelectedKey, out Guid objectId))
            {
                var objRef = RhinoDoc.ActiveDoc.Objects.FindId(objectId);
                if (objRef == null) return;
                objRef.Select(true);

                RhinoDoc.ActiveDoc.Views.Redraw();
            }
        }

        private void PhysicalGroupList_Load(object sender, EventArgs e)
        {
            var listbox = sender as ListBox;
            if (ETContext.SimulationContext.Model != null)
            {
                var index = listbox.SelectedIndex;

                var model = ETContext.SimulationContext.Model;

                listbox.Items.Clear();
                foreach (var eset in model.Mesh.ElementSets)
                {
                    listbox.Items.Add(eset.Key);
                }

                listbox.SelectedIndex = Math.Min(index, listbox.Items.Count);
            }

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

            Update();
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

