Vortragsfinger 2 - Draw with your hand/finger over eg. your presentation slides

## 1) Kinect in front of user:
Track user, use KinectRegion to get interacting hand and HandState to determine hand-gesture.
Some additional gestures trained to hide/show a menu, as well as minimize/maximize the programm.

## 2) Kinect behind user:
KinectRegion & HandState not working (Kinect has to be in front of user). 
Work in progress: Implemented own Phiz + segment hand and recognize hand/finger pose to get the appropriate HandState.

## You need:
- Win 8/8.1
- Kinect v2
- Kinect SDK 2 
- Visual Gesture Builder (optional)
- Emgu CV (optional)

## Used third party libraries / dependencies:
- nini.dll http://nini.sourceforge.net/ Jun. '15

- Emgu CV: at least: Emgu.CV.dll, Emgu.CV.UI.dll, Emgu.Util.dll -- opencv_core2410.dll, opencv_imgproc2410.dll, cudart32_65.dll

- Kinect SDK: at least: Microsoft.Kinect.dll, Microsoft.Kinect.VisualGestureBuilder.dll, Microsoft.Kinect.WPF.Controls.dll


## How to 

#Start interacting with your hands
a) If KinectRegion is enabled: slowly raise your hand (hand should be opened).
b) If my own Phiz implementation is used: To start an interaction, raise your hand over your head.

# Make gestures for interacting with the canvas
- move cursor: opened hand
- drawing: a) if KinectRegion is used: Lasso HandState b) If my own Phiz implementation is used: "peace hand sign"
- delete strokes: closed hand

if Kinect is in front of user: program and also a menu can be minimized/maximized with some trained gestures: spread and squeeze gesture with both hands. It's possible to train new gestures with Microsoft's Visual Gesture Builder.

# Alter .ini file

Explanation for some variables:

-Kinect behind user
true if Kinect is behind user, false if it's in front

-Use KinectRegion
true if Microsoft's KinectRegion (part of SDK) is used, false if my own Phiz and Hand State implementation is used

-no delete gesture
only if "Use KinectRegion = False": if true, just work with two instead of three gestures (more accurate recognition) - hand opened for moving cursor, hand closed for drawing. if false, see above "Make gestures for interacting with the canvas"

-Minimum time
for gestures to show/hide menu and minimize/maximize programm

-Minimum confidence
for gestures to show/hide menu and minimize/maximize programm

-draw hand state & rubber hand state & move hand state
to switch gesture action


---------

##### This is a prototype - not a finished program