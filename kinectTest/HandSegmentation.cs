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
        public bool isVisOutputActive = false;

        private FixSizeQueue<byte> filterHandState = new FixSizeQueue<byte>(14);

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







        public Microsoft.Kinect.HandState ExtractHandState()
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


            return ExtractHandState(new Image<Gray, byte>(pixels));
        }



        private Microsoft.Kinect.HandState ExtractHandState(Image<Gray, byte> handSegment)
        {
            using (MemStorage storage = new MemStorage())
            {
                Contour<Point> contours = handSegment.FindContours(Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE, Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_LIST, storage);
                Contour<Point> biggestContour = null;

                Double biggestContourArea = 0;
                //could be removed - there should always be just one blob
                while (contours != null)
                {
                    if (contours.Area > biggestContourArea)
                    {
                        biggestContourArea = contours.Area;
                        biggestContour = contours;
                    }
                    contours = contours.HNext;
                }

                if (isVisOutputActive)
                    visOutput = new Image<Bgr, byte>(handSegment.Bitmap);

                if (biggestContour != null)
                {
                    Contour<Point> currentContour = biggestContour.ApproxPoly(biggestContour.Perimeter * 0.0025, storage);
                    //visOutput.Draw(currentContour, new Bgr(System.Drawing.Color.Green), 2);
                    biggestContour = currentContour;


                    Seq<Point> hull = biggestContour.GetConvexHull(Emgu.CV.CvEnum.ORIENTATION.CV_CLOCKWISE);
                    box = biggestContour.GetMinAreaRect();
                    PointF[] points = box.GetVertices();

                    Point[] ps = new Point[points.Length];
                    for (int i = 0; i < points.Length; i++)
                        ps[i] = new Point((int)points[i].X, (int)points[i].Y);

                    if (isVisOutputActive)
                        visOutput.Draw(new CircleF(new PointF(xSeed, ySeed), 3), new Bgr(200, 125, 75), 2);


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



               //Very simple & weak heuristic for finger detection 
               if ((startPoint.Y - ySeed < 0 && depthPoint.Y - ySeed < maxDist / 8) && (startPoint.Y < depthPoint.Y) && (Math.Sqrt(Math.Pow(startPoint.X - depthPoint.X, 2) + Math.Pow(startPoint.Y - depthPoint.Y, 2)) > maxDist / 8))
               {
                   fingerNum++;
                   if (isVisOutputActive)
                        visOutput.Draw(startDepthLine, new Bgr(System.Drawing.Color.Green), 2);
               }

           }

           return getHandStateFromFingerAmount(fingerNum);
       }

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

            if (mode == 0)
                return Microsoft.Kinect.HandState.Closed;
            else if (mode == 1)
                return Microsoft.Kinect.HandState.Lasso;
            else
                return Microsoft.Kinect.HandState.Open;

        }

    }
}
