using System;
using System.Globalization;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormEnterAB : Form
    {
        private readonly FormGPS mf = null;

        private bool isAB = true;
        private double desHeading = 0;

        public FormEnterAB(Form callingForm)
        {
            //get copy of the calling main form
            mf = callingForm as FormGPS;

            InitializeComponent();

            this.Text = gStr.gsEditABLine;
            nudLatitude.Controls[0].Enabled = false;
            nudLongitude.Controls[0].Enabled = false;
            nudLatitudeB.Controls[0].Enabled = false;
            nudLatitudeB.Controls[0].Enabled = false;
            nudHeading.Controls[0].Enabled = false;

            nudLatitude.Value = (decimal)mf.pn.latitude;
            nudLatitudeB.Value = (decimal)mf.pn.latitude + 0.000001m;
            nudLongitude.Value = (decimal)mf.pn.longitude;
            nudLongitudeB.Value = (decimal)mf.pn.longitude + 0.000001m;
        }

        private void FormEnterAB_Load(object sender, EventArgs e)
        {
            btnEnterManual.Focus();
            textBox1.Text = "Create A New Line";
        }

        private void nudHeading_Click(object sender, EventArgs e)
        {
            mf.KeypadToNUD((NumericUpDown)sender, this);
            btnEnterManual.Focus();
            CalcHeading();
        }

        private void btnChooseType_Click(object sender, EventArgs e)
        {
            isAB = !isAB;
            if (isAB)
            {
                nudLatitudeB.Enabled = true;
                nudLongitudeB.Enabled = true;
                nudHeading.Enabled = false;
                nudLatitudeB.Visible = true;
                nudLongitudeB.Visible = true;
                nudHeading.Visible = false;
                label4.Visible = false;
                label5.Visible = true;
            }
            else
            {
                nudLatitudeB.Enabled = false;
                nudLongitudeB.Enabled = false;
                nudHeading.Enabled = true;
                nudLatitudeB.Visible = false;
                nudLongitudeB.Visible = false;
                nudHeading.Visible = true;
                label4.Visible = true;
                label5.Visible = false;
            }
            CalcHeading();
        }

        private void nudLatitude_Click(object sender, EventArgs e)
        {
            mf.KeypadToNUD((NumericUpDown)sender, this);
            btnEnterManual.Focus();
            CalcHeading();
        }

        private void nudLongitude_Click(object sender, EventArgs e)
        {
            mf.KeypadToNUD((NumericUpDown)sender, this);
            btnEnterManual.Focus();
            CalcHeading();
        }

        private void nudLatitudeB_Click(object sender, EventArgs e)
        {
            mf.KeypadToNUD((NumericUpDown)sender, this);
            btnEnterManual.Focus();
            CalcHeading();
        }

        private void nudLongitudeB_Click(object sender, EventArgs e)
        {
            mf.KeypadToNUD((NumericUpDown)sender, this);
            btnEnterManual.Focus();
            CalcHeading();
        }

        private void btnEnterManual_Click(object sender, EventArgs e)
        {
            if (textBox1.Text == "Create A New Line") this.DialogResult = DialogResult.Cancel;
            Close();
        }

        public void CalcHeading()
        {
            if (isAB)
            {
                mf.pn.ConvertWGS84ToLocal((double)nudLatitude.Value, (double)nudLongitude.Value, out double nort, out double east);
                mf.pn.ConvertWGS84ToLocal((double)nudLatitudeB.Value, (double)nudLongitudeB.Value, out double nort2, out double east2);

                // heading based on AB points
                desHeading = Math.Atan2(east2 - east, nort2 - nort);
                if (desHeading < 0) desHeading += glm.twoPI;

                if (mf.gyd.desList.Count > 0)
                    mf.gyd.desList[0] = new vec3(east, nort, desHeading);
                else
                    mf.gyd.desList.Add(new vec3(east, nort, desHeading));

                if (mf.gyd.desList.Count > 1)
                    mf.gyd.desList[1] = new vec3(east2, nort2, desHeading);
                else
                    mf.gyd.desList.Add(new vec3(east2, nort2, desHeading));

                nudHeading.Value = (decimal)(glm.toDegrees(desHeading));
            }
            else
            {
                mf.pn.ConvertWGS84ToLocal((double)nudLatitude.Value, (double)nudLongitude.Value, out double nort, out double east);

                desHeading = glm.toRadians((double)nudHeading.Value);

                if (mf.gyd.desList.Count > 0)
                    mf.gyd.desList[0] = new vec3(east, nort, desHeading);
                else
                    mf.gyd.desList.Add(new vec3(east, nort, desHeading));

                if (mf.gyd.desList.Count > 1)
                    mf.gyd.desList[1] = new vec3(east + Math.Sin(desHeading), nort + Math.Cos(desHeading), desHeading);
                else
                    mf.gyd.desList.Add(new vec3(east + Math.Sin(desHeading), nort + Math.Cos(desHeading), desHeading));
            }

            textBox1.Text = "Manual AB " +
                (Math.Round(glm.toDegrees(desHeading), 1)).ToString(CultureInfo.InvariantCulture) +
                "\u00B0 " + mf.FindDirection(desHeading);
            if (textBox1.Text != "Create A New Line") btnEnterManual.Enabled = true;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
