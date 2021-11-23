using System;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormSmoothAB : Form
    {
        //class variables
        private readonly FormGPS mf = null;

        private int smoothCount = 20;

        public FormSmoothAB(Form callingForm)
        {
            //get copy of the calling main form
            mf = callingForm as FormGPS;
            InitializeComponent();

            this.bntOK.Text = gStr.gsForNow;
            this.btnSave.Text = gStr.gsToFile;

            this.Text = gStr.gsSmoothABCurve;
        }

        private void bntOK_Click(object sender, EventArgs e)
        {
            SaveSmoothAsRefList();
            Close();
        }

        private void FormSmoothAB_Load(object sender, EventArgs e)
        {
            smoothCount = 20;
            lblSmooth.Text = "**";
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            mf.FileLoadCurveLines();
            mf.gyd.desList.Clear();
            Close();
        }

        private void btnNorth_MouseDown(object sender, MouseEventArgs e)
        {
            if (smoothCount++ > 100) smoothCount = 100;
            SmoothAB(smoothCount * 2);
            lblSmooth.Text = smoothCount.ToString();
        }

        private void btnSouth_MouseDown(object sender, MouseEventArgs e)
        {
            smoothCount--;
            if (smoothCount < 2) smoothCount = 2;
            SmoothAB(smoothCount * 2);
            lblSmooth.Text = smoothCount.ToString();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            SaveSmoothAsRefList();
            mf.FileSaveCurveLines();
            Close();
        }

        //turning the visual line into the real reference line to use
        public void SaveSmoothAsRefList()
        {
            if (mf.gyd.selectedLine != null)
            {
                //oops no smooth list generated
                int cnt = mf.gyd.desList.Count;
                if (cnt == 0) return;

                //eek
                mf.gyd.selectedLine.curvePts.Clear();

                //copy to an array to calculate all the new headings
                vec3[] arr = new vec3[cnt];
                mf.gyd.desList.CopyTo(arr);

                //calculate new headings on smoothed line
                for (int i = 1; i < cnt - 1; i++)
                {
                    arr[i].heading = Math.Atan2(arr[i + 1].easting - arr[i].easting, arr[i + 1].northing - arr[i].northing);
                    if (arr[i].heading < 0) arr[i].heading += glm.twoPI;
                    mf.gyd.selectedLine.curvePts.Add(arr[i]);
                }
            }
            mf.gyd.desList.Clear();
        }

        //for calculating for display the averaged new line
        public void SmoothAB(int smPts)
        {
            if (mf.gyd.selectedLine?.curvePts.Count > 399)
            {
                //count the reference list of original curve
                int cnt = mf.gyd.selectedLine.curvePts.Count;

                //just go back if not very long
                if (cnt < 400) return;

                //the temp array
                vec3[] arr = new vec3[cnt];

                //read the points before and after the setpoint
                for (int s = 0; s < smPts / 2; s++)
                {
                    arr[s].easting = mf.gyd.selectedLine.curvePts[s].easting;
                    arr[s].northing = mf.gyd.selectedLine.curvePts[s].northing;
                    arr[s].heading = mf.gyd.selectedLine.curvePts[s].heading;
                }

                for (int s = cnt - (smPts / 2); s < cnt; s++)
                {
                    arr[s].easting = mf.gyd.selectedLine.curvePts[s].easting;
                    arr[s].northing = mf.gyd.selectedLine.curvePts[s].northing;
                    arr[s].heading = mf.gyd.selectedLine.curvePts[s].heading;
                }

                //average them - center weighted average
                for (int i = smPts / 2; i < cnt - (smPts / 2); i++)
                {
                    for (int j = -smPts / 2; j < smPts / 2; j++)
                    {
                        arr[i].easting += mf.gyd.selectedLine.curvePts[j + i].easting;
                        arr[i].northing += mf.gyd.selectedLine.curvePts[j + i].northing;
                    }
                    arr[i].easting /= smPts;
                    arr[i].northing /= smPts;
                    arr[i].heading = mf.gyd.selectedLine.curvePts[i].heading;
                }

                //make a list to draw
                mf.gyd.desList.Clear();
                for (int i = 0; i < cnt; i++)
                {
                    mf.gyd.desList.Add(arr[i]);
                }
            }
        }
    }
}