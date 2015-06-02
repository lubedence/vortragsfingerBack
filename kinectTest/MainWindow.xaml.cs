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
using Emgu.CV;
using votragsfinger2Back.util;


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
        private Phiz phiz;
        private HandState currentCursorBackHandState = HandState.Unknown; 

        public MainWindow()
        {
            InitializeComponent();
            //read/set user parameters
            UserSettings.Instance.parseIniFile();
            //minimize to tray
            MinimizeToTray.Enable(this);

            //kinect sensor setup
            KinectSetup();
            
            //menu handlers
            kinectMenu.ColorChanged += new menu.ColorChangedEventHandler(kinectMenu_ColorChanged);
            kinectMenu.ThicknessChanged += new menu.ThicknessChangedEventHandler(kinectMenu_ThicknessChanged);
            kinectMenu.DrawTypeChanged += new menu.DrawTypeChangedEventHandler(kinectMenu_DrawTypeChanged);
            //read and set line thickness from user settings
            kinectMenu_ThicknessChanged(UserSettings.Instance.LINE_THICKNESS);

            //hide mouse after short time period
            this.MouseMove += new MouseEventHandler(mainWindow_MouseMove);
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
            
            
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

            //kinect sdk phiz pointer
            if (UserSettings.Instance.IS_KINECTREGION_USED)
            {
                KinectRegion.SetKinectRegion(this, kinectRegion);
                App app = ((App)Application.Current);
                app.KinectRegion = kinectRegion;
                kinectRegion.KinectSensor = _sensor;
                KinectCoreWindow kinectCoreWindow = KinectCoreWindow.GetForCurrentThread();
                kinectCoreWindow.PointerMoved += kinectCoreWindow_PointerMoved;
                
            }
            else
            {
                //rough-and-ready way to disable kinect region (hide the cursor)
                kinectRegion.CursorSpriteSheetDefinition = new CursorSpriteSheetDefinition(new Uri("img/cursor.png", UriKind.Relative), 0, 0, 0, 0);

                //init own phiz and hand state recognition
                phiz = new Phiz(UserSettings.Instance.IS_KINECT_BEHIND_USER); 
            }
           
            _sensor.Open();

            _reader = _sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Body | FrameSourceTypes.BodyIndex);
            _reader.MultiSourceFrameArrived += OnMultiSourceFrameArrived;

            LoadGestures();

            return true;
        }


        void LoadGestures()
        {
            VisualGestureBuilderDatabase db = new VisualGestureBuilderDatabase(@"gestures/gestures1.gbd");
            this.openingGesture = db.AvailableGestures.Where(g => g.Name == "HandsApart").Single();
            this.closingGesture = db.AvailableGestures.Where(g => g.Name == "HandsTogether").Single();

            this._gestureSource = new VisualGestureBuilderFrameSource(this._sensor, 0);

            this._gestureSource.AddGesture(this.openingGesture);
            this._gestureSource.AddGesture(this.closingGesture);

            this._gestureSource.TrackingIdLost += OnTrackingIdLost;

            this._gestureReader = this._gestureSource.OpenReader();
            this._gestureReader.IsPaused = true;
            this._gestureReader.FrameArrived += OnGestureFrameArrived;

            lastGestureTime.Start();
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
                            //information about user for gesture recognition
                            if (this._gestureReader != null && this._gestureReader.IsPaused)
                            {
                                this._gestureSource.TrackingId = trackedBody.TrackingId;
                                this._gestureReader.IsPaused = false;
                            }
                            
                            //current interacting body
                            interactingBody = trackedBody;

                            if (UserSettings.Instance.IS_KINECTREGION_USED)
                            {
                                //hand states - just working well if kinect is mounted in front of user
                                rHand = trackedBody.HandRightState;
                                lHand = trackedBody.HandLeftState;
                            }
                            else
                            {
                                if (phiz.checkForInteraction(trackedBody, bodyIndexFrame))
                                {
                                    rHand = phiz.getHandState(HandType.RIGHT);
                                    lHand = phiz.getHandState(HandType.LEFT);
                                    canvasInteraction(phiz.getInteractionPoint(), phiz.getInteractionHand());

                                    testImg.Source = BitmapSourceConvert.ToBitmapSource(phiz.getVisOutputFromHandSegmentation(phiz.getInteractionHand()));
                                }
                            }
                        }
                    else
                    {
                        OnTrackingIdLost(null, null);
                    }
                }
            }
        }

        private void kinectCoreWindow_PointerMoved(object sender, KinectPointerEventArgs args)
        {
            KinectPointerPoint kinectPointerPoint = args.CurrentPoint;

            //just one of the >2 hands is the active one
            if (!kinectPointerPoint.Properties.IsEngaged) return;

            Point pointRelativeToKinectRegion = new Point(kinectPointerPoint.Position.X * kinectRegion.ActualWidth, kinectPointerPoint.Position.Y * kinectRegion.ActualHeight);

            //CANVAS INTERACTION
            canvasInteraction(pointRelativeToKinectRegion, kinectPointerPoint.Properties.HandType);

        }

        private void canvasInteraction(Point p,  HandType ht)
        {
            if (!isMenuOpen)
            {
                if ((ht == HandType.RIGHT && rHand == UserSettings.Instance.GESTURE_MOVE) || (ht == HandType.LEFT && lHand == UserSettings.Instance.GESTURE_MOVE))
                {
                    myCanvas.updateStrokes(p, SketchCanvas.UserAction.Move);
                }
                else if ((ht == HandType.RIGHT && rHand == UserSettings.Instance.GESTURE_DRAW) || (ht == HandType.LEFT && lHand == UserSettings.Instance.GESTURE_DRAW))
                {
                    myCanvas.updateStrokes(p, SketchCanvas.UserAction.Draw);
                }
                else if ((ht == HandType.RIGHT && rHand == UserSettings.Instance.GESTURE_RUBBER) || (ht == HandType.LEFT && lHand == UserSettings.Instance.GESTURE_RUBBER))
                {
                    myCanvas.updateStrokes(p, SketchCanvas.UserAction.Cancel);
                }

                if(!UserSettings.Instance.IS_KINECTREGION_USED)
                    setInteractionCursor(p, ht);
            }
        }

        private void setInteractionCursor(Point p, HandType ht)
        {
            HandState hs = HandState.Unknown;

            if (ht == HandType.RIGHT)
                hs = rHand;
            else if (ht == HandType.LEFT)
                hs = lHand;

            if (currentCursorBackHandState != hs)
            {
                if (hs == HandState.Open)
                {
                    interactionCursorBack.Source = new BitmapImage(new Uri("img/cursor.png", UriKind.Relative));
                }
                else if (hs == HandState.Lasso)
                {
                    interactionCursorBack.Source = new BitmapImage(new Uri("img/cursor_lasso.png", UriKind.Relative));
                }
                else if (hs == HandState.Closed)
                {
                    interactionCursorBack.Source = new BitmapImage(new Uri("img/cursor_closed.png", UriKind.Relative));
                }
                else
                {
                    interactionCursorBack.Visibility = Visibility.Hidden;
                    return;
                }

                if (ht == HandType.RIGHT && UserSettings.Instance.IS_KINECT_BEHIND_USER || ht == HandType.LEFT && !UserSettings.Instance.IS_KINECT_BEHIND_USER)
                {

                    interactionCursorBack.RenderTransformOrigin = new Point(0.5, 0.5);
                    ScaleTransform flipTrans = new ScaleTransform();
                    flipTrans.ScaleX = -1;
                    interactionCursorBack.RenderTransform = flipTrans;
                }
                else
                {
                    interactionCursorBack.RenderTransform = null;
                }

                interactionCursorBack.Visibility = Visibility.Visible;
            }


            double offsetX = interactionCursorBack.Width;
            double offsetY = interactionCursorBack.Height;

            if (double.IsNaN(offsetX) || double.IsNaN(offsetY))
                offsetX = offsetY = 0;

            interactionCursorBack.Margin = new Thickness(p.X - offsetX / 2, p.Y - offsetY / 2, 0, 0);
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
