using Emgu.CV;
using Microsoft.Kinect;
using Microsoft.Kinect.Input;
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
        private JointType handLeftJoint = JointType.HandLeft;
        private JointType handRightJoint = JointType.HandRight;
        private JointType elbowLeftJoint = JointType.ElbowLeft;
        private JointType elbowRightJoint = JointType.ElbowRight;

        private HandType handLeftType = HandType.LEFT;
        private HandType handRightType = HandType.RIGHT;

        private Vector3D originJointV3;

        private HandType interactingHand = HandType.NONE;
        private Point interactingPoint = new Point(-1, -1);
        private HandState handStateLeft = HandState.Unknown;
        private HandState handStateRight = HandState.Unknown;

        private KinectJointStabilization originJointFilter = new KinectJointStabilization();
        private KinectJointFilter allJointFilter = new KinectJointFilter(0.6f,0.4f, 0.3f, 0.5f, 0.4f);
        private KinectJointFilter allJointFilterForHandSegmentation = new KinectJointFilter();

        private Vector3D PHIZ_DIMENSION = new Vector3D(0.3, 0.2, 0);
        private Vector3D PHIZ_OFFSET = new Vector3D(-0.3, -0.1, 0);

        private HandSegmentation handLeftSegmentation = new HandSegmentation(512,424);
        private HandSegmentation handRightSegmentation = new HandSegmentation(512, 424);

        public Phiz( bool backTracking)
        {
            if (backTracking)
            {
                backtrackingFactor = 1;
                handLeftJoint = JointType.HandRight;
                handRightJoint = JointType.HandLeft;
                elbowLeftJoint = JointType.ElbowRight;
                elbowRightJoint =  JointType.ElbowLeft;

                handLeftType = HandType.RIGHT;
                handRightType = HandType.LEFT;
            }
        }

        private Vector3D convertToV3(CameraSpacePoint csp)
        {
            return new Vector3D(csp.X * backtrackingFactor, csp.Y, csp.Z);
        }


        private HandState calcHandState(BodyIndexFrame bif, HandType ht)
        {
            int depthFrameWidth = bif.FrameDescription.Width;
            int depthFrameHeight = bif.FrameDescription.Height;
            byte[] _bodyIndexData = new byte[depthFrameWidth * depthFrameHeight];

            bif.CopyFrameDataToArray(_bodyIndexData);

            CameraSpacePoint cspCenter;
            CameraSpacePoint cspEllbow;
            if (ht == handLeftType)
            {
                cspCenter = allJointFilterForHandSegmentation.getFilteredJoint(handLeftJoint);
                cspEllbow = allJointFilterForHandSegmentation.getFilteredJoint(elbowLeftJoint);
            }
            else if (ht == handRightType)
            {
                cspCenter = allJointFilterForHandSegmentation.getFilteredJoint(handRightJoint);
                cspEllbow = allJointFilterForHandSegmentation.getFilteredJoint(elbowRightJoint);
            }
            else return HandState.Unknown;

            double cspRadius3d = (Math.Sqrt(Math.Pow((cspCenter.X - cspEllbow.X), 2) + Math.Pow((cspCenter.Y - cspEllbow.Y), 2) + Math.Pow((cspCenter.Z - cspEllbow.Z), 2)));
            double cspRadius2d = (Math.Sqrt(Math.Pow((cspCenter.X - cspEllbow.X), 2) + Math.Pow((cspCenter.Y - cspEllbow.Y), 2)));
            DepthSpacePoint dspCenter = KinectSensor.GetDefault().CoordinateMapper.MapCameraPointToDepthSpace(cspCenter);
            DepthSpacePoint dspElbow = KinectSensor.GetDefault().CoordinateMapper.MapCameraPointToDepthSpace(cspEllbow);
            double radius = (Math.Sqrt(Math.Pow((dspCenter.X - dspElbow.X), 2) + Math.Pow((dspCenter.Y - dspElbow.Y), 2)));
            radius = radius / cspRadius2d * cspRadius3d; //third(z) dimension

            HandSegmentation handSegmentation;
            if (ht == handLeftType)
                handSegmentation = handLeftSegmentation;
            else
                handSegmentation = handRightSegmentation;

            handSegmentation.searchFloodFill(_bodyIndexData, (int)dspCenter.X, (int)dspCenter.Y, (int)radius);
            return handSegmentation.ExtractHandState();
        }


        public bool checkForInteraction(Body trackedBody, BodyIndexFrame bif)
        {
            interactingPoint = new Point(-1, -1);

            handStateLeft = calcHandState(bif, handLeftType);
            handStateRight = calcHandState(bif, handRightType);

            allJointFilter.UpdateFilter(trackedBody);
            allJointFilterForHandSegmentation.UpdateFilter(trackedBody);
            originJointV3 = convertToV3(allJointFilter.getFilteredJoint(originJointType));
            originJointV3 = originJointFilter.getFilteredPosition(originJointV3);

            Vector3D handLeftV3 = convertToV3(allJointFilter.getFilteredJoint(handLeftJoint));
            Vector3D handRightV3 = convertToV3(allJointFilter.getFilteredJoint(handRightJoint));

            double distR = handRightV3.Y - originJointV3.Y;
            double distL = handLeftV3.Y - originJointV3.Y;

            if (interactingHand == HandType.NONE)
            {
                if (distR > 0)
                {
                    interactingHand = handRightType;
                }
                else if (distL > 0)
                {
                    interactingHand = handLeftType;
                }
                else
                    return false;
            }

            Vector3D distV3;

            if (interactingHand == handRightType)
                distV3 = (originJointV3 + PHIZ_OFFSET - handRightV3);
            else
            {
                distV3 = (originJointV3 + PHIZ_OFFSET - handLeftV3);
                distV3.X = (handLeftV3.X - originJointV3.X + PHIZ_OFFSET.X);
            }

            if (distV3.X < 0) distV3.X = 0;
            else if (distV3.X > PHIZ_DIMENSION.X) distV3.X = PHIZ_DIMENSION.X;

            if (interactingHand == handLeftType)
            {
                distV3.X = (distV3.X - PHIZ_DIMENSION.X) * -1;
            }

            if (distV3.Y < 0) distV3.Y = 0;
            else if (distV3.Y > PHIZ_DIMENSION.Y)
            {
                if (distV3.Y > PHIZ_DIMENSION.Y * 1.5)
                {
                    interactingHand = HandType.NONE;
                    return false;
                }
                distV3.Y = PHIZ_DIMENSION.Y;
            }


            var screen = System.Windows.Forms.Screen.PrimaryScreen.Bounds;  //Todo: more than one display: not guaranteed if primary?
            double xScale = screen.Width / PHIZ_DIMENSION.X;
            double yScale = screen.Height / PHIZ_DIMENSION.Y;
            double scale = xScale;
            if (yScale > xScale) scale = yScale;
            //todo: do not distort ratio
            interactingPoint = new Point(distV3.X * scale, distV3.Y * scale);

            return true; 
        }

        public Point getInteractionPoint()
        {
            return interactingPoint;
        }


        public HandType getInteractionHand()
        {
            return interactingHand;
        }


        public bool isUserInteracting()
        {
            if (interactingHand == HandType.NONE) return false;
            return true;
        }

        public HandState getHandState(HandType ht)
        {
            if (ht == HandType.NONE)
                return HandState.Unknown;

            if (ht == handLeftType)
                return handStateLeft;
            else
                return handStateRight;
        }

        public IImage getVisOutputFromHandSegmentation(HandType ht)
        {
            HandSegmentation handSegmentation;
            if (ht == handLeftType)
                handSegmentation = handLeftSegmentation;
            else if (ht == handRightType)
                handSegmentation = handRightSegmentation;
            else
                return null;

            if (handSegmentation.isVisOutputActive)
                return handSegmentation.visOutput;

            return null;
        }
    }
}
