using System;
using System.Globalization;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormABCurve : Form
    {
        //access to the main GPS form and all its variables
        private readonly FormGPS mf;

        public FormABCurve(Form _mf)
        {
            mf = _mf as FormGPS;
            InitializeComponent();

            //btnPausePlay.Text = gStr.gsPause;
            this.Text = gStr.gsABCurve;
        }

        private void FormABCurve_Load(object sender, EventArgs e)
        {
            panelPick.Top = 3;
            panelPick.Left = 3;
            panelAPlus.Top = 3;
            panelAPlus.Left = 3;
            panelName.Top = 3;
            panelName.Left = 3;

            panelEditName.Top = 3;
            panelEditName.Left = 3;

            panelEditName.Visible = false;

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
                if ((mf.gyd.refList[i].Mode == Mode.Curve || mf.gyd.refList[i].Mode == Mode.Boundary) && mf.gyd.refList[i].curvePts.Count > 1)
                {
                    lvLines.Items.Add(new ListViewItem(mf.gyd.refList[i].Name.Trim(), i));
                    if (select && mf.gyd.refList[i] == mf.gyd.selectedCurveLine) idx = lvLines.Items.Count - 1;
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
            panelName.Visible = false;

            btnAPoint.Enabled = true;
            btnBPoint.Enabled = false;
            btnPausePlay.Enabled = false;
            mf.gyd.desList.Clear();

            this.Size = new System.Drawing.Size(270, 360);
        }

        private void btnAPoint_Click(object sender, System.EventArgs e)
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

        private void btnBPoint_Click(object sender, System.EventArgs e)
        {
            mf.gyd.isOkToAddDesPoints = false;
            panelAPlus.Visible = false;
            panelName.Visible = true;

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

                panelAPlus.Visible = false;
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
                panelAPlus.Visible = false;

                textBox1.Enter -= textBox1_Enter;
                panelName.Visible = false;
                textBox1.Enter += textBox1_Enter;

                this.Size = new System.Drawing.Size(470, 360);

                UpdateLineList();
            }
        }
        private void btnAddTime_Click(object sender, EventArgs e)
        {
            textBox1.Text += DateTime.Now.ToString(" hh:mm:ss", CultureInfo.InvariantCulture);
        }


        private void btnPausePlay_Click(object sender, EventArgs e)
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


        private void btnCancelMain_Click(object sender, EventArgs e)
        {
            mf.gyd.isValid = false;
            mf.gyd.moveDistance = 0;
            mf.gyd.isOkToAddDesPoints = false;
            mf.DisableYouTurnButtons();
            //mf.btnContourPriority.Enabled = false;
            //mf.curve.ResetCurveLine();
            mf.gyd.isBtnCurveOn = false;
            mf.btnCurve.Image = Properties.Resources.CurveOff;
            if (mf.isAutoSteerBtnOn) mf.btnAutoSteer.PerformClick();
            if (mf.yt.isYouTurnBtnOn) mf.btnAutoYouTurn.PerformClick();

            mf.gyd.selectedCurveLine = null;
            Close();
        }

        private void btnCancelCurve_Click(object sender, EventArgs e)
        {
            mf.gyd.isOkToAddDesPoints = false;
            mf.gyd.desList.Clear();

            panelPick.Visible = true;
            panelAPlus.Visible = false;
            panelEditName.Visible = false;

            textBox1.Enter -= textBox1_Enter;
            panelName.Visible = false;
            textBox1.Enter += textBox1_Enter;

            this.Size = new System.Drawing.Size(470, 360);

            UpdateLineList();

        }

        private void textBox1_Enter(object sender, EventArgs e)
        {
            if (mf.isKeyboardOn)
            {
                mf.KeyboardToText((TextBox)sender, this);
                btnAdd.Focus();
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            if (mf.gyd.desList.Count > 0)
            {
                CGuidanceLine New = new CGuidanceLine(Mode.Curve);

                string text = textBox1.Text.Trim();
                if (text.Length == 0) text = "No Name " + DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);

                while (mf.gyd.refList.Exists(x => x.Name == text))//generate unique name!
                    text += " ";
                New.Name = text;

                //write out the Curve Points
                foreach (vec3 item in mf.gyd.desList)
                {
                    New.curvePts.Add(item);
                }
                mf.gyd.refList.Add(New);

                mf.FileSaveCurveLines();
                mf.gyd.desList.Clear();
            }

            panelPick.Visible = true;
            panelAPlus.Visible = false;

            textBox1.Enter -= textBox1_Enter;
            panelName.Visible = false;
            textBox1.Enter += textBox1_Enter;

            this.Size = new System.Drawing.Size(470, 360);

            UpdateLineList();
            lvLines.Focus();
            mf.gyd.desList.Clear();
        }

        private void btnListDelete_Click(object sender, EventArgs e)
        {
            mf.gyd.moveDistance = 0;

            if (lvLines.SelectedItems.Count > 0)
            {
                int idx = lvLines.Items[lvLines.SelectedIndices[0]].ImageIndex;
                if (idx > 0)
                {
                    //everything changed, so make sure its right
                    mf.gyd.numCurveLines--;

                    if (mf.gyd.selectedCurveLine == mf.gyd.refList[idx]) mf.gyd.selectedCurveLine = null;

                    lvLines.SelectedItems[0].Remove();
                    mf.gyd.refList.RemoveAt(idx);

                    //if there are no saved oned, empty out current curve line and turn off
                    if (mf.gyd.numCurveLines == 0)
                    {
                        mf.gyd.selectedCurveLine = null;
                        if (mf.isAutoSteerBtnOn) mf.btnAutoSteer.PerformClick();
                        if (mf.yt.isYouTurnBtnOn) mf.btnAutoYouTurn.PerformClick();
                    }

                    mf.FileSaveCurveLines();
                }
            }

            UpdateLineList();
            lvLines.Focus();
        }

        private void btnListUse_Click(object sender, EventArgs e)
        {
            //reset to generate new reference
            mf.gyd.isValid = false;
            mf.gyd.moveDistance = 0;

            if (lvLines.SelectedItems.Count > 0)
            {
                 mf.gyd.selectedCurveLine = mf.gyd.refList[lvLines.Items[lvLines.SelectedIndices[0]].ImageIndex];
                 mf.yt.ResetYouTurn();
            }
            else
            {
                mf.gyd.isOkToAddDesPoints = false;
                mf.DisableYouTurnButtons();
                mf.gyd.isBtnCurveOn = false;
                mf.btnCurve.Image = Properties.Resources.CurveOff;
                if (mf.isAutoSteerBtnOn) mf.btnAutoSteer.PerformClick();
                if (mf.yt.isYouTurnBtnOn) mf.btnAutoYouTurn.PerformClick();

                mf.gyd.selectedCurveLine = null;
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
                if (idx > 0)
                {
                    panelPick.Visible = false;
                    panelName.Visible = true;
                    this.Size = new System.Drawing.Size(270, 360);

                    panelAPlus.Visible = false;
                    panelName.Visible = true;

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
                if (idx > 0)
                {
                    textBox2.Text = mf.gyd.refList[idx].Name.Trim();

                    panelPick.Visible = false;
                    panelEditName.Visible = true;
                    this.Size = new System.Drawing.Size(270, 360);
                }
            }
        }

        private void btnAddTimeEdit_Click(object sender, EventArgs e)
        {
            textBox2.Text += DateTime.Now.ToString(" hh:mm:ss", CultureInfo.InvariantCulture);
        }

        private void btnSaveEditName_Click(object sender, EventArgs e)
        {
            if (textBox2.Text.Trim() == "") textBox2.Text = "No Name " + DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);

            int idx = lvLines.Items[lvLines.SelectedIndices[0]].ImageIndex;
            if (idx > 0)
            {
                string text = textBox2.Text.Trim();
                while (mf.gyd.refList.Exists(x => x != mf.gyd.refList[idx] && x.Name == text))//generate unique name!
                    text += " ";
                mf.gyd.refList[idx].Name = text;

                textBox2.Enter -= textBox2_Enter;
                panelEditName.Visible = false;
                textBox2.Enter += textBox2_Enter;

                panelPick.Visible = true;

                mf.FileSaveCurveLines();
                mf.gyd.desList.Clear();

                this.Size = new System.Drawing.Size(470, 360);
            }
            UpdateLineList();
            lvLines.Focus();
        }

        private void textBox2_Enter(object sender, EventArgs e)
        {
            if (mf.isKeyboardOn)
            {
                mf.KeyboardToText((TextBox)sender, this);
                btnSaveEditName.Focus();
            }
        }

        private void btnSwapAB_Click(object sender, EventArgs e)
        {
            if (lvLines.SelectedItems.Count > 0)
            {
                int idx = lvLines.Items[lvLines.SelectedIndices[0]].ImageIndex;
                if (idx > 0)
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
                    mf.FileSaveCurveLines();
                }
                UpdateLineList();
                lvLines.Focus();

                _ = new FormTimedMessage(1500, "A B Swapped", "Curve is Reversed");
            }
        }
    }
}