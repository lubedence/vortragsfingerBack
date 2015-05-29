using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Microsoft.Kinect;
using Microsoft.Kinect.Wpf.Controls;
using Microsoft.Kinect.Input;
using System.Diagnostics;
using System.Windows.Ink;
using Microsoft.Kinect.VisualGestureBuilder;
using Microsoft.Kinect.Toolkit.Input;
using votragsfinger2.util;
using System.Windows.Threading;
using System.Windows.Media.Media3D;
using votragsfinger2Back;
using System.Runtime.InteropServices;

namespace votragsfinger2
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        KinectSensor _sensor;
        MultiSourceFrameReader _reader;
        IList<Body> _bodies;
        VisualGestureBuilderFrameSource _gestureSource;
        VisualGestureBuilderFrameReader _gestureReader; 
        Body interactingBody = null;

        HandState rHand = HandState.Unknown;
        HandState lHand = HandState.Unknown;
        Stopwatch lastGestureTime = new Stopwatch();

        Gesture openingGesture;
        Gesture closingGesture;

        bool isMenuOpen = false;

        private DateTime lastMouseMove = DateTime.Now;
        private bool isMouseHidden = false;
        DispatcherTimer dispatcherTimer = new DispatcherTimer();
        Cursor currentCursor;

        private List<Point> stroke = new List<Point>();


        //BACK
        private Phiz phiz = new Phiz(JointType.HandRight, true);

        WriteableBitmap bitmap = new WriteableBitmap(512, 424, 96, 96, PixelFormats.Bgr32, null);
        private KinectJointFilter allJointFilter = new KinectJointFilter();

        public MainWindow()
        {
            InitializeComponent();
            UserSettings.Instance.parseIniFile();
            KinectSetup();
            MinimizeToTray.Enable(this);
            lastGestureTime.Start();

            kinectMenu.ColorChanged += new menu.ColorChangedEventHandler(kinectMenu_ColorChanged);
            kinectMenu.ThicknessChanged += new menu.ThicknessChangedEventHandler(kinectMenu_ThicknessChanged);
            kinectMenu.DrawTypeChanged += new menu.DrawTypeChangedEventHandler(kinectMenu_DrawTypeChanged);

            this.MouseMove += new MouseEventHandler(mainWindow_MouseMove);
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
            
            kinectMenu_ThicknessChanged(UserSettings.Instance.LINE_THICKNESS);


            testImg.Source = bitmap;
        }

        private void kinectMenu_ColorChanged(Color c)
        {
            myCanvas.LineColor = c;
            myCanvas.DefaultDrawingAttributes.Color = c;
        }

        private void kinectMenu_ThicknessChanged(Double t)
        {
            myCanvas.LineThickness = t;
            myCanvas.DefaultDrawingAttributes.Height = t;
            myCanvas.DefaultDrawingAttributes.Width = t;
        }

        private void kinectMenu_DrawTypeChanged(votragsfinger2.SketchCanvas.DrawType dt)
        {
            myCanvas.LineDrawType = dt;
        }


        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            if (DateTime.Now - lastMouseMove > TimeSpan.FromSeconds(2) && !isMouseHidden)
            {
                currentCursor = Mouse.OverrideCursor;
                Mouse.OverrideCursor = Cursors.None;
                isMouseHidden = true;
            }
        }
        private void mainWindow_MouseMove(object sender, MouseEventArgs e)
        {
            lastMouseMove = DateTime.Now;

            if (isMouseHidden)
            {
                Mouse.OverrideCursor = currentCursor;
                isMouseHidden = false;
            }
        }

        //TODO: Close sensor method & call
        private bool KinectSetup()
        {
            _sensor = KinectSensor.GetDefault();
            if (_sensor == null)
            {
                return false;
            }


            //KinectRegion.SetKinectRegion(this, kinectRegion);

            //App app = ((App)Application.Current);
            //app.KinectRegion = kinectRegion;
            //kinectRegion.KinectSensor = _sensor;

            //this.Loaded += Window_Loaded;


            _sensor.Open();

            _reader = _sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Body | FrameSourceTypes.BodyIndex);
            _reader.MultiSourceFrameArrived += OnMultiSourceFrameArrived;

            LoadGestures();

            return true;
        }


        void LoadGestures()
        {
            if (UserSettings.Instance.GESTURE_PATH.CompareTo("null") == 0)
                return;

            VisualGestureBuilderDatabase db = new VisualGestureBuilderDatabase(@UserSettings.Instance.GESTURE_PATH);
            this.openingGesture = db.AvailableGestures.Where(g => g.Name == "HandsApart").Single();
            this.closingGesture = db.AvailableGestures.Where(g => g.Name == "HandsTogether").Single();

            this._gestureSource = new VisualGestureBuilderFrameSource(this._sensor, 0);

            this._gestureSource.AddGesture(this.openingGesture);
            this._gestureSource.AddGesture(this.closingGesture);

            this._gestureSource.TrackingIdLost += OnTrackingIdLost;

            this._gestureReader = this._gestureSource.OpenReader();
            this._gestureReader.IsPaused = true;
            this._gestureReader.FrameArrived += OnGestureFrameArrived; 
        }

        void OnTrackingIdLost(object sender, TrackingIdLostEventArgs e)
        {
            this._gestureReader.IsPaused = true;
        }

        void OnGestureFrameArrived(object sender, VisualGestureBuilderFrameArrivedEventArgs e)
        {
            using (var frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    var discreteResults = frame.DiscreteGestureResults;
                    if (discreteResults == null) return;

                    if (discreteResults.ContainsKey(this.closingGesture))
                    {
                        var result = discreteResults[this.closingGesture];
                        if (result.Detected && result.Confidence > UserSettings.Instance.GESTURE_MIN_CONFIDENCE && lastGestureTime.ElapsedMilliseconds > UserSettings.Instance.GESTURE_MIN_TIME)
                        {

                            lastGestureTime.Restart();

                            //Closing Gesture started
                            if (isMenuOpen)
                            {
                                hideMenu();
                            }
                            else if (WindowState != System.Windows.WindowState.Minimized)
                            {
                                hideWindow();
                            }

                        }
                    }
                    
                    if (discreteResults.ContainsKey(this.openingGesture))
                    {
                        var result = discreteResults[this.openingGesture];
                        if (result.Detected && result.Confidence > UserSettings.Instance.GESTURE_MIN_CONFIDENCE && lastGestureTime.ElapsedMilliseconds > UserSettings.Instance.GESTURE_MIN_TIME)
                        {

                            lastGestureTime.Restart();

                            //Opening Gesture started
                            if (WindowState == System.Windows.WindowState.Minimized)
                            {
                                showWindow();
                            }
                            else if(!isMenuOpen){
                                showMenu();
                            }
                        }
                    }
                }
            }
        }


        void OnMultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            var reference = e.FrameReference.AcquireFrame();

            using (var bodyFrame = reference.BodyFrameReference.AcquireFrame())
            using (var bodyIndexFrame = reference.BodyIndexFrameReference.AcquireFrame())
            {
                if (bodyFrame != null && bodyIndexFrame != null)
                {
                    _bodies = new Body[bodyFrame.BodyFrameSource.BodyCount];
                    bodyFrame.GetAndRefreshBodyData(_bodies);
                    var trackedBody = this._bodies.Where(b => b.IsTracked).FirstOrDefault();

                    if (trackedBody != null)
                        {
                            //depth coordinate mapping to screen coordinates   
                            myCanvas.updateStrokes(phiz.getDisplayCoordinate(trackedBody), SketchCanvas.UserAction.Draw);

                            //information about user for gesture recognition
                            if (this._gestureReader != null && this._gestureReader.IsPaused)
                            {
                                this._gestureSource.TrackingId = trackedBody.TrackingId;
                                this._gestureReader.IsPaused = false;
                            }
                            
                            //current interacting body
                            interactingBody = trackedBody;

                            //hand states - just working well if kinect is mounted in front of user
                            rHand = trackedBody.HandRightState;
                            lHand = trackedBody.HandLeftState;

                            allJointFilter.UpdateFilter(trackedBody);

                            //hand segmentation - if kinect mounted behind user
                            int depthFrameWidth = bodyIndexFrame.FrameDescription.Width;
                            int depthFrameHeight = bodyIndexFrame.FrameDescription.Height;
                            byte[] _bodyIndexData = new byte[depthFrameWidth * depthFrameHeight];

                            bodyIndexFrame.CopyFrameDataToArray(_bodyIndexData);

                            CameraSpacePoint cspCenter = allJointFilter.getFilteredJoint(JointType.HandLeft);
                            CameraSpacePoint cspEllbow = allJointFilter.getFilteredJoint(JointType.ElbowLeft);
                            double cspRadius3d = (Math.Sqrt(Math.Pow((cspCenter.X - cspEllbow.X), 2) + Math.Pow((cspCenter.Y - cspEllbow.Y), 2) + Math.Pow((cspCenter.Z - cspEllbow.Z), 2)));
                            double cspRadius2d = (Math.Sqrt(Math.Pow((cspCenter.X - cspEllbow.X), 2) + Math.Pow((cspCenter.Y - cspEllbow.Y), 2)));
                            DepthSpacePoint dspCenter = _sensor.CoordinateMapper.MapCameraPointToDepthSpace(cspCenter);
                            DepthSpacePoint dspElbow = _sensor.CoordinateMapper.MapCameraPointToDepthSpace(cspEllbow);
                            double radius = (Math.Sqrt(Math.Pow((dspCenter.X - dspElbow.X), 2) + Math.Pow((dspCenter.Y - dspElbow.Y), 2)));
                            radius = radius / cspRadius2d * cspRadius3d;
                            int bytesPerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;

                            HandSegmentation bfs = new HandSegmentation(depthFrameWidth, depthFrameHeight);
                            bfs.searchBFS(_bodyIndexData, (int)dspCenter.X, (int)dspCenter.Y, (int)radius);

                            //show segmented hand
                            byte[] pixels = bfs.getBitmapData(bytesPerPixel);
                            bitmap.Lock();
                            Marshal.Copy(pixels, 0, bitmap.BackBuffer, pixels.Length);
                            bitmap.AddDirtyRect(new Int32Rect(0, 0, depthFrameWidth, depthFrameHeight));
                            bitmap.Unlock();

                            bfs = null;
                            //gesture recognition from segmented hand

                        }
                    else
                    {
                        OnTrackingIdLost(null, null);
                    }
                }
            }
        }


        private void recBRSearch(byte[] data, int x, int y, float dist)
        {
            int depthIndex = (y * 512) + x;
            byte isPlayerPixel = data[depthIndex];

            if (isPlayerPixel == 0xff || dist > 30) return;

            
        }


       /* private Point phiz(Joint center, Joint hand)
        {



            //rotate hand to be aligned with the center(head) on the X axes.
            Vector3D dist = (accumulatedCenter - handV);
            Vector3D hand_CenterAsOrigin = handV - accumulatedCenter;
            double angle = Math.Atan(hand_CenterAsOrigin.Z / hand_CenterAsOrigin.X);
            Vector3D hand_rotated = new Vector3D(hand_CenterAsOrigin.Z * Math.Sin(angle) + hand_CenterAsOrigin.X * Math.Cos(angle), hand_CenterAsOrigin.Y, hand_CenterAsOrigin.Z * Math.Cos(angle) - hand_CenterAsOrigin.X * Math.Sin(angle));
            //hand_rotated += dist;

            //calc point on display in relation to center(head)


           // dist = (new Vector3D(-0.10, 0.30, 0) - hand_rotated);
            dist = (accumulatedCenter + new Vector3D(-0.10, 0.30, 0) - handV);
        }*/



        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            KinectCoreWindow kinectCoreWindow = KinectCoreWindow.GetForCurrentThread();
            kinectCoreWindow.PointerMoved += kinectCoreWindow_PointerMoved;
        }

        private void kinectCoreWindow_PointerMoved(object sender, KinectPointerEventArgs args)
        {
            KinectPointerPoint kinectPointerPoint = args.CurrentPoint;

            //just one of the >2 hands is the active one
            if (!kinectPointerPoint.Properties.IsEngaged) return;

            Point pointRelativeToKinectRegion = new Point(kinectPointerPoint.Position.X * kinectRegion.ActualWidth, kinectPointerPoint.Position.Y * kinectRegion.ActualHeight);

            //CANVAS INTERACTION
            if (!isMenuOpen)
            {
                if ((kinectPointerPoint.Properties.HandType == HandType.RIGHT && rHand == UserSettings.Instance.GESTURE_MOVE) || (kinectPointerPoint.Properties.HandType == HandType.LEFT && lHand == UserSettings.Instance.GESTURE_MOVE))
                {
                    myCanvas.updateStrokes(pointRelativeToKinectRegion, SketchCanvas.UserAction.Move);
                }
                else if ((kinectPointerPoint.Properties.HandType == HandType.RIGHT && rHand == UserSettings.Instance.GESTURE_DRAW) || (kinectPointerPoint.Properties.HandType == HandType.LEFT && lHand == UserSettings.Instance.GESTURE_DRAW))
                {
                    myCanvas.updateStrokes(pointRelativeToKinectRegion, SketchCanvas.UserAction.Draw);
                }
                else if ((kinectPointerPoint.Properties.HandType == HandType.RIGHT && rHand == UserSettings.Instance.GESTURE_RUBBER) || (kinectPointerPoint.Properties.HandType == HandType.LEFT && lHand == UserSettings.Instance.GESTURE_RUBBER))
                {
                    myCanvas.updateStrokes(pointRelativeToKinectRegion, SketchCanvas.UserAction.Cancel);
                }
            }

        }

        private void showMenu()
        {
            this.navigationRegion.Visibility = Visibility.Visible;
            this.myCanvas.IsEnabled = false;
            isMenuOpen = true;
        }

        private void hideMenu()
        {
            this.navigationRegion.Visibility = Visibility.Hidden;
            this.myCanvas.IsEnabled = true;
            isMenuOpen = false;
        }


        private void hideWindow()
        {
            WindowState = WindowState.Minimized;
        }

        private void showWindow()
        {
            WindowState = WindowState.Maximized;
        }

        private void Menu_Button_Click(object sender, RoutedEventArgs e)
        {
            if (isMenuOpen)
            {
                hideMenu();
            }
            else
            {
                showMenu();
            }
        }

        private void Activate_Mouse_Erase_Mode(object sender, RoutedEventArgs e)
        {
            myCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
        }

        private void Activate_Mouse_Draw_Mode(object sender, RoutedEventArgs e)
        {
            myCanvas.EditingMode = InkCanvasEditingMode.Ink;
        }

    }
}
