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
using votragsfinger2Back.kinect;

namespace votragsfinger2Back
{
    /// <summary>
    /// TODO: improvement needed regarding:
    /// a) Hand segmentation: eg. Depth based thresholding
    /// b) Finger detection: better heuristics (or machine learning) to map hand/finger pose to HandState(Closed, Open, Lasso)
    /// 
    /// - Segments Hand from BodyIndexFrame using the position of the hand joint as well as the relation/proportion to the elbow. see above a)
    /// - Interpret segmented hand (eg. convex hull, defects, enclosingCircle, ...) and recognize hand/finger pose and map to HandState. see above b)
    /// 
    /// The former use of this class was to calculate the HandState of a tracked hand. Was extended to return a stable hand-joint (center of hand).
    /// </summary>
    class HandSegmentation
    {

        private int width; //width of depth frame 
        private int height; //height of depth frame

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
        private FixSizeQueue<byte> filterHandState = new FixSizeQueue<byte>(12); //queue size == frame amount

        //hand segment (binary)
        private Image<Gray, byte> handSegment;

        //centroid of segmented hand
        private Point handCentroid;
        private PointF handCentroidSmoothed;
        private DoubleExponentialFilter handCentroidFilter;
        private double centroidDistToContour = 0;

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

            handCentroidFilter = new DoubleExponentialFilter(0.5f, 0.3f, 0, 5f, 10f);
        }


        /// <summary>
        /// Starting from hand-joint position (x,y) try to segment hand using customized seed fill algo. 
        /// TODO: some improvement needed, eg. Depth based thresholding, using rgb-image, ...
        /// Problem: sdk hand-joint position sometimes has large jitter (centroid of segmented hand will be used instead for further actions)
        /// </summary>
        /// <param name="x">hand-joint x</param>
        /// <param name="y"> hand-joint y</param>
        /// <param name="x2">elbow-joint x</param>
        /// <param name="y2"> elbow-joint y</param>
        private bool seedFill(int x, int y, int x2, int y2)
        {
            Queue<Point> queue = new Queue<Point>();
            queue.Enqueue(new Point(x, y));

            //seed fill
            while (queue.Count > 0)
            {
                Point p = queue.Dequeue();
                if (p.X < 0 || p.X > width - 1 || p.Y < 0 || p.Y > height - 1) continue;

                int depthIndex = (p.Y * width) + p.X;
                byte isPlayerPixel = inputData[depthIndex];

                double dX = (xSeed - p.X);
                double dY = (ySeed - p.Y);

                if (searchedData[depthIndex] < 250) continue;
                if (isPlayerPixel == 0xff)
                {
                    searchedData[depthIndex] = 0;
                    continue;
                }

                double distToHandCenter = Math.Sqrt(dX * dX + dY * dY);
                //calculate distance between hand joint and elbow joint. used to have some kind of proportion
                double maxDist = getDist(x, y, x2, y2);
                if ((distToHandCenter > maxDist * 0.5 && dY < 0) || (distToHandCenter > maxDist * 1.5 && dY >= 0) || getDist(p.X, p.Y, x2, y2) < maxDist * 0.65)
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


            //Create gray-image matrix out of segmented hand byte array
            byte[, ,] pixels = new byte[height, width, 1];

            int minX = width, minY = 0, maxX = 0, maxY = height;
            bool firstVal = false;
            for (int _y = 0; _y < height; ++_y)
            {
                bool rowIsEmtpy = true;
                for (int _x = 0; _x < width; ++_x)
                {
                    byte val = 0;
                    if (searchedData[(_y * width) + _x] == 1)
                    {
                        val = 255;
                        if (rowIsEmtpy) rowIsEmtpy = false;
                        if (!firstVal)
                        {
                            firstVal = true;
                            minY = _y;
                        }
                        if (minX > _x) minX = _x;
                        if (maxX < _x) maxX = _x;
                    }
                    pixels[_y, _x, 0] = val;
                }
                if (firstVal && rowIsEmtpy)
                {
                    maxY = _y;
                    break;
                }
            }

         handSegment = null;
         if (maxX - minX < 15 || maxY - minY <15)
             return false;

         handSegment = new Image<Gray, byte>(pixels);
         handSegment.ROI = new Rectangle(minX, minY, maxX - minX, maxY - minY); //further calculations just in this region of the image
         
            return true;
        }

        private void FindCentroidByDistanceTrans(Image<Gray, byte> binary_image)
        {
            double max_value = 0.0d, min_value = 0.0d;

            handCentroid = new Point(0, 0);
            Point min_location = new Point(0, 0);

            using (Image<Gray, float> distTransform = new Image<Gray, float>(binary_image.Width, binary_image.Height))
            {
                CvInvoke.cvDistTransform(binary_image, distTransform, Emgu.CV.CvEnum.DIST_TYPE.CV_DIST_L2, 3, null, IntPtr.Zero);
                CvInvoke.cvMinMaxLoc(distTransform, ref min_value, ref max_value, ref min_location, ref handCentroid, IntPtr.Zero);
                centroidDistToContour = max_value;
            }

            //smoothing centroid
            handCentroidFilter.UpdateFilter(new PointF((float)(handCentroidSmoothed.X * 0.7 + (handCentroid.X + handSegment.ROI.X) * 0.3), (float)(handCentroidSmoothed.Y * 0.7 + (handCentroid.Y + handSegment.ROI.Y) * 0.3)));
            handCentroidSmoothed = new PointF(handCentroidFilter.getFilteredPoint().X, handCentroidFilter.getFilteredPoint().Y);

            //handCentroidSmoothed = new PointF((float)(handCentroidSmoothed.X * 0.9 + (handCentroid.X + handSegment.ROI.X) * 0.1), (float)(handCentroidSmoothed.Y * 0.9 + (handCentroid.Y + handSegment.ROI.Y) * 0.1));
        }


        public PointF getFilteredHandCenter()
        {
            if (handSegment == null) return new PointF(0, 0);

            //return new PointF(handCentroidSmoothed.X + handSegment.ROI.X, handCentroidSmoothed.Y + handSegment.ROI.Y);
            return handCentroidSmoothed;
        }

        /// <summary>
        /// Starting from hand-joint position (x,y) try to segment hand using seed fill algo. 
        /// </summary>
        /// <param name="data">BodyIndexFrame byte data</param>
        /// <param name="x">hand joint x value</param>
        /// <param name="y">hand joint y value</param>
        /// <param name="dist">max distance to hand joint</param>
        /// <returns></returns>
        public void searchFloodFill(byte[] data, int x, int y, int x2, int y2)
        {
            inputData = data;
            searchedData = new byte[data.Length];
            for (int i = 0; i < width * height; ++i)
                searchedData[i] = 255;

            xSeed = x;
            ySeed = y;

            seedFill(x, y, x2, y2);

            if (handSegment == null) return;

            FindCentroidByDistanceTrans(handSegment);
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
            return ExtractHandState(handSegment);
        }


        /// <summary>
        /// Recognize HandState of segmented hand.
        /// </summary>
        /// <param name="handSegment">Gray image matrix of segmented hand</param>
        /// <returns>HandState of segmented hand</returns>
        private Microsoft.Kinect.HandState ExtractHandState(Image<Gray, byte> handSegment)
        {
            if (handSegment == null) return Microsoft.Kinect.HandState.Unknown;
 
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
                        visOutput.Draw(new CircleF(handCentroid, 3), new Bgr(200, 125, 75), 2);
                        //visOutput.Draw(new CircleF(new Point(xSeed, ySeed), 3), new Bgr(200, 125, 75), 2);

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
        private Microsoft.Kinect.HandState calcHandState()
       {
           int featureAmount = 0;

           MCvConvexityDefect[] defectArray = defects.ToArray();
           
            
           PointF[] enclosingPointsDepth = new PointF[defects.Total];
           PointF[] enclosingPointsStart = new PointF[defects.Total];

           if (defects.Total > 0) { 
               for (int i = 0; i < defects.Total; i++)
               {
                   enclosingPointsDepth[i] = new PointF((float)defectArray[i].DepthPoint.X, (float)defectArray[i].DepthPoint.Y);
                   enclosingPointsStart[i] = new PointF((float)defectArray[i].StartPoint.X, (float)defectArray[i].StartPoint.Y);
               }

               CircleF minEncCircleDepth = Emgu.CV.PointCollection.MinEnclosingCircle(enclosingPointsDepth);
               CircleF minEncCircleStart = Emgu.CV.PointCollection.MinEnclosingCircle(enclosingPointsStart);
               if (isVisOutputActive)
               {
                   //visOutput.Draw(minEncCircleDepth, new Bgr(100, 125, 75), 2);
                   //visOutput.Draw(minEncCircleStart, new Bgr(200, 125, 75), 2);
               }
            }

           
          for (int i = 0; i < defects.Total; i++)
          {
              PointF startPoint = new PointF((float)defectArray[i].StartPoint.X, (float)defectArray[i].StartPoint.Y);

              PointF depthPoint = new PointF((float)defectArray[i].DepthPoint.X, (float)defectArray[i].DepthPoint.Y);

              PointF endPoint = new PointF((float)defectArray[i].EndPoint.X, (float)defectArray[i].EndPoint.Y);

              LineSegment2D startDepthLine = new LineSegment2D(defectArray[i].StartPoint, defectArray[i].DepthPoint);

              LineSegment2D depthEndLine = new LineSegment2D(defectArray[i].DepthPoint, defectArray[i].EndPoint);

              //visOutput.Draw(new CircleF(depthPoint, 2), new Bgr(200, 125, 75), 2);
               
             
              if ((startPoint.Y - handCentroid.Y < 0) && (startPoint.Y < depthPoint.Y) && (Math.Sqrt(Math.Pow(startPoint.X - depthPoint.X, 2) + Math.Pow(startPoint.Y - depthPoint.Y, 2)) > Math.Abs(startPoint.Y - handCentroid.Y)/5))
              {
                  double angle1 = Math.Atan2(startPoint.Y - depthPoint.Y, startPoint.X - depthPoint.X) * 180.0 / Math.PI;
                  double angle2 = Math.Atan2(endPoint.Y - depthPoint.Y, endPoint.X - depthPoint.X) * 180.0 / Math.PI;
                  if (angle1 < 0) angle1 += 360;
                  if (angle2 < 0) angle2 += 360;

                  if(Math.Abs(angle1 - angle2) < 90)
                      featureAmount++;
                  if (isVisOutputActive)
                      visOutput.Draw(new CircleF(startPoint, 3), new Bgr(System.Drawing.Color.Green), 2);
              }

          }


          //if hand has quadratic form, likely to be closed. if form is rectangular, likely to be not closed.
          double _dist = 0;
          for (int i = 0; i < filteredHull.Total; i++)
          {
              Point _p = filteredHull[i];
              if (_p.Y < handCentroid.Y && _dist < getDist(_p.X, _p.Y, handCentroid.X, handCentroid.Y))
              {
                  _dist = getDist(_p.X, _p.Y, handCentroid.X, handCentroid.Y);
              }
          }

          if (_dist / centroidDistToContour > 1.8 && featureAmount == 0)
              featureAmount = 2;

          return getHandStateFromFingerAmount(featureAmount);
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
        private Microsoft.Kinect.HandState getHandStateFromFingerAmount(int featureAmount)
        {

            if (featureAmount > 1) featureAmount = 2;

            filterHandState.Enqueue((byte)featureAmount);

            byte[] arr = filterHandState.ToArray();
            
            int mode = arr.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key;

            if (false && isVisOutputActive)
            {
                MCvFont font = new MCvFont(Emgu.CV.CvEnum.FONT.CV_FONT_HERSHEY_DUPLEX, 1d, 1d);
                visOutput.Draw(featureAmount.ToString() + "::" + mode.ToString(), ref font, new Point(10, 10), new Bgr(System.Drawing.Color.White));
            }

            //Lasso gesture not working well
            if (!UserSettings.Instance.IS_NO_DELETE_GESTURE)
            {
                if (mode > 1)
                    return Microsoft.Kinect.HandState.Open;
                else if (mode > 0)
                    return Microsoft.Kinect.HandState.Lasso;
                else
                    return Microsoft.Kinect.HandState.Closed;
            }
            else //recognize just two, not three gestures. works better
            {
                if (mode > 0)
                    return Microsoft.Kinect.HandState.Open;
                else
                    return Microsoft.Kinect.HandState.Lasso;
            }

        }

    }
}
