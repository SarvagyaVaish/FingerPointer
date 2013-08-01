using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace PresentationApp
{
    class _3DLine
    {
        public Point3D Point1;
        public Point3D Point2;

        /// <summary>
        /// Slope of 3D Line
        /// </summary>
        public Point3D u_vec;

        public _3DLine(Point3D Point1, Point3D Point2)
        {
            // TODO: Complete member initialization
            this.Point1 = Point1;
            this.Point2 = Point2;

            u_vec = new Point3D();
            u_vec.X = Point2.X - Point1.X;
            u_vec.Y = Point2.Y - Point1.Y;
            u_vec.Z = Point2.Z - Point1.Z;
        }


        public void printCoords()
        {
            if (Point1.X > 0 && Point1.Y > 0 && Point1.Z > 0)
            {
                Console.WriteLine("Point 1 X: " + Point1.X + " Point 1 Y: " + Point1.Y + " Point 1 Z: " + Point1.Z);
                Console.WriteLine("Point 2 X: " + Point2.X + " Point 2 Y: " + Point2.Y + " Point 2 Z: " + Point2.Z);
            }
        }

        public void drawPoints(DrawingContext dc)
        {
            if (Point1.X > 0 && Point1.Y > 0 && Point1.Z > 0)
            {
                //using (dc)
                {
                    Point p1 = new Point(Point1.X, Point1.Y);
                    Point p2 = new Point(Point2.X, Point2.Y);

                    //dc.DrawEllipse(Brushes.Aqua, null, p1, .01, .01);
                    //dc.DrawEllipse(Brushes.Brown, null, p2, .5, .5);

                    dc.DrawLine(new Pen(Brushes.Cyan, 1), p1, p2);
                }
            }
        }
    }
}
