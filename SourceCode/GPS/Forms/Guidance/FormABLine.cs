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

            UpdateLineList(true);
        }

        private void UpdateLineList(bool select = false)
        {
            lvLines.Clear();
            int idx = -1;

            for (int i = 0; i < mf.gyd.refList.Count; i++)
            {
                if (mf.gyd.refList[i].Mode == Mode.AB && mf.gyd.refList[i].curvePts.Count > 1)
                {
                    lvLines.Items.Add(new ListViewItem(mf.gyd.refList[i].Name.Trim(), i));
                    if (select && mf.gyd.refList[i] == mf.gyd.selectedABLine) idx = lvLines.Items.Count - 1;
                }
            }

            if (idx > -1)
            {
                lvLines.Items[idx].EnsureVisible();
                lvLines.Items[idx].Selected = true;
            }
            // go to bottom of list - if there is a bottom
            else if (lvLines.Items.Count > 0)
            {
                lvLines.Items[lvLines.Items.Count - 1].EnsureVisible();
                lvLines.Items[lvLines.Items.Count - 1].Selected = true;
            }
            lvLines.Select();
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
            mf.gyd.isABLineBeingSet = false;
            btnBPoint.BackColor = System.Drawing.Color.Transparent;
        }

        private void btnAPoint_Click(object sender, EventArgs e)
        {
            vec3 fix = new vec3(mf.pivotAxlePos);

            mf.gyd.desPoint1.easting = fix.easting + Math.Cos(fix.heading) * mf.tool.toolOffset;
            mf.gyd.desPoint1.northing = fix.northing - Math.Sin(fix.heading) * mf.tool.toolOffset;
            desHeading = fix.heading;

            nudHeading.Enabled = true;
            nudHeading.Value = (decimal)glm.toDegrees(desHeading);

            BuildDesLine();

            btnBPoint.Enabled = true;
            btnAPoint.Enabled = false;

            btnEnter_APlus.Enabled = true;
            mf.gyd.isABLineBeingSet = true;
        }

        private void btnBPoint_Click(object sender, EventArgs e)
        {
            vec3 fix = new vec3(mf.pivotAxlePos);

            btnBPoint.BackColor = System.Drawing.Color.Teal;

            mf.gyd.desPoint2.easting = fix.easting + Math.Cos(fix.heading) * mf.tool.toolOffset;
            mf.gyd.desPoint2.northing = fix.northing - Math.Sin(fix.heading) * mf.tool.toolOffset;

            // heading based on AB points
            desHeading = Math.Atan2(fix.easting + Math.Cos(fix.heading) * mf.tool.toolOffset - mf.gyd.desPoint1.easting,
                fix.northing - Math.Sin(fix.heading) * mf.tool.toolOffset - mf.gyd.desPoint1.northing);
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

            mf.gyd.desPoint2.easting = mf.gyd.desPoint1.easting + Math.Sin(desHeading);
            mf.gyd.desPoint2.northing = mf.gyd.desPoint1.northing + Math.Cos(desHeading);
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

            textBox1.Text = ("AB " +
                Math.Round(glm.toDegrees(desHeading), 1).ToString(CultureInfo.InvariantCulture) +
                "\u00B0 " + mf.FindDirection(desHeading)).Trim();
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
                int idx = lvLines.Items[lvLines.SelectedIndices[0]].ImageIndex;
                if (idx > 0)
                {
                    textBox2.Text = mf.gyd.refList[idx].Name.Trim();

                    panelPick.Visible = false;
                    panelEditName.Visible = true;
                    this.Size = new System.Drawing.Size(270, 360);
                }
            }
        }

        private void btnSaveEditName_Click(object sender, EventArgs e)
        {
            int idx = lvLines.Items[lvLines.SelectedIndices[0]].ImageIndex;
            if (idx > 0)
            {
                textBox2.Enter -= textBox2_Enter;
                panelEditName.Visible = false;
                textBox2.Enter += textBox2_Enter;

                panelPick.Visible = true;
                string text = textBox2.Text.Trim();
                if (text.Trim() == "") text = "No Name " + DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);
                while (mf.gyd.refList.Exists(x => x != mf.gyd.refList[idx] && x.Name == text))//generate unique name!
                    text += " ";

                mf.gyd.refList[idx].Name = text;
            }
            mf.FileSaveABLines();

            this.Size = new System.Drawing.Size(470, 360);

            UpdateLineList();
            lvLines.Focus();
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            CGuidanceLine New = new CGuidanceLine(Mode.AB);

            New.curvePts.Add(new vec3(mf.gyd.desPoint1.easting, mf.gyd.desPoint1.northing, desHeading));
            New.curvePts.Add(new vec3(mf.gyd.desPoint1.easting + Math.Sin(desHeading), mf.gyd.desPoint1.northing + Math.Cos(desHeading), desHeading));

            //name
            string text = textBox1.Text.Trim();
            if (text.Trim() == "") text = "No Name " + DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);
            while (mf.gyd.refList.Exists(x => x.Name == text))//generate unique name!
                text += " ";

            New.Name = text;

            mf.gyd.refList.Add(New);
            mf.gyd.numABLines++;
            mf.gyd.selectedABLine = New;

            mf.FileSaveABLines();

            panelPick.Visible = true;
            panelAPlus.Visible = false;

            textBox1.Enter -= textBox1_Enter;
            panelName.Visible = false;
            textBox1.Enter += textBox1_Enter;

            this.Size = new System.Drawing.Size(470, 360);

            UpdateLineList();
            lvLines.Focus();
            mf.gyd.isABLineBeingSet = false;
        }

        private void btnDuplicate_Click(object sender, EventArgs e)
        {
            if (lvLines.SelectedItems.Count > 0)
            {
                int idx = lvLines.Items[lvLines.SelectedIndices[0]].ImageIndex;
                if (idx > 0)
                {
                    panelPick.Visible = false;
                    panelName.Visible = true;
                    this.Size = new System.Drawing.Size(270, 360);

                    panelAPlus.Visible = false;
                    panelName.Visible = true;

                    if (mf.gyd.refList[idx].curvePts.Count > 1)
                    {
                        desHeading = Math.Atan2(mf.gyd.refList[idx].curvePts[1].easting - mf.gyd.refList[idx].curvePts[0].easting, mf.gyd.refList[idx].curvePts[1].northing - mf.gyd.refList[idx].curvePts[0].northing);

                        //calculate the new points for the reference line and points                
                        mf.gyd.desPoint1.easting = mf.gyd.refList[idx].curvePts[0].easting;
                        mf.gyd.desPoint1.northing = mf.gyd.refList[idx].curvePts[0].northing;
                    }

                    textBox1.Text = mf.gyd.refList[idx].Name.Trim() + " Copy";
                }
            }
        }


        private void btnListUse_Click(object sender, EventArgs e)
        {
            mf.gyd.moveDistance = 0;
            //reset to generate new reference
            mf.gyd.isValid = false;

            if (lvLines.SelectedItems.Count > 0)
            {
                mf.gyd.selectedABLine = mf.gyd.refList[lvLines.Items[lvLines.SelectedIndices[0]].ImageIndex];

                mf.EnableYouTurnButtons();

                //Go back with Line enabled
                Close();
            }

            //no item selected
            else
            {
                mf.btnABLine.Image = Properties.Resources.ABLineOff;
                mf.gyd.isBtnABLineOn = false;
                mf.gyd.selectedABLine = null;
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
                int idx = lvLines.Items[lvLines.SelectedIndices[0]].ImageIndex;
                if (idx > 0 && mf.gyd.refList[idx].curvePts.Count > 1)
                {
                    double heading = Math.Atan2(mf.gyd.refList[idx].curvePts[1].easting - mf.gyd.refList[idx].curvePts[0].easting, mf.gyd.refList[idx].curvePts[1].northing - mf.gyd.refList[idx].curvePts[0].northing) + Math.PI;

                    if (heading > glm.twoPI) heading -= glm.twoPI;

                    vec3 pos = mf.gyd.refList[idx].curvePts[0];

                    mf.gyd.refList[idx].curvePts.Clear();

                    mf.gyd.refList[idx].curvePts.Add(new vec3(pos.easting, pos.northing, heading));
                    mf.gyd.refList[idx].curvePts.Add(new vec3(pos.easting + Math.Sin(heading), pos.northing + Math.Cos(heading), heading));

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
                int idx = lvLines.Items[lvLines.SelectedIndices[0]].ImageIndex;
                if (idx > 0)
                {
                    mf.gyd.numABLines--;
                    if (mf.gyd.selectedABLine == mf.gyd.refList[idx]) mf.gyd.selectedABLine = null;

                    mf.gyd.refList.RemoveAt(idx);
                    lvLines.SelectedItems[0].Remove();

                    if (mf.gyd.numABLines == 0)
                    {
                        mf.gyd.DeleteAB();
                        if (mf.isAutoSteerBtnOn) mf.btnAutoSteer.PerformClick();
                        if (mf.yt.isYouTurnBtnOn) mf.btnAutoYouTurn.PerformClick();
                    }
                    mf.FileSaveABLines();
                }
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
            mf.gyd.isBtnABLineOn = false;
            mf.gyd.selectedABLine = null;
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

                    mf.gyd.desPoint2.easting = mf.gyd.desPoint1.easting + Math.Sin(desHeading);
                    mf.gyd.desPoint2.northing = mf.gyd.desPoint1.northing + Math.Cos(desHeading);
                }
                else
                    btnCancel_APlus.PerformClick();
            }
        }

        private void FormABLine_FormClosing(object sender, FormClosingEventArgs e)
        {
            mf.gyd.isABLineBeingSet = false;
        }
    }
}
