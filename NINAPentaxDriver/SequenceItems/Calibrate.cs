#region "copyright"

/*
    Copyright © 2025 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using Rtg.NINA.NinaPentaxDriver.NinaPentaxDriverDrivers;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Rtg.NINA.NinaPentaxDriver.NinaPentaxDriverSequenceItems {
    [ExportMetadata("Name", "Calibrate Lens Pentax")]
    [ExportMetadata("Description", "Calibrates the lens Pentax")]
    [ExportMetadata("Icon", "CameraSVG")]
    [ExportMetadata("Category", "Lens")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class CalibrateLens : SequenceItem, IValidatable
    {
        private IList<string> issues = new List<string>();

        public IList<string> Issues
        {
            get => issues;
            set
            {
                issues = value;
                RaisePropertyChanged();
            }
        }

        private readonly IFocuserMediator focuserMediator;

        [ImportingConstructor]
        public CalibrateLens(IFocuserMediator focuserMediator)
        {
            this.focuserMediator = focuserMediator;
        }

        public override object Clone()
        {
            return new CalibrateLens(focuserMediator)
            {
                Icon = Icon,
                Name = Name,
                Category = Category,
                Description = Description,
            };
        }

        public bool Validate()
        {
            List<string> i = new List<string>();
            if (!focuserMediator.GetInfo().Connected)
            {
                i.Add("Camera is not connected");
            }
            IDevice device = focuserMediator.GetDevice();
            if (device as FocuserDriver==null)
            {
                i.Add("No camera lens connected for calibration");
            }
            Issues = i;
            return i.Count == 0;
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            IDevice device = focuserMediator.GetDevice();
            var focuser = device as FocuserDriver;
            if (focuser is not null) 
            {
                // Try to invoke ICommand property named StartCalibration
                var prop = focuser.GetType().GetProperty("StartCalibration", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null) {
                    if (typeof(ICommand).IsAssignableFrom(prop.PropertyType)) {
                        var cmd = prop.GetValue(focuser) as ICommand;
                        if (cmd != null && cmd.CanExecute(null)) {
                            Logger.Info("Calling Property StartCalibration");
                            cmd.Execute(null);
                            return;
                        }
                    }
                }

            }
        }
    }
}
