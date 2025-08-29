# Pentax Camera Plugin

You can install the plugin from the NINA PlugIn tab.

Currently tested with KP, K3-III, K1, K1ii, and 645Z.  KF and K70 should work.

Make sure your camera is in MTP/PTP mode.  Turn on Electronic Shutter if desired.  When connecting always make sure that the camera is connected first and then connect the focuser.

If your computer is sluggish after installation, you should reboot because N.I.N.A. is in a weird state.

The software supports both Manual and Bulb modes.  No other modes are supported.  The software will check to make sure it is in one of the two modes during connection.  Once connected disconnect before switching modes since the software does not check for mode changes once it is connected. When in Bulb mode make sure it is not in timer setting but in true Bulb mode.  Typically you press the Green button to toggle between them.  If you are using a K3 Mark III make sure that in the menu Bulb mode is also set to Bulb and not Time.  You can verify the functionality by testing manually.  If pressing and holding the shutter button starts the capture and releasing the shutter button ends the capture.

The focuser speaks to the autofocus lens to change focus.  If keeps the Camera LiveView active to allow focusing with the Ricoh SDK.  You can use all the focusing tools in NINA.  

The is an Aperture window in the Imaging tab.  This allows changing the Aperture.  You can press Refresh and the driver will rescan the available Apertures in the current lens.

The Video button is available in the Imaging tab in addition to the shutter button.  This will display the LiveView image streaming from the camera allow easier framing and supporting initial focusing.

Any issues please reach out on the Discussions board.
