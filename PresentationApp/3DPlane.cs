using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Media3D;

namespace PresentationApp
{
    class _3DPlane
    {
        public Point3D normal_vec;

        public _3DPlane()
        {
            normal_vec = new Point3D(0,0,1);
        }
        public _3DPlane(Point3D vec, Point3D origin = new Point3D())
        {
            normal_vec = vec;
        }
    }
}
