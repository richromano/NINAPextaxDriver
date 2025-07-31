# Pentax Camera Plugin

Currently tested with KP and K3-III. Should also work with K1, K1ii, and 645Z.  Maybe KF and K70.

When connecting always make sure that the camera is connected first and then connect the focuser.  Always disconnect the focuser first then the camera.  If you don't follow these steps then you will get the error: MISSING LABEL for SetPosition.

If your computer is sluggish after installation, you should reboot because N.I.N.A. is in a weird state.

The software supports both Manual and Bulb modes.  No other modes are supported.  The software will check to make sure it is in one of the two modes during connection.  Once connected disconnect before switching modes since the software does not check for mode changes once it is connected. 

The focuser speaks to the autofocus lens to change focus.  If keeps LiveView active to allow focusing with the Ricoh SDK.

If you want to use Bulb mode you need to connect a shutter release cable from the SNAP port on your mount to the camera. Also you need to select SNAP as the desired shutter control. Finally you need set your Auto Focus Lock setting to not use the shutter for focusing.
https://nighttime-imaging.eu/docs/master/site/advanced/bulbshutter/

Note that your mount needs an ASCOM driver that supports triggering the SNAP port.  EQMOD does this.

Any issues please reach out on the Discussions board.
