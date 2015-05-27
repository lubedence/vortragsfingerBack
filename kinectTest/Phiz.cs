using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Media3D;

namespace votragsfinger2Back
{
    class Phiz
    {
        private int backtrackingFactor = -1;

        private JointType originJointType = JointType.Head;
        private JointType interactionJointType = JointType.HandLeft;

        private JointFilter originJointFilter = new JointFilter();
        private JointFilter originJointFilterSL = new JointFilter();
        private JointFilter originJointFilterSR = new JointFilter();

        private KinectJointFilter allJointFilter = new KinectJointFilter(0.5f,0.3f, 0.2f, 0.4f, 0.3f);

        private Vector3D PHIZ_DIMENSION = new Vector3D(0.3, 0.2, 0);
        private Vector3D PHIZ_OFFSET = new Vector3D(-0.3, -0.1, 0);


        public Phiz(JointType interactionJointType, bool backTracking)
        {
            if (backTracking) backtrackingFactor = 1;
            else backtrackingFactor = -1;
            
            this.interactionJointType = interactionJointType;

            //swich left & right hand if backtracking enabled
            if (backTracking && interactionJointType.Equals(JointType.HandLeft))
                this.interactionJointType = JointType.HandRight;
            else if (backTracking && interactionJointType.Equals(JointType.HandRight))
                this.interactionJointType = JointType.HandLeft;
        }


        private Vector3D convertToV3(CameraSpacePoint csp)
        {
            return new Vector3D(csp.X * backtrackingFactor, csp.Y, csp.Z);
        }

        public Point getDisplayCoordinate(Body trackedBody)
        {
            allJointFilter.UpdateFilter(trackedBody);

            Vector3D centerV = convertToV3(allJointFilter.getFilteredJoint(originJointType));
            centerV = originJointFilter.getFilteredPosition(centerV);
            Vector3D handV = convertToV3(allJointFilter.getFilteredJoint(interactionJointType));

            //calc point on display in relation to center(head)
            Vector3D dist = (centerV + PHIZ_OFFSET - handV);

            Console.WriteLine(dist.ToString());

            return mapCameraSpacePointToScreenCoordinates(dist);
        }

        private Point mapCameraSpacePointToScreenCoordinates(Vector3D csp){
            if (csp.X < 0) csp.X = 0;
            else if (csp.X > PHIZ_DIMENSION.X) csp.X = PHIZ_DIMENSION.X;

            if (csp.Y < 0) csp.Y = 0;
            else if (csp.Y > PHIZ_DIMENSION.Y) csp.Y = PHIZ_DIMENSION.Y;

            var screen = System.Windows.Forms.Screen.PrimaryScreen.Bounds;  //Todo: more than one display: not guaranteed if primary
            double xScale = screen.Width / PHIZ_DIMENSION.X;
            double yScale = screen.Height / PHIZ_DIMENSION.Y;


            return new Point(csp.X * xScale, csp.Y * yScale);
        }

    }
}
