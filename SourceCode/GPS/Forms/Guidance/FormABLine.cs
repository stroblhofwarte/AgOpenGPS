//Please, if you use this, share the improvements

using System;
using System.Globalization;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormABLine : Form
    {
        //access to the main GPS form and all its variables
        private readonly FormGPS mf = null;

        private int originalLine = 0;
        private double desHeading = 0;

        public FormABLine(Form callingForm)
        {
            //get copy of the calling main form
            mf = callingForm as FormGPS;

            InitializeComponent();
            this.Text = gStr.gsABline;
        }

        private void FormABLine_Load(object sender, EventArgs e)
        {
            //tboxABLineName.Enabled = false;
            //btnAddToFile.Enabled = false;
            //btnAddAndGo.Enabled = false;
            //btnAPoint.Enabled = false;
            //btnBPoint.Enabled = false;
            //cboxHeading.Enabled = false;
            //tboxHeading.Enabled = false;
            //tboxABLineName.Text = "";
            //tboxABLineName.Enabled = false;

            //small window
            //ShowFullPanel(true);

            panelPick.Top = 3;
            panelPick.Left = 3;
            panelAPlus.Top = 3;
            panelAPlus.Left = 3;
            panelName.Top = 3;
            panelName.Left = 3;

            panelEditName.Top = 3;
            panelEditName.Left = 3;

            panelPick.Visible = true;
            panelAPlus.Visible = false;
            panelName.Visible = false;
            panelEditName.Visible = false;

            this.Size = new System.Drawing.Size(470, 360);

            originalLine = mf.ABLine.numABLineSelected;

            mf.ABLine.isABLineBeingSet = false;
            UpdateLineList();
            if (lvLines.Items.Count > 0 && originalLine > 0)
            {
                lvLines.Items[originalLine - 1].EnsureVisible();
                lvLines.Items[originalLine - 1].Selected = true;
                lvLines.Select();
            }
        }

        private void UpdateLineList()
        {
            lvLines.Clear();
            ListViewItem itm;

            foreach (CABLines item in mf.ABLine.lineArr)
            {
                itm = new ListViewItem(item.Name);
                lvLines.Items.Add(itm);
            }

            // go to bottom of list - if there is a bottom
            if (lvLines.Items.Count > 0)
            {
                lvLines.Items[lvLines.Items.Count - 1].EnsureVisible();
                lvLines.Items[lvLines.Items.Count - 1].Selected = true;
                lvLines.Select();
            }
        }
        private void btnCancel_APlus_Click(object sender, EventArgs e)
        {
            panelPick.Visible = true;
            panelAPlus.Visible = false;
            panelEditName.Visible = false;

            textBox1.Enter -= textBox1_Enter;
            panelName.Visible = false;
            textBox1.Enter += textBox1_Enter;

            this.Size = new System.Drawing.Size(470, 360);

            UpdateLineList();
            mf.ABLine.isABLineBeingSet = false;
            btnBPoint.BackColor = System.Drawing.Color.Transparent;
        }

        private void btnAPoint_Click(object sender, EventArgs e)
        {
            vec3 fix = new vec3(mf.pivotAxlePos);

            mf.ABLine.desPoint1.easting = fix.easting + Math.Cos(fix.heading) * mf.tool.toolOffset;
            mf.ABLine.desPoint1.northing = fix.northing - Math.Sin(fix.heading) * mf.tool.toolOffset;
            desHeading = fix.heading;

            nudHeading.Enabled = true;
            nudHeading.Value = (decimal)glm.toDegrees(desHeading);

            BuildDesLine();

            btnBPoint.Enabled = true;
            btnAPoint.Enabled = false;

            btnEnter_APlus.Enabled = true;
            mf.ABLine.isABLineBeingSet = true;
        }

        private void btnBPoint_Click(object sender, EventArgs e)
        {
            vec3 fix = new vec3(mf.pivotAxlePos);

            btnBPoint.BackColor = System.Drawing.Color.Teal;

            mf.ABLine.desPoint2.easting = fix.easting + Math.Cos(fix.heading) * mf.tool.toolOffset;
            mf.ABLine.desPoint2.northing = fix.northing - Math.Sin(fix.heading) * mf.tool.toolOffset;

            // heading based on AB points
            desHeading = Math.Atan2(fix.easting + Math.Cos(fix.heading) * mf.tool.toolOffset - mf.ABLine.desPoint1.easting,
                fix.northing - Math.Sin(fix.heading) * mf.tool.toolOffset - mf.ABLine.desPoint1.northing);
            if (desHeading < 0) desHeading += glm.twoPI;

            nudHeading.Value = (decimal)(glm.toDegrees(desHeading));
        }

        private void nudHeading_Click(object sender, EventArgs e)
        {
            if (mf.KeypadToNUD((NumericUpDown)sender, this))
            {
                BuildDesLine();
            }
        }

        private void BuildDesLine()
        {
            desHeading = glm.toRadians((double)nudHeading.Value);

            mf.ABLine.desPoint2.easting = mf.ABLine.desPoint1.easting + Math.Sin(desHeading);
            mf.ABLine.desPoint2.northing = mf.ABLine.desPoint1.northing + Math.Cos(desHeading);
        }

        private void textBox1_Enter(object sender, EventArgs e)
        {
            if (mf.isKeyboardOn)
            {
                mf.KeyboardToText((TextBox)sender, this);
                btnAdd.Focus();
            }
        }

        private void btnAddTime_Click(object sender, EventArgs e)
        {
            textBox1.Text += DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);
        }

        private void btnEnter_APlus_Click(object sender, EventArgs e)
        {
            panelAPlus.Visible = false;
            panelName.Visible = true;

            textBox1.Text = "AB " +
                Math.Round(glm.toDegrees(desHeading), 1).ToString(CultureInfo.InvariantCulture) +
                "\u00B0 " + mf.FindDirection(desHeading);
        }

        private void BtnNewABLine_Click(object sender, EventArgs e)
        {
            lvLines.SelectedItems.Clear();
            panelPick.Visible = false;
            panelAPlus.Visible = true;
            panelName.Visible = false;

            btnAPoint.Enabled = true;
            btnBPoint.Enabled = false;
            nudHeading.Enabled = false;

            btnEnter_APlus.Enabled = false;

            this.Size = new System.Drawing.Size(270, 360);

        }

        private void btnEditName_Click(object sender, EventArgs e)
        {
            if (lvLines.SelectedItems.Count > 0)
            {
                int idx = lvLines.SelectedIndices[0];
                textBox2.Text = mf.ABLine.lineArr[idx].Name;

                panelPick.Visible = false;
                panelEditName.Visible = true;
                this.Size = new System.Drawing.Size(270, 360);
            }
        }

        private void btnSaveEditName_Click(object sender, EventArgs e)
        {
            if (textBox2.Text.Trim() == "") textBox2.Text = "No Name " + DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);

            int idx = lvLines.SelectedIndices[0];

            textBox2.Enter -= textBox2_Enter;
            panelEditName.Visible = false;
            textBox2.Enter += textBox2_Enter;

            panelPick.Visible = true;

            mf.ABLine.lineArr[idx].Name = textBox2.Text.Trim();
            mf.FileSaveABLines();

            this.Size = new System.Drawing.Size(470, 360);

            UpdateLineList();
            lvLines.Focus();
            mf.ABLine.isABLineBeingSet = false;
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            CABLines New = new CABLines();

            New.curvePts.Add(new vec3(mf.ABLine.desPoint1.easting, mf.ABLine.desPoint1.northing, desHeading));
            New.curvePts.Add(new vec3(mf.ABLine.desPoint1.easting + Math.Sin(desHeading), mf.ABLine.desPoint1.northing + Math.Cos(desHeading), desHeading));

            //name
            if (textBox2.Text.Trim() == "") textBox2.Text = "No Name " + DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);

            New.Name = textBox1.Text.Trim();

            mf.ABLine.lineArr.Add(New);
            mf.ABLine.numABLines = mf.ABLine.lineArr.Count;
            mf.ABLine.numABLineSelected = mf.ABLine.numABLines;

            mf.FileSaveABLines();

            panelPick.Visible = true;
            panelAPlus.Visible = false;

            textBox1.Enter -= textBox1_Enter;
            panelName.Visible = false;
            textBox1.Enter += textBox1_Enter;

            this.Size = new System.Drawing.Size(470, 360);

            UpdateLineList();
            lvLines.Focus();
            mf.ABLine.isABLineBeingSet = false;
        }

        private void btnDuplicate_Click(object sender, EventArgs e)
        {
            if (lvLines.SelectedItems.Count > 0)
            {
                int idx = lvLines.SelectedIndices[0];

                panelPick.Visible = false;
                panelName.Visible = true;
                this.Size = new System.Drawing.Size(270, 360);

                panelAPlus.Visible = false;
                panelName.Visible = true;

                if (mf.ABLine.lineArr[idx].curvePts.Count > 1)
                {
                    desHeading = Math.Atan2(mf.ABLine.lineArr[idx].curvePts[1].easting - mf.ABLine.lineArr[idx].curvePts[0].easting, mf.ABLine.lineArr[idx].curvePts[1].northing - mf.ABLine.lineArr[idx].curvePts[0].northing);

                    //calculate the new points for the reference line and points                
                    mf.ABLine.desPoint1.easting = mf.ABLine.lineArr[idx].curvePts[0].easting;
                    mf.ABLine.desPoint1.northing = mf.ABLine.lineArr[idx].curvePts[0].northing;
                }

                textBox1.Text = mf.ABLine.lineArr[idx].Name + " Copy";
            }
        }


        private void btnListUse_Click(object sender, EventArgs e)
        {
            mf.gyd.moveDistance = 0;
            //reset to generate new reference
            mf.gyd.isValid = false;

            if (lvLines.SelectedItems.Count > 0)
            {
                int idx = lvLines.SelectedIndices[0];
                mf.ABLine.numABLineSelected = idx + 1;

                mf.ABLine.refList.Clear();
                for (int i = 0; i < mf.ABLine.lineArr[idx].curvePts.Count; i++)
                {
                    mf.ABLine.refList.Add(mf.ABLine.lineArr[idx].curvePts[i]);
                }

                mf.EnableYouTurnButtons();

                //Go back with Line enabled
                Close();
            }

            //no item selected
            else
            {
                mf.btnABLine.Image = Properties.Resources.ABLineOff;
                mf.ABLine.isBtnABLineOn = false;
                mf.ABLine.numABLineSelected = 0;
                mf.DisableYouTurnButtons();
                if (mf.isAutoSteerBtnOn) mf.btnAutoSteer.PerformClick();
                if (mf.yt.isYouTurnBtnOn) mf.btnAutoYouTurn.PerformClick();
                Close();
            }
        }

        private void btnSwapAB_Click(object sender, EventArgs e)
        {
            if (lvLines.SelectedItems.Count > 0)
            {
                mf.gyd.isValid = false;
                int idx = lvLines.SelectedIndices[0];

                if (mf.ABLine.lineArr[idx].curvePts.Count > 1)
                {
                    double heading = Math.Atan2(mf.ABLine.lineArr[idx].curvePts[1].easting - mf.ABLine.lineArr[idx].curvePts[0].easting, mf.ABLine.lineArr[idx].curvePts[1].northing - mf.ABLine.lineArr[idx].curvePts[0].northing) + Math.PI;

                    if (heading > glm.twoPI) heading -= glm.twoPI;

                    vec3 pos = mf.ABLine.lineArr[idx].curvePts[0];

                    mf.ABLine.lineArr[idx].curvePts.Clear();

                    mf.ABLine.lineArr[idx].curvePts.Add(new vec3(pos.easting, pos.northing, heading));
                    mf.ABLine.lineArr[idx].curvePts.Add(new vec3(pos.easting + Math.Sin(heading), pos.northing + Math.Cos(heading), heading));

                    mf.FileSaveABLines();

                }

                UpdateLineList();
                lvLines.Focus();
            }
        }

        private void btnListDelete_Click(object sender, EventArgs e)
        {
            if (lvLines.SelectedItems.Count > 0)
            {
                int num = lvLines.SelectedIndices[0];
                mf.ABLine.lineArr.RemoveAt(num);
                lvLines.SelectedItems[0].Remove();

                mf.ABLine.numABLines = mf.ABLine.lineArr.Count;
                if (mf.ABLine.numABLineSelected == num+1) mf.ABLine.numABLineSelected = 0;
                else if (mf.ABLine.numABLineSelected > num) mf.ABLine.numABLineSelected--;
                if (mf.ABLine.numABLineSelected > mf.ABLine.numABLines) mf.ABLine.numABLineSelected = mf.ABLine.numABLines;

                if (mf.ABLine.numABLines == 0)
                {
                    mf.ABLine.DeleteAB();
                    if (mf.isAutoSteerBtnOn) mf.btnAutoSteer.PerformClick();
                    if (mf.yt.isYouTurnBtnOn) mf.btnAutoYouTurn.PerformClick();
                }
                mf.FileSaveABLines();
            }
            else
            {
                if (mf.isAutoSteerBtnOn) mf.btnAutoSteer.PerformClick();
                if (mf.yt.isYouTurnBtnOn) mf.btnAutoYouTurn.PerformClick();
            }
            UpdateLineList();
            lvLines.Focus();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            mf.btnABLine.Image = Properties.Resources.ABLineOff;
            mf.ABLine.isBtnABLineOn = false;
            mf.ABLine.numABLineSelected = 0;
            mf.DisableYouTurnButtons();
            if (mf.isAutoSteerBtnOn) mf.btnAutoSteer.PerformClick();
            if (mf.yt.isYouTurnBtnOn) mf.btnAutoYouTurn.PerformClick();
            Close();
            mf.gyd.isValid = false;
        }

        private void textBox2_Enter(object sender, EventArgs e)
        {
            if (mf.isKeyboardOn)
            {
                mf.KeyboardToText((TextBox)sender, this);
                btnSaveEditName.Focus();
            }
        }

        private void btnAddTimeEdit_Click(object sender, EventArgs e)
        {
            textBox2.Text += DateTime.Now.ToString(" hh:mm:ss", CultureInfo.InvariantCulture);
        }

        private void btnManual_Click(object sender, EventArgs e)
        {
            using (var form = new FormEnterAB(mf))
            {
                if (form.ShowDialog(this) == DialogResult.OK) 
                {
                    panelAPlus.Visible = false;
                    panelName.Visible = true;

                    textBox1.Text = "AB m " +
                        (Math.Round(glm.toDegrees(desHeading), 1)).ToString(CultureInfo.InvariantCulture) +
                        "\u00B0 " + mf.FindDirection(desHeading);

                    mf.ABLine.desPoint2.easting = mf.ABLine.desPoint1.easting + Math.Sin(desHeading);
                    mf.ABLine.desPoint2.northing = mf.ABLine.desPoint1.northing + Math.Cos(desHeading);
                }
                else
                    btnCancel_APlus.PerformClick();
            }
        }
    }
}
