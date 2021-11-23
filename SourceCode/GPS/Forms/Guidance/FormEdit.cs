using System;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormEditAB : Form
    {
        private readonly FormGPS mf = null;

        private double snapAdj = 0;
        private double heading = 0;
        private Mode mode;

        public FormEditAB(Form callingForm, Mode mode2)
        {
            //get copy of the calling main form
            mf = callingForm as FormGPS;

            InitializeComponent();
            mode = mode2;

            if (mode.HasFlag(Mode.AB))
                Text = gStr.gsEditABLine;
            else
                Text = gStr.gsEditABCurve;

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
            if (mf.gyd.selectedLine?.curvePts.Count > 1 && mf.gyd.selectedLine.Mode.HasFlag(Mode.AB))
                heading = Math.Atan2(mf.gyd.selectedLine.curvePts[1].easting - mf.gyd.selectedLine.curvePts[0].easting,
                    mf.gyd.selectedLine.curvePts[1].northing - mf.gyd.selectedLine.curvePts[0].northing);

            tboxHeading.Text = Math.Round(glm.toDegrees(heading), 5).ToString();
        }

        private void tboxHeading_Click(object sender, EventArgs e)
        {
            using (FormNumeric form = new FormNumeric(0, 360, Math.Round(glm.toDegrees(heading), 5)))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    tboxHeading.Text = ((double)form.ReturnValue).ToString();
                    heading = form.ReturnValue;
                    mf.gyd.SetABLineByHeading(glm.toRadians((double)form.ReturnValue));
                }
            }

            mf.gyd.isValid = false;
            btnCancel.Focus();
        }

        private void nudMinTurnRadius_Click(object sender, EventArgs e)
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
            mf.gyd.MoveABLine(snapAdj);
        }

        private void btnAdjLeft_Click(object sender, EventArgs e)
        {
            mf.gyd.MoveABLine(-snapAdj);
        }

        private void bntOk_Click(object sender, EventArgs e)
        {
            //save entire list
            if (mode.HasFlag(Mode.AB))
                mf.FileSaveABLines();
            else
                mf.FileSaveCurveLines();

            mf.gyd.moveDistance = 0;
            mf.gyd.isValid = false;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            if (mode.HasFlag(Mode.AB))
                mf.FileLoadABLines();
            else
                mf.FileLoadCurveLines();

            mf.gyd.isValid = false;
            Close();
        }

        private void btnSwapAB_Click(object sender, EventArgs e)
        {
            mf.gyd.isValid = false;
            if (mf.gyd.selectedLine?.curvePts.Count > 1)
            {
                if (mode.HasFlag(Mode.AB))
                {
                    double heading = Math.Atan2(mf.gyd.selectedLine.curvePts[0].easting - mf.gyd.selectedLine.curvePts[1].easting,
                        mf.gyd.selectedLine.curvePts[0].northing - mf.gyd.selectedLine.curvePts[1].northing);
                    mf.gyd.SetABLineByHeading(heading);

                    tboxHeading.Text = Math.Round(glm.toDegrees(heading), 5).ToString();
                }
                else
                {
                    mf.gyd.selectedLine.curvePts.Reverse();

                    for (int i = 0; i < mf.gyd.selectedLine.curvePts.Count; i++)
                    {
                        vec3 pt3 = mf.gyd.selectedLine.curvePts[i];
                        pt3.heading += Math.PI;
                        if (pt3.heading > glm.twoPI) pt3.heading -= glm.twoPI;
                        mf.gyd.selectedLine.curvePts[i] = pt3;
                    }
                }
            }
        }

        private void btnContourPriority_Click(object sender, EventArgs e)
        {
            mf.gyd.MoveABLine(mf.gyd.distanceFromCurrentLinePivot);
        }

        private void btnRightHalfWidth_Click(object sender, EventArgs e)
        {
            mf.gyd.MoveABLine(mf.tool.toolWidth * 0.5);
        }

        private void btnLeftHalfWidth_Click(object sender, EventArgs e)
        {
            mf.gyd.MoveABLine(mf.tool.toolWidth * -0.5);
        }

        private void btnNoSave_Click(object sender, EventArgs e)
        {
            mf.gyd.isValid = false;
            Close();
        }

        private void cboxDegrees_SelectedIndexChanged(object sender, EventArgs e)
        {
            double heading = glm.toRadians(double.Parse(cboxDegrees.SelectedItem.ToString()));
            mf.gyd.SetABLineByHeading(heading);
            tboxHeading.Text = Math.Round(glm.toDegrees(heading), 5).ToString();
        }
    }
}
