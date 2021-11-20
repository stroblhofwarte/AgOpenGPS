using System;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormEditCurve : Form
    {
        private readonly FormGPS mf = null;

        private double snapAdj = 0;

        public FormEditCurve(Form callingForm)
        {
            //get copy of the calling main form
            mf = callingForm as FormGPS;

            InitializeComponent();

            this.Text = gStr.gsEditABCurve;
            nudMinTurnRadius.Controls[0].Enabled = false;
        }

        private void FormEditAB_Load(object sender, EventArgs e)
        {
            label1.Text = mf.unitsInCm;
            label2.Text = mf.unitsFtM;

            //btnLeft.Text = "-"+Properties.Settings.Default.setDisplay_snapDistanceSmall.ToString() + "cm";
            lblHalfWidth.Text = (mf.tool.toolWidth * 0.5 * mf.m2FtOrM).ToString("N2");

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

        private void bntOk_Click(object sender, EventArgs e)
        {
            //save entire list
            mf.FileSaveCurveLines();
            mf.gyd.moveDistance = 0;
            mf.gyd.isValid = false;

            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            mf.FileLoadCurveLines();

            mf.gyd.isValid = false;
            Close();
        }

        private void btnSwapAB_Click(object sender, EventArgs e)
        {
            mf.gyd.isValid = false;
            mf.gyd.lastSecond = 0;

            if (mf.gyd.selectedCurveLine?.curvePts.Count > 1)
            {
                mf.gyd.selectedCurveLine.curvePts.Reverse();

                for (int i = 0; i < mf.gyd.selectedCurveLine.curvePts.Count; i++)
                {
                    vec3 pt3 = mf.gyd.selectedCurveLine.curvePts[i];
                    pt3.heading += Math.PI;
                    if (pt3.heading > glm.twoPI) pt3.heading -= glm.twoPI;
                    mf.gyd.selectedCurveLine.curvePts[i] = pt3;
                }
            }
        }

        private void btnContourPriority_Click(object sender, EventArgs e)
        {
            mf.gyd.MoveABCurve(mf.gyd.distanceFromCurrentLinePivot);
        }

        private void btnAdjRight_Click(object sender, EventArgs e)
        {
            mf.gyd.MoveABCurve(snapAdj);
        }

        private void btnAdjLeft_Click(object sender, EventArgs e)
        {
            mf.gyd.MoveABCurve(-snapAdj);
        }

        private void btnRightHalfWidth_Click(object sender, EventArgs e)
        {
            mf.gyd.MoveABCurve(mf.tool.toolWidth * 0.5);
        }

        private void btnLeftHalfWidth_Click(object sender, EventArgs e)
        {
            mf.gyd.MoveABCurve(mf.tool.toolWidth * -0.5);
        }

        private void btnNosave_Click(object sender, EventArgs e)
        {
            mf.gyd.isValid = false;
            Close();
        }
    }
}
