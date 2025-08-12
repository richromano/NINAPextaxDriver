using NINA.Core.Utility.Extensions;
using NINA.Equipment.Equipment.MyCamera;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Rtg.NINA.NinaPentaxDriver.NinaPentaxDriverDockables {
    [Export(typeof(ResourceDictionary))]
    public partial class MyPluginDockableTemplates : ResourceDictionary {
        public MyPluginDockableTemplates() {
            InitializeComponent();
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            // Handle selection change
            var listBox = sender as ListBox;
            var selectedItem = listBox?.SelectedItem;
            MessageBox.Show($"Selected Item: {selectedItem}");
            NinaPentaxDriverDockable parent= (NinaPentaxDriverDockable)e.Source;
            parent.cameraMediator.SendCommandBool("SetAperture 28");
        }
    }
}