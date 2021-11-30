using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormABDraw : Form
    {
        //access to the main GPS form and all its variables
        private readonly FormGPS mf = null;

        private bool isA = true;
        private int start = -1, end = -1;

        private bool isDrawSections = false;
        private CGuidanceLine selectedLine;

        public FormABDraw(Form callingForm)
        {
            //get copy of the calling main form
            mf = callingForm as FormGPS;

            mf.CalculateMinMax();

            InitializeComponent();
            //lblPick.Text = gStr.gsSelectALine;
            //label5.Text = gStr.gsToolWidth;
            //this.Text = gStr.gsClick2Pointsontheboundary;

            lblCmInch.Text = mf.unitsInCm;

            nudDistance.Controls[0].Enabled = false;

            if (!mf.isMetric)
            {
                nudDistance.Maximum = (int)(nudDistance.Maximum / 2.54M);
                nudDistance.Minimum = (int)(nudDistance.Minimum / 2.54M);
            }
        }

        private void FormABDraw_Load(object sender, EventArgs e)
        {
            nudDistance.Value = (decimal)Math.Round(((mf.tool.toolWidth * mf.m2InchOrCm) * 0.5), 0); // 
            label6.Text = Math.Round((mf.tool.toolWidth * mf.m2InchOrCm), 0).ToString();
            FixLabels();

            if (isDrawSections) btnDrawSections.Image = Properties.Resources.MappingOn;
            else btnDrawSections.Image = Properties.Resources.MappingOff;
        }

        private void FixLabels()
        {
            int totalCurves = 0;
            int totalAB = 0;

            bool counting = selectedLine?.Mode.HasFlag(Mode.Curve) == true;
            bool counting2 = selectedLine?.Mode.HasFlag(Mode.AB) == true;
            int count = 0;
            int count2 = 0;
            for (int i = 0; i < mf.gyd.refList.Count; i++)
            {
                if (mf.gyd.refList[i].Mode.HasFlag(Mode.Curve))
                {
                    if (counting)
                        count++;
                    totalCurves++;
                    if (mf.gyd.refList[i] == selectedLine) counting = false;
                }

                if (mf.gyd.refList[i].Mode.HasFlag(Mode.AB))
                {
                    if (counting2)
                        count2++;
                    totalAB++;
                    if (mf.gyd.refList[i] == selectedLine) counting2 = false;
                }
            }

            if (count > 0)
            {
                tboxNameCurve.Text = selectedLine.Name.Trim();
                tboxNameCurve.Enabled = true;
                lblCurveSelected.Text = count.ToString();
                btnDelete.Enabled = true;
            }
            else
            {
                tboxNameCurve.Text = "***";
                tboxNameCurve.Enabled = false;
                lblCurveSelected.Text = "0";
                btnDelete.Enabled = false;
            }
            lblNumCu.Text = totalCurves.ToString();

            if (count2 > 0)
            {
                tboxNameLine.Text = selectedLine.Name.Trim();
                tboxNameLine.Enabled = true;
                lblABSelected.Text = count2.ToString();
                btnDelete2.Enabled = true;
            }
            else
            {
                tboxNameLine.Text = "***";
                tboxNameLine.Enabled = false;
                lblABSelected.Text = "0";
                btnDelete2.Enabled = false;
            }

            lblNumAB.Text = totalAB.ToString();
        }

        private void btnSelectCurve_Click(object sender, EventArgs e)
        {
            bool found = !(selectedLine?.Mode.HasFlag(Mode.Curve) == true);
            bool loop = true;
            for (int i = 0; i < mf.gyd.refList.Count || loop; i++)
            {
                if (i >= mf.gyd.refList.Count)
                {
                    loop = false;
                    i = -1;
                    if (!found) break;
                    else continue;
                }
                if (mf.gyd.refList[i] == selectedLine)
                    found = true;
                else if (found && mf.gyd.refList[i].Mode.HasFlag(Mode.Curve))
                {
                    selectedLine = mf.gyd.refList[i];
                    break;
                }
            }
            if (!found)
                selectedLine = null;

            FixLabels();
        }

        private void btnSelectABLine_Click(object sender, EventArgs e)
        {
            bool found = !(selectedLine?.Mode.HasFlag(Mode.AB) == true);
            bool loop = true;
            for (int i = 0; i < mf.gyd.refList.Count || loop; i++)
            {
                if (i >= mf.gyd.refList.Count)
                {
                    loop = false;
                    i = -1;
                    if (!found) break;
                    else continue;
                }
                if (mf.gyd.refList[i] == selectedLine)
                    found = true;
                else if (found && mf.gyd.refList[i].Mode.HasFlag(Mode.AB))
                {
                    selectedLine = mf.gyd.refList[i];
                    break;
                }
            }
            if (!found)
                selectedLine = null;

            FixLabels();
        }

        private void btnCancelTouch_Click(object sender, EventArgs e)
        {
            btnMakeABLine.Enabled = false;
            btnMakeCurve.Enabled = false;

            isA = true;
            start = end = -1;

            btnCancelTouch.Enabled = false;
            btnExit.Focus();
        }

        private void nudDistance_Click(object sender, EventArgs e)
        {
            mf.KeypadToNUD((NumericUpDown)sender, this);
            btnSelectABLine.Focus();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (selectedLine != null)
            {
                if (mf.gyd.selectedLine?.Name == selectedLine.Name)
                {
                    mf.gyd.selectedLine = null;
                    mf.enableABLineButton(false);
                    mf.enableAutoSteerButton(false);
                    mf.enableCurveButton(false);
                    mf.enableYouTurnButton(false);
                }
                mf.gyd.refList.Remove(selectedLine);

                if (selectedLine.Mode.HasFlag(Mode.AB))
                    mf.FileSaveABLines();
                else
                    mf.FileSaveCurveLines();

                selectedLine = null;
            }

            FixLabels();
        }

        private void btnDrawSections_Click(object sender, EventArgs e)
        {
            isDrawSections = !isDrawSections;
            if (isDrawSections) btnDrawSections.Image = Properties.Resources.MappingOn;
            else btnDrawSections.Image = Properties.Resources.MappingOff;
        }

        private void btnFlipOffset_Click(object sender, EventArgs e)
        {
            nudDistance.Value *= -1;
        }

        private void oglSelf_MouseDown(object sender, MouseEventArgs e)
        {
            btnCancelTouch.Enabled = true;
            btnMakeABLine.Enabled = false;
            btnMakeCurve.Enabled = false;

            Point pt = oglSelf.PointToClient(Cursor.Position);

            //convert screen coordinates to field coordinates
            vec2 plotPt = new vec2((pt.X - 350)/350.0 * mf.minmax, (350 - pt.Y)/350.0 * mf.minmax);

            plotPt.easting += mf.fieldCenterX;
            plotPt.northing += mf.fieldCenterY;

            int ptCount = mf.bnd.bndList[0].fenceLine.Points.Count;
            if (ptCount > 0)
            {
                double minDistA = double.MaxValue;
                int A = 0;
                //find the closest 2 points to current fix
                for (int t = 0; t < ptCount; t++)
                {
                    double dist = ((plotPt.easting - mf.bnd.bndList[0].fenceLine.Points[t].easting) * (plotPt.easting - mf.bnd.bndList[0].fenceLine.Points[t].easting))
                                    + ((plotPt.northing - mf.bnd.bndList[0].fenceLine.Points[t].northing) * (plotPt.northing - mf.bnd.bndList[0].fenceLine.Points[t].northing));
                    if (dist < minDistA)
                    {
                        minDistA = dist;
                        A = t;
                    }
                }

                if (isA)
                {
                    start = A;
                    isA = false;
                    end = -1;
                }
                else
                {
                    isA = true;
                    end = A;
                    btnMakeABLine.Enabled = true;
                    btnMakeCurve.Enabled = true;
                }
            }
        }

        private void btnMakeBoundaryCurve_Click(object sender, EventArgs e)
        {
            CGuidanceLine New = new CGuidanceLine(Mode.Curve | Mode.Boundary);

            //count the points from the boundary
            int ptCount = mf.bnd.bndList[0].fenceLine.Points.Count;

            //outside point
            vec3 pt3 = new vec3();

            double moveDist = (double)nudDistance.Value * mf.inchOrCm2m;
            double distSq = (moveDist) * (moveDist) * 0.999;

            //make the boundary tram outer array
            for (int i = 0; i < ptCount; i++)
            {
                //calculate the point inside the boundary
                pt3.easting = mf.bnd.bndList[0].fenceLine.Points[i].easting -
                    (Math.Sin(glm.PIBy2 + mf.bnd.bndList[0].fenceLine.Points[i].heading) * (moveDist));

                pt3.northing = mf.bnd.bndList[0].fenceLine.Points[i].northing -
                    (Math.Cos(glm.PIBy2 + mf.bnd.bndList[0].fenceLine.Points[i].heading) * (moveDist));

                pt3.heading = mf.bnd.bndList[0].fenceLine.Points[i].heading;

                bool Add = true;

                for (int j = 0; j < ptCount; j++)
                {
                    double check = glm.DistanceSquared(pt3.northing, pt3.easting,
                                        mf.bnd.bndList[0].fenceLine.Points[j].northing, mf.bnd.bndList[0].fenceLine.Points[j].easting);
                    if (check < distSq)
                    {
                        Add = false;
                        break;
                    }
                }

                if (Add)
                {
                    if (New.curvePts.Count > 0)
                    {
                        double dist = ((pt3.easting - New.curvePts[New.curvePts.Count - 1].easting) * (pt3.easting - New.curvePts[New.curvePts.Count - 1].easting))
                            + ((pt3.northing - New.curvePts[New.curvePts.Count - 1].northing) * (pt3.northing - New.curvePts[New.curvePts.Count - 1].northing));
                        if (dist > 1)
                            New.curvePts.Add(pt3);
                    }
                    else New.curvePts.Add(pt3);
                }
            }

            btnCancelTouch.Enabled = false;

            int cnt = New.curvePts.Count;
            if (cnt > 3)
            {
                //make sure distance isn't too big between points on Turn
                for (int i = 0; i < cnt - 1; i++)
                {
                    int j = i + 1;
                    double distance = glm.Distance(New.curvePts[i], New.curvePts[j]);
                    if (distance > 1.2)
                    {
                        vec3 pointB = new vec3((New.curvePts[i].easting + New.curvePts[j].easting) / 2.0,
                            (New.curvePts[i].northing + New.curvePts[j].northing) / 2.0,
                           New.curvePts[i].heading);

                        New.curvePts.Insert(j, pointB);
                        cnt = New.curvePts.Count;
                        i = -1;
                    }
                }

                //who knows which way it actually goes
                CalculateTurnHeadings(New);

                //create a name
                New.Name = "Boundary Curve";
                mf.gyd.refList.Add(New);
                selectedLine = New;
                mf.FileSaveCurveLines();

                //update the arrays
                btnMakeABLine.Enabled = false;
                btnMakeCurve.Enabled = false;
                start = end = -1;

                FixLabels();
            }
            btnExit.Focus();
        }

        private void BtnMakeCurve_Click(object sender, EventArgs e)
        {
            CGuidanceLine New = new CGuidanceLine(Mode.Curve);

            btnCancelTouch.Enabled = false;

            double moveDist = (double)nudDistance.Value * mf.inchOrCm2m;
            double distSq = (moveDist) * (moveDist) * 0.999;

            vec3 pt3 = new vec3(mf.bnd.bndList[0].fenceLine.Points[start]);

            if (((mf.bnd.bndList[0].fenceLine.Points.Count - end + start) % mf.bnd.bndList[0].fenceLine.Points.Count) < ((mf.bnd.bndList[0].fenceLine.Points.Count - start + end) % mf.bnd.bndList[0].fenceLine.Points.Count)) { int index = start; start = end; end = index; }
            bool reverse = end < start;
            int count = reverse ? -1 : 1;

            if (reverse) { int B = start; start = end; end = B; }

            for (int i = start; i < end || i > end; i += count)
            {
                if (i == -1)
                {
                    i = mf.bnd.bndList[0].fenceLine.Points.Count;
                    continue;
                }
                //calculate the point inside the boundary
                pt3.easting = mf.bnd.bndList[0].fenceLine.Points[i].easting -
                    (Math.Sin(glm.PIBy2 + mf.bnd.bndList[0].fenceLine.Points[i].heading) * (moveDist));

                pt3.northing = mf.bnd.bndList[0].fenceLine.Points[i].northing -
                    (Math.Cos(glm.PIBy2 + mf.bnd.bndList[0].fenceLine.Points[i].heading) * (moveDist));

                pt3.heading = mf.bnd.bndList[0].fenceLine.Points[i].heading;

                bool Add = true;

                for (int j = start; j < end; j++)
                {
                    double check = glm.DistanceSquared(pt3.northing, pt3.easting,
                                        mf.bnd.bndList[0].fenceLine.Points[j].northing, mf.bnd.bndList[0].fenceLine.Points[j].easting);
                    if (check < distSq)
                    {
                        Add = false;
                        break;
                    }
                }

                if (Add)
                {
                    if (New.curvePts.Count > 0)
                    {
                        double dist = ((pt3.easting - New.curvePts[New.curvePts.Count - 1].easting) * (pt3.easting - New.curvePts[New.curvePts.Count - 1].easting))
                            + ((pt3.northing - New.curvePts[New.curvePts.Count - 1].northing) * (pt3.northing - New.curvePts[New.curvePts.Count - 1].northing));
                        if (dist > 1)
                            New.curvePts.Add(pt3);
                    }
                    else New.curvePts.Add(pt3);
                }
            }
            if (reverse)
                New.curvePts.Reverse();

            int cnt = New.curvePts.Count;
            if (cnt > 3)
            {
                //make sure distance isn't too big between points on Turn
                for (int i = 0; i < cnt - 1; i++)
                {
                    int j = i + 1;
                    //if (j == cnt) j = 0;
                    double distance = glm.Distance(New.curvePts[i], New.curvePts[j]);
                    if (distance > 1.6)
                    {
                        vec3 pointB = new vec3((New.curvePts[i].easting + New.curvePts[j].easting) / 2.0,
                            (New.curvePts[i].northing + New.curvePts[j].northing) / 2.0,
                            New.curvePts[i].heading);

                        New.curvePts.Insert(j, pointB);
                        cnt = New.curvePts.Count;
                        i = -1;
                    }
                }

                //who knows which way it actually goes
                CalculateTurnHeadings(New);

                //calculate average heading of line
                double x = 0, y = 0;

                foreach (vec3 pt in New.curvePts)
                {
                    x += Math.Cos(pt.heading);
                    y += Math.Sin(pt.heading);
                }
                x /= New.curvePts.Count;
                y /= New.curvePts.Count;
                double aveLineHeading = Math.Atan2(y, x);
                if (aveLineHeading < 0) aveLineHeading += glm.twoPI;

                //build the tail extensions

                AddFirstLastPoints(New);
                CalculateTurnHeadings(New);

                //create a name
                string text = (Math.Round(glm.toDegrees(aveLineHeading), 1)).ToString(CultureInfo.InvariantCulture)
                     + "\u00B0" + mf.FindDirection(aveLineHeading) + DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);
                while (mf.gyd.refList.Exists(z => z.Name == text))//generate unique name!
                    text += " ";
                New.Name = text;

                mf.gyd.refList.Add(New);
                selectedLine = New;
                mf.FileSaveCurveLines();

                //update the arrays
                btnMakeABLine.Enabled = false;
                btnMakeCurve.Enabled = false;
                start = end = -1;

                FixLabels();
            }

            btnExit.Focus();
        }

        private void BtnMakeABLine_Click(object sender, EventArgs e)
        {
            btnCancelTouch.Enabled = false;

            //calculate the AB Heading
            if (start < end) { int B = start; start = end; end = B; }
            double abHead = Math.Atan2(mf.bnd.bndList[0].fenceLine.Points[end].easting - mf.bnd.bndList[0].fenceLine.Points[start].easting,
               mf.bnd.bndList[0].fenceLine.Points[end].northing - mf.bnd.bndList[0].fenceLine.Points[start].northing);
            if (abHead < 0) abHead += glm.twoPI;

            double offset = ((double)nudDistance.Value * mf.inchOrCm2m);

            double headingCalc = abHead + glm.PIBy2;

            CGuidanceLine New = new CGuidanceLine(Mode.AB);

            New.curvePts.Add(new vec3((Math.Sin(headingCalc) * offset) + mf.bnd.bndList[0].fenceLine.Points[start].easting, (Math.Cos(headingCalc) * offset) + mf.bnd.bndList[0].fenceLine.Points[start].northing, abHead));
            New.curvePts.Add(new vec3(New.curvePts[0].easting + Math.Sin(abHead), New.curvePts[0].northing + Math.Cos(abHead), abHead));

            //create a name
            string text = (Math.Round(glm.toDegrees(abHead), 1)).ToString(CultureInfo.InvariantCulture)
                 + "\u00B0" + mf.FindDirection(abHead) + DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);
            while (mf.gyd.refList.Exists(x => x.Name == text))//generate unique name!
                text += " ";
            New.Name = text;

            mf.gyd.refList.Add(New);
            selectedLine = New;
            mf.FileSaveABLines();

            //clean up gui
            btnMakeABLine.Enabled = false;
            btnMakeCurve.Enabled = false;

            start = end = -1;

            FixLabels();
        }

        public void CalculateTurnHeadings(CGuidanceLine New)
        {
            //to calc heading based on next and previous points to give an average heading.
            int cnt = New.curvePts.Count;
            if (cnt > 0)
            {
                vec3[] arr = new vec3[cnt];
                cnt--;
                New.curvePts.CopyTo(arr);
                New.curvePts.Clear();

                //middle points
                for (int i = 1; i < cnt; i++)
                {
                    vec3 pt3 = arr[i];
                    pt3.heading = Math.Atan2(arr[i + 1].easting - arr[i - 1].easting, arr[i + 1].northing - arr[i - 1].northing);
                    if (pt3.heading < 0) pt3.heading += glm.twoPI;
                    New.curvePts.Add(pt3);
                }
            }
        }

        private void tboxNameLine_Click(object sender, EventArgs e)
        {
            if (selectedLine?.Mode.HasFlag(Mode.AB) == true)
            {
                if (mf.isKeyboardOn)
                {
                    mf.KeyboardToText((TextBox)sender, this);

                    string text = tboxNameLine.Text.Trim();
                    while (mf.gyd.refList.Exists(x => x != selectedLine && x.Name == text))//generate unique name!
                        text += " ";
                    selectedLine.Name = text;

                    btnExit.Focus();
                }
            }
        }

        private void tboxNameLine_TextChanged(object sender, EventArgs e)
        {
            if (selectedLine?.Mode.HasFlag(Mode.AB) == true)
            {
                string text = tboxNameLine.Text.Trim();
                while (mf.gyd.refList.Exists(x => x != selectedLine && x.Name == text))//generate unique name!
                    text += " ";
                selectedLine.Name = text;
            }
        }

        private void tboxNameCurve_TextChanged(object sender, EventArgs e)
        {
            if (selectedLine?.Mode.HasFlag(Mode.Curve) == true)
            {
                string text = tboxNameCurve.Text.Trim();
                while (mf.gyd.refList.Exists(x => x != selectedLine && x.Name == text))//generate unique name!
                    text += " ";
                selectedLine.Name = text;
            }
        }

        private void tboxNameCurve_Click(object sender, EventArgs e)
        {
            if (selectedLine?.Mode.HasFlag(Mode.Curve) == true)
            {
                if (mf.isKeyboardOn)
                {
                    mf.KeyboardToText((TextBox)sender, this);

                    string text = tboxNameCurve.Text.Trim();
                    while (mf.gyd.refList.Exists(x => x != selectedLine && x.Name == text))//generate unique name!
                        text += " ";
                    selectedLine.Name = text;

                    btnExit.Focus();
                }
            }
        }

        public void AddFirstLastPoints(CGuidanceLine New)
        {
            int ptCnt = New.curvePts.Count - 1;
            for (int i = 1; i < 200; i++)
            {
                vec3 pt = new vec3(New.curvePts[ptCnt]);
                pt.easting += (Math.Sin(pt.heading) * i);
                pt.northing += (Math.Cos(pt.heading) * i);
                New.curvePts.Add(pt);
            }

            //and the beginning
            vec3 start = new vec3(New.curvePts[0]);
            for (int i = 1; i < 200; i++)
            {
                vec3 pt = new vec3(start);
                pt.easting -= (Math.Sin(pt.heading) * i);
                pt.northing -= (Math.Cos(pt.heading) * i);
                New.curvePts.Insert(0, pt);
            }
        }

        private void oglSelf_Paint(object sender, PaintEventArgs e)
        {
            oglSelf.MakeCurrent();

            GL.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
            GL.LoadIdentity();                  // Reset The View

            //translate to that spot in the world
            GL.Translate(-mf.fieldCenterX, -mf.fieldCenterY, 0);

            GL.Color3(1, 1, 1);

            //draw all the boundaries
            mf.bnd.DrawFenceLines();

            //the vehicle
            GL.PointSize(16.0f);
            GL.Begin(PrimitiveType.Points);
            GL.Color3(0.95f, 0.90f, 0.0f);
            GL.Vertex3(mf.pivotAxlePos.easting, mf.pivotAxlePos.northing, 0.0);
            GL.End();

            if (isDrawSections) DrawSections();

            //draw the line building graphics
            if (start > -1 || end > -1) DrawABTouchLine();

            //draw the actual built lines
            if (start == -1 && end == -1)
            {
                DrawBuiltLines();
            }

            GL.Flush();
            oglSelf.SwapBuffers();
        }

        private void DrawBuiltLines()
        {
            GL.Enable(EnableCap.LineStipple);
            GL.LineStipple(1, 0x0707);
            GL.Color3(1.0f, 0.0f, 0.0f);

            GL.LineWidth(2);
            GL.Begin(PrimitiveType.Lines);

            for (int i = 0; i < mf.gyd.refList.Count; i++)
            {
                CGuidanceLine item = mf.gyd.refList[i];
                if (mf.gyd.selectedLine?.Name == item.Name) item = mf.gyd.selectedLine;

                if (item.Mode.HasFlag(Mode.AB) && item.curvePts.Count > 1)
                {
                    double abHead = Math.Atan2(item.curvePts[1].easting - item.curvePts[0].easting, item.curvePts[1].northing - item.curvePts[0].northing);

                    GL.Vertex3(item.curvePts[0].easting - (Math.Sin(abHead) * mf.gyd.abLength), item.curvePts[0].northing - (Math.Cos(abHead) * mf.gyd.abLength), 0);
                    GL.Vertex3(item.curvePts[1].easting + (Math.Sin(abHead) * mf.gyd.abLength), item.curvePts[1].northing + (Math.Cos(abHead) * mf.gyd.abLength), 0);
                }
            }

            GL.End();

            GL.LineStipple(1, 0x7070);

            for (int i = 0; i < mf.gyd.refList.Count; i++)
            {
                CGuidanceLine item = mf.gyd.refList[i];
                if (mf.gyd.selectedLine?.Name == item.Name) item = mf.gyd.selectedLine;

                if (item.Mode.HasFlag(Mode.Curve))
                {
                    GL.LineWidth(2);
                    GL.Color3(0.0f, 1.0f, 0.0f);
                    GL.Begin(PrimitiveType.LineStrip);
                    foreach (vec3 item2 in item.curvePts)
                    {
                        GL.Vertex3(item2.easting, item2.northing, 0);
                    }
                    GL.End();
                }
            }

            GL.Disable(EnableCap.LineStipple);

            if (selectedLine?.Mode.HasFlag(Mode.AB) == true)
            {
                CGuidanceLine item = selectedLine;
                if (mf.gyd.selectedLine?.Name == item.Name) item = mf.gyd.selectedLine;

                GL.Color3(1.0f, 0.0f, 0.0f);

                GL.LineWidth(4);
                GL.Begin(PrimitiveType.Lines);

                if (item.curvePts.Count > 1)
                {
                    double abHead = Math.Atan2(item.curvePts[1].easting - item.curvePts[0].easting,
                        item.curvePts[1].northing - item.curvePts[0].northing);

                    GL.Vertex3(item.curvePts[0].easting - (Math.Sin(abHead) * mf.gyd.abLength),
                        item.curvePts[0].northing - (Math.Cos(abHead) * mf.gyd.abLength), 0);
                    GL.Vertex3(item.curvePts[0].easting + (Math.Sin(abHead) * mf.gyd.abLength),
                        item.curvePts[0].northing + (Math.Cos(abHead) * mf.gyd.abLength), 0);
                }
                GL.End();
            }
            else if (selectedLine?.Mode.HasFlag(Mode.Curve) == true)
            {
                CGuidanceLine item = selectedLine;
                if (mf.gyd.selectedLine?.Name == item.Name) item = mf.gyd.selectedLine;
                GL.LineWidth(4);
                GL.Color3(0.0f, 1.0f, 0.0f);
                GL.Begin(PrimitiveType.LineStrip);
                foreach (vec3 item2 in item.curvePts)
                {
                    GL.Vertex3(item2.easting, item2.northing, 0);
                }
                GL.End();
            }
        }

        private void DrawABTouchLine()
        {
            GL.Color3(0.65, 0.650, 0.0);
            GL.PointSize(8);
            GL.Begin(PrimitiveType.Points);

            GL.Color3(0.95, 0.950, 0.0);
            if (start > -1) GL.Vertex3(mf.bnd.bndList[0].fenceLine.Points[start].easting, mf.bnd.bndList[0].fenceLine.Points[start].northing, 0);

            GL.Color3(0.950, 096.0, 0.0);
            if (end > -1) GL.Vertex3(mf.bnd.bndList[0].fenceLine.Points[end].easting, mf.bnd.bndList[0].fenceLine.Points[end].northing, 0);
            GL.End();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            oglSelf.Refresh();

            bool isBounCurve = false;
            for (int i = 0; i < mf.gyd.refList.Count; i++)
            {
                if (mf.gyd.refList[i].Name == "Boundary Curve") isBounCurve = true;
            }

            if (isBounCurve) btnMakeBoundaryCurve.Enabled = false;
            else btnMakeBoundaryCurve.Enabled = true;
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            mf.gyd.moveDistance = 0;

            if (selectedLine != null)
            {
                if (mf.gyd.selectedLine == null)
                {
                    mf.gyd.selectedLine = new CGuidanceLine(selectedLine);
                    if (selectedLine.Mode.HasFlag(Mode.AB))
                        mf.enableABLineButton(true);
                    else
                        mf.enableCurveButton(true);
                }
                else if ((mf.gyd.isBtnABLineOn && selectedLine.Mode.HasFlag(Mode.AB)) || (mf.gyd.isBtnCurveOn && selectedLine.Mode.HasFlag(Mode.Curve)))
                    if (mf.gyd.selectedLine.Name != selectedLine.Name)
                        mf.gyd.selectedLine = new CGuidanceLine(selectedLine);
            }
            Close();
        }

        private void oglSelf_Resize(object sender, EventArgs e)
        {
            oglSelf.MakeCurrent();

            Matrix4 projection = Matrix4.CreateOrthographicOffCenter(-mf.minmax, mf.minmax, -mf.minmax, mf.minmax, -1.0f, 1.0f);

            GL.MatrixMode(MatrixMode.Projection);//set state to load the projection matrix
            GL.LoadMatrix(ref projection);
            GL.MatrixMode(MatrixMode.Modelview);//set state to draw global coordinates into clip space;
        }

        private void oglSelf_Load(object sender, EventArgs e)
        {
            oglSelf.MakeCurrent();
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.ClearColor(0.23122f, 0.2318f, 0.2315f, 1.0f);
        }

        private void DrawSections()
        {
            int cnt, step;
            int mipmap = 8;

            GL.Color3(0.0, 0.0, 0.352);

            //for every new chunk of patch
            foreach (System.Collections.Generic.List<vec3> triList in mf.tool.patchList)
            {
                //draw the triangle in each triangle strip
                GL.Begin(PrimitiveType.TriangleStrip);
                cnt = triList.Count;

                //if large enough patch and camera zoomed out, fake mipmap the patches, skip triangles
                if (cnt >= (mipmap))
                {
                    step = mipmap;
                    for (int i = 1; i < cnt; i += step)
                    {
                        GL.Vertex3(triList[i].easting, triList[i].northing, 0); i++;
                        GL.Vertex3(triList[i].easting, triList[i].northing, 0); i++;

                        //too small to mipmap it
                        if (cnt - i <= (mipmap + 2))
                            step = 0;
                    }
                }

                else { for (int i = 1; i < cnt; i++) GL.Vertex3(triList[i].easting, triList[i].northing, 0); }
                GL.End();
            }//end of section patches
        }
    }
}