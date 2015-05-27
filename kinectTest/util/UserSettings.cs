using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;
using Nini.Config;
using Nini.Ini;
using Microsoft.Kinect;

namespace votragsfinger2.util
{
    class UserSettings
    {

        public int GESTURE_MIN_TIME = 1000;
        public double GESTURE_MIN_CONFIDENCE = 0.85;
        public int LINE_THICKNESS = 10;
        public int LINE_RESUME_THRESHOLD = 100;
        public int LINE_FREEHAND_STRAIGHT_MIN_DIST = 6;
        public int LINE_FREEHAND_STRAIGHT_ANGLE = 15;
        public double SMOOTHING = 0.75;
        public int RUBBER_SIZE = 30;
        public int USER_ACTION_MIN_TIME = 250;

        public string GESTURE_PATH = @"gestures/gestures1.gbd";
        public HandState GESTURE_DRAW = HandState.Lasso;
        public HandState GESTURE_RUBBER = HandState.Closed;
        public HandState GESTURE_MOVE = HandState.Open;

        private static UserSettings instance;

        private UserSettings() { }

        public static UserSettings Instance
       {
          get 
          {
             if (instance == null)
             {
                 instance = new UserSettings();
             }
             return instance;
          }
       }

        private string path = "UserConfig.ini";

        private bool fileExists(string p)
        {
            if (File.Exists(p)) return true;
            else return false;
        }

        public void parseIniFile()
        {
            if (fileExists(path))
            {
                IConfigSource source = new IniConfigSource(path);


                if (source.Configs["Gestures"].GetInt("Minimum time", GESTURE_MIN_TIME) < 0 || source.Configs["Gestures"].GetInt("Minimum time", GESTURE_MIN_TIME) > 5000)
                    source.Configs["Gestures"].Set("Minimum time", GESTURE_MIN_TIME);
                if (source.Configs["Gestures"].GetDouble("Minimum confidence", GESTURE_MIN_CONFIDENCE) < 0 || source.Configs["Gestures"].GetDouble("Minimum confidence", GESTURE_MIN_CONFIDENCE) > 1)
                    source.Configs["Gestures"].Set("Minimum confidence", GESTURE_MIN_CONFIDENCE);
                if (source.Configs["Gestures"].Get("path", GESTURE_PATH).CompareTo("null") == 0 && !fileExists(source.Configs["Gestures"].Get("path", GESTURE_PATH)))
                    source.Configs["Gestures"].Set("path", "null");
                if (source.Configs["Drawing"].GetInt("Line thickness", LINE_THICKNESS) < 5 || source.Configs["Drawing"].GetInt("Line thickness", LINE_THICKNESS) > 100)
                    source.Configs["Drawing"].Set("Line thickness", LINE_THICKNESS);
                if (source.Configs["Drawing"].GetInt("Resume threshold", LINE_RESUME_THRESHOLD) < 0 || source.Configs["Drawing"].GetInt("Resume threshold", LINE_RESUME_THRESHOLD) > 200)
                    source.Configs["Drawing"].Set("Resume threshold", LINE_RESUME_THRESHOLD);
                if (source.Configs["Drawing"].GetInt("Freehand straight minimum distance", LINE_FREEHAND_STRAIGHT_MIN_DIST) < 0 || source.Configs["Drawing"].GetInt("Freehand straight minimum distance", LINE_FREEHAND_STRAIGHT_MIN_DIST) > 99)
                    source.Configs["Drawing"].Set("Freehand straight minimum distance", LINE_FREEHAND_STRAIGHT_MIN_DIST);
                if (source.Configs["Drawing"].GetInt("Freehand straight angle", LINE_FREEHAND_STRAIGHT_ANGLE) < 0 || source.Configs["Drawing"].GetInt("Freehand straight angle", LINE_FREEHAND_STRAIGHT_ANGLE) > 90)
                    source.Configs["Drawing"].Set("Freehand straight angle", LINE_FREEHAND_STRAIGHT_ANGLE);
                if (source.Configs["Drawing"].GetDouble("Smoothing", SMOOTHING) < 0 || source.Configs["Drawing"].GetDouble("Smoothing", SMOOTHING) > 0.999)
                    source.Configs["Drawing"].Set("Smoothing", SMOOTHING);
                if (source.Configs["Drawing"].GetInt("Rubber size", RUBBER_SIZE) < 0 || source.Configs["Drawing"].GetInt("Rubber size", RUBBER_SIZE) > 200)
                    source.Configs["Drawing"].Set("Rubber size", RUBBER_SIZE);
                if (source.Configs["Drawing"].GetInt("Minimum time between user action", USER_ACTION_MIN_TIME) < 0 || source.Configs["Drawing"].GetInt("Minimum time between user action", USER_ACTION_MIN_TIME) > 1000)
                    source.Configs["Drawing"].Set("Minimum time between user action", USER_ACTION_MIN_TIME);


                source.Save();

                GESTURE_MIN_TIME = source.Configs["Gestures"].GetInt("Minimum time", GESTURE_MIN_TIME);
                GESTURE_MIN_CONFIDENCE = source.Configs["Gestures"].GetDouble("Minimum confidence", GESTURE_MIN_CONFIDENCE);
                LINE_THICKNESS = source.Configs["Drawing"].GetInt("Line thickness", LINE_THICKNESS);
                LINE_RESUME_THRESHOLD = source.Configs["Drawing"].GetInt("Resume threshold", LINE_RESUME_THRESHOLD);
                LINE_FREEHAND_STRAIGHT_MIN_DIST = source.Configs["Drawing"].GetInt("Freehand straight minimum distance", LINE_FREEHAND_STRAIGHT_MIN_DIST);
                LINE_FREEHAND_STRAIGHT_ANGLE = source.Configs["Drawing"].GetInt("Freehand straight angle", LINE_FREEHAND_STRAIGHT_ANGLE);
                SMOOTHING = source.Configs["Drawing"].GetDouble("Smoothing", SMOOTHING);
                RUBBER_SIZE = source.Configs["Drawing"].GetInt("Rubber size", RUBBER_SIZE);
                USER_ACTION_MIN_TIME = source.Configs["Drawing"].GetInt("Minimum time between user action", USER_ACTION_MIN_TIME);

                if (source.Configs["Gestures"].Get("draw hand state") != null && source.Configs["Gestures"].Get("rubber hand state") != null && source.Configs["Gestures"].Get("move hand state") != null)
                {

                    if (source.Configs["Gestures"].Get("draw hand state").CompareTo(HandState.Lasso.ToString()) == 0)
                        GESTURE_DRAW = HandState.Lasso;
                    else if (source.Configs["Gestures"].Get("rubber hand state").CompareTo(HandState.Lasso.ToString()) == 0)
                        GESTURE_RUBBER = HandState.Lasso;
                    else if (source.Configs["Gestures"].Get("move hand state").CompareTo(HandState.Lasso.ToString()) == 0)
                        GESTURE_MOVE = HandState.Lasso;

                    if (source.Configs["Gestures"].Get("draw hand state").CompareTo(HandState.Open.ToString()) == 0)
                        GESTURE_DRAW = HandState.Open;
                    else if (source.Configs["Gestures"].Get("rubber hand state").CompareTo(HandState.Open.ToString()) == 0)
                        GESTURE_RUBBER = HandState.Open;
                    else if (source.Configs["Gestures"].Get("move hand state").CompareTo(HandState.Open.ToString()) == 0)
                        GESTURE_MOVE = HandState.Open;

                    if (source.Configs["Gestures"].Get("draw hand state").CompareTo(HandState.Closed.ToString()) == 0)
                        GESTURE_DRAW = HandState.Closed;
                    else if (source.Configs["Gestures"].Get("rubber hand state").CompareTo(HandState.Closed.ToString()) == 0)
                        GESTURE_RUBBER = HandState.Closed;
                    else if (source.Configs["Gestures"].Get("move hand state").CompareTo(HandState.Closed.ToString()) == 0)
                        GESTURE_MOVE = HandState.Closed;
                }
            }
            else
            {
                System.IO.File.Create(@path).Close();

                IConfigSource source = new IniConfigSource("UserConfig.ini");

                IConfig config = source.AddConfig("Gestures");
                config.Set("Minimum time", GESTURE_MIN_TIME);
                config.Set("Minimum confidence", GESTURE_MIN_CONFIDENCE);
                config.Set("draw hand state", GESTURE_DRAW.ToString());
                config.Set("rubber hand state", GESTURE_RUBBER.ToString());
                config.Set("move hand state", GESTURE_MOVE.ToString());

                config = source.AddConfig("Drawing");
                config.Set("Line thickness", LINE_THICKNESS);
                config.Set("Resume threshold", LINE_RESUME_THRESHOLD);
                config.Set("Freehand straight minimum distance", LINE_FREEHAND_STRAIGHT_MIN_DIST);
                config.Set("Freehand straight angle", LINE_FREEHAND_STRAIGHT_ANGLE);
                config.Set("Smoothing", SMOOTHING);
                config.Set("Rubber size", RUBBER_SIZE);
                config.Set("Minimum time between user action", USER_ACTION_MIN_TIME);

                source.Save();
            }
        }
    }
}
