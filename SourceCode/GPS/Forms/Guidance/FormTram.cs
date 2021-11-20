using System;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormTram : Form
    {
        //access to the main GPS form and all its variables
        private readonly FormGPS mf = null;

        public FormTram(Form callingForm)
        {
            //get copy of the calling main form
            mf = callingForm as FormGPS;
            InitializeComponent();

            this.Text = gStr.gsTramLines;
            label3.Text = gStr.gsPasses;
            label2.Text = ((int)(0.1 * mf.m2InchOrCm)).ToString() + mf.unitsInCm;
            lblTramWidth.Text = (mf.tram.tramWidth * mf.m2FtOrM).ToString("N2") + mf.unitsFtM;

            nudPasses.Controls[0].Enabled = false;
        }

        private void FormTram_Load(object sender, EventArgs e)
        {
            nudPasses.ValueChanged -= nudPasses_ValueChanged;
            nudPasses.Value = Properties.Settings.Default.setTram_passes;
            nudPasses.ValueChanged += nudPasses_ValueChanged;

            lblTrack.Text = (mf.vehicle.trackWidth * mf.m2FtOrM).ToString("N2") + mf.unitsFtM;

            mf.tool.halfToolWidth = (mf.tool.toolWidth - mf.tool.toolOverlap) / 2.0;
            lblToolWidthHalf.Text = (mf.tool.halfToolWidth * mf.m2FtOrM).ToString("N2") + mf.unitsFtM;

            mf.panelRight.Enabled = false;

            //if off, turn it on because they obviously want a tram.
            if (mf.tram.displayMode == 0) mf.tram.displayMode = 1;

            switch (mf.tram.displayMode)
            {
                case 0:
                    btnMode.Image = Properties.Resources.TramOff;
                    break;
                case 1:
                    btnMode.Image = Properties.Resources.TramAll;
                    break;
                case 2:
                    btnMode.Image = Properties.Resources.TramLines;
                    break;
                case 3:
                    btnMode.Image = Properties.Resources.TramOuter;
                    break;

                default:
                    break;
            }
            mf.CloseTopMosts();
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            mf.FileSaveABLines();

            mf.gyd.moveDistance = 0;
            mf.panelRight.Enabled = true;
            mf.panelDrag.Visible = false;
            mf.offX = 0;
            mf.offY = 0;
            mf.FileSaveTram();
            mf.FixTramModeButton();
            Close();
        }

        private void btnLeft_Click(object sender, EventArgs e)
        {
            double dist = -0.1;
            mf.gyd.MoveABLine(dist);
            mf.gyd.BuildTram();
        }

        private void btnRight_Click(object sender, EventArgs e)
        {
            double dist = 0.1;
            mf.gyd.MoveABLine(dist);
            mf.gyd.BuildTram();
        }

        private void btnAdjLeft_Click(object sender, EventArgs e)
        {
            mf.gyd.MoveABLine(-mf.tool.halfToolWidth);
            mf.gyd.BuildTram();
        }

        private void btnAdjRight_Click(object sender, EventArgs e)
        {
            mf.gyd.MoveABLine(mf.tool.halfToolWidth);
            mf.gyd.BuildTram();
        }

        //determine mins maxs of patches and whole field.
        private void nudSnapAdj_Enter(object sender, EventArgs e)
        {
            mf.KeypadToNUD((NumericUpDown)sender, this);
            btnCancel.Focus();
        }

        private void nudPasses_ValueChanged(object sender, EventArgs e)
        {
            mf.tram.passes = (int)nudPasses.Value;
            Properties.Settings.Default.setTram_passes = mf.tram.passes;
            Properties.Settings.Default.Save();
            mf.gyd.BuildTram();
        }

        private void nudPasses_Enter(object sender, EventArgs e)
        {
            mf.KeypadToNUD((NumericUpDown)sender, this);
            btnCancel.Focus();
            mf.gyd.BuildTram();
        }

        private void btnSwapAB_Click(object sender, EventArgs e)
        {
            if (mf.gyd.selectedABLine?.curvePts.Count > 1)
                mf.gyd.SetABLineByHeading(Math.Atan2(mf.gyd.selectedABLine.curvePts[0].easting - mf.gyd.selectedABLine.curvePts[1].easting,
                    mf.gyd.selectedABLine.curvePts[0].northing - mf.gyd.selectedABLine.curvePts[1].northing));

            mf.gyd.BuildTram();
        }

        private void btnTriggerDistanceUp_MouseDown(object sender, MouseEventArgs e)
        {
            nudPasses.UpButton();
            mf.gyd.BuildTram();
        }

        private void btnTriggerDistanceDn_MouseDown(object sender, MouseEventArgs e)
        {
            nudPasses.DownButton();
            mf.gyd.BuildTram();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            mf.FileLoadABLines();

            mf.tram.tramList.Clear();
            mf.tram.tramBndOuterArr.Clear();
            mf.tram.tramBndInnerArr.Clear();

            //mf.ABLine.tramPassEvery = 0;
            //mf.ABLine.tramBasedOn = 0;
            mf.panelRight.Enabled = true;
            mf.panelDrag.Visible = false;
            mf.offX = 0;
            mf.offY = 0;

            mf.tram.displayMode = 0;
            mf.FileSaveTram();
            mf.FixTramModeButton();
            Close();
        }

        private void btnMode_Click(object sender, EventArgs e)
        {
            mf.tram.displayMode++;
            if (mf.tram.displayMode > 3) mf.tram.displayMode = 0;

            switch (mf.tram.displayMode)
            {
                case 0:
                    btnMode.Image = Properties.Resources.TramOff;
                    break;
                case 1:
                    btnMode.Image = Properties.Resources.TramAll;
                    break;
                case 2:
                    btnMode.Image = Properties.Resources.TramLines;
                    break;
                case 3:
                    btnMode.Image = Properties.Resources.TramOuter;
                    break;

                default:
                    break;
            }
        }
    }
}