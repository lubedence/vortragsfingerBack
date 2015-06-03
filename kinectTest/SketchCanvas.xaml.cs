using votragsfinger2.util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace votragsfinger2
{
    /// <summary>
    /// Interaktionslogik für SketchCanvas.xaml
    /// </summary>
    public partial class SketchCanvas : InkCanvas
    {
   
        public enum UserAction {Draw, Move, Cancel};
        public enum DrawType {Freehand,  FreehandStraight, Line};

        private List<Point> stroke = new List<Point>();
        private Point lastDrawnPoint;
        private Point smoothPoint;
        private bool isUserDrawing = false;
        private UserAction lastUserAction = UserAction.Move;
        private DrawType lineDrawType = DrawType.Freehand;

        private Stopwatch lastUserActionTime = new Stopwatch();

        private double lineThickness = 10;
        private Color lineColor = Color.FromRgb(0, 0, 0);

        public double LineThickness
        {
            get { return this.lineThickness; }
            set { this.lineThickness = value; }
        }

        public Color LineColor
        {
            get { return this.lineColor; }
            set { this.lineColor = value; }
        }

        public DrawType LineDrawType
        {
            get { return this.lineDrawType; }
            set { this.lineDrawType = value; }
        }

        public SketchCanvas()
        {
            InitializeComponent();
            lastUserActionTime.Start();
        }

        /// <summary>
        /// Freehand drawing mode
        /// </summary>
        /// <param name="nextPoint">next point which should be drawn</param>
        private void drawFreehand(Point nextPoint)
        {
            if (!isUserDrawing)
            {
                isUserDrawing = true;
            }

            Line line = new Line();
            line.Stroke = new SolidColorBrush(lineColor);
            line.StrokeThickness = lineThickness*1.5;

            line.X1 = lastDrawnPoint.X;
            line.Y1 = lastDrawnPoint.Y;
            line.X2 = nextPoint.X;
            line.Y2 = nextPoint.Y;
            line.StrokeDashCap = PenLineCap.Round;
            line.StrokeStartLineCap = PenLineCap.Round;
            line.StrokeEndLineCap = PenLineCap.Round;
            this.Children.Add(line);

            if(stroke.Count == 0)
                stroke.Add(new Point(lastDrawnPoint.X, lastDrawnPoint.Y));

            stroke.Add(nextPoint);
            
            lastDrawnPoint = nextPoint;
        }

        /// <summary>
        /// Freehand straight (horizontal and vertical) lines mode
        /// TODO: needs some tuning
        /// </summary>
        /// <param name="nextPoint">next point which should be drawn</param>
        private void drawFreehandStraight(Point nextPoint)
        {
            if (!isUserDrawing)
            {
                isUserDrawing = true;
            }

            Line line = new Line();
            line.Stroke = new SolidColorBrush(lineColor);
            line.StrokeThickness = lineThickness * 1.5;

            double deltaX = nextPoint.X - lastDrawnPoint.X;
            double deltaY = nextPoint.Y - lastDrawnPoint.Y;

            double angle = Math.Atan(deltaY / deltaX)* 180 / Math.PI;
            double angle4 = Math.Abs(Math.Abs(angle - 180) - 90);

            double dist = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY,2));

            if (angle4 < UserSettings.Instance.LINE_FREEHAND_STRAIGHT_ANGLE && dist > UserSettings.Instance.LINE_FREEHAND_STRAIGHT_MIN_DIST) //horizontal line
            {
                nextPoint.X = lastDrawnPoint.X;
            }
            else if (angle4 > (90 - UserSettings.Instance.LINE_FREEHAND_STRAIGHT_ANGLE) && dist > UserSettings.Instance.LINE_FREEHAND_STRAIGHT_MIN_DIST)  //vertical line
            {
                nextPoint.Y = lastDrawnPoint.Y;
            }
            else
            {
                return;
            }

            line.X1 = lastDrawnPoint.X;
            line.Y1 = lastDrawnPoint.Y;
            line.X2 = nextPoint.X;
            line.Y2 = nextPoint.Y;
            line.StrokeDashCap = PenLineCap.Round;
            line.StrokeStartLineCap = PenLineCap.Round;
            line.StrokeEndLineCap = PenLineCap.Round;
            this.Children.Add(line);

            if (stroke.Count == 0)
                stroke.Add(new Point(lastDrawnPoint.X, lastDrawnPoint.Y));

            stroke.Add(nextPoint);

            lastDrawnPoint = nextPoint;
        }

        /// <summary>
        /// draw lines mode: specify start and end point of the line
        /// </summary>
        /// <param name="nextPoint">new end point</param>
        private void drawLine(Point nextPoint)
        {
            if (!isUserDrawing)
            {
                isUserDrawing = true;
            }

            this.Children.Clear();
            if (stroke.Count > 0)
            {
                stroke.RemoveAt(stroke.Count-1);
            }

            Line line = new Line();
            line.Stroke = new SolidColorBrush(lineColor);
            line.StrokeThickness = lineThickness * 1.5;

            line.X1 = lastDrawnPoint.X;
            line.Y1 = lastDrawnPoint.Y;
            line.X2 = nextPoint.X;
            line.Y2 = nextPoint.Y;
            line.StrokeDashCap = PenLineCap.Round;
            line.StrokeStartLineCap = PenLineCap.Round;
            line.StrokeEndLineCap = PenLineCap.Round;
            this.Children.Add(line);

            if (stroke.Count == 0)
                stroke.Add(new Point(lastDrawnPoint.X, lastDrawnPoint.Y));

            stroke.Add(nextPoint);

        }

        /// <summary>
        /// checks if user has completed his drawing move. if yes, convert single drawn lines to stroke and add them to the canvas
        /// </summary>
        /// <param name="nextPoint"></param>
        private void evaluateStrokePart(Point nextPoint)
        {
            int distToLastDrawnPoint = (int)Math.Sqrt(Math.Pow((lastDrawnPoint.X - nextPoint.X), 2) + Math.Pow((lastDrawnPoint.Y - nextPoint.Y), 2));

            if (isUserDrawing && distToLastDrawnPoint > UserSettings.Instance.LINE_RESUME_THRESHOLD)
            {
                isUserDrawing = false;

                if (stroke.Count > 0)
                {
                    Stroke _s = new Stroke(new StylusPointCollection(stroke));
                    _s.DrawingAttributes.Color = lineColor;
                    _s.DrawingAttributes.Width = lineThickness;
                    _s.DrawingAttributes.Height = lineThickness;
                    this.Strokes.Add(_s);
                    stroke.Clear();
                }
                this.Children.Clear();

            }

           // if (distToLastDrawnPoint > LINE_RESUME_THRESHOLD)
            if (!isUserDrawing)
            {
                lastDrawnPoint = new Point(nextPoint.X, nextPoint.Y);
                smoothPoint    = new Point(nextPoint.X, nextPoint.Y);
            }
        }

        /// <summary>
        /// delete strokes that are near the hand-position (nextPoint)
        /// </summary>
        /// <param name="nextPoint">surrounding strokes will be deleted</param>
        private void deleteStrokes(Point nextPoint)
        {
            this.Strokes.Remove(this.Strokes.HitTest(nextPoint, UserSettings.Instance.RUBBER_SIZE));
        }


        /// <summary>
        /// Manage UserAction (Draw stroke, Cancel stroke, Move)
        /// </summary>
        /// <param name="nextPoint">position of hand-cursor</param>
        /// <param name="ua">action for current HandState</param>
        public void updateStrokes(Point nextPoint, UserAction ua)
        {
            switch (ua)
            {
                case UserAction.Move:
                    evaluateStrokePart(nextPoint);
                    lastUserActionTime.Restart();
                    lastUserAction = UserAction.Move;
                    break;
                case UserAction.Draw:
                    if (lastUserAction != UserAction.Cancel && lastUserActionTime.ElapsedMilliseconds > UserSettings.Instance.USER_ACTION_MIN_TIME)
                    {

                        smoothPoint = new Point(smoothPoint.X * UserSettings.Instance.SMOOTHING + nextPoint.X * (1 - UserSettings.Instance.SMOOTHING), smoothPoint.Y * UserSettings.Instance.SMOOTHING + nextPoint.Y * (1 - UserSettings.Instance.SMOOTHING));

                        if (lineDrawType == DrawType.Freehand)
                            drawFreehand(smoothPoint);
                        else if (lineDrawType == DrawType.FreehandStraight)
                            drawFreehandStraight(smoothPoint);
                        else if (lineDrawType == DrawType.Line)
                            drawLine(nextPoint);
                        else return;

                        lastUserAction = UserAction.Draw;
                    } 
                    break;
                case UserAction.Cancel:
                    if (lastUserAction != UserAction.Draw && lastUserActionTime.ElapsedMilliseconds > UserSettings.Instance.USER_ACTION_MIN_TIME)
                    {
                        deleteStrokes(nextPoint);
                        lastUserAction = UserAction.Cancel;
                    }
                    break;
                default:
                    return;
            }
        }
    }
}
