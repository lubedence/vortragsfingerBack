Vortragsfinger 2 - Draw with your hand/finger over eg. your presentation slides

1) Kinect in front of user:
Track user, use KinectRegion to get interacting hand and HandState to determine hand-gesture.
Some additional gestures trained to hide/show a menu, as well as minimize/maximize the programm.

2) Kinect behind user:
KinectRegion & HandState not working (Kinect has to be in front of user). 
Work in progress: Implemented own Phiz + segment hand and recognize hand/finger pose to get the appropriate HandState.

You need:
- Win 8/8.1
- Kinect v2
- Kinect SDK 2 
- Visual Gesture Builder (optional)
- Emgu CV (optional)

Used third party libraries / dependencies:
- nini.dll http://nini.sourceforge.net/ Jun. '15

- Emgu CV: at least: Emgu.CV.dll, Emgu.CV.UI.dll, Emgu.Util.dll -- opencv_core2410.dll, opencv_imgproc2410.dll, cudart32_65.dll

- Kinect SDK: at least: Microsoft.Kinect.dll, Microsoft.Kinect.VisualGestureBuilder.dll, Microsoft.Kinect.WPF.Controls.dll


---------

This is a prototype - not a finished program