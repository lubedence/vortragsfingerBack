using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace votragsfinger2Back.kinect
{
    class DoubleExponentialFilter{
    public struct TRANSFORM_SMOOTH_PARAMETERS
        {
            public float fSmoothing;             // [0..1], lower values closer to raw data
            public float fCorrection;            // [0..1], lower values slower to correct towards the raw data
            public float fPrediction;            // [0..n], the number of frames to predict into the future
            public float fJitterRadius;          // The radius in meters for jitter reduction
            public float fMaxDeviationRadius;    // The maximum radius in meters that filtered positions are allowed to deviate from raw data
        }

        public class FilterDoubleExponentialData
        {
            public PointF m_vRawPosition;
            public PointF m_vFilteredPosition;
            public PointF m_vTrend;
            public int m_dwFrameCount;
        }

        // Holt Double Exponential Smoothing filter
        PointF m_pFilteredJoints;
        FilterDoubleExponentialData m_pHistory;
        float m_fSmoothing;
        float m_fCorrection;
        float m_fPrediction;
        float m_fJitterRadius;
        float m_fMaxDeviationRadius;

        public DoubleExponentialFilter(float fSmoothing = 0.25f, float fCorrection = 0.25f, float fPrediction = 0.25f, float fJitterRadius = 0.03f, float fMaxDeviationRadius = 0.05f)
        {
            m_pFilteredJoints = new PointF();
            m_pHistory = new FilterDoubleExponentialData();

            Init(fSmoothing, fCorrection, fPrediction, fJitterRadius, fMaxDeviationRadius);
        }

        ~DoubleExponentialFilter()
        {
            Shutdown();
        }

        public void Init(float fSmoothing = 0.25f, float fCorrection = 0.25f, float fPrediction = 0.25f, float fJitterRadius = 0.03f, float fMaxDeviationRadius = 0.05f)
        {
            Reset(fSmoothing, fCorrection, fPrediction, fJitterRadius, fMaxDeviationRadius);
        }


        public PointF getFilteredPoint()
        {
            return m_pFilteredJoints;
        }

        public void Shutdown()
        {
        }

        public void Reset(float fSmoothing = 0.25f, float fCorrection = 0.25f, float fPrediction = 0.25f, float fJitterRadius = 0.03f, float fMaxDeviationRadius = 0.05f)
        {
            if (m_pFilteredJoints == null || m_pHistory == null)
            {
                return;
            }

            m_fMaxDeviationRadius = fMaxDeviationRadius; // Size of the max prediction radius Can snap back to noisy data when too high
            m_fSmoothing = fSmoothing;                   // How much smothing will occur.  Will lag when too high
            m_fCorrection = fCorrection;                 // How much to correct back from prediction.  Can make things springy
            m_fPrediction = fPrediction;                 // Amount of prediction into the future to use. Can over shoot when too high
            m_fJitterRadius = fJitterRadius;             // Size of the radius where jitter is removed. Can do too much smoothing when too high


            m_pFilteredJoints.X = 0.0f;
            m_pFilteredJoints.Y = 0.0f;

            m_pHistory.m_vFilteredPosition.X = 0.0f;
            m_pHistory.m_vFilteredPosition.Y = 0.0f;

            m_pHistory.m_vRawPosition.X = 0.0f;
            m_pHistory.m_vRawPosition.Y = 0.0f;

            m_pHistory.m_vTrend.X = 0.0f;
            m_pHistory.m_vTrend.Y = 0.0f;

            m_pHistory.m_dwFrameCount = 0;

        }

        //--------------------------------------------------------------------------------------
        // Implementation of a Holt Double Exponential Smoothing filter. The double exponential
        // smooths the curve and predicts.  There is also noise jitter removal. And maximum
        // prediction bounds.  The paramaters are commented in the init function.
        //--------------------------------------------------------------------------------------
        public void UpdateFilter(PointF newPoint)
        {
            // Check for divide by zero. Use an epsilon of a 10th of a millimeter
            m_fJitterRadius = Math.Max(0.0001f, m_fJitterRadius);

            TRANSFORM_SMOOTH_PARAMETERS SmoothingParams;

            SmoothingParams.fSmoothing = m_fSmoothing;
            SmoothingParams.fCorrection = m_fCorrection;
            SmoothingParams.fPrediction = m_fPrediction;
            SmoothingParams.fJitterRadius = m_fJitterRadius;
            SmoothingParams.fMaxDeviationRadius = m_fMaxDeviationRadius;

            UpdateJoint(newPoint, SmoothingParams);

        }

        //--------------------------------------------------------------------------------------
        // if joint is 0 it is not valid.
        //--------------------------------------------------------------------------------------
        bool JointPositionIsValid(PointF vJointPosition)
        {
            return (!(vJointPosition.X < 0.0f || vJointPosition.Y < 0.0f));
        }

        PointF CSVectorSet(float x, float y)
        {
            PointF point = new PointF();

            point.X = x;
            point.Y = y;

            return point;
        }

        PointF CSVectorZero()
        {
            PointF point = new PointF();

            point.X = 0.0f;
            point.Y = 0.0f;

            return point;
        }

        PointF CSVectorAdd(PointF p1, PointF p2)
        {
            PointF sum = new PointF();

            sum.X = p1.X + p2.X;
            sum.Y = p1.Y + p2.Y;

            return sum;
        }

        PointF CSVectorScale(PointF p, float scale)
        {
            PointF point = new PointF();

            point.X = p.X * scale;
            point.Y = p.Y * scale;

            return point;
        }

        PointF CSVectorSubtract(PointF p1, PointF p2)
        {
            PointF diff = new PointF();

            diff.X = p1.X - p2.X;
            diff.Y = p1.Y - p2.Y;

            return diff;
        }

        float CSVectorLength(PointF p)
        {
            return Convert.ToSingle(Math.Sqrt(p.X * p.X + p.Y * p.Y));
        }

        void UpdateJoint(PointF point, TRANSFORM_SMOOTH_PARAMETERS smoothingParams)
        {
            PointF vPrevRawPosition;
            PointF vPrevFilteredPosition;
            PointF vPrevTrend;
            PointF vRawPosition;
            PointF vFilteredPosition;
            PointF vPredictedPosition;
            PointF vDiff;
            PointF vTrend;
            float fDiff;
            bool bJointIsValid;

            vRawPosition = point;
            vPrevFilteredPosition = m_pHistory.m_vFilteredPosition;
            vPrevTrend = m_pHistory.m_vTrend;
            vPrevRawPosition = m_pHistory.m_vRawPosition;
            bJointIsValid = JointPositionIsValid(vRawPosition);

            // If joint is invalid, reset the filter
            if (!bJointIsValid && false)
            {
                m_pHistory.m_dwFrameCount = 0;
            }

            // Initial start values
            if (m_pHistory.m_dwFrameCount == 0)
            {
                vFilteredPosition = vRawPosition;
                vTrend = CSVectorZero();
                m_pHistory.m_dwFrameCount++;
            }
            else if (m_pHistory.m_dwFrameCount == 1)
            {
                vFilteredPosition = CSVectorScale(CSVectorAdd(vRawPosition, vPrevRawPosition), 0.5f);
                vDiff = CSVectorSubtract(vFilteredPosition, vPrevFilteredPosition);
                vTrend = CSVectorAdd(CSVectorScale(vDiff, smoothingParams.fCorrection), CSVectorScale(vPrevTrend, 1.0f - smoothingParams.fCorrection));
                m_pHistory.m_dwFrameCount++;
            }
            else
            {
                // First apply jitter filter
                vDiff = CSVectorSubtract(vRawPosition, vPrevFilteredPosition);
                fDiff = CSVectorLength(vDiff);

                if (fDiff <= smoothingParams.fJitterRadius)
                {
                    vFilteredPosition = CSVectorAdd(CSVectorScale(vRawPosition, fDiff / smoothingParams.fJitterRadius),
                        CSVectorScale(vPrevFilteredPosition, 1.0f - fDiff / smoothingParams.fJitterRadius));
                }
                else
                {
                    vFilteredPosition = vRawPosition;
                }

                // Now the double exponential smoothing filter
                vFilteredPosition = CSVectorAdd(CSVectorScale(vFilteredPosition, 1.0f - smoothingParams.fSmoothing),
                    CSVectorScale(CSVectorAdd(vPrevFilteredPosition, vPrevTrend), smoothingParams.fSmoothing));


                vDiff = CSVectorSubtract(vFilteredPosition, vPrevFilteredPosition);
                vTrend = CSVectorAdd(CSVectorScale(vDiff, smoothingParams.fCorrection), CSVectorScale(vPrevTrend, 1.0f - smoothingParams.fCorrection));
            }

            // Predict into the future to reduce latency
            vPredictedPosition = CSVectorAdd(vFilteredPosition, CSVectorScale(vTrend, smoothingParams.fPrediction));

            // Check that we are not too far away from raw data
            vDiff = CSVectorSubtract(vPredictedPosition, vRawPosition);
            fDiff = CSVectorLength(vDiff);

            if (fDiff > smoothingParams.fMaxDeviationRadius)
            {
                vPredictedPosition = CSVectorAdd(CSVectorScale(vPredictedPosition, smoothingParams.fMaxDeviationRadius / fDiff),
                    CSVectorScale(vRawPosition, 1.0f - smoothingParams.fMaxDeviationRadius / fDiff));
            }

            // Save the data from this frame
            m_pHistory.m_vRawPosition = vRawPosition;
            m_pHistory.m_vFilteredPosition = vFilteredPosition;
            m_pHistory.m_vTrend = vTrend;

            // Output the data
            m_pFilteredJoints = vPredictedPosition;

            Console.WriteLine(vFilteredPosition.X+":-:"+vRawPosition.X);
        }
    }
}

