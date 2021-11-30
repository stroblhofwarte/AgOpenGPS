using System;
using System.Globalization;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormABCurve : Form
    {
        //access to the main GPS form and all its variables
        private readonly FormGPS mf;
        private bool editName = false;
        private Mode mode;
        private double desHeading = 0;
        private bool isClosing;

        public FormABCurve(Form _mf, Mode mode2)
        {
            mf = _mf as FormGPS;
            InitializeComponent();

            //btnPausePlay.Text = gStr.gsPause;
            this.Text = gStr.gsABCurve;
            mode = mode2;
            btnListDelete.Image = mode.HasFlag(Mode.AB) ? Properties.Resources.ABLineDelete : Properties.Resources.HideContour;
        }

        private void FormABCurve_Load(object sender, EventArgs e)
        {
            panelPick.Top = 3;
            panelPick.Left = 3;
            panelAPlus.Top = 3;
            panelAPlus.Left = 3;
            panelName.Top = 3;
            panelName.Left = 3;

            panelPick.Visible = true;
            panelAPlus.Visible = false;
            panelName.Visible = false;

            this.Size = new System.Drawing.Size(470, 360);

            mf.gyd.isOkToAddDesPoints = false;

            UpdateLineList(true);
        }

        private void UpdateLineList(bool select = false)
        {
            lvLines.Clear();
            int idx = -1;

            for (int i = 0; i < mf.gyd.refList.Count; i++)
            {
                if (mf.gyd.refList[i].Mode.HasFlag(mode) && mf.gyd.refList[i].curvePts.Count > 1)
                {
                    lvLines.Items.Add(new ListViewItem(mf.gyd.refList[i].Name.Trim(), i));
                    if (select && mf.gyd.refList[i] == mf.gyd.selectedLine) idx = lvLines.Items.Count - 1;
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
        //for calculating for display the averaged new line

        private void btnNewCurve_Click(object sender, EventArgs e)
        {
            lvLines.SelectedItems.Clear();
            panelPick.Visible = false;
            panelAPlus.Visible = true;

            if (mode.HasFlag(Mode.AB))
            {
                btnPausePlay.Enabled = false;
                nudHeading.Visible = true;
                btnManual.Visible = true;
                btnPausePlay.Image = Properties.Resources.OK64;
                label2.Visible = false;
                lblCurveExists.Visible = false;
            }
            else
            {
                nudHeading.Visible = false;
                btnManual.Visible = false;
                btnPausePlay.Image = Properties.Resources.boundaryPause;
                label2.Visible = true;
                lblCurveExists.Visible = true;
                lblCurveExists.Text = "> OFF <";
            }

            panelName.Visible = false;

            btnAPoint.Enabled = true;
            btnBPoint.Enabled = false;
            btnPausePlay.Enabled = false;
            mf.gyd.desList.Clear();

            this.Size = new System.Drawing.Size(270, 360);
        }

        private void btnAPoint_Click(object sender, System.EventArgs e)
        {
            if (mode.HasFlag(Mode.AB))
            {
                vec3 fix = new vec3(mf.pivotAxlePos);
                mf.gyd.desList.Add(new vec3(fix.easting + Math.Cos(fix.heading) * mf.tool.toolOffset, fix.northing - Math.Sin(fix.heading) * mf.tool.toolOffset, fix.heading));
                mf.gyd.desList.Add(new vec3(mf.gyd.desList[0].easting + Math.Sin(fix.heading), mf.gyd.desList[0].northing + Math.Cos(fix.heading), fix.heading));

                desHeading = fix.heading;

                nudHeading.Enabled = true;
                nudHeading.Value = (decimal)glm.toDegrees(desHeading);

                btnBPoint.Enabled = true;
                btnAPoint.Enabled = false;

                btnPausePlay.Enabled = true;
            }
            else
            {
                //mf.curve.moveDistance = 0;
                //clear out the reference list
                lblCurveExists.Text = gStr.gsDriving;
                btnBPoint.Enabled = true;
                //mf.curve.ResetCurveLine();

                btnAPoint.Enabled = false;
                mf.gyd.isOkToAddDesPoints = true;
                btnPausePlay.Enabled = true;
                btnPausePlay.Visible = true;
            }
        }

        private void btnBPoint_Click(object sender, System.EventArgs e)
        {
            if (mode.HasFlag(Mode.AB))
            {
                vec3 fix = new vec3(mf.pivotAxlePos);

                btnBPoint.BackColor = System.Drawing.Color.Teal;

                vec3 point = new vec3(fix.easting + Math.Cos(fix.heading) * mf.tool.toolOffset, fix.northing - Math.Sin(fix.heading) * mf.tool.toolOffset, 0);

                // heading based on AB points
                desHeading = Math.Atan2(point.easting - mf.gyd.desList[0].easting, point.northing - mf.gyd.desList[0].northing);
                if (desHeading < 0) desHeading += glm.twoPI;

                point.heading = desHeading;
                mf.gyd.desList[1] = point;

                nudHeading.Value = (decimal)(glm.toDegrees(desHeading));
            }
            else
            {
                mf.gyd.isOkToAddDesPoints = false;
                panelAPlus.Visible = false;

                int cnt = mf.gyd.desList.Count;
                if (cnt > 3)
                {
                    //make sure distance isn't too big between points on Turn
                    for (int i = 0; i < cnt - 1; i++)
                    {
                        int j = i + 1;
                        //if (j == cnt) j = 0;
                        double distance = glm.Distance(mf.gyd.desList[i], mf.gyd.desList[j]);
                        if (distance > 1.2)
                        {
                            vec3 pointB = new vec3((mf.gyd.desList[i].easting + mf.gyd.desList[j].easting) / 2.0,
                                (mf.gyd.desList[i].northing + mf.gyd.desList[j].northing) / 2.0,
                                mf.gyd.desList[i].heading);

                            mf.gyd.desList.Insert(j, pointB);
                            cnt = mf.gyd.desList.Count;
                            i = -1;
                        }
                    }

                    //calculate average heading of line
                    double x = 0, y = 0;
                    foreach (vec3 pt in mf.gyd.desList)
                    {
                        x += Math.Cos(pt.heading);
                        y += Math.Sin(pt.heading);
                    }
                    x /= mf.gyd.desList.Count;
                    y /= mf.gyd.desList.Count;
                    double aveLineHeading = Math.Atan2(y, x);
                    if (aveLineHeading < 0) aveLineHeading += glm.twoPI;

                    //build the tail extensions
                    AddFirstLastPoints();
                    SmoothAB(4);
                    CalculateTurnHeadings();

                    panelName.Visible = true;

                    textBox1.Text = "Cu " +
                        (Math.Round(glm.toDegrees(aveLineHeading), 1)).ToString(CultureInfo.InvariantCulture) +
                        "\u00B0 " + mf.FindDirection(aveLineHeading);
                }
                else
                {
                    mf.gyd.isOkToAddDesPoints = false;
                    mf.gyd.desList.Clear();

                    panelPick.Visible = true;
                    panelName.Visible = false;

                    this.Size = new System.Drawing.Size(470, 360);

                    UpdateLineList();
                }
            }
        }
        private void btnAddTime_Click(object sender, EventArgs e)
        {
            textBox1.Text += DateTime.Now.ToString(" hh:mm:ss", CultureInfo.InvariantCulture);
        }


        private void btnPausePlay_Click(object sender, EventArgs e)
        {
            if (mode.HasFlag(Mode.AB))
            {
                panelAPlus.Visible = false;
                panelName.Visible = true;

                textBox1.Text = ("AB " +
                    Math.Round(glm.toDegrees(desHeading), 1).ToString(CultureInfo.InvariantCulture) +
                    "\u00B0 " + mf.FindDirection(desHeading)).Trim();
            }
            else
            {
                if (mf.gyd.isOkToAddDesPoints)
                {
                    mf.gyd.isOkToAddDesPoints = false;
                    btnPausePlay.Image = Properties.Resources.BoundaryRecord;
                    //btnPausePlay.Text = gStr.gsRecord;
                    btnBPoint.Enabled = false;
                }
                else
                {
                    mf.gyd.isOkToAddDesPoints = true;
                    btnPausePlay.Image = Properties.Resources.boundaryPause;
                    //btnPausePlay.Text = gStr.gsPause;
                    btnBPoint.Enabled = true;
                }
            }
        }

        private void btnCancelMain_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnCancelCurve_Click(object sender, EventArgs e)
        {
            btnBPoint.BackColor = System.Drawing.Color.Transparent;
            editName = false;
            mf.gyd.isOkToAddDesPoints = false;
            mf.gyd.desList.Clear();

            panelPick.Visible = true;
            panelAPlus.Visible = false;
            panelName.Visible = false;

            this.Size = new System.Drawing.Size(470, 360);

            UpdateLineList();

        }

        private void textBox1_Click(object sender, EventArgs e)
        {
            if (mf.isKeyboardOn)
            {
                mf.KeyboardToText((TextBox)sender, this);
                btnAdd.Focus();
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            if (editName)
            {
                int idx = lvLines.Items[lvLines.SelectedIndices[0]].ImageIndex;
                if (idx > -1)
                {
                    string text = textBox1.Text.Trim();
                    if (text == "") text = "No Name " + DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);
                    while (mf.gyd.refList.Exists(x => x != mf.gyd.refList[idx] && x.Name == text))//generate unique name!
                        text += " ";
                    mf.gyd.refList[idx].Name = text;

                    mf.FileSaveCurveLines();
                }
            }
            else
            {
                if (mf.gyd.desList.Count > 1)
                {
                    CGuidanceLine New = new CGuidanceLine(mode);

                    //name
                    string text = textBox1.Text.Trim();
                    if (text.Trim() == "") text = "No Name " + DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);
                    while (mf.gyd.refList.Exists(x => x.Name == text))//generate unique name!
                        text += " ";

                    New.Name = text;

                    foreach (vec3 item in mf.gyd.desList)
                    {
                        New.curvePts.Add(item);
                    }

                    mf.gyd.refList.Add(New);
                    mf.gyd.selectedLine = new CGuidanceLine(New);

                    if (mode.HasFlag(Mode.AB))
                        mf.FileSaveABLines();
                    else
                        mf.FileSaveCurveLines();

                    panelPick.Visible = true;
                    panelAPlus.Visible = false;
                    panelName.Visible = false;

                    this.Size = new System.Drawing.Size(470, 360);

                    UpdateLineList();
                    lvLines.Focus();
                }
                mf.gyd.desList.Clear();
            }
        }

        private void btnListDelete_Click(object sender, EventArgs e)
        {
            mf.gyd.moveDistance = 0;

            if (lvLines.SelectedItems.Count > 0)
            {
                int idx = lvLines.Items[lvLines.SelectedIndices[0]].ImageIndex;
                if (idx > -1)
                {
                    if (mf.gyd.selectedLine?.Name == mf.gyd.refList[idx].Name)
                    {
                        mf.gyd.selectedLine = null;
                        mf.enableAutoSteerButton(false);
                        mf.enableYouTurnButton(false);
                    }

                    lvLines.SelectedItems[0].Remove();
                    mf.gyd.refList.RemoveAt(idx);

                    if (mode.HasFlag(Mode.AB))
                        mf.FileSaveABLines();
                    else
                        mf.FileSaveCurveLines();
                }
            }

            UpdateLineList();
            lvLines.Focus();
        }

        private void btnListUse_Click(object sender, EventArgs e)
        {
            isClosing = true;
            //reset to generate new reference
            mf.gyd.isValid = false;
            mf.gyd.moveDistance = 0;

            if (lvLines.SelectedItems.Count > 0)
            {
                if (mf.gyd.refList[lvLines.Items[lvLines.SelectedIndices[0]].ImageIndex].Name != mf.gyd.selectedLine?.Name)
                {
                    mf.gyd.selectedLine = new CGuidanceLine(mf.gyd.refList[lvLines.Items[lvLines.SelectedIndices[0]].ImageIndex]);
                    mf.setYouTurnButtonStatus(true);
                }
            }
            else
            {
                mf.gyd.isOkToAddDesPoints = false;
                mf.enableAutoSteerButton(false);
                mf.enableABLineButton(false);
                mf.enableCurveButton(false);
                mf.setYouTurnButtonStatus(false);

                mf.gyd.selectedLine = null;
            }
            Close();
        }

        public void SmoothAB(int smPts)
        {
            //count the reference list of original curve
            int cnt = mf.gyd.desList.Count;

            //the temp array
            vec3[] arr = new vec3[cnt];

            //read the points before and after the setpoint
            for (int s = 0; s < smPts / 2; s++)
            {
                arr[s].easting = mf.gyd.desList[s].easting;
                arr[s].northing = mf.gyd.desList[s].northing;
                arr[s].heading = mf.gyd.desList[s].heading;
            }

            for (int s = cnt - (smPts / 2); s < cnt; s++)
            {
                arr[s].easting = mf.gyd.desList[s].easting;
                arr[s].northing = mf.gyd.desList[s].northing;
                arr[s].heading = mf.gyd.desList[s].heading;
            }

            //average them - center weighted average
            for (int i = smPts / 2; i < cnt - (smPts / 2); i++)
            {
                for (int j = -smPts / 2; j < smPts / 2; j++)
                {
                    arr[i].easting += mf.gyd.desList[j + i].easting;
                    arr[i].northing += mf.gyd.desList[j + i].northing;
                }
                arr[i].easting /= smPts;
                arr[i].northing /= smPts;
                arr[i].heading = mf.gyd.desList[i].heading;
            }

            //make a list to draw
            mf.gyd.desList.Clear();
            for (int i = 0; i < cnt; i++)
            {
                mf.gyd.desList.Add(arr[i]);
            }
        }

        public void AddFirstLastPoints()
        {
            int ptCnt = mf.gyd.desList.Count - 1;
            for (int i = 1; i < 200; i++)
            {
                vec3 pt = new vec3(mf.gyd.desList[ptCnt]);
                pt.easting += (Math.Sin(pt.heading) * i);
                pt.northing += (Math.Cos(pt.heading) * i);
                mf.gyd.desList.Add(pt);
            }

            //and the beginning
            vec3 start = new vec3(mf.gyd.desList[0]);
            for (int i = 1; i < 200; i++)
            {
                vec3 pt = new vec3(start);
                pt.easting -= (Math.Sin(pt.heading) * i);
                pt.northing -= (Math.Cos(pt.heading) * i);
                mf.gyd.desList.Insert(0, pt);
            }
        }

        public void CalculateTurnHeadings()
        {
            //to calc heading based on next and previous points to give an average heading.
            int cnt = mf.gyd.desList.Count;
            if (cnt > 0)
            {
                vec3[] arr = new vec3[cnt];
                cnt--;
                mf.gyd.desList.CopyTo(arr);
                mf.gyd.desList.Clear();

                //middle points
                for (int i = 1; i < cnt; i++)
                {
                    vec3 pt3 = arr[i];
                    pt3.heading = Math.Atan2(arr[i + 1].easting - arr[i - 1].easting, arr[i + 1].northing - arr[i - 1].northing);
                    if (pt3.heading < 0) pt3.heading += glm.twoPI;
                    mf.gyd.desList.Add(pt3);
                }
            }
        }

        private void btnDuplicate_Click(object sender, EventArgs e)
        {
            if (lvLines.SelectedItems.Count > 0)
            {
                int idx = lvLines.Items[lvLines.SelectedIndices[0]].ImageIndex;
                if (idx > -1)
                {
                    panelAPlus.Visible = false;
                    panelName.Visible = true;
                    panelPick.Visible = false;
                    this.Size = new System.Drawing.Size(270, 360);

                    textBox1.Text = mf.gyd.refList[idx].Name.Trim() + " Copy";

                    mf.gyd.desList.Clear();

                    for (int i = 0; i < mf.gyd.refList[idx].curvePts.Count; i++)
                    {
                        vec3 pt = new vec3(mf.gyd.refList[idx].curvePts[i]);
                        mf.gyd.desList.Add(pt);
                    }
                }
            }
        }

        private void btnEditName_Click(object sender, EventArgs e)
        {
            if (lvLines.SelectedItems.Count > 0)
            {
                int idx = lvLines.Items[lvLines.SelectedIndices[0]].ImageIndex;
                if (idx > -1)
                {
                    editName = true;
                    textBox1.Text = mf.gyd.refList[idx].Name.Trim();

                    panelPick.Visible = false;
                    panelName.Visible = true;
                    this.Size = new System.Drawing.Size(270, 360);
                }
            }
        }

        private void btnSwapAB_Click(object sender, EventArgs e)
        {
            if (mode.HasFlag(Mode.AB) && lvLines.SelectedItems.Count > 1)
            {
                int idx = lvLines.Items[lvLines.SelectedIndices[0]].ImageIndex;
                if (idx > -1 && mf.gyd.refList[idx].curvePts.Count > 1)
                {
                    double heading = Math.Atan2(mf.gyd.refList[idx].curvePts[1].easting - mf.gyd.refList[idx].curvePts[0].easting, mf.gyd.refList[idx].curvePts[1].northing - mf.gyd.refList[idx].curvePts[0].northing) + Math.PI;

                    if (heading > glm.twoPI) heading -= glm.twoPI;

                    mf.gyd.refList[idx].curvePts[1] = new vec3(mf.gyd.refList[idx].curvePts[0].easting + Math.Sin(heading), mf.gyd.refList[idx].curvePts[0].northing + Math.Cos(heading), heading);

                    mf.gyd.isValid = false;
                    mf.FileSaveABLines();
                }
            }
            else if (mode != Mode.AB && lvLines.SelectedItems.Count > 1)
            {
                int idx = lvLines.Items[lvLines.SelectedIndices[0]].ImageIndex;
                if (idx > -1)
                {
                    int cnt = mf.gyd.refList[idx].curvePts.Count;
                    if (cnt > 0)
                    {
                        mf.gyd.refList[idx].curvePts.Reverse();

                        vec3[] arr = new vec3[cnt];
                        cnt--;
                        mf.gyd.refList[idx].curvePts.CopyTo(arr);
                        mf.gyd.refList[idx].curvePts.Clear();

                        for (int i = 1; i < cnt; i++)
                        {
                            vec3 pt3 = arr[i];
                            pt3.heading += Math.PI;
                            if (pt3.heading > glm.twoPI) pt3.heading -= glm.twoPI;
                            if (pt3.heading < 0) pt3.heading += glm.twoPI;
                            mf.gyd.refList[idx].curvePts.Add(pt3);
                        }
                    }
                    mf.gyd.isValid = false;
                    mf.FileSaveCurveLines();
                }
            }
            UpdateLineList();
            lvLines.Focus();
        }

        private void nudHeading_Click(object sender, EventArgs e)
        {
            if (mf.KeypadToNUD((NumericUpDown)sender, this))
            {
                desHeading = glm.toRadians((double)nudHeading.Value);

                if (mf.gyd.desList.Count > 1)
                    mf.gyd.desList[1] = new vec3(mf.gyd.desList[0].easting + Math.Sin(desHeading), mf.gyd.desList[0].northing + Math.Cos(desHeading), desHeading);
            }
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

                    if (mf.gyd.desList.Count > 1)
                        mf.gyd.desList[1] = new vec3(mf.gyd.desList[0].easting + Math.Sin(desHeading), mf.gyd.desList[0].northing + Math.Cos(desHeading), desHeading);
                }
                else
                    btnCancelCurve.PerformClick();
            }
        }

        private void FormSelect_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!isClosing)
            {
                mf.gyd.moveDistance = 0;
                mf.gyd.isValid = false;

                mf.gyd.isOkToAddDesPoints = false;

                mf.enableABLineButton(false);
                mf.enableAutoSteerButton(false);
                mf.enableCurveButton(false);
                mf.setYouTurnButtonStatus(false);

                mf.gyd.selectedLine = null;
            }
        }

        #region Help

        private void btnListDelete_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.ha_btnListDelete, gStr.gsHelp);
        }

        private void btnCancelMain_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.ha_btnCancel, gStr.gsHelp);
        }

        private void btnNewCurve_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.ha_btnNewABLine, gStr.gsHelp);
        }

        private void btnListUse_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.ha_btnListUse, gStr.gsHelp);
        }

        private void btnSwapAB_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.ht_btnSwapAB, gStr.gsHelp);
        }

        private void btnEditName_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.hd_tboxNameLine, gStr.gsHelp);
        }

        private void btnDuplicate_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.ha_btnDuplicate, gStr.gsHelp);
        }

        private void btnAddTime_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.ha_btnAddTime, gStr.gsHelp);
        }

        private void btnCancel_Name_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.ha_btnCancelCreate, gStr.gsHelp);
        }

        private void btnCancelCurve_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.ha_btnCancelCreate, gStr.gsHelp);
        }

        private void btnAdd_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.ha_btnEnterContinue, gStr.gsHelp);
        }

        private void btnAPoint_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.hcur_btnAPoint, gStr.gsHelp);
        }

        private void btnBPoint_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.hcur_btnBPoint, gStr.gsHelp);
        }

        private void btnPausePlay_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.hcur_btnPausePlay, gStr.gsHelp);
        }

        private void textBox1_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.ha_textBox1, gStr.gsHelp);
        }
        #endregion 
    }
}