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
    [ExportMetadata("Name", "Auto Focus Pentax")]
    [ExportMetadata("Description", "Triggers camera AF (Pentax)")]
    [ExportMetadata("Icon", "CameraSVG")]
    [ExportMetadata("Category", "Lens")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class AutoFocus : SequenceItem, IValidatable
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

        private readonly ICameraMediator cameraMediator;

        [ImportingConstructor]
        public AutoFocus(ICameraMediator cameraMediator)
        {
            this.cameraMediator = cameraMediator;
        }

        public override object Clone()
        {
            return new AutoFocus(cameraMediator)
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
            if (!cameraMediator.GetInfo().Connected)
            {
                i.Add("Camera is not connected");
            }
            IDevice device = cameraMediator.GetDevice();
            if (device as CameraDriver == null)
            {
                i.Add("No camera driver available for AF");
            }
            Issues = i;
            return i.Count == 0;
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            IDevice device = cameraMediator.GetDevice();
            var camera = device as CameraDriver;
            if (camera is not null)
            {
                // Try to invoke ICommand property named StartAF
                var prop = camera.GetType().GetProperty("StartAF", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null)
                {
                    if (typeof(ICommand).IsAssignableFrom(prop.PropertyType))
                    {
                        var cmd = prop.GetValue(camera) as ICommand;
                        if (cmd != null && cmd.CanExecute(null))
                        {
                            Logger.Info("Calling Property StartAF");
                            cmd.Execute(null);
                            return;
                        }
                    }
                }

                // Fallback: try to call a method named 'Focus' if present
                var method = camera.GetType().GetMethod("Focus", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null)
                {
                    try
                    {
                        Logger.Info("Calling method Focus on camera driver");
                        var result = method.Invoke(camera, null);
                        // if Focus is async-returning Task, await it
                        if (result is Task t) await t;
                        return;
                    }
                    catch (TargetInvocationException ex)
                    {
                        Logger.Error("Error invoking Focus method", ex);
                    }
                }
            }
        }
    }
}