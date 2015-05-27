using votragsfinger2.util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace votragsfinger2
{
    /// <summary>
    /// Interaktionslogik für menu.xaml
    /// </summary>
    public partial class menu : UserControl
    {

        public delegate void ColorChangedEventHandler(Color newColor);
        public event ColorChangedEventHandler ColorChanged;

        public delegate void ThicknessChangedEventHandler(double newThickness);
        public event ThicknessChangedEventHandler ThicknessChanged;

        public delegate void DrawTypeChangedEventHandler(votragsfinger2.SketchCanvas.DrawType dt);
        public event DrawTypeChangedEventHandler DrawTypeChanged;

        public menu()
        {
            InitializeComponent();
        }

        private void onColorSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int hue = (int)e.NewValue;
            SolidColorBrush brushColor = (SolidColorBrush)this.Resources["brushColor"];

            int sat = 1;
            int val = 1;

            if (hue == 0)
            {
                sat = 0;
                val = 0;
            }

            else if (hue == this.sliderBrushColor.Maximum)
            {
                sat = 0;
            }

            //TODO: add black and white (black if val < 10 - white if val > 370) val between 0-380
            brushColor.Color = HsvColor.ColorFromHSV(360 / (this.sliderBrushColor.Maximum - 1) * (hue - 1), sat, val);

            if (this.ColorChanged != null)
            {
                this.ColorChanged(brushColor.Color);
            }
        }

        private void onLineSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.ThicknessChanged != null)
            {
                this.ThicknessChanged(e.NewValue);
            }
        }

        private void RadioButton_Checked_Freehand(object sender, RoutedEventArgs e)
        {
            if (this.DrawTypeChanged != null)
            {
                this.DrawTypeChanged(votragsfinger2.SketchCanvas.DrawType.Freehand);
            }
        }

        private void RadioButton_Checked_FreehandStraight(object sender, RoutedEventArgs e)
        {
            if (this.DrawTypeChanged != null)
            {
                this.DrawTypeChanged(votragsfinger2.SketchCanvas.DrawType.FreehandStraight);
            }
        }

        private void RadioButton_Checked_Line(object sender, RoutedEventArgs e)
        {
            if (this.DrawTypeChanged != null)
            {
                this.DrawTypeChanged(votragsfinger2.SketchCanvas.DrawType.Line);
            }
        }






    }
}
