using NINA.Core.API.ASCOM.Camera;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Mediator;
using NINA.Equipment.Interfaces.Mediator;
using Ricoh.CameraController;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rtg.NINA.NinaPentaxDriver.NinaPentaxDriverDrivers {
    /// <summary>
    /// This Class shows the basic principle on how to add a new Device driver to N.I.N.A. via the plugin interface
    /// When the application scans for equipment the "GetEquipment" method of a device provider is called.
    /// This method should then return the specific List of Devices that you can connect to
    /// </summary>
    [Export(typeof(IEquipmentProvider))]
    public class CameraProvider : IEquipmentProvider<ICamera> {
        private IProfileService profileService;
        private IExposureDataFactory exposureDataFactory;
        private ITelescopeMediator telescopeMediator;
        //        SonyDriver driver;

        [ImportingConstructor]
        public CameraProvider(IProfileService profileService, ITelescopeMediator telescopeMediator, IExposureDataFactory exposureDataFactory) {
            this.profileService = profileService;
            this.exposureDataFactory = exposureDataFactory;
            this.telescopeMediator = telescopeMediator;

            /*           if (!DllLoader.IsX86()) {
                           try {
                               this.driver = SonyDriver.GetInstance();
                           } catch (Exception ex) {
                               Logger.Error(ex);
                           }
                       }*/
        }

        public string Name => "NINAPentaxCamera";

        public class PentaxKPProfile {
            public const int PERSONALITY_SHARPCAP = 0;
            public const int PERSONALITY_NINA = 1;
            public const short OUTPUTFORMAT_RAWBGR = 0;
            public const short OUTPUTFORMAT_BGR = 1;
            public const short OUTPUTFORMAT_RGGB = 2;

            private DeviceInfo m_info;

            public bool EnableLogging = false;
            public int DebugLevel = 0;
            public string DeviceId = "";
            //        public int DeviceIndex = 0;
            public short DefaultReadoutMode = PentaxKPProfile.OUTPUTFORMAT_RAWBGR;
            public bool UseLiveview = true;
            public int Personality = PERSONALITY_SHARPCAP;
            public bool BulbModeEnable = false;
            public bool KeepInterimFiles = false;

            public void assignCamera(int index) {
                m_info.ImageWidthPixels = PentaxCameraInfo.ElementAt(index).ImageWidthPixels;
                m_info.ImageHeightPixels = PentaxCameraInfo.ElementAt(index).ImageHeightPixels;
                m_info.LiveViewWidthPixels = PentaxCameraInfo.ElementAt(index).LiveViewWidthPixels;
                m_info.LiveViewHeightPixels = PentaxCameraInfo.ElementAt(index).LiveViewHeightPixels;
                m_info.PixelWidth = PentaxCameraInfo.ElementAt(index).PixelWidth;
                m_info.PixelHeight = PentaxCameraInfo.ElementAt(index).PixelHeight;
            }

            public void assignCamera(string name) {
                for (int i = 0; i < PentaxCameraInfo.Count; i++) {
                    if (PentaxCameraInfo.ElementAt(i).label == name) {
                        assignCamera(i);
                        return;
                    }
                }

                assignCamera(0);
                return;
            }

            public struct DeviceInfo {
                public int Version;
                public int ImageWidthPixels;
                public int ImageHeightPixels;
                public int LiveViewHeightPixels;
                public int LiveViewWidthPixels;
                //            public int BayerXOffset;
                //            public int BayerYOffset;
                //            public int ExposureTimeMin;
                //            public int ExposureTimeMax;
                //            public int ExposureTimeStep;
                public double PixelWidth;
                public double PixelHeight;
                //            public int BitsPerPixel;

                public string Manufacturer;
                public string Model;
                public string SerialNumber;
                public string DeviceName;
                public string SensorName;
                public string DeviceVersion;
            }

            public struct CameraInfo {
                internal readonly string label;
                internal readonly int id;
                internal readonly int ImageWidthPixels;
                internal readonly int ImageHeightPixels;
                internal readonly int LiveViewWidthPixels;
                internal readonly int LiveViewHeightPixels;
                internal readonly double PixelWidth;
                internal readonly double PixelHeight;

                public CameraInfo(string label, int id, int ImageWidthPixels, int ImageHeightPixels, int LiveViewWidthPixels, int LiveViewHeightPixels, double PixelWidth, double PixelHeight) {
                    this.label = label;
                    this.id = id;
                    this.ImageWidthPixels = ImageWidthPixels;
                    this.ImageHeightPixels = ImageHeightPixels;
                    this.LiveViewWidthPixels = LiveViewWidthPixels;
                    this.LiveViewHeightPixels = LiveViewHeightPixels;
                    this.PixelWidth = PixelWidth;
                    this.PixelHeight = PixelHeight;
                }

                public string Label { get { return label; } }
                public int Id { get { return id; } }

            }

            // KP 6016x4000 14bit
            // K70 6000x4000 14bit
            // KF 6000x4000 14bit
            // K1ii 7360x4912 14bit
            // K1  7360x4912 14bit
            // K3iii 6192x4128 14bit 
            // 645Z 8256x6192 14bit

            static readonly IList<CameraInfo> PentaxCameraInfo = new ReadOnlyCollection<CameraInfo>
                (new[] {
                // TODO: fix preview size
             new CameraInfo ("PENTAX KP", 0, 6016, 4000, 720, 480, 3.88, 3.88),
             new CameraInfo ("PENTAX K-70", 1, 6000, 4000, 720, 480, 3.88, 3.88),
             new CameraInfo ("PENTAX KF", 2, 6000, 4000, 720, 480, 3.88, 3.88),
             new CameraInfo ("PENTAX K-1 Mark II", 3, 7360, 4912, 720, 480, 4.86, 4.86),
             new CameraInfo ("PENTAX K-1", 4, 7360, 4912, 720, 480, 4.86, 4.86),
             new CameraInfo ("PENTAX K-3 Mark III", 5, 6192, 4128, 1080, 720, 3.75, 3.75),
             new CameraInfo ("PENTAX 645Z", 6, 8256, 6192, 720, 480, 5.32, 5.32)
                });

            public DeviceInfo Info {
                get {
                    return m_info;
                }
            }

            public String SerialNumber {
                get {
                    return m_info.SerialNumber.TrimStart(new char[] { '0' });
                }
            }

            public String DisplayName {
                get {
                    return String.Format("{0} (s/n: {1})", "Pentax KP", SerialNumber);
                }
            }

            public String Model {
                get {
                    return m_info.Model;
                }
            }




        }

        public IList<ICamera> GetEquipment() {
            var devices = new List<ICamera>();

            //ArrayList result = new ArrayList();
            Ricoh.CameraController.DeviceInterface deviceInterface = Ricoh.CameraController.DeviceInterface.USB;
            List<CameraDevice> detectedCameraDevices =
                CameraDeviceDetector.Detect(deviceInterface);
            UInt32 count = (UInt32)detectedCameraDevices.Count();

            foreach (CameraDevice camera in detectedCameraDevices) {
                // Try to open the device
                // Sequence contains no elements
                //Response response = camera.Connect(Ricoh.CameraController.DeviceInterface.USB);

                PentaxKPProfile.DeviceInfo info = new PentaxKPProfile.DeviceInfo() {
                    Version = 1
                };

                info.DeviceName = camera.Model;
                info.SerialNumber = camera.SerialNumber;
                info.Model = camera.Model;

                /*LiveViewSpecification liveViewSpecification = new LiveViewSpecification();
                camera.GetCameraDeviceSettings(
                    new List<CameraDeviceSetting>() { liveViewSpecification }); ;
                LiveViewSpecificationValue liveViewSpecificationValue =
                    (LiveViewSpecificationValue)liveViewSpecification.Value;

                LiveViewImage liveViewImage = liveViewSpecificationValue.Get();
                info.ImageWidthPixels = (int)liveViewImage.Width;
                info.ImageHeightPixels = (int)liveViewImage.Height;*/

                //if (camera.IsConnected(Ricoh.CameraController.DeviceInterface.USB))
                {
                    devices.Add(new CameraDriver(profileService, telescopeMediator, exposureDataFactory, info));
                }

                //camera.Disconnect(Ricoh.CameraController.DeviceInterface.USB);
            }

            return devices;
        }
    }
}
