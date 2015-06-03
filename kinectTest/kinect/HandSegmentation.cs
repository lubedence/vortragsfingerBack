using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Emgu.CV.Structure;
using Emgu.CV;
using votragsfinger2Back.util;
using votragsfinger2.util;

namespace votragsfinger2Back
{
    /// <summary>
    /// TODO: improvement needed regarding:
    /// a) Hand segmentation: eg. Depth based thresholding, remove segmented parts below hand wrist, ...
    /// b) Finger detection: better heuristics (or machine learning) to map hand/finger pose to HandState(Closed, Open, Lasso)
    /// 
    /// - Segments Hand from BodyIndexFrame using the position of the hand joint as well as the relation/proportion to the elbow. see above a)
    /// - Interpret segmented hand (eg. convex hull, defects, enclosingCircle, todo: k-curvature) and recognize hand/finger pose and map to HandState. see above b)
    /// 
    /// The current use of this class is to calculate the HandState of a tracked hand. Could be extended to return a stable hand-joint (center of hand).
    /// </summary>
    class HandSegmentation
    {

        private int width; //width of depth frame 
        private int height; //height of depth frame

        private int maxDist; //max radius for seedFill
        private int xSeed; //seedPoint(handcenter) x value
        private int ySeed; //seedPoint(handcenter) y value

        private byte[] inputData; //byte data from current BodyIndexFrame
        private byte[] searchedData; //segmented hand from current BodyIndexFrame

        //some data extracted from the segmented hand
        private Seq<MCvConvexityDefect> defects;
        private MCvBox2D box;
        private Seq<Point> filteredHull;
       
        //for debug purposes: visual feedback of segmented hand + data
        public Image<Bgr, byte> visOutput;
        public bool isVisOutputActive = false;

        //simple mode filtering of extracted HandState
        private FixSizeQueue<byte> filterHandState = new FixSizeQueue<byte>(8); //queue size == frame amount

        /// <summary>
        /// Init
        /// </summary>
        /// <param name="width">width of depth frame (512)</param>
        /// <param name="height">height of depth frame (424)</param>
        public HandSegmentation(int width, int height)
        {
            this.width = width;
            this.height = height;
            isVisOutputActive = UserSettings.Instance.IS_DEBUG_OUTPUT;
        }


        /// <summary>
        /// Starting from hand-joint position (x,y) try to segment hand using seed fill algo. 
        /// TODO: some improvement needed, eg. Depth based thresholding, remove segmented parts below hand wrist, ...
        /// Problem: sdk hand-joint position sometimes has large jitter 
        /// </summary>
        /// <param name="x">hand-joint x</param>
        /// <param name="y"> hand-joint y</param>
        private void seedFill(int x, int y)
        {
            Queue<Point> queue = new Queue<Point>();
            queue.Enqueue(new Point(x, y));

            while (queue.Count > 0)
            {
                Point p = queue.Dequeue();
                if (p.X < 0 || p.X > width - 1 || p.Y < 0 || p.Y > height - 1) continue;

                int depthIndex = (p.Y * width) + p.X;
                byte isPlayerPixel = inputData[depthIndex];

                double dX = Math.Abs((xSeed - p.X));
                double dY = Math.Abs((ySeed - p.Y));

                if (searchedData[depthIndex] != 255) continue;

                if (Math.Sqrt(dX * dX + dY * dY) > maxDist * 0.5 || isPlayerPixel == 0xff)
                {
                    searchedData[depthIndex] = 0;
                    continue;
                }

                searchedData[depthIndex] = 1;

                queue.Enqueue(new Point(p.X + 0, p.Y + 1));
                queue.Enqueue(new Point(p.X + 0, p.Y - 1));
                queue.Enqueue(new Point(p.X + 1, p.Y + 0));
                queue.Enqueue(new Point(p.X - 1, p.Y + 0));
            }
        }


        /// <summary>
        /// Starting from hand-joint position (x,y) try to segment hand using seed fill algo. 
        /// </summary>
        /// <param name="data">BodyIndexFrame byte data</param>
        /// <param name="x">hand joint x value</param>
        /// <param name="y">hand joint y value</param>
        /// <param name="dist">max distance to hand joint</param>
        /// <returns></returns>
        public byte[] searchFloodFill(byte[] data, int x, int y, int dist)
        {
            inputData = data;
            searchedData = new byte[data.Length];
            for (int i = 0; i < width * height; ++i)
                searchedData[i] = 255;

            xSeed = x;
            ySeed = y;

            maxDist = dist;

            seedFill(x, y);

            return searchedData;
        }

        //just for debug purposes
        public byte[] getBitmapData(int bytesPerPixel)
        {
            byte[] pixels = new byte[width * height * bytesPerPixel];
            int pixelIndex = 0;

            for (int i = 0; i < width*height; ++i)
            {
                System.Windows.Media.Color c = Colors.Black;
                if (searchedData[i] == 1) c = Colors.White;
                pixels[pixelIndex++] = c.B;
                pixels[pixelIndex++] = c.G;
                pixels[pixelIndex++] = c.R;
                pixelIndex++;
            }

            return pixels;
        }


        /// <summary>
        /// Recognize HandState of segmented hand.
        /// </summary>
        /// <returns>HandState of segmented hand</returns>
        public Microsoft.Kinect.HandState ExtractHandState()
        {
            //Create gray-image matrix out of segmented hand byte array
            byte[, ,] pixels = new byte[height, width, 1];

            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    byte val = 0;
                    if (searchedData[(y * width) + x] == 1)
                        val = 255;
                    pixels[y, x, 0] = val;
                }
            }


            return ExtractHandState(new Image<Gray, byte>(pixels));
        }


        /// <summary>
        /// Recognize HandState of segmented hand.
        /// </summary>
        /// <param name="handSegment">Gray image matrix of segmented hand</param>
        /// <returns>HandState of segmented hand</returns>
        private Microsoft.Kinect.HandState ExtractHandState(Image<Gray, byte> handSegment)
        {
            using (MemStorage storage = new MemStorage())
            {
                //search biggest contour in image
                //could be removed - there should always be just one blob(==segmented hand)
                Contour<Point> contours = handSegment.FindContours(Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE, Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_LIST, storage);
                Contour<Point> biggestContour = null;
                Double biggestContourArea = 0;
                while (contours != null)
                {
                    if (contours.Area > biggestContourArea)
                    {
                        biggestContourArea = contours.Area;
                        biggestContour = contours;
                    }
                    contours = contours.HNext;
                }

                //debug
                if (isVisOutputActive)
                    visOutput = new Image<Bgr, byte>(handSegment.Bitmap);

                if (biggestContour != null)
                {
                    biggestContour = biggestContour.ApproxPoly(biggestContour.Perimeter * 0.0025, storage);
                    //visOutput.Draw(biggestContour, new Bgr(System.Drawing.Color.Green), 2);

                    //extract convex hull
                    Seq<Point> hull = biggestContour.GetConvexHull(Emgu.CV.CvEnum.ORIENTATION.CV_CLOCKWISE);
                    //extract minimum area rectangle
                    box = biggestContour.GetMinAreaRect();
                    //PointF[] points = box.GetVertices();

                    //Debug
                    if (isVisOutputActive)
                        visOutput.Draw(new CircleF(new Point(xSeed, ySeed), 3), new Bgr(200, 125, 75), 2);

                    //filter convex hull: remove point if distance to neighboring point is too small
                    filteredHull = new Seq<Point>(storage);
                    for (int i = 0; i < hull.Total; i++)
                    {
                        if (hull[i].Y < ySeed && getDist(hull[i], hull[i + 1]) > box.size.Width / 10)
                        {
                            filteredHull.Push(hull[i]);
                        }
                    }

                    //Debug
                    if (isVisOutputActive && false)
                        for (int i = 0; i < filteredHull.Total; i++)
                        {
                            CircleF hullCircle = new CircleF(new PointF((float)filteredHull[i].X, (float)filteredHull[i].Y), 4);
                            visOutput.Draw(hullCircle, new Bgr(200, 125, 75), 2);
                        }

                    //calc convexity defacts
                    defects = biggestContour.GetConvexityDefacts(storage, Emgu.CV.CvEnum.ORIENTATION.CV_CLOCKWISE);


                    return calcHandState(); //use data of hand which has been calculated above to recognize the HandState
                }
            }

            return Microsoft.Kinect.HandState.Unknown;
        }


        /// <summary>
        /// TODO: Not finshed, just some expermimental code
        /// </summary>
        /// <returns>return HandState of Segmented Hand using data, calculated in ExtractHandState(..)</returns>
        public Microsoft.Kinect.HandState calcHandState()
       {
           int fingerAmount = 0;

           MCvConvexityDefect[] defectArray = defects.ToArray();
           
            
           /*PointF[] enclosingPointsDepth = new PointF[defects.Total];
           PointF[] enclosingPointsStart = new PointF[defects.Total];

           if (defects.Total > 0) { 
               for (int i = 0; i < defects.Total; i++)
               {
                   enclosingPointsDepth[i] = new PointF((float)defectArray[i].DepthPoint.X, (float)defectArray[i].DepthPoint.Y);
                   enclosingPointsStart[i] = new PointF((float)defectArray[i].StartPoint.X, (float)defectArray[i].StartPoint.Y);
               }

               CircleF minEncCircleDepth = Emgu.CV.PointCollection.MinEnclosingCircle(enclosingPointsDepth);
               CircleF minEncCircleStart = Emgu.CV.PointCollection.MinEnclosingCircle(enclosingPointsStart);
               //visOutput.Draw(minEncCircleDepth, new Bgr(100, 125, 75), 2);
               //visOutput.Draw(minEncCircleStart, new Bgr(200, 125, 75), 2);
        }*/

           
          for (int i = 0; i < defects.Total; i++)
          {
              PointF startPoint = new PointF((float)defectArray[i].StartPoint.X, (float)defectArray[i].StartPoint.Y);

              PointF depthPoint = new PointF((float)defectArray[i].DepthPoint.X, (float)defectArray[i].DepthPoint.Y);

              PointF endPoint = new PointF((float)defectArray[i].EndPoint.X, (float)defectArray[i].EndPoint.Y);

              LineSegment2D startDepthLine = new LineSegment2D(defectArray[i].StartPoint, defectArray[i].DepthPoint);

              LineSegment2D depthEndLine = new LineSegment2D(defectArray[i].DepthPoint, defectArray[i].EndPoint);

              //visOutput.Draw(new CircleF(depthPoint, 2), new Bgr(200, 125, 75), 2);
               
             // if ((startPoint.Y - ySeed < 0 && depthPoint.Y - ySeed < 0) && (startPoint.Y < depthPoint.Y) && (Math.Sqrt(Math.Pow(startPoint.X - depthPoint.X, 2) + Math.Pow(startPoint.Y - depthPoint.Y, 2)) > maxDist / 8))
              if ((startPoint.Y - ySeed < 0) && (startPoint.Y < depthPoint.Y) && (Math.Sqrt(Math.Pow(startPoint.X - depthPoint.X, 2) + Math.Pow(startPoint.Y - depthPoint.Y, 2)) > maxDist / 8))
              {
                  double angle1 = Math.Atan2(startPoint.Y - depthPoint.Y, startPoint.X - depthPoint.X) * 180.0 / Math.PI;
                  double angle2 = Math.Atan2(endPoint.Y - depthPoint.Y, endPoint.X - depthPoint.X) * 180.0 / Math.PI;
                  if (angle1 < 0) angle1 += 360;
                  if (angle2 < 0) angle2 += 360;

                  if(Math.Abs(angle1 - angle2) < 90)
                    fingerAmount++;
                  if (isVisOutputActive)
                       visOutput.Draw(startDepthLine, new Bgr(System.Drawing.Color.Green), 2);
              }

          }
           


           return getHandStateFromFingerAmount(fingerAmount);
       }


        private double getDist(double x1, double y1, double x2, double y2)
        {
            return Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2));
        }

        private double getDist(PointF p1, PointF p2)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }




        /// <summary>
        /// TODO: Not finished, just some experimental code
        /// final mode filtering and classification using number of fingers. 
        /// HandState.Closed == 0 fingers
        /// HandState.Lasso == Peace/Victory gesture - TODO: to be consistent with the SDK gesture: 1 finger (should be forefinger, or one "thick finger" out of forefinger and middle finger.
        /// HandState.Open == 5 fingers (or more than 1)
        /// </summary>
        /// <param name="fingers">amount of visible fingers - extracted from segmented hand</param>
        /// <returns>HandState</returns>
        private Microsoft.Kinect.HandState getHandStateFromFingerAmount(int fingers)
        {
            filterHandState.Enqueue((byte)fingers);

            byte[] arr = filterHandState.ToArray();
            
            int mode = arr.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key;

            if (isVisOutputActive)
            {
                MCvFont font = new MCvFont(Emgu.CV.CvEnum.FONT.CV_FONT_HERSHEY_DUPLEX, 2d, 2d);
                visOutput.Draw(fingers.ToString() + "::" + mode.ToString(), ref font, new Point(50, 150), new Bgr(System.Drawing.Color.White));
            }

            if (mode > 1)
                return Microsoft.Kinect.HandState.Open;
            else if (mode > 0)
                return Microsoft.Kinect.HandState.Lasso;
            else
                return Microsoft.Kinect.HandState.Closed;

        }

    }
}
