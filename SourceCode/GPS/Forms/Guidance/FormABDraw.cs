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

        private Point fixPt;

        private bool isA = true, isMakingAB = false, isMakingCurve = false;
        public double low = 0, high = 1;
        private int A, B, C, D, E, start = 99999, end = 99999;

        private bool isDrawSections = false;

        private vec3[] arr;
        public FormABDraw(Form callingForm)
        {
            //get copy of the calling main form
            mf = callingForm as FormGPS;

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

            mf.CalculateMinMax();
        }

        private void FormABDraw_Load(object sender, EventArgs e)
        {
            int cnt = mf.bnd.bndList[0].fenceLine.Points.Count;
            arr = new vec3[cnt * 2];

            for (int i = 0; i < cnt; i++)
            {
                arr[i].easting = mf.bnd.bndList[0].fenceLine.Points[i].easting;
                arr[i].northing = mf.bnd.bndList[0].fenceLine.Points[i].northing;
                arr[i].heading = mf.bnd.bndList[0].fenceLine.Points[i].heading;
            }

            for (int i = cnt; i < cnt * 2; i++)
            {
                arr[i].easting = mf.bnd.bndList[0].fenceLine.Points[i - cnt].easting;
                arr[i].northing = mf.bnd.bndList[0].fenceLine.Points[i - cnt].northing;
                arr[i].heading = mf.bnd.bndList[0].fenceLine.Points[i - cnt].heading;
            }

            nudDistance.Value = (decimal)Math.Round(((mf.tool.toolWidth * mf.m2InchOrCm) * 0.5), 0); // 
            label6.Text = Math.Round((mf.tool.toolWidth * mf.m2InchOrCm), 0).ToString();
            FixLabelsABLine();
            FixLabelsCurve();

            if (isDrawSections) btnDrawSections.Image = Properties.Resources.MappingOn;
            else btnDrawSections.Image = Properties.Resources.MappingOff;

        }

        private void FixLabelsCurve()
        {
            lblNumCu.Text = mf.curve.numCurveLines.ToString();
            lblCurveSelected.Text = mf.curve.numCurveLineSelected.ToString();

            if (mf.curve.numCurveLineSelected > 0)
            {
                tboxNameCurve.Text = mf.curve.curveArr[mf.curve.numCurveLineSelected - 1].Name;
                tboxNameCurve.Enabled = true;
            }
            else
            {
                tboxNameCurve.Text = "***";
                tboxNameCurve.Enabled = false;
            }
        }

        private void FixLabelsABLine()
        {
            lblNumAB.Text = mf.ABLine.numABLines.ToString();
            lblABSelected.Text = mf.ABLine.numABLineSelected.ToString();

            if (mf.ABLine.numABLineSelected > 0)
            {
                tboxNameLine.Text = mf.ABLine.lineArr[mf.ABLine.numABLineSelected - 1].Name;
                tboxNameLine.Enabled = true;
            }
            else
            {
                tboxNameLine.Text = "***";
                tboxNameLine.Enabled = false;
            }
        }

        private void btnSelectCurve_Click(object sender, EventArgs e)
        {
            if (mf.curve.numCurveLines > 0)
            {
                mf.curve.numCurveLineSelected++;
                if (mf.curve.numCurveLineSelected > mf.curve.numCurveLines) mf.curve.numCurveLineSelected = 1;
            }
            else
            {
                mf.curve.numCurveLineSelected = 0;
            }

            FixLabelsCurve();
        }

        private void btnSelectABLine_Click(object sender, EventArgs e)
        {
            if (mf.ABLine.numABLines > 0)
            {
                mf.ABLine.numABLineSelected++;
                if (mf.ABLine.numABLineSelected > mf.ABLine.numABLines) mf.ABLine.numABLineSelected = 1;
            }
            else
            {
                mf.ABLine.numABLineSelected = 0;
            }

            FixLabelsABLine();
        }

        private void btnCancelTouch_Click(object sender, EventArgs e)
        {
            btnMakeABLine.Enabled = false;
            btnMakeCurve.Enabled = false;

            isMakingAB = isMakingCurve = false;
            isA = true;
            start = 99999; end = 99999;

            btnCancelTouch.Enabled = false;
            btnExit.Focus();
        }

        private void nudDistance_Click(object sender, EventArgs e)
        {
            mf.KeypadToNUD((NumericUpDown)sender, this);
            btnSelectABLine.Focus();

        }

        private void btnDeleteCurve_Click(object sender, EventArgs e)
        {
            if (mf.curve.curveArr.Count > 0 && mf.curve.numCurveLineSelected > 0)
            {
                mf.curve.curveArr.RemoveAt(mf.curve.numCurveLineSelected - 1);
                mf.curve.numCurveLines--;

            }

            if (mf.curve.numCurveLines > 0) mf.curve.numCurveLineSelected = 1;
            else mf.curve.numCurveLineSelected = 0;

            FixLabelsCurve();
        }

        private void btnDeleteABLine_Click(object sender, EventArgs e)
        {
            if (mf.ABLine.lineArr.Count > 0 && mf.ABLine.numABLineSelected > 0)
            {
                mf.ABLine.lineArr.RemoveAt(mf.ABLine.numABLineSelected - 1);
                mf.ABLine.numABLines--;
                mf.ABLine.numABLineSelected--;
            }

            if (mf.ABLine.numABLines > 0) mf.ABLine.numABLineSelected = 1;
            else mf.ABLine.numABLineSelected = 0;

            FixLabelsABLine();
        }

        private void btnDrawSections_Click(object sender, EventArgs e)
        {
            isDrawSections = !isDrawSections;
            if (isDrawSections) btnDrawSections.Image = Properties.Resources.MappingOn;
            else btnDrawSections.Image = Properties.Resources.MappingOff;
        }

        public vec3 pint = new vec3(0.0, 1.0, 0.0);

        private void tboxNameCurve_Leave(object sender, EventArgs e)
        {
            if (mf.curve.numCurveLineSelected > 0)
                mf.curve.curveArr[mf.curve.numCurveLineSelected - 1].Name = tboxNameCurve.Text.Trim();
        }

        private void tboxNameLine_Leave(object sender, EventArgs e)
        {
            if (mf.ABLine.numABLineSelected > 0)
                mf.ABLine.lineArr[mf.ABLine.numABLineSelected - 1].Name = tboxNameLine.Text.Trim();
        }

        private void btnFlipOffset_Click(object sender, EventArgs e)
        {
            nudDistance.Value *= -1;
        }

        private void tboxNameCurve_Enter(object sender, EventArgs e)
        {
            if (mf.curve.curveArr[mf.curve.numCurveLineSelected - 1].Name == "Boundary Curve")
            {
                btnExit.Focus();
                return;
            }

            if (mf.isKeyboardOn)
            {
                mf.KeyboardToText((TextBox)sender, this);
                if (mf.curve.numCurveLineSelected > 0)
                    mf.curve.curveArr[mf.curve.numCurveLineSelected - 1].Name = tboxNameCurve.Text.Trim();
                btnExit.Focus();
            }
        }

        private void tboxNameLine_Enter(object sender, EventArgs e)
        {
            if (mf.isKeyboardOn)
            {
                mf.KeyboardToText((TextBox)sender, this);
                if (mf.ABLine.numABLineSelected > 0)
                    mf.ABLine.lineArr[mf.ABLine.numABLineSelected - 1].Name = tboxNameLine.Text.Trim();
                btnExit.Focus();
            }
        }

        private void oglSelf_MouseDown(object sender, MouseEventArgs e)
        {
            btnCancelTouch.Enabled = true;

            btnMakeABLine.Enabled = false;
            btnMakeCurve.Enabled = false;
            isMakingAB = isMakingCurve = false;

            Point pt = oglSelf.PointToClient(Cursor.Position);

            //Convert to Origin in the center of window, 800 pixels
            fixPt.X = pt.X - 350;
            fixPt.Y = (700 - pt.Y - 350);
            vec3 plotPt = new vec3
            {
                //convert screen coordinates to field coordinates
                easting = fixPt.X * mf.maxFieldDistance / 632.0,
                northing = fixPt.Y * mf.maxFieldDistance / 632.0,
                heading = 0
            };

            plotPt.easting += mf.fieldCenterX;
            plotPt.northing += mf.fieldCenterY;

            pint.easting = plotPt.easting;
            pint.northing = plotPt.northing;

            if (isA)
            {
                double minDistA = 1000000, minDistB = 1000000;
                start = 99999; end = 99999;

                int ptCount = arr.Length;

                if (ptCount > 0)
                {
                    //find the closest 2 points to current fix
                    for (int t = 0; t < ptCount; t++)
                    {
                        double dist = ((pint.easting - arr[t].easting) * (pint.easting - arr[t].easting))
                                        + ((pint.northing - arr[t].northing) * (pint.northing - arr[t].northing));
                        if (dist < minDistA)
                        {
                            minDistB = minDistA;
                            B = A;
                            minDistA = dist;
                            A = t;
                        }
                        else if (dist < minDistB)
                        {
                            minDistB = dist;
                            B = t;
                        }
                    }

                    //just need to make sure the points continue ascending or heading switches all over the place
                    if (A > B) { E = A; A = B; B = E; }

                    start = A;
                }

                isA = false;
            }
            else
            {
                double minDistA = 1000000, minDistB = 1000000;

                int ptCount = arr.Length;

                if (ptCount > 0)
                {
                    //find the closest 2 points to current point
                    for (int t = 0; t < ptCount; t++)
                    {
                        double dist = ((pint.easting - arr[t].easting) * (pint.easting - arr[t].easting))
                                        + ((pint.northing - arr[t].northing) * (pint.northing - arr[t].northing));
                        if (dist < minDistA)
                        {
                            minDistB = minDistA;
                            D = C;
                            minDistA = dist;
                            C = t;
                        }
                        else if (dist < minDistB)
                        {
                            minDistB = dist;
                            D = t;
                        }
                    }

                    //just need to make sure the points continue ascending or heading switches all over the place
                    if (C > D) { E = C; C = D; D = E; }
                }

                isA = true;

                int A1 = Math.Abs(A - C);
                int B1 = Math.Abs(A - D);
                int C1 = Math.Abs(B - C);
                int D1 = Math.Abs(B - D);

                if (A1 <= B1 && A1 <= C1 && A1 <= D1) { start = A; end = C; }
                else if (B1 <= A1 && B1 <= C1 && B1 <= D1) { start = A; end = D; }
                else if (C1 <= B1 && C1 <= A1 && C1 <= D1) { start = B; end = C; }
                else if (D1 <= B1 && D1 <= C1 && D1 <= A1) { start = B; end = D; }

                if (start > end) { E = start; start = end; end = E; }

                btnMakeABLine.Enabled = true;
                btnMakeCurve.Enabled = true;
            }
        }

        private void btnMakeBoundaryCurve_Click(object sender, EventArgs e)
        {
            CCurveLines New = new CCurveLines();

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
                
                mf.curve.curveArr.Add(New);
                mf.curve.numCurveLines = mf.curve.curveArr.Count;
                mf.curve.numCurveLineSelected = mf.curve.numCurveLines;//force change ab?

                mf.FileSaveCurveLines();

                //update the arrays
                btnMakeABLine.Enabled = false;
                btnMakeCurve.Enabled = false;
                isMakingCurve = false;
                isMakingAB = false;
                start = 99999; end = 99999;

                FixLabelsCurve();
            }
            btnExit.Focus();
        }

        private void BtnMakeCurve_Click(object sender, EventArgs e)
        {
            CCurveLines New = new CCurveLines();

            btnCancelTouch.Enabled = false;

            double moveDist = (double)nudDistance.Value * mf.inchOrCm2m;
            double distSq = (moveDist) * (moveDist) * 0.999;

            vec3 pt3 = new vec3(arr[start]);

            for (int i = start; i < end; i++)
            {
                //calculate the point inside the boundary
                pt3.easting = arr[i].easting -
                    (Math.Sin(glm.PIBy2 + arr[i].heading) * (moveDist));

                pt3.northing = arr[i].northing -
                    (Math.Cos(glm.PIBy2 + arr[i].heading) * (moveDist));

                pt3.heading = arr[i].heading;

                bool Add = true;

                for (int j = start; j < end; j++)
                {
                    double check = glm.DistanceSquared(pt3.northing, pt3.easting,
                                        arr[j].northing, arr[j].easting);
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
                New.Name = (Math.Round(glm.toDegrees(aveLineHeading), 1)).ToString(CultureInfo.InvariantCulture)
                     + "\u00B0" + mf.FindDirection(aveLineHeading) + DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);

                mf.curve.curveArr.Add(New);
                mf.curve.numCurveLines = mf.curve.curveArr.Count;
                mf.curve.numCurveLineSelected = mf.curve.numCurveLines;

                mf.FileSaveCurveLines();

                //update the arrays
                btnMakeABLine.Enabled = false;
                btnMakeCurve.Enabled = false;
                isMakingCurve = false;
                isMakingAB = false;
                start = 99999; end = 99999;

                FixLabelsCurve();
            }

            btnExit.Focus();
        }

        private void BtnMakeABLine_Click(object sender, EventArgs e)
        {
            btnCancelTouch.Enabled = false;

            //calculate the AB Heading
            if (A < C) { B = A; A = C; C = B; }
            double abHead = Math.Atan2(arr[C].easting - arr[A].easting, arr[C].northing - arr[A].northing);
            if (abHead < 0) abHead += glm.twoPI;

            double offset = ((double)nudDistance.Value * mf.inchOrCm2m);

            double headingCalc = abHead + glm.PIBy2;

            CABLines New = new CABLines();

            New.curvePts.Add(new vec3((Math.Sin(headingCalc) * offset) + arr[A].easting, (Math.Cos(headingCalc) * offset) + arr[A].northing, abHead));
            New.curvePts.Add(new vec3(New.curvePts[0].easting + Math.Sin(abHead), New.curvePts[0].northing + Math.Cos(abHead), abHead));

            //create a name
            New.Name = (Math.Round(glm.toDegrees(abHead), 1)).ToString(CultureInfo.InvariantCulture)
                 + "\u00B0" + mf.FindDirection(abHead) + DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);

            mf.ABLine.lineArr.Add(New);
            mf.ABLine.numABLines = mf.ABLine.lineArr.Count;
            mf.ABLine.numABLineSelected = mf.ABLine.numABLines;

            //clean up gui
            btnMakeABLine.Enabled = false;
            btnMakeCurve.Enabled = false;

            isMakingCurve = false;
            isMakingAB = false;
            start = 99999; end = 99999;

            FixLabelsABLine();
        }

        public void CalculateTurnHeadings(CCurveLines New)
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

        public void AddFirstLastPoints(CCurveLines New)
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

            //back the camera up
            GL.Translate(0, 0, -mf.maxFieldDistance);

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
            if (start != 99999 || end != 99999) DrawABTouchLine();

            //draw the actual built lines
            if (start == 99999 && end == 99999)
            {
                DrawBuiltLines();
            }

            GL.Flush();
            oglSelf.SwapBuffers();
        }

        private void DrawBuiltLines()
        {
            int numLines = mf.ABLine.lineArr.Count;

            if (numLines > 0)
            {
                GL.Enable(EnableCap.LineStipple);
                GL.LineStipple(1, 0x0707);
                GL.Color3(1.0f, 0.0f, 0.0f);

                for (int i = 0; i < numLines; i++)
                {
                    GL.LineWidth(2);
                    GL.Begin(PrimitiveType.Lines);

                    foreach (CABLines item in mf.ABLine.lineArr)
                    {
                        if (item.curvePts.Count > 1)
                        {
                            double abHead = Math.Atan2(item.curvePts[1].easting - item.curvePts[0].easting, item.curvePts[1].northing - item.curvePts[0].northing);

                            GL.Vertex3(item.curvePts[0].easting - (Math.Sin(abHead) * mf.ABLine.abLength), item.curvePts[0].northing - (Math.Cos(abHead) * mf.ABLine.abLength), 0);
                            GL.Vertex3(item.curvePts[1].easting + (Math.Sin(abHead) * mf.ABLine.abLength), item.curvePts[1].northing + (Math.Cos(abHead) * mf.ABLine.abLength), 0);
                        }
                    }

                    GL.End();
                }

                GL.Disable(EnableCap.LineStipple);

                if (mf.ABLine.numABLineSelected > 0)
                {
                    GL.Color3(1.0f, 0.0f, 0.0f);

                    GL.LineWidth(4);
                    GL.Begin(PrimitiveType.Lines);

                    int idx = mf.ABLine.numABLineSelected - 1;
                    if (mf.ABLine.lineArr[idx].curvePts.Count > 1)
                    {
                        double abHead = Math.Atan2(mf.ABLine.lineArr[idx].curvePts[1].easting - mf.ABLine.lineArr[idx].curvePts[0].easting,
                            mf.ABLine.lineArr[idx].curvePts[1].northing - mf.ABLine.lineArr[idx].curvePts[0].northing);

                        GL.Vertex3(mf.ABLine.lineArr[idx].curvePts[0].easting - (Math.Sin(abHead) * mf.ABLine.abLength),
                            mf.ABLine.lineArr[idx].curvePts[0].northing - (Math.Cos(abHead) * mf.ABLine.abLength), 0);
                        GL.Vertex3(mf.ABLine.lineArr[idx].curvePts[0].easting + (Math.Sin(abHead) * mf.ABLine.abLength),
                            mf.ABLine.lineArr[idx].curvePts[0].northing + (Math.Cos(abHead) * mf.ABLine.abLength), 0);
                    }
                    GL.End();
                }
            }

            int numCurv = mf.curve.curveArr.Count;

            if (numCurv > 0)
            {
                GL.Enable(EnableCap.LineStipple);
                GL.LineStipple(1, 0x7070);

                for (int i = 0; i < numCurv; i++)
                {
                    GL.LineWidth(2);
                    GL.Color3(0.0f, 1.0f, 0.0f);
                    GL.Begin(PrimitiveType.LineStrip);
                    foreach (vec3 item in mf.curve.curveArr[i].curvePts)
                    {
                        GL.Vertex3(item.easting, item.northing, 0);
                    }
                    GL.End();
                }

                GL.Disable(EnableCap.LineStipple);

                if (mf.curve.numCurveLineSelected > 0)
                {
                    GL.LineWidth(4);
                    GL.Color3(0.0f, 1.0f, 0.0f);
                    GL.Begin(PrimitiveType.LineStrip);
                    foreach (vec3 item in mf.curve.curveArr[mf.curve.numCurveLineSelected - 1].curvePts)
                    {
                        GL.Vertex3(item.easting, item.northing, 0);
                    }
                    GL.End();
                }
            }
        }

        private void DrawABTouchLine()
        {
            GL.Color3(0.65, 0.650, 0.0);
            GL.PointSize(8);
            GL.Begin(PrimitiveType.Points);

            GL.Color3(0.95, 0.950, 0.0);
            if (start != 99999) GL.Vertex3(arr[start].easting, arr[start].northing, 0);

            GL.Color3(0.950, 096.0, 0.0);
            if (end != 99999) GL.Vertex3(arr[end].easting, arr[end].northing, 0);
            GL.End();

            if (isMakingCurve)
            {
                //draw the turn line oject
                GL.LineWidth(4.0f);
                GL.Begin(PrimitiveType.LineStrip);
                int ptCount = arr.Length;
                if (ptCount < 1) return;
                for (int c = start; c < end; c++) GL.Vertex3(arr[c].easting, arr[c].northing, 0);

                GL.End();
            }

            if (isMakingAB)
            {
                GL.LineWidth(4.0f);
                GL.Color3(0.95, 0.0, 0.0);
                GL.Begin(PrimitiveType.Lines);
                GL.Vertex3(arr[A].easting, arr[A].northing, 0);
                GL.Vertex3(arr[C].easting, arr[C].northing, 0);
                GL.End();
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            oglSelf.Refresh();

            bool isBounCurve = false;
            for (int i = 0; i < mf.curve.curveArr.Count; i++)
            {
                if (mf.curve.curveArr[i].Name == "Boundary Curve") isBounCurve = true;
            }

            if (isBounCurve) btnMakeBoundaryCurve.Enabled = false;
            else btnMakeBoundaryCurve.Enabled = true;
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            mf.gyd.moveDistance = 0;
            mf.ABLine.refList.Clear();
            if (mf.ABLine.numABLineSelected > 0)
            {
                for (int i = 0; i < mf.ABLine.lineArr[mf.ABLine.numABLineSelected - 1].curvePts.Count; i++)
                {
                    mf.ABLine.refList.Add(mf.ABLine.lineArr[mf.ABLine.numABLineSelected - 1].curvePts[i]);
                }
            }
            else
            {
                mf.ABLine.DeleteAB();
            }

            mf.FileSaveABLines();


            //curve
            mf.curve.refList.Clear();
            if (mf.curve.numCurveLineSelected > 0)
            {
                int idx = mf.curve.numCurveLineSelected - 1;
                foreach (vec3 v in mf.curve.curveArr[idx].curvePts)
                    mf.curve.refList.Add(v);
            }

            mf.FileSaveCurveLines();

            if (mf.ABLine.isBtnABLineOn)
            {
                if (mf.ABLine.numABLineSelected == 0)
                {
                    if (mf.isAutoSteerBtnOn) mf.btnAutoSteer.PerformClick();
                    if (mf.yt.isYouTurnBtnOn) mf.btnAutoYouTurn.PerformClick();
                    mf.btnABLine.Image = Properties.Resources.ABLineOff;
                    mf.ABLine.isBtnABLineOn = false;
                }
            }

            if (mf.curve.isBtnCurveOn)
            {
                if (mf.curve.numCurveLineSelected == 0)
                {
                    if (mf.isAutoSteerBtnOn) mf.btnAutoSteer.PerformClick();
                    if (mf.yt.isYouTurnBtnOn) mf.btnAutoYouTurn.PerformClick();
                    mf.curve.isBtnCurveOn = false;
                    mf.btnCurve.Image = Properties.Resources.CurveOff;
                }
            }

            Close();
        }

        private void oglSelf_Resize(object sender, EventArgs e)
        {
            oglSelf.MakeCurrent();
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();

            //58 degrees view
            Matrix4 mat = Matrix4.CreatePerspectiveFieldOfView(1.01f, 1.0f, 1.0f, 20000);
            GL.LoadMatrix(ref mat);

            GL.MatrixMode(MatrixMode.Modelview);
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