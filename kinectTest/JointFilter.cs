using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace votragsfinger2Back
{
    class JointFilter
    {

        private bool initialAccumulation;
        private int accumulationCount;
        private Vector3D accumulatedPosition;

        private double MAX_DEVIATION;

        public JointFilter()
        {
            initialAccumulation = true;
            accumulationCount = 1;
            MAX_DEVIATION = 0.2;
        }

        public JointFilter(double maxDeviation)
        {
            initialAccumulation = true;
            accumulationCount = 1;
            this.MAX_DEVIATION = maxDeviation;
        }

        public Vector3D getFilteredPosition(Vector3D v)
        {
            if (initialAccumulation)
            {
                accumulatedPosition = v;
                initialAccumulation = false;
            }
            else
            {
                accumulatedPosition = (accumulatedPosition * (double)accumulationCount / (double)(accumulationCount + 1)) + v * (1f / (double)(accumulationCount + 1));
            }

            accumulationCount++;

            if ((accumulatedPosition - v).Length > MAX_DEVIATION)
            {
                //accumulatedPosition = v;
                accumulationCount = 1;
            }

            return accumulatedPosition;
        }

    }
}
