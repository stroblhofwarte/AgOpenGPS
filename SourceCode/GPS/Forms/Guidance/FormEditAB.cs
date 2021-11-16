using System;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormEditAB : Form
    {
        private readonly FormGPS mf = null;

        private double snapAdj = 0;
        private double heading = 0;

        public FormEditAB(Form callingForm)
        {
            //get copy of the calling main form
            mf = callingForm as FormGPS;

            InitializeComponent();

            this.Text = gStr.gsEditABLine;
            nudMinTurnRadius.Controls[0].Enabled = false;
        }

        private void FormEditAB_Load(object sender, EventArgs e)
        {
            if (mf.isMetric)
            {
                nudMinTurnRadius.DecimalPlaces = 0;
                nudMinTurnRadius.Value = (int)((double)Properties.Settings.Default.setAS_snapDistance * mf.cm2CmOrIn);
            }
            else
            {
                nudMinTurnRadius.DecimalPlaces = 1;
                nudMinTurnRadius.Value = (decimal)Math.Round(((double)Properties.Settings.Default.setAS_snapDistance * mf.cm2CmOrIn), 1);
            }

            label1.Text = mf.unitsInCm;
            btnCancel.Focus();
            lblHalfSnapFtM.Text = mf.unitsFtM;
            lblHalfWidth.Text = (mf.tool.toolWidth * 0.5 * mf.m2FtOrM).ToString("N2");
            if (mf.ABLine.selectedABIndex > -1 && mf.gyd.refList[mf.ABLine.selectedABIndex].curvePts.Count > 1)
                heading = Math.Atan2(mf.gyd.refList[mf.ABLine.selectedABIndex].curvePts[1].easting - mf.gyd.refList[mf.ABLine.selectedABIndex].curvePts[0].easting,
                    mf.gyd.refList[mf.ABLine.selectedABIndex].curvePts[1].northing - mf.gyd.refList[mf.ABLine.selectedABIndex].curvePts[0].northing);

            tboxHeading.Text = Math.Round(glm.toDegrees(heading), 5).ToString();
        }

        private void tboxHeading_Enter(object sender, EventArgs e)
        {
            using (FormNumeric form = new FormNumeric(0, 360, Math.Round(glm.toDegrees(heading), 5)))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    tboxHeading.Text = ((double)form.ReturnValue).ToString();
                    heading = form.ReturnValue;
                    mf.ABLine.SetABLineByHeading(glm.toRadians((double)form.ReturnValue));
                }
            }

            mf.gyd.isValid = false;
            btnCancel.Focus();
        }

        private void nudMinTurnRadius_Enter(object sender, EventArgs e)
        {
            mf.KeypadToNUD((NumericUpDown)sender, this);
            btnCancel.Focus();
        }

        private void nudMinTurnRadius_ValueChanged(object sender, EventArgs e)
        {
            snapAdj = (double)nudMinTurnRadius.Value * mf.inOrCm2Cm * 0.01;
        }

        private void btnAdjRight_Click(object sender, EventArgs e)
        {
            mf.ABLine.MoveABLine(snapAdj);
        }

        private void btnAdjLeft_Click(object sender, EventArgs e)
        {
            mf.ABLine.MoveABLine(-snapAdj);
        }

        private void bntOk_Click(object sender, EventArgs e)
        {
            mf.FileSaveABLines();
            mf.gyd.moveDistance = 0;

            mf.panelRight.Enabled = true;
            mf.gyd.isValid = false;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            int last = mf.ABLine.selectedABIndex;
            mf.FileLoadABLines();

            if (last < mf.gyd.refList.Count)
                mf.panelRight.Enabled = true;

            mf.gyd.moveDistance = 0;
            mf.gyd.isValid = false;
            Close();
        }

        private void btnSwapAB_Click(object sender, EventArgs e)
        {
            if (mf.ABLine.selectedABIndex > -1 && mf.gyd.refList[mf.ABLine.selectedABIndex].curvePts.Count > 1)
            {
                double heading = Math.Atan2(mf.gyd.refList[mf.ABLine.selectedABIndex].curvePts[0].easting - mf.gyd.refList[mf.ABLine.selectedABIndex].curvePts[1].easting,
                    mf.gyd.refList[mf.ABLine.selectedABIndex].curvePts[0].northing - mf.gyd.refList[mf.ABLine.selectedABIndex].curvePts[1].northing);
                mf.ABLine.SetABLineByHeading(heading);

                tboxHeading.Text = Math.Round(glm.toDegrees(heading), 5).ToString();
            }
            mf.gyd.isValid = false;
        }

        private void btnContourPriority_Click(object sender, EventArgs e)
        {
            mf.ABLine.MoveABLine(mf.gyd.distanceFromCurrentLinePivot);
        }

        private void btnRightHalfWidth_Click(object sender, EventArgs e)
        {
            mf.ABLine.MoveABLine(mf.tool.toolWidth * 0.5);
        }

        private void btnLeftHalfWidth_Click(object sender, EventArgs e)
        {
            mf.ABLine.MoveABLine(mf.tool.toolWidth * -0.5);
        }

        private void btnNoSave_Click(object sender, EventArgs e)
        {
            mf.gyd.isValid = false;
            Close();
        }

        private void cboxDegrees_SelectedIndexChanged(object sender, EventArgs e)
        {
            double heading = glm.toRadians(double.Parse(cboxDegrees.SelectedItem.ToString()));
            mf.ABLine.SetABLineByHeading(heading);
            tboxHeading.Text = Math.Round(glm.toDegrees(heading), 5).ToString();
        }
    }
}
