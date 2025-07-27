using ASCOM;
using FTD2XX_NET;
using NINA.Core.Enum;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Equipment.SDK.CameraSDKs.ASTPANSDK;
using NINA.Equipment.Utility;
using NINA.Image.ImageData;
using NINA.Image.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using Ricoh.CameraController;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Xml;
using static Rtg.NINA.NinaPentaxDriver.NinaPentaxDriverDrivers.CameraProvider;

namespace Rtg.NINA.NinaPentaxDriver.NinaPentaxDriverDrivers {
    public class CameraDriver : BaseINPC, ICamera {
        // Some camera settings we are interested in
        private const uint PROPID_BATTERY     = 53784;
        private const uint PROPID_ISO         = 53790;  // Actual ISO currently set
        private const uint PROPID_ISOS        = 65534; // List of learnt ISOs this camera supports

        // Capture Status
        private const uint CAPTURE_CREATED    = 0x0000;
        private const uint CAPTURE_CAPTURING  = 0x0001;
        private const uint CAPTURE_FAILED     = 0x0002;
        private const uint CAPTURE_CANCELLED  = 0x0003;
        private const uint CAPTURE_COMPLETE   = 0x0004;
        private const uint CAPTURE_STARTING   = 0x8001;
        private const uint CAPTURE_READING    = 0x8002;
        private const uint CAPTURE_PROCESSING = 0x8003;

        private static Ricoh.CameraController.CameraDevice _camera = null;
        private PentaxKPProfile.DeviceInfo _device;
        private IProfileService _profileService;
        private readonly IExposureDataFactory _exposureDataFactory;
        private bool _liveViewEnabled;
        private short _readoutModeForSnapImages;
        private short _readoutModeForNormalImages;
        private AsyncObservableCollection<BinningMode> _binningModes;

        // Extra data - could be in a separate class
        private static int MaxImageWidthPixels;
        private static int MaxImageHeightPixels;
        private static PentaxKPProfile Settings = new PentaxKPProfile();
        private int gainIndex;
        public static CameraStates m_captureState = CameraStates.Error;
        public static bool LastSetFastReadout;
        internal static Queue<String> imagesToProcess = new Queue<string>();
        internal static Queue<BitmapImage> bitmapsToProcess = new Queue<BitmapImage>();
        internal static double previousDuration = 0;
        internal static string lastCaptureResponse = "None";
        internal static string canceledCaptureResponse = "None";
        internal static DateTime lastCaptureStartTime = DateTime.MinValue;
        private ImageDataProcessor _imageDataProcessor;

        public CameraDriver(IProfileService profileService, IExposureDataFactory exposureDataFactory, PentaxKPProfile.DeviceInfo device) {
            _profileService = profileService;
            _exposureDataFactory = exposureDataFactory;
            _device = device;
            _imageDataProcessor = new ImageDataProcessor();
        }

        #region Internal Helpers

        public static void LogCameraMessage(int level, string identifier, string message, params object[] args) {
            /*if (level <= Settings.DebugLevel)*/ {
                var msg = string.Format(message, args);
                Logger.Info($"[camera] {identifier}", msg);
            }
        }

        public static void LogFocuserMessage(int level, string identifier, string message, params object[] args) {
            /*if (level <= Settings.DebugLevel)*/ {
                var msg = string.Format(message, args);
                Logger.Info($"[focuser] {identifier}", msg);
            }
        }

        private static void Log(String message, String source = "DriverCommon") {
            Logger.Info(source, message);
        }

        //private PropertyValue GetPropertyValue(uint id) {
        //    return SonyDriver.GetInstance().GetProperty(_camera.Handle, id);
        //}
        class EventListener : CameraEventListener {
            public override void LiveViewFrameUpdated(
                CameraDevice sender,
                byte[] liveViewFrame) {
                // Display liveViewFrame in Image control (Name: image) of WPF
                var memoryStream = new MemoryStream(liveViewFrame);
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.EndInit();
                if (LastSetFastReadout && m_captureState == CameraStates.Exposing) {
                    bitmapsToProcess.Enqueue(bitmapImage);
                    m_captureState = CameraStates.Idle;
                    LogCameraMessage(1,"", "Enqueued LiveView Image");
                }

            }

            // Image Added
            public override void ImageAdded(CameraDevice sender, CameraImage image) {
                LogCameraMessage(5, "", "Received Image " + image.Name + " Capture state "+m_captureState.ToString());
                if (!Settings.BulbModeEnable) {
                    LogCameraMessage(5, "", sender.Status.CurrentCapture.ID.ToString() + " " + lastCaptureResponse.ToString() + " " + canceledCaptureResponse.ToString());

                    if (lastCaptureResponse == canceledCaptureResponse) {
                        image.Delete();
                        return;
                    }
                }

                // Get the image and save it in the current directory
                if ((!LastSetFastReadout) && (m_captureState == CameraStates.Exposing))
                    using (FileStream fs = new FileStream(
                        System.IO.Path.GetTempPath() + Path.DirectorySeparatorChar +
                        image.Name, FileMode.Create, FileAccess.Write)) {
                        m_captureState = CameraStates.Reading;
                        // TODO: Add frame progress
                        Response imageGetResponse = image.GetData(fs);
                        LogCameraMessage(0,"","Get Image has " +
                            (imageGetResponse.Result == Result.OK ?
                                "SUCCEED." : "FAILED."));
                        // TODO: save to memory instead MemoryStream
                        LogCameraMessage(0,"", System.IO.Path.GetTempPath() + Path.DirectorySeparatorChar +
                        image.Name);
                        imagesToProcess.Enqueue(System.IO.Path.GetTempPath() + Path.DirectorySeparatorChar + image.Name);
                        if (Settings.BulbModeEnable)
                            m_captureState = CameraStates.Idle;
                    }
            }

            // Capture Complete
            public override void CaptureComplete(CameraDevice sender, Capture capture) {
                m_captureState = CameraStates.Idle;
                LogCameraMessage(0,"","Capture Complete. Capture ID: "+capture.ID.ToString()+" tracking "+lastCaptureResponse.ToString()+" "+canceledCaptureResponse.ToString());
            }

            public override void DeviceDisconnected(CameraDevice sender, Ricoh.CameraController.DeviceInterface deviceInterface) {
                //Best we can do
                LogCameraMessage(0,"","Device Disconnected.");
                //_requestTermination.Set();
                m_captureState = CameraStates.Error;
                _camera = null;
            }
        }



        #endregion

        #region Supported Properties

        public bool HasShutter => true;

        // Although the driver supports camera temperature, it gets it from the ARW's
        // metadata after a photo is taken, because this code doesn't request processed
        // ARW, the temp cannot be determined.
        public double Temperature {
            get => double.NaN;
            /*{

                if (_camera != null) {
                    PropertyValue value = GetPropertyValue(PROPID_TEMPERATURE);

                    return (value.Value) / 10.0;
                } else {
                    return double.NaN;
                }
            }*/
        }

        public short BinX { get => 1; set => throw new ASCOM.NotImplementedException(); }
        public short BinY { get => 1; set => throw new ASCOM.NotImplementedException(); }

        public string SensorName {
            get {
                //if (_camera != null) {
                  //  return _camera.SensorName;
                //} else {
                    return string.Empty;
                //}
            }
        }

        public SensorType SensorType { get => SensorType.RGGB; set => throw new ASCOM.NotImplementedException(); }

        public short BayerOffsetX { get => 1; set => throw new ASCOM.NotImplementedException(); }

        public short BayerOffsetY { get => 1; set => throw new ASCOM.NotImplementedException(); }

        public int CameraXSize {
            get {
                return MaxImageWidthPixels;
            }
        }

        public int CameraYSize {
            get {
                return MaxImageHeightPixels;
            }
        }

        public double ExposureMin {
            get {
                return 1.0 / 24000.0;
            }
        }

        public double ExposureMax {
            get {
                return 1200;
            }
        }

        public short MaxBinX { get => 1; set => throw new ASCOM.NotImplementedException(); }

        public short MaxBinY { get => 1; set => throw new ASCOM.NotImplementedException(); }

        public double PixelSizeX {
            get {
                return Settings.Info.PixelWidth;
            }
        }

        public double PixelSizeY {
            get {
                return Settings.Info.PixelHeight;
            }
        }

        public bool CanSetTemperature => false;

        public CameraStates CameraState => CameraStates.NoState; // TODO

        public bool CanShowLiveView {
            get {
               return true;
            }
        }

        public bool LiveViewEnabled {
            get => _liveViewEnabled;
            set {
                _liveViewEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool HasBattery => true;

        public int BatteryLevel {
            get {
                // TODO: Fix
                return 100;
            }
        }

        public int BitDepth {
            get {
                int bpp = 8;
                if (Settings.DefaultReadoutMode == PentaxKPProfile.OUTPUTFORMAT_RGGB || Settings.DefaultReadoutMode == PentaxKPProfile.OUTPUTFORMAT_RAWBGR)
                    bpp = 16;
                return bpp;
            }
        }

        public bool CanGetGain {
            get {
               return false;
            }
        }

        public bool CanSetGain => CanGetGain;

        public int GainMax {
            get {
                return 5;
            }
        }

        public int GainMin {
            get {
                return 0;
            }
        }

        public int Gain {
            get {
                return gainIndex;
            }

            set {
                LogCameraMessage(0,"", "set_Gain "+value.ToString());
                gainIndex = value;
                if (gainIndex < 0)
                    gainIndex = 0;
                if (gainIndex > 5)
                    gainIndex = 5;
                //using (new DriverCommon.SerializedAccess("get_Gain"))
                {
                    // TODO: Can I set this any time?  Do we need more?
                    // TODO: Save time and what else to return later
                    if (_camera != null) {
                        ISO iso = new ISO();
                        if (gainIndex == 0)
                            iso = ISO.ISO100;
                        if (gainIndex == 1)
                            iso = ISO.ISO200;
                        if (gainIndex == 2)
                            iso = ISO.ISO400;
                        if (gainIndex == 3)
                            iso = ISO.ISO800;
                        if (gainIndex == 4)
                            iso = ISO.ISO1600;
                        if (gainIndex == 5)
                            iso = ISO.ISO3200;
                        _camera.SetCaptureSettings(new List<CaptureSetting>() { iso });
                    }
                }
            }
        }

        public IList<int> Gains {
            get {
                List<int> gains = new List<int>();
/*                gainIndex = 0;
                m_gains = new ArrayList();
                m_gains.Add("ISO 100");
                m_gains.Add("ISO 200");
                m_gains.Add("ISO 400");
                m_gains.Add("ISO 800");
                m_gains.Add("ISO 1600");
                m_gains.Add("ISO 3200");*/

                LogCameraMessage(0,"", "get_Gains");

                for (int i=0;i<6;i++) {
                     gains.Add(i);
                }

                return gains;
            }
        }

        public string Id => "Pentax";

        public string Name {
            get => _device.Model;
            set => throw new ASCOM.NotImplementedException();
        }

        public string DisplayName {
            get => _device.Model;
            set => throw new ASCOM.NotImplementedException();
        }

        public string Category { get => "Pentax"; }

        public bool Connected {
            get {
                return _camera != null;
            }
        }

        public string Description {
            get {
//                if (_camera != null) {
//                    return _camera.GetDescription();
//                } else {
                    return _device.GetDescription();
//                }
            }
        }

        public string DriverInfo => "https://github.com/richromano/NINAPextaxDriver";

        public string DriverVersion => string.Empty;

        public double TemperatureSetPoint {
            get => double.NaN;

            set {
            }
        }
        public bool CanSubSample => false;

        public bool EnableSubSample { get; set; }

        public int SubSampleX { get; set; }

        public int SubSampleY { get; set; }

        public int SubSampleWidth { get; set; }

        public int SubSampleHeight { get; set; }

        public bool CoolerOn {
            get => false;
            set {
            }
        }

        public double CoolerPower => double.NaN;

        public bool HasDewHeater => false;

        public bool DewHeaterOn {
            get => false;
            set {
            }
        }

        public bool CanSetOffset => false;

        public int Offset { get => -1; set => throw new ASCOM.NotImplementedException(); }

        public int OffsetMin => 0;

        public int OffsetMax => 0;

        public bool CanSetUSBLimit => false;

        public int USBLimit { get => -1; set => throw new ASCOM.NotImplementedException(); }

        public int USBLimitMin => -1;

        public int USBLimitMax => -1;

        public int USBLimitStep => -1;

        public double ElectronsPerADU => double.NaN;

        public IList<string> ReadoutModes => new List<string> { "Default" };

        public short ReadoutMode {
            get => 0;
            set { }
        }

        public short ReadoutModeForSnapImages {
            get => _readoutModeForSnapImages;
            set {
                _readoutModeForSnapImages = value;
                RaisePropertyChanged();
            }
        }

        public short ReadoutModeForNormalImages {
            get => _readoutModeForNormalImages;
            set {
                _readoutModeForNormalImages = value;
                RaisePropertyChanged();
            }
        }

        public AsyncObservableCollection<BinningMode> BinningModes {
            get {
                if (_binningModes == null) {
                    _binningModes = new AsyncObservableCollection<BinningMode>();
                    _binningModes.Add(new BinningMode(1, 1));
                }

                return _binningModes;
            }
        }

        #endregion

        #region Supported Methods

        public void StartLiveView(CaptureSequence sequence) {
            LiveViewEnabled = true;
        }

        public void StopLiveView() {
            LiveViewEnabled = false;
        }

        public Task<bool> Connect(CancellationToken token) {
            return Task.Run<bool>(() => {
                /*if (_camera != null) {
                    if (_camera.IsConnected(Ricoh.CameraController.DeviceInterface.USB)) {
                        LogCameraMessage(0, "Connected", "Disconnecting first...");
                        _camera.Disconnect(Ricoh.CameraController.DeviceInterface.USB);
                    }
                    _camera = null;
                }*/

                if (_camera == null) {
                    /*if (System.Diagnostics.Process.GetCurrentProcess().ProcessName == "SharpCap") {
                        SetupDialog();
                    }

                    if (Settings.DeviceId == "") {
                        SetupDialog();
                    }*/

                    Settings.DeviceId = "PENTAX K-3 Mark III";

                    LogCameraMessage(0,"Connected", "Connecting...");
                    List<CameraDevice> detectedCameraDevices = CameraDeviceDetector.Detect(Ricoh.CameraController.DeviceInterface.USB);
                    //                            Thread.Sleep(500);
                    //                            detectedCameraDevices = CameraDeviceDetector.Detect(Ricoh.CameraController.DeviceInterface.USB);
                    LogCameraMessage(0, "Connected", "Number of detected cameras " + detectedCameraDevices.Count.ToString()+" "+Settings.DeviceId.ToString());
                    foreach (CameraDevice camera in detectedCameraDevices) {
                        LogCameraMessage(0, "Connected", "Checking " + camera.Model.ToString() + "  " + Settings.DeviceId.ToString());
                        if (camera.Model == Settings.DeviceId) {
                            _camera = camera;
                            break;
                        }
                    }

                    if (_camera != null) {
                        var response = _camera.Connect(Ricoh.CameraController.DeviceInterface.USB);
                        if (response.Equals(Response.OK)) {
                            LogCameraMessage(0,"Connected", "Connected. Model: " + _camera.Model + ", SerialNumber:" + _camera.SerialNumber);
                            Settings.DeviceId = _camera.Model;

                            bool k3m3 = false;

                            if (_camera.Model.StartsWith("PENTAX K-3 Mark III")) {
                                LogCameraMessage(0, "Connect", "Bulb mode not supported on K-3 Mark III");
                                k3m3 = true;
                                Settings.BulbModeEnable = false;
                            }

                            LiveViewSpecification liveViewSpecification = new LiveViewSpecification();
                            _camera.GetCameraDeviceSettings(
                                new List<CameraDeviceSetting>() { liveViewSpecification }); ;
                            LiveViewSpecificationValue liveViewSpecificationValue =
                                (LiveViewSpecificationValue)liveViewSpecification.Value;

                            /*LiveViewImage liveViewImage = liveViewSpecificationValue.Get();
                            info.ImageWidthPixels = (int)liveViewImage.Width;
                            info.ImageHeightPixels = (int)liveViewImage.Height;*/

                            ExposureProgram exposureProgram = new ExposureProgram();

/*                            while (true) {
                                try {
                                    _camera.GetCaptureSettings(
                                        new List<CaptureSetting>() { exposureProgram });
                                } catch {
                                    throw new ASCOM.DriverException("Can't get capture settings.");
                                }

                                if (Settings.BulbModeEnable) {
                                    if (exposureProgram.Equals(Ricoh.CameraController.ExposureProgram.Bulb))
                                        break;
                                    else
                                        System.Windows.Forms.MessageBox.Show("Set the Camera Exposure Program to BULB");
                                } else {
                                    if (exposureProgram.Equals(Ricoh.CameraController.ExposureProgram.Manual))
                                        break;
                                    else
                                        System.Windows.Forms.MessageBox.Show("Set the Camera Exposure Program to MANUAL");
                                }
                            }*/

                            bool connect = _camera.IsConnected(Ricoh.CameraController.DeviceInterface.USB);
                            if (!connect) {
                                //System.Windows.Forms.MessageBox.Show("Connect seems to have failed");
                                LogCameraMessage(0, "Connected", "IsConnected false");
                            }

                            LogCameraMessage(0, "Connected", "IsConnected true");

                            StorageWriting sw = new StorageWriting();
                            sw = Ricoh.CameraController.StorageWriting.False;
                            StillImageCaptureFormat sicf = new StillImageCaptureFormat();

                            sicf = Ricoh.CameraController.StillImageCaptureFormat.JPEG;
                            if (Settings.DefaultReadoutMode == PentaxKPProfile.OUTPUTFORMAT_RAWBGR
                                || Settings.DefaultReadoutMode == PentaxKPProfile.OUTPUTFORMAT_RGGB)
                                sicf = Ricoh.CameraController.StillImageCaptureFormat.DNG;
                            StillImageQuality siq = new StillImageQuality();
                            siq = Ricoh.CameraController.StillImageQuality.LargeBest;

                            //ExposureProgram ep = new ExposureProgram();
                            //ep = Ricoh.CameraController.ExposureProgram.Bulb;
                            //DriverCommon.m_camera.SetCaptureSettings(new List<CaptureSetting>() { ep });
                            LogCameraMessage(0, "Connected", "Setting capture setting");
                            try {
                                //_camera.SetCaptureSettings(new List<CaptureSetting>() { sw });
                                //_camera.SetCaptureSettings(new List<CaptureSetting>() { siq });
                                //_camera.SetCaptureSettings(new List<CaptureSetting>() { sicf });
                            } catch (Exception e) {
                                LogCameraMessage(0, "Connected", e.Message.ToString());
                                return false;
                            }

                            LogCameraMessage(0, "Connect", "Driver Version: 7/25/2025");
                            LogCameraMessage(0, "Bulb mode", Settings.BulbModeEnable.ToString()+" mode "+exposureProgram.ToString());
                            // Sleep to let the settings take effect
                            Thread.Sleep(1000);
                            if (Settings.UseLiveview)
                                _camera.StartLiveView();

                            string deviceModel = Settings.DeviceId;
                            Settings.assignCamera(deviceModel);
                            MaxImageWidthPixels = Settings.Info.ImageWidthPixels; // Constants to define the ccd pixel dimension
                            MaxImageHeightPixels = Settings.Info.ImageHeightPixels;
                            //StartX = 0;
                            //StartY = 0;
                            //NumX = MaxImageWidthPixels;
                            //NumY = MaxImageHeightPixels;

                            Gain = gainIndex;
                            m_captureState = CameraStates.Idle;

                            if (_camera.EventListeners.Count == 0)
                                _camera.EventListeners.Add(new EventListener());
                        } else {
                            LogCameraMessage(0,"Connected", "Connection failed.");
                            return false;
                        }
                    } else {
                        Settings.DeviceId = "";
                        return false;
                    }

                }

                return true;
            });
        }

        public void Disconnect() {
            if (_camera != null) {
                if (_camera != null) {
                    // Stop the capture if necessary
                    // TODO: Should be async
                    _camera.Disconnect(Ricoh.CameraController.DeviceInterface.USB);
                }

                m_captureState = CameraStates.Error;
                _camera = null;
                LogCameraMessage(0,"Connected", "Closed connection to camera");
            }
        }

        public Task<IExposureData> DownloadLiveView(CancellationToken token) {
            return Task.Run<IExposureData>(() => {
  /*              using (var memStream = new MemoryStream(SonyDriver.GetInstance().GetLiveView(_camera.Handle)))*/ {
/*                    memStream.Position = 0;

                    JpegBitmapDecoder decoder = new JpegBitmapDecoder(memStream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);*/

                    FormatConvertedBitmap bitmap = new FormatConvertedBitmap();
                    bitmap.BeginInit();
//                    bitmap.Source = decoder.Frames[0];
                    bitmap.DestinationFormat = System.Windows.Media.PixelFormats.Gray16;
                    bitmap.EndInit();

                    ushort[] outArray = new ushort[bitmap.PixelWidth * bitmap.PixelHeight];
                    bitmap.CopyPixels(outArray, 2 * bitmap.PixelWidth, 0);

                    var metaData = new ImageMetaData();

                    return _exposureDataFactory.CreateImageArrayExposureData(
                            input: outArray,
                            width: bitmap.PixelWidth,
                            height: bitmap.PixelHeight,
                            bitDepth: 16,
                            isBayered: false,
                            metaData: metaData);
                }
            });
        }
        public void SetupDialog() {
            throw new ASCOM.NotImplementedException();
        }

        public void StartExposure(CaptureSequence sequence) {
            if (_camera != null) {

                double Duration = sequence.ExposureTime;

                LogCameraMessage(0, "", "StartExposure()");
                //Check duration range and save 
                if (Duration <= 0.0) {
                    throw new InvalidValueException("StartExposure", "Duration", " > 0");
                }

                if (Settings.BulbModeEnable) {
                    if (m_captureState != CameraStates.Idle)
                        throw new InvalidValueException("StartExposure", "CameraState", "Not idle");

                    imagesToProcess.Clear();
                    m_captureState = CameraStates.Exposing;
                    return;
                }


                // Light or dark frame
                // TODO:  I think we need to update the state back and forth for LastSetFastReadout
                //          using (new DriverCommon.SerializedAccess("StartExposure()"))
                while (m_captureState != CameraStates.Idle)
                    Thread.Sleep(100);

                imagesToProcess.Clear();
                m_captureState = CameraStates.Waiting;

                if (LastSetFastReadout) {
                    //No need to start exposure
                    LogCameraMessage(0, "", "StartExposure() fast");
                    if (Duration <= 0.0) {
                        throw new InvalidValueException("StartExposure", "Duration", " > 0");
                    }

                    m_captureState = CameraStates.Exposing;
                    previousDuration = Duration;
                    return;
                }

                ShutterSpeed shutterSpeed;
                shutterSpeed = ShutterSpeed.SS1_24000;
                if (Duration > 1.0 / 20000.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_20000;
                if (Duration > 1.0 / 16000.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_16000;
                if (Duration > 1.0 / 12800.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_12800;
                if (Duration > 1.0 / 12000.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_12000;
                if (Duration > 1.0 / 10000.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_10000;
                if (Duration > 1.0 / 8000.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_8000;
                if (Duration > 1.0 / 6400.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_6400;
                if (Duration > 1.0 / 6000.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_6000;
                if (Duration > 1.0 / 5000.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_5000;
                if (Duration > 1.0 / 4000.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_4000;
                if (Duration > 1.0 / 3200.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_3200;
                if (Duration > 1.0 / 3000.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_3000;
                if (Duration > 1.0 / 2500.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_2500;
                if (Duration > 1.0 / 2000.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_2000;
                if (Duration > 1.0 / 1600.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_1600;
                if (Duration > 1.0 / 1500.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_1500;
                if (Duration > 1.0 / 1250.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_1250;
                if (Duration > 1.0 / 1000.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_1000;
                if (Duration > 1.0 / 800.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_800;
                if (Duration > 1.0 / 750.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_750;
                if (Duration > 1.0 / 640.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_640;
                if (Duration > 1.0 / 500.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_500;
                if (Duration > 1.0 / 400.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_400;
                if (Duration > 1.0 / 350.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_350;
                if (Duration > 1.0 / 320.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_320;
                if (Duration > 1.0 / 250.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_250;
                if (Duration > 1.0 / 200.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_200;
                if (Duration > 1.0 / 180.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_180;
                if (Duration > 1.0 / 160.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_160;
                if (Duration > 1.0 / 125.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_125;
                if (Duration > 1.0 / 100.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_100;
                if (Duration > 1.0 / 90.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_90;
                if (Duration > 1.0 / 80.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_80;
                if (Duration > 1.0 / 60.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_60;
                if (Duration > 1.0 / 50.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_50;
                if (Duration > 1.0 / 45.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_45;
                if (Duration > 1.0 / 40.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_40;
                if (Duration > 1.0 / 30.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_30;
                if (Duration > 1.0 / 25.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_25;
                if (Duration > 1.0 / 20.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_20;
                if (Duration > 1.0 / 15.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_15;
                if (Duration > 1.0 / 13.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_13;
                if (Duration > 1.0 / 10.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_10;
                if (Duration > 1.0 / 8.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_8;
                if (Duration > 1.0 / 6.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_6;
                if (Duration > 1.0 / 5.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_5;
                if (Duration > 1.0 / 4.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_4;
                if (Duration > 1.0 / 3.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_3;
                if (Duration > 1.0 / 2.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_2;
                if (Duration > 6.0 / 10.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS6_10;
                if (Duration > 7.0 / 10.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS7_10;
                if (Duration > 8.0 / 10.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS8_10;
                if (Duration > 0.99)
                    shutterSpeed = ShutterSpeed.SS1;
                if (Duration > 13.0 / 10.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS13_10;
                if (Duration > 15.0 / 10.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS15_10;
                if (Duration > 16.0 / 10.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS16_10;
                // TODO: add additional times 
                //public static readonly ShutterSpeed SS10_13;
                //public static readonly ShutterSpeed SS10_16;
                //public static readonly ShutterSpeed SS10_25;
                //public static readonly ShutterSpeed SS25_10;
                //public static readonly ShutterSpeed SS32_10;
                //public static readonly ShutterSpeed SS3_10;
                //public static readonly ShutterSpeed SS4_10;
                //public static readonly ShutterSpeed SS5_10;
                if (Duration > 1.99)
                    shutterSpeed = ShutterSpeed.SS2;
                if (Duration > 2.99)
                    shutterSpeed = ShutterSpeed.SS3;
                if (Duration > 3.99)
                    shutterSpeed = ShutterSpeed.SS4;
                if (Duration > 4.99)
                    shutterSpeed = ShutterSpeed.SS5;
                if (Duration > 5.99)
                    shutterSpeed = ShutterSpeed.SS6;
                if (Duration > 7.99)
                    shutterSpeed = ShutterSpeed.SS8;
                if (Duration > 9.99)
                    shutterSpeed = ShutterSpeed.SS10;
                if (Duration > 12.99)
                    shutterSpeed = ShutterSpeed.SS13;
                if (Duration > 14.99)
                    shutterSpeed = ShutterSpeed.SS15;
                if (Duration > 19.99)
                    shutterSpeed = ShutterSpeed.SS20;
                if (Duration > 24.99)
                    shutterSpeed = ShutterSpeed.SS25;
                if (Duration > 29.99)
                    shutterSpeed = ShutterSpeed.SS30;
                if (Duration > 39.99)
                    shutterSpeed = ShutterSpeed.SS40;
                if (Duration > 49.99)
                    shutterSpeed = ShutterSpeed.SS50;
                if (Duration > 59.99)
                    shutterSpeed = ShutterSpeed.SS60;
                if (Duration > 69.99)
                    shutterSpeed = ShutterSpeed.SS70;
                if (Duration > 79.99)
                    shutterSpeed = ShutterSpeed.SS80;
                if (Duration > 89.99)
                    shutterSpeed = ShutterSpeed.SS90;
                if (Duration > 99.99)
                    shutterSpeed = ShutterSpeed.SS100;
                if (Duration > 109.99)
                    shutterSpeed = ShutterSpeed.SS110;
                if (Duration > 119.99)
                    shutterSpeed = ShutterSpeed.SS120;
                if (Duration > 129.99)
                    shutterSpeed = ShutterSpeed.SS130;
                if (Duration > 139.99)
                    shutterSpeed = ShutterSpeed.SS140;
                if (Duration > 149.99)
                    shutterSpeed = ShutterSpeed.SS150;
                if (Duration > 159.99)
                    shutterSpeed = ShutterSpeed.SS160;
                if (Duration > 169.99)
                    shutterSpeed = ShutterSpeed.SS170;
                if (Duration > 179.99)
                    shutterSpeed = ShutterSpeed.SS180;
                if (Duration > 189.99)
                    shutterSpeed = ShutterSpeed.SS190;
                if (Duration > 199.99)
                    shutterSpeed = ShutterSpeed.SS200;
                if (Duration > 209.99)
                    shutterSpeed = ShutterSpeed.SS210;
                if (Duration > 219.99)
                    shutterSpeed = ShutterSpeed.SS220;
                if (Duration > 229.99)
                    shutterSpeed = ShutterSpeed.SS230;
                if (Duration > 239.99)
                    shutterSpeed = ShutterSpeed.SS240;
                if (Duration > 249.99)
                    shutterSpeed = ShutterSpeed.SS250;
                if (Duration > 259.99)
                    shutterSpeed = ShutterSpeed.SS260;
                if (Duration > 269.99)
                    shutterSpeed = ShutterSpeed.SS270;
                if (Duration > 279.99)
                    shutterSpeed = ShutterSpeed.SS280;
                if (Duration > 289.99)
                    shutterSpeed = ShutterSpeed.SS290;
                if (Duration > 299.99)
                    shutterSpeed = ShutterSpeed.SS300;
                if (Duration > 359.99)
                    shutterSpeed = ShutterSpeed.SS360;
                if (Duration > 419.99)
                    shutterSpeed = ShutterSpeed.SS420;
                if (Duration > 479.99)
                    shutterSpeed = ShutterSpeed.SS480;
                if (Duration > 539.99)
                    shutterSpeed = ShutterSpeed.SS540;
                if (Duration > 599.99)
                    shutterSpeed = ShutterSpeed.SS600;
                if (Duration > 659.99)
                    shutterSpeed = ShutterSpeed.SS660;
                if (Duration > 719.99)
                    shutterSpeed = ShutterSpeed.SS720;
                if (Duration > 779.99)
                    shutterSpeed = ShutterSpeed.SS780;
                if (Duration > 839.99)
                    shutterSpeed = ShutterSpeed.SS840;
                if (Duration > 899.99)
                    shutterSpeed = ShutterSpeed.SS900;
                if (Duration > 959.99)
                    shutterSpeed = ShutterSpeed.SS960;
                if (Duration > 1019.99)
                    shutterSpeed = ShutterSpeed.SS1020;
                if (Duration > 1079.99)
                    shutterSpeed = ShutterSpeed.SS1080;
                if (Duration > 1139.99)
                    shutterSpeed = ShutterSpeed.SS1140;
                if (Duration > 1199.99)
                    shutterSpeed = ShutterSpeed.SS1200;


                _camera.SetCaptureSettings(new List<CaptureSetting>() { shutterSpeed });

                FNumber fNumber = new FNumber();
                _camera.GetCaptureSettings(new List<CaptureSetting>() { fNumber });
                List<CaptureSetting> availableFNumberSettings = fNumber.AvailableSettings;

                //Number fNumber = FNumber.F5_6;
                //cameraDevice.SetCaptureSettings(new List<CaptureSetting>() { fNumber });

                // The list above might contain the following values.
                // F4.0 (F4_0), F4.5 (F4_5), F5.0 (F5_0)


                StartCaptureResponse response = _camera.StartCapture(false);
                if (response.Result == Result.OK) {
                    lastCaptureResponse = response.Capture.ID;
                    previousDuration = Duration;
                    lastCaptureStartTime = DateTime.Now;
                    // Make sure we don't change a reading to exposing
                    if (m_captureState == CameraStates.Waiting)
                        m_captureState = CameraStates.Exposing;
                } else {
                    lastCaptureResponse = "None";
                    m_captureState = CameraStates.Error;
                    LogCameraMessage(0, "StartExposure", "Call to StartExposure SDK not successful: Disconnect camera USB and make sure you can take a picture with shutter button");
                    throw new ASCOM.InvalidOperationException("Call to StartExposure SDK not successful: Disconnect camera USB and make sure you can take a picture with shutter button");
                }
            }
        }

        public void StopExposure() {
            AbortExposure();
        }

        public void AbortExposure() {
            // TODO: fix abort exposure - test bulb mode
            LogCameraMessage(0, "", "AbortExposure");
            if (LastSetFastReadout) {
                m_captureState = CameraStates.Idle;
                return;
            }

            // TODO: cameraWaiting is bad because it will get set to other, we check in connect though
            if (m_captureState != CameraStates.Exposing && m_captureState != CameraStates.Waiting)
                return;

            //StopCapture doesn't get called
            LogCameraMessage(0, "AbortExposure", "Stopping Capture.");
            while (m_captureState != CameraStates.Exposing) {
                Thread.Sleep(100);
                LogCameraMessage(0, "AbortExposure", "Waiting for capture to start.");
            }

            if (Settings.BulbModeEnable)
                canceledCaptureResponse = lastCaptureResponse;

            /*if (previousDuration > 5)
            {
                //DriverCommon.m_camera.Disconnect(Ricoh.CameraController.DeviceInterface.USB);
                //m_captureState = CameraStates.cameraError;
                Disconnect();
                return;
            }*/
            //Response response =DriverCommon.m_camera.StopCapture();

            while (m_captureState == CameraStates.Exposing) {
                Thread.Sleep(100);
                LogCameraMessage(0, "AbortExposure", "Waiting for capture to finish.");
            }

            return;
            //DriverCommon.LogCameraMessage(0, "AbortExposure", "Failed. "+response.Errors.First().Message);
            //return;
        }

        private bool IsFileClosed(string filePath) {
            try {
                using (var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.None)) {
                    return true;
                }
            } catch {
                return false;
            }
        }

        private byte[] ReadImageFileRGGB(string MNewFile) {
            object result = null;
            //Bitmap _bmp;
            //int MSensorWidthPx = DriverCommon.Settings.Info.ImageWidthPixels;
            //int MSensorHeightPx = DriverCommon.Settings.Info.ImageHeightPixels;
            // TODO: Should be returned based on image size
            int[,] rgbImage;// = new int[MSensorWidthPx, MSensorHeightPx]; // Assuming this is declared and initialized elsewhere.


            // Wait for the file to be closed and available.
            while (!IsFileClosed(MNewFile)) { }
            rgbImage = _imageDataProcessor.ReadRBBGPentax(MNewFile);

            int scale = 1;

            if (Settings.DefaultReadoutMode == PentaxKPProfile.OUTPUTFORMAT_RAWBGR ||
                Settings.DefaultReadoutMode == PentaxKPProfile.OUTPUTFORMAT_RGGB)
                scale = 4;

            for (int y = 0; y < rgbImage.GetLength(1); y++) {
                for (int x = 0; x < rgbImage.GetLength(0); x++) {
                    rgbImage[x, y] = scale * rgbImage[x, y];
                }
            }

            byte[] byteImage =new byte[rgbImage.GetLength(0)*rgbImage.GetLength(0)*sizeof(int)];

            // TODO: Sharpcap problem
            //result = Resize(rgbImage, 2, StartX, StartY, NumX, NumY);
            return byteImage;
        }

        public async Task WaitUntilExposureIsReady(CancellationToken token) {
            using (token.Register(AbortExposure)) {
                while (m_captureState != CameraStates.Idle) {
                    await CoreUtil.Wait(TimeSpan.FromMilliseconds(100), token);
                }
            }
        }

        public Task<IExposureData> DownloadExposure(CancellationToken token) {
            return Task.Run<IExposureData>(() => {
                string filename=imagesToProcess.Dequeue();
                FileStream fs = new FileStream(filename, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.None);

                byte[] readData = new byte[fs.Length];
                fs.Read(readData, 0, readData.Length);

                var metaData = new ImageMetaData();

                return _exposureDataFactory.CreateRAWExposureData(
                    converter: _profileService.ActiveProfile.CameraSettings.RawConverter,
                    rawBytes: readData,
                    rawType: "dng",
                    bitDepth: this.BitDepth,
                    metaData: metaData);
            });
        }
        #endregion

        #region Unsupported Methods

        public string Action(string actionName, string actionParameters) {
            throw new ASCOM.NotImplementedException();
        }


        public void SendCommandBlind(string command, bool raw = true) {
            throw new ASCOM.NotImplementedException();
        }

        public bool SendCommandBool(string command, bool raw = true) {
            throw new ASCOM.NotImplementedException();
        }

        public string SendCommandString(string command, bool raw = true) {
            throw new ASCOM.NotImplementedException();
        }

        public void SetBinning(short x, short y) {
            // Ignore
        }

        #endregion


        // TODO!!! WE NEED ONE
        public bool HasSetupDialog => false;

        public IList<string> SupportedActions => new List<string>();
    }
}
