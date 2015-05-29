using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

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

        public HandSegmentation(int width, int height)
        {
            this.width = width;
            this.height = height;
        }

        //TODO: creates stackoverflow ecxeptions -> rewrite with loops || evtl. iterative breitensuche
        private void recBRSearch(int x, int y)
        {
            if (x < 0 || x > width - 1 || y < 0 || y > height - 1) return;

            int depthIndex = (y * width) + x;
            byte isPlayerPixel = inputData[depthIndex];

            double dX = Math.Abs((xSeed - x));
            double dY = Math.Abs((ySeed - y));

            if (searchedData[depthIndex] != 255) return;

            if (Math.Sqrt(dX * dX + dY * dY) > maxDist * 0.5 || isPlayerPixel == 0xff) {
                searchedData[depthIndex] = 0;
                return; 
            }

            searchedData[depthIndex] = 1;

            recBRSearch(x + 0, y + 1);
            recBRSearch(x + 0, y - 1);
            recBRSearch(x + 1, y + 0);
            recBRSearch(x - 1, y + 0);
            
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

            recBRSearch(x, y);

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
                Color c = Colors.Black;
                if (searchedData[i] == 1) c = Colors.White;
                pixels[pixelIndex++] = c.B;
                pixels[pixelIndex++] = c.G;
                pixels[pixelIndex++] = c.R;
                pixelIndex++;
            }

            return pixels;
        }
    }
}
