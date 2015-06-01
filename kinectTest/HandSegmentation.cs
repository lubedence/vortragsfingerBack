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

namespace votragsfinger2Back
{
    class HandSegmentation
    {

        private int width;
        private int height;

        private int maxDist;
        private int xSeed;
        private int ySeed;

        private byte[] inputData;
        private byte[] searchedData;

        private Seq<MCvConvexityDefect> defects;
        private MCvBox2D box;
        public Image<Bgr, byte> visOutput;

        private FixSizeQueue<byte> filterHandState = new FixSizeQueue<byte>(10);

        public HandSegmentation(int width, int height)
        {
            this.width = width;
            this.height = height;
        }

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


        
        public byte[] searchBFS(byte[] data, int x, int y, int dist)
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

        public byte[] searchSimple(byte[] data, int x, int y, int dist)
        {
            inputData = data;
            searchedData = new byte[data.Length];
            for (int i = 0; i < width * height; ++i)
                searchedData[i] = 255;

            for (int _y = 0; _y < height; ++_y)
            {
                for (int _x = 0; _x < width; ++_x)
                {
                    int depthIndex = (_y * width) + _x;
                    byte isPlayerPixel = data[depthIndex];

                    if (isPlayerPixel != 0xff)
                    {
                        if (Math.Sqrt(Math.Pow((x - _x), 2) + Math.Pow((y - _y), 2)) < dist * 0.5)
                            searchedData[depthIndex] = 1;
                    }

                }
            }

            return searchedData;
        } 

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







        public Microsoft.Kinect.HandState ExtractContourAndHull()
        {
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


            return ExtractContourAndHull(new Image<Gray, byte>(pixels));
        }



        private Microsoft.Kinect.HandState ExtractContourAndHull(Image<Gray, byte> handSegment)
        {
            using (MemStorage storage = new MemStorage())
            {
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

                visOutput = new Image<Bgr, byte>(handSegment.Bitmap);

                if (biggestContour != null)
                {
                    //currentFrame.Draw(biggestContour, new Bgr(Color.DarkViolet), 2);
                    Contour<Point> currentContour = biggestContour.ApproxPoly(biggestContour.Perimeter * 0.0025, storage);
                    //visOutput.Draw(currentContour, new Bgr(System.Drawing.Color.LimeGreen), 2);
                    biggestContour = currentContour;


                    Seq<Point> hull = biggestContour.GetConvexHull(Emgu.CV.CvEnum.ORIENTATION.CV_CLOCKWISE);
                    box = biggestContour.GetMinAreaRect();
                    PointF[] points = box.GetVertices();

                    //visOutput.Draw(box.MinAreaRect(), new Bgr(200, 0, 0), 1);

                    Point[] ps = new Point[points.Length];
                    for (int i = 0; i < points.Length; i++)
                        ps[i] = new Point((int)points[i].X, (int)points[i].Y);

                    //visOutput.DrawPolyline(hull.ToArray(), true, new Bgr(200, 125, 75), 2);
                    visOutput.Draw(new CircleF(new PointF(xSeed, ySeed), 3), new Bgr(200, 125, 75), 2);

                    //ellip.MCvBox2D= CvInvoke.cvFitEllipse2(biggestContour.Ptr);
                    //currentFrame.Draw(new Ellipse(ellip.MCvBox2D), new Bgr(Color.LavenderBlush), 3);

                    PointF center;
                    float radius;
                    //CvInvoke.cvMinEnclosingCircle(biggestContour.Ptr, out  center, out  radius);
                    //currentFrame.Draw(new CircleF(center, radius), new Bgr(Color.Gold), 2);

                    //currentFrame.Draw(new CircleF(new PointF(ellip.MCvBox2D.center.X, ellip.MCvBox2D.center.Y), 3), new Bgr(100, 25, 55), 2);
                    //currentFrame.Draw(ellip, new Bgr(Color.DeepPink), 2);

                    //CvInvoke.cvEllipse(currentFrame, new Point((int)ellip.MCvBox2D.center.X, (int)ellip.MCvBox2D.center.Y), new System.Drawing.Size((int)ellip.MCvBox2D.size.Width, (int)ellip.MCvBox2D.size.Height), ellip.MCvBox2D.angle, 0, 360, new MCvScalar(120, 233, 88), 1, Emgu.CV.CvEnum.LINE_TYPE.EIGHT_CONNECTED, 0);
                    //currentFrame.Draw(new Ellipse(new PointF(box.center.X, box.center.Y), new SizeF(box.size.Height, box.size.Width), box.angle), new Bgr(0, 0, 0), 2);


                    Seq<Point> filteredHull = new Seq<Point>(storage);
                    for (int i = 0; i < hull.Total; i++)
                    {
                        if (Math.Sqrt(Math.Pow(hull[i].X - hull[i + 1].X, 2) + Math.Pow(hull[i].Y - hull[i + 1].Y, 2)) > box.size.Width / 10)
                        {
                            filteredHull.Push(hull[i]);
                        }
                    }

                    defects = biggestContour.GetConvexityDefacts(storage, Emgu.CV.CvEnum.ORIENTATION.CV_CLOCKWISE);
                    return calcHandState();
                }
            }

            return Microsoft.Kinect.HandState.Unknown;
        }




        public Microsoft.Kinect.HandState calcHandState()
       {
           int fingerNum = 0;

           MCvConvexityDefect[] defectArray = defects.ToArray();

           #region hull drawing
           //for (int i = 0; i < filteredHull.Total; i++)
           //{
           //    PointF hullPoint = new PointF((float)filteredHull[i].X,
           //                                  (float)filteredHull[i].Y);
           //    CircleF hullCircle = new CircleF(hullPoint, 4);
           //    currentFrame.Draw(hullCircle, new Bgr(Color.Aquamarine), 2);
           //}
           #endregion

           #region defects drawing
           for (int i = 0; i < defects.Total; i++)
           {
               PointF startPoint = new PointF((float)defectArray[i].StartPoint.X,
                                               (float)defectArray[i].StartPoint.Y);

               PointF depthPoint = new PointF((float)defectArray[i].DepthPoint.X,
                                               (float)defectArray[i].DepthPoint.Y);

               PointF endPoint = new PointF((float)defectArray[i].EndPoint.X,
                                               (float)defectArray[i].EndPoint.Y);

               LineSegment2D startDepthLine = new LineSegment2D(defectArray[i].StartPoint, defectArray[i].DepthPoint);

               LineSegment2D depthEndLine = new LineSegment2D(defectArray[i].DepthPoint, defectArray[i].EndPoint);

               CircleF startCircle = new CircleF(startPoint, 5f);

               CircleF depthCircle = new CircleF(depthPoint, 5f);

               CircleF endCircle = new CircleF(endPoint, 5f);

               

               //Custom heuristic based on some experiment, double check it before use
               if ((startPoint.Y - ySeed < 0 && depthPoint.Y - ySeed < 0) && (startCircle.Center.Y < depthCircle.Center.Y) && (Math.Sqrt(Math.Pow(startCircle.Center.X - depthCircle.Center.X, 2) + Math.Pow(startCircle.Center.Y - depthCircle.Center.Y, 2)) > maxDist / 7))
               {
                   fingerNum++;
                   visOutput.Draw(startDepthLine, new Bgr(System.Drawing.Color.Green), 2);
                   //currentFrame.Draw(depthEndLine, new Bgr(Color.Magenta), 2);
               }


               //visOutput.Draw(startCircle, new Bgr(System.Drawing.Color.Red), 2);
               //visOutput.Draw(depthCircle, new Bgr(System.Drawing.Color.Yellow), 5);
               //currentFrame.Draw(endCircle, new Bgr(Color.DarkBlue), 4);
           }
           #endregion

           //MCvFont font = new MCvFont(Emgu.CV.CvEnum.FONT.CV_FONT_HERSHEY_DUPLEX, 5d, 5d);
           //visOutput.Draw(fingerNum.ToString(), ref font, new Point(50, 150), new Bgr(System.Drawing.Color.White));

           return getHandStateFromFingerAmount(fingerNum);
       }

        private Microsoft.Kinect.HandState getHandStateFromFingerAmount(int fingers)
        {
            filterHandState.Enqueue((byte)fingers);

            byte[] arr = filterHandState.ToArray();

            var groups = arr.GroupBy(v => v);
            int maxCount = groups.Max(g => g.Count());
            int mode = groups.First(g => g.Count() == maxCount).Key;

            MCvFont font = new MCvFont(Emgu.CV.CvEnum.FONT.CV_FONT_HERSHEY_DUPLEX, 5d, 5d);
            visOutput.Draw(fingers.ToString() + "::" + mode.ToString(), ref font, new Point(50, 150), new Bgr(System.Drawing.Color.White));

            if (mode == 0)
                return Microsoft.Kinect.HandState.Closed;
            else if (mode == 1)
                return Microsoft.Kinect.HandState.Lasso;
            else
                return Microsoft.Kinect.HandState.Open;

        }

    }
}
