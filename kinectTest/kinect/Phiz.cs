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
    /// <summary>
    /// TODO: improvement needed regarding:
    /// a) more advanced phiz-area (not just simple rectangular area)
    /// b) sometimes hand joints include heavy jitter if user is tracked from behind --> eg. not use the hand-joint provided by KinectSdk, but calculate own position of hand in HandSegmentation.cs
    /// </summary>
    class Phiz
    {
        private int backtrackingFactor = -1;

        private JointType originJointType = JointType.Head; //joint acts as origin of the phiz coordinate system
        //if Kinect is mounted behind the user, X-axis needs to be inverted. means left becomes right, right becomes left.
        //this variables will be inverted if Kinect is mounted behind user
        private JointType handLeftJoint = JointType.HandLeft; 
        private JointType handRightJoint = JointType.HandRight;
        private JointType elbowLeftJoint = JointType.ElbowLeft;
        private JointType elbowRightJoint = JointType.ElbowRight;
        private HandType handLeftType = HandType.LEFT; 
        private HandType handRightType = HandType.RIGHT;

        //this variables form the output of the phiz calculations
        private HandType interactingHand = HandType.NONE; //hand which currently interacts
        private Point interactingPoint = new Point(-1, -1); //current interacting hand position in screen coordinates
        private HandState handStateLeft = HandState.Unknown; //HandState left hand 
        private HandState handStateRight = HandState.Unknown;//HandState right hand

        //some joint filtering
        private KinectJointStabilization originJointFilter = new KinectJointStabilization();
        private KinectJointFilter allJointFilter = new KinectJointFilter(0.6f,0.4f, 0.3f, 0.5f, 0.4f);
        private KinectJointFilter allJointFilterForHandSegmentation = new KinectJointFilter();

        //dimension of the Phiz
        private Vector3D PHIZ_DIMENSION = new Vector3D(90, 60, 0); //TODO: dimension relative to sensor distance
        //Phiz offset from origin(=originJointType)
        private Vector3D PHIZ_OFFSET = new Vector3D(50, 20, 0); //TODO: offset relative to sensor distance

        //segment hands in each frame to extract HandState(=hand gestures)
        private HandSegmentation handLeftSegmentation = new HandSegmentation(512,424);
        private HandSegmentation handRightSegmentation = new HandSegmentation(512, 424);

        /// <summary>
        /// initialize Phiz.
        /// </summary>
        /// <param name="backTracking">if true: Kinect mounted behind user -> invert X-axis</param>
        public Phiz(bool backTracking)
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

        private Vector3D convertToV3(System.Drawing.PointF pf)
        {
            return new Vector3D(pf.X, pf.Y, 0);
        }

        private Vector3D convertToV3(DepthSpacePoint dsp)
        {
            return new Vector3D(dsp.X, dsp.Y, 0);
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

            
            DepthSpacePoint dspCenter = KinectSensor.GetDefault().CoordinateMapper.MapCameraPointToDepthSpace(cspCenter);
            DepthSpacePoint dspElbow = KinectSensor.GetDefault().CoordinateMapper.MapCameraPointToDepthSpace(cspEllbow);

            HandSegmentation handSegmentation;
            if (ht == handLeftType)
                handSegmentation = handLeftSegmentation;
            else
                handSegmentation = handRightSegmentation;

            //segment hand from body and extract HandState
            handSegmentation.searchFloodFill(_bodyIndexData, (int)dspCenter.X, (int)dspCenter.Y, (int)dspElbow.X, (int)dspElbow.Y);
            HandState hs = handSegmentation.ExtractHandState();
            return hs;
        }

        /// <summary>
        /// 1. Calculate the HandState of both hands 
        /// 2. Check if a hand is interacting. interacting if hand has moved over head. stop interacting, if hand has moved way under the Phiz.
        /// 3. If a hand is interacting, transform the hand position in screen coordinate according to the Phiz.
        /// </summary>
        /// <param name="trackedBody">current interacting Body</param>
        /// <param name="bif">current BodyIndexFrame</param>
        /// <returns>true if a hand is interacting, otherwise return false </returns>
        public bool checkForInteraction(Body trackedBody, BodyIndexFrame bif)
        {
            //interactingPoint = new Point(-1, -1); //reset screen cooordinate position of hand

            //calc HandState for both hands
            allJointFilterForHandSegmentation.UpdateFilter(trackedBody);
            handStateLeft = calcHandState(bif, handLeftType);
            handStateRight = calcHandState(bif, handRightType);

            //some joint filtering
            allJointFilter.UpdateFilter(trackedBody);
            Vector3D originJointV3 = convertToV3(allJointFilter.getFilteredJoint(originJointType));
            originJointV3 = originJointFilter.getFilteredPosition(originJointV3);
            Vector3D handLeftV3 = convertToV3(allJointFilter.getFilteredJoint(handLeftJoint));
            Vector3D handRightV3 = convertToV3(allJointFilter.getFilteredJoint(handRightJoint));


            //check if hand interaction has started
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

            originJointV3 = convertToV3(KinectSensor.GetDefault().CoordinateMapper.MapCameraPointToDepthSpace(allJointFilter.getFilteredJoint(originJointType)));

            //some calculations to map interacting hand position to Phiz
            Vector3D distV3;

            if (interactingHand == handRightType)
            {
                handRightV3.X = handRightSegmentation.getNewHandCenter().X;
                handRightV3.Y = handRightSegmentation.getNewHandCenter().Y;
                distV3 = (handRightV3 - (originJointV3 - PHIZ_OFFSET) );
                distV3.X *= -1;
            }
            else
            {
                handLeftV3.X = handLeftSegmentation.getNewHandCenter().X;
                handLeftV3.Y = handLeftSegmentation.getNewHandCenter().Y;
                Vector3D _phiz_offset = PHIZ_OFFSET;
                _phiz_offset.X *= -1;
                distV3 = (handLeftV3 - (originJointV3 - _phiz_offset));
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
                //hand position is way under the Phiz boundary (y-axis) --> stop current hand interaction
                if (distV3.Y > PHIZ_DIMENSION.Y * 1.5)
                {
                    interactingHand = HandType.NONE;
                    return false;
                }
                distV3.Y = PHIZ_DIMENSION.Y;
            }

            //scale hand position in Phiz to screen coordinates
            var screen = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            double xScale = screen.Width / PHIZ_DIMENSION.X;
            double yScale = screen.Height / PHIZ_DIMENSION.Y;
            double scale = xScale;
            if (yScale > xScale) scale = yScale;

            Point _tmp = new Point(distV3.X * scale, distV3.Y * scale); //screen coordinates of interacting hand
            interactingPoint = new Point(interactingPoint.X * 0.9 + _tmp.X * 0.1, interactingPoint.Y * 0.9 + _tmp.Y * 0.1);
            //interactingPoint = _tmp;
            return true; 
        }

        /// <summary>
        /// get the screen coordinates of the current interacting hand
        /// </summary>
        /// <returns>Point in screen coordinates. Point == (-1,-1) if there is no interaction.</returns>
        public Point getInteractionPoint()
        {
            return interactingPoint;
        }

        /// <summary>
        /// which hand is interacting?
        /// </summary>
        /// <returns>returns HandType.Left or .Right if there is an interaction and .None if there is not.</returns>
        public HandType getInteractionHand()
        {
            return interactingHand;
        }

        /// <summary>
        /// look for user(hand) interaction
        /// </summary>
        /// <returns>true if user (one of his hands) is interacting</returns>
        public bool isUserInteracting()
        {
            if (interactingHand == HandType.NONE) return false;
            return true;
        }

        /// <summary>
        /// Get HandState of corresponding hand/HandType
        /// </summary>
        /// <param name="ht">HandType for which the current HandState should be returned</param>
        /// <returns>HandType of corresponding hand</returns>
        public HandState getHandState(HandType ht)
        {
            if (ht == HandType.NONE)
                return HandState.Unknown;

            if (ht == handLeftType)
                return handStateLeft;
            else
                return handStateRight;
        }


        //for debug purposes
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
