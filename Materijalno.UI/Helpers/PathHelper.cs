using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Materijalno.UI.Helpers
{
    public class PathHelper
    {
        private String mExecutablePath;
        private String mExecutableRootDirectory;


        public PathHelper()
        {
            GetPaths();
        }

        private void GetPaths()
        {
            mExecutablePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            mExecutableRootDirectory = System.IO.Path.GetDirectoryName(mExecutablePath);
        }

        public String MExecutableRootDirectory
        {
            get { return mExecutableRootDirectory; }
            set { mExecutableRootDirectory = value; }
        }

        public String MExecutablePath
        {
            get { return mExecutablePath; }
            set { mExecutablePath = value; }
        }
    }
}
