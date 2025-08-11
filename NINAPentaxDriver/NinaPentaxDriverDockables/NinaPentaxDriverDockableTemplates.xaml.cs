using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rtg.NINA.NinaPentaxDriver.NinaPentaxDriverDockables {
    [Export(typeof(ResourceDictionary))]
    public partial class MyPluginDockableTemplates : ResourceDictionary {
        public MyPluginDockableTemplates() {
            InitializeComponent();
            //Fstops.Add({ 0,"Hello"});
        }

        public class FSManager {
            public long FSCode {  get; set; }
            public string FSName { get; set; }
        }

        public List<FSManager> Fstops { get; set; }
    }
}