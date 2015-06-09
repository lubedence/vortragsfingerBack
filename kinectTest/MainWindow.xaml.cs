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
        //gesture recognition for hide&show program as well as menu
        VisualGestureBuilderFrameSource _gestureSource;
        VisualGestureBuilderFrameReader _gestureReader;
        Stopwatch lastGestureTime = new Stopwatch(); //a gesture needs to be recognized for a specific amount of time
        Gesture openingGesture; //gesture to open program or menu
        Gesture closingGesture; //gesture to close program or menu

        //Handstate of both hands, updated every time a new frame arrives 
        HandState rHandState = HandState.Unknown;
        HandState lHandState = HandState.Unknown;

        //flag if menu is open or closed
        bool isMenuOpen = false;

        //variables for hiding windows-cursor as soon as the cursor stands still for a while 
        private DateTime lastMouseMove = DateTime.Now;
        private bool isMouseHidden = false;
        DispatcherTimer dispatcherTimer = new DispatcherTimer();
        Cursor currentCursor;

        //own physical interaction zone + handsegmentation / Handstate recognition. Used if Kinect is mounted behind the user, or you do not want to use the Microsoft KinectRegion implementation
        private Phiz phiz;
        private HandState currentCursorBackHandState = HandState.Unknown; 

        public MainWindow()
        {
            InitializeComponent();
            UserSettings.Instance.parseIniFile();   //read and set user parameters
            MinimizeToTray.Enable(this);            //minimize to tray

            KinectSetup();  //kinect sensor setup
            
            //menu handlers, if user changes drawing properties on the menu
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

        /// <summary>
        /// handler: drawing color changed
        /// </summary>
        /// <param name="c">the new color</param>
        private void kinectMenu_ColorChanged(Color c)
        {
            myCanvas.LineColor = c;
            myCanvas.DefaultDrawingAttributes.Color = c;
        }

        /// <summary>
        /// handler: drawing thickness changed
        /// </summary>
        /// <param name="t">the new thickness</param>
        private void kinectMenu_ThicknessChanged(Double t)
        {
            myCanvas.LineThickness = t;
            myCanvas.DefaultDrawingAttributes.Height = t;
            myCanvas.DefaultDrawingAttributes.Width = t;
        }

        /// <summary>
        /// handler: drawing type changed (like free drawing, or drawing straight lines)
        /// </summary>
        /// <param name="dt">the new DrawingType</param>
        private void kinectMenu_DrawTypeChanged(votragsfinger2.SketchCanvas.DrawType dt)
        {
            myCanvas.LineDrawType = dt;
        }

        /// <summary>
        /// checks if enough time has passed since last windows-cursor movement. if yes, hide windows-cursor.
        /// </summary>
        /// <param name="sender">not used in this context</param>
        /// <param name="e">not used in this context</param>
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            if (DateTime.Now - lastMouseMove > TimeSpan.FromSeconds(2) && !isMouseHidden)
            {
                currentCursor = Mouse.OverrideCursor;
                Mouse.OverrideCursor = Cursors.None;
                isMouseHidden = true;
            }
        }

        /// <summary>
        /// Handler: Windows-cursor has moved.
        /// TODO: use cursor coordinates to check if the cursor actualy moved -> event is triggered also at other circumstances
        /// </summary>
        /// <param name="sender">not used in this context</param>
        /// <param name="e">not used in this context</param>
        private void mainWindow_MouseMove(object sender, MouseEventArgs e)
        {
            lastMouseMove = DateTime.Now;

            if (isMouseHidden)
            {
                Mouse.OverrideCursor = currentCursor;
                isMouseHidden = false;
            }
        }

        /// <summary>
        /// 1) tries to get and open a Kinect-sensor
        /// 2) checks if user wants to use Kinect-Region, or the custom Phiz & Handstate recognition 
        /// 3) initialize gesture recognition to show/hide menu or the program itself
        /// TODO: Close sensor method & call
        /// </summary>
        /// <returns></returns>
        private bool KinectSetup()
        {
            KinectSensor _sensor = KinectSensor.GetDefault();
            if (_sensor == null)
            {
                return false;
            }

            //Use KinectRegion (integrated in Kinect SDK)
            if (UserSettings.Instance.IS_KINECTREGION_USED)
            {
                KinectRegion.SetKinectRegion(this, kinectRegion);
                App app = ((App)Application.Current);
                app.KinectRegion = kinectRegion;
                kinectRegion.KinectSensor = _sensor;
                KinectCoreWindow kinectCoreWindow = KinectCoreWindow.GetForCurrentThread();
                kinectCoreWindow.PointerMoved += kinectCoreWindow_PointerMoved; //fired as soon as user moves his hand (if this hand is identified as interacting)
                
            }
            else
            {
                //rough-and-ready way to disable kinect region (hide the cursor)
                kinectRegion.CursorSpriteSheetDefinition = new CursorSpriteSheetDefinition(new Uri("img/cursor.png", UriKind.Relative), 0, 0, 0, 0);

                //init own phiz and hand state recognition
                phiz = new Phiz(UserSettings.Instance.IS_KINECT_BEHIND_USER); 
            }

            //open sensor
            _sensor.Open(); 
            //add handler
            _sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Body | FrameSourceTypes.BodyIndex).MultiSourceFrameArrived += OnMultiSourceFrameArrived;

            LoadGestures(); //gesture recognition for showing/hiding menu or the program itself

            return true;
        }

        /// <summary>
        /// gesture recognition for showing/hiding menu or the program itself.
        /// Trained with Microsoft Visual Gesture Builder.
        /// Two different gestures: 
        /// a) "HandsApart" for showing the program itself (if minimized) or the menu (if hidden)
        /// b) "HandsTogether" for hiding the menu (if shown) or the program itself (if not minimized)
        /// 
        /// TODO: Catch exceptions if gesture files cant be found.
        /// </summary>
        void LoadGestures()
        {
            VisualGestureBuilderDatabase db = new VisualGestureBuilderDatabase(@"gestures/gestures1.gbd");
            this.openingGesture = db.AvailableGestures.Where(g => g.Name == "HandsApart").Single();
            this.closingGesture = db.AvailableGestures.Where(g => g.Name == "HandsTogether").Single();

            this._gestureSource = new VisualGestureBuilderFrameSource(KinectSensor.GetDefault(), 0);

            this._gestureSource.AddGesture(this.openingGesture);
            this._gestureSource.AddGesture(this.closingGesture);

            this._gestureSource.TrackingIdLost += OnTrackingIdLost;

            this._gestureReader = this._gestureSource.OpenReader();
            this._gestureReader.IsPaused = true;
            this._gestureReader.FrameArrived += OnGestureFrameArrived; //handler for new frames to check for gestures

            lastGestureTime.Start();
        }

        void OnTrackingIdLost(object sender, TrackingIdLostEventArgs e)
        {
            this._gestureReader.IsPaused = true;
        }

        /// <summary>
        /// handler: new frame arrived and is checked for gestures
        /// </summary>
        /// <param name="sender">not used in this context</param>
        /// <param name="e">contains new frame</param>
        void OnGestureFrameArrived(object sender, VisualGestureBuilderFrameArrivedEventArgs e)
        {
            using (var frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    var discreteResults = frame.DiscreteGestureResults;
                    if (discreteResults == null) return;

                    //Closing Gesture
                    if (discreteResults.ContainsKey(this.closingGesture))
                    {
                        var result = discreteResults[this.closingGesture];
                        if (result.Detected && result.Confidence > UserSettings.Instance.GESTURE_MIN_CONFIDENCE && lastGestureTime.ElapsedMilliseconds > UserSettings.Instance.GESTURE_MIN_TIME)
                        {
                            lastGestureTime.Restart();
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

                    //Opening Gesture
                    if (discreteResults.ContainsKey(this.openingGesture))
                    {
                        var result = discreteResults[this.openingGesture];
                        if (result.Detected && result.Confidence > UserSettings.Instance.GESTURE_MIN_CONFIDENCE && lastGestureTime.ElapsedMilliseconds > UserSettings.Instance.GESTURE_MIN_TIME)
                        {
                            lastGestureTime.Restart();
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

        /// <summary>
        /// handler: new multisource-frame arrived.
        /// if KinectRegion is used: just extraced HandState for both hands
        /// if not used: calc HandState for both hands, show interacting hand-position on screen as hand-cursor and carry drawing interactions out
        /// </summary>
        /// <param name="sender">not used in this context</param>
        /// <param name="e">contains new multisource-frame</param>
        void OnMultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            var reference = e.FrameReference.AcquireFrame();

            using (var bodyFrame = reference.BodyFrameReference.AcquireFrame())
            using (var bodyIndexFrame = reference.BodyIndexFrameReference.AcquireFrame())
            {
                if (bodyFrame != null && bodyIndexFrame != null)
                {
                    IList<Body> _bodies = new Body[bodyFrame.BodyFrameSource.BodyCount];
                    bodyFrame.GetAndRefreshBodyData(_bodies);
                    var trackedBody = _bodies.Where(b => b.IsTracked).FirstOrDefault();

                    if (trackedBody != null)
                        {
                            //information about user for gesture recognition
                            if (this._gestureReader != null && this._gestureReader.IsPaused)
                            {
                                this._gestureSource.TrackingId = trackedBody.TrackingId;
                                this._gestureReader.IsPaused = false;
                            }

                            //hand states - just working well if kinect is mounted in front of user
                            if (UserSettings.Instance.IS_KINECTREGION_USED)
                            {
                                rHandState = trackedBody.HandRightState;
                                lHandState = trackedBody.HandLeftState;
                            }
                            else
                            {
                                //if one of the two hands is interacting
                                if (phiz.checkForInteraction(trackedBody, bodyIndexFrame))
                                {
                                    rHandState = phiz.getHandState(HandType.RIGHT);
                                    lHandState = phiz.getHandState(HandType.LEFT);
                                    //show interacting hand position as cursor and carry drawing interactions out
                                    canvasInteraction(phiz.getInteractionPoint(), phiz.getInteractionHand());

                                    //just for debugging purposes: if activated in HandSegmentation.cs, a not-null image of the segmented hand with some graphical information is returned
                                    testImg.Source = BitmapSourceConvert.ToBitmapSource(phiz.getVisOutputFromHandSegmentation(phiz.getInteractionHand()));
                                }
                                else
                                {
                                    interactionCursorBack.Visibility = Visibility.Hidden;
                                    currentCursorBackHandState = HandState.Unknown;
                                }
                            }
                        }
                    else
                    {
                        OnTrackingIdLost(null, null); //used for gesture recogntion (hide/show menu/programm)
                    }
                }
            }
        }

        /// <summary>
        /// handler: called if KinectRegion is activated and the interacting hand has moved
        /// </summary>
        /// <param name="sender">not used in this context</param>
        /// <param name="args">contains current hand position as KinectPointerPoint</param>
        private void kinectCoreWindow_PointerMoved(object sender, KinectPointerEventArgs args)
        {
            //current hand position
            KinectPointerPoint kinectPointerPoint = args.CurrentPoint;

            //just one of the >2 hands is the active one
            if (!kinectPointerPoint.Properties.IsEngaged) return;

            //map hand position to display coordinates (kinectRegion == display-width/height)
            Point pointRelativeToKinectRegion = new Point(kinectPointerPoint.Position.X * kinectRegion.ActualWidth, kinectPointerPoint.Position.Y * kinectRegion.ActualHeight);

            //carry drawing interactions out
            canvasInteraction(pointRelativeToKinectRegion, kinectPointerPoint.Properties.HandType);

        }

        /// <summary>
        /// carries drawing interactions out. 
        /// if KinectRegion is not used, shows hand-cursor in addition
        /// </summary>
        /// <param name="p">interacting hand as screen coordinates</param>
        /// <param name="ht">left or right hand</param>
        private void canvasInteraction(Point p,  HandType ht)
        {
            if (!isMenuOpen)
            {
                if ((ht == HandType.RIGHT && rHandState == UserSettings.Instance.GESTURE_MOVE) || (ht == HandType.LEFT && lHandState == UserSettings.Instance.GESTURE_MOVE))
                {
                    myCanvas.updateStrokes(p, SketchCanvas.UserAction.Move);
                }
                else if ((ht == HandType.RIGHT && rHandState == UserSettings.Instance.GESTURE_DRAW) || (ht == HandType.LEFT && lHandState == UserSettings.Instance.GESTURE_DRAW))
                {
                    myCanvas.updateStrokes(p, SketchCanvas.UserAction.Draw);
                }
                else if ((ht == HandType.RIGHT && rHandState == UserSettings.Instance.GESTURE_RUBBER) || (ht == HandType.LEFT && lHandState == UserSettings.Instance.GESTURE_RUBBER))
                {
                    myCanvas.updateStrokes(p, SketchCanvas.UserAction.Cancel);
                }

                //if KinectRegion is not used, show hand-cursor
                if(!UserSettings.Instance.IS_KINECTREGION_USED)
                    setInteractionCursor(p, ht);
            }
        }

        /// <summary>
        /// sets and shows hand-cursor
        /// </summary>
        /// <param name="p">screeen coordinates, position of cursor</param>
        /// <param name="ht">left or right hand</param>
        private void setInteractionCursor(Point p, HandType ht)
        {
            HandState hs = HandState.Unknown;

            if (ht == HandType.RIGHT)
                hs = rHandState;
            else if (ht == HandType.LEFT)
                hs = lHandState;

            if (currentCursorBackHandState != hs)
            {
                currentCursorBackHandState = hs;
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


            //Todo: calc offset relative to icon-size
            double offsetX = 20;
            double offsetY = 20;

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
