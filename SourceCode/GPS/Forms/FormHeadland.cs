using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormHeadland : Form
    {
        //access to the main GPS form and all its variables
        private readonly FormGPS mf = null;

        private bool isA, isSet, reset, isClosing;
        private int start = -1, end = -1;
        private double totalHeadlandWidth = 0;

        //list of coordinates of boundary line
        public List<vec3> headLineTemplate = new List<vec3>();

        public FormHeadland(Form callingForm)
        {
            //get copy of the calling main form
            mf = callingForm as FormGPS;

            mf.CalculateMinMax();

            InitializeComponent();
            //lblPick.Text = gStr.gsSelectALine;
            this.Text = gStr.gsHeadlandForm;
            btnReset.Text = gStr.gsResetAll;

            nudDistance.Controls[0].Enabled = false;
            lblWidthUnits.Text = mf.unitsFtM;
        }

        private void FormHeadland_Load(object sender, EventArgs e)
        {
            if (mf.bnd.bndList[0].hdLine.Points.Count > 0)
                BuildHeadLineTemplate(true);
            else
                BuildHeadLineTemplate(false);
            mf.CloseTopMosts();
        }

        private void FormHeadland_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!isClosing)
            {
                mf.bnd.bndList[0].hdLine.Points.Clear();
                mf.bnd.bndList[0].hdLine.ResetPoints = true;
                mf.FileSaveHeadland();
            }
        }

        public void BuildHeadLineTemplate(bool fromHdLine)
        {
            totalHeadlandWidth = 0;
            lblHeadlandWidth.Text = "0";

            headLineTemplate.Clear();
            if (fromHdLine)
            {
                for (int i = 0; i < mf.bnd.bndList[0].hdLine.Points.Count; i++)
                {
                    headLineTemplate.Add(mf.bnd.bndList[0].hdLine.Points[i]);
                }
            }
            else
            {
                for (int i = 0; i < mf.bnd.bndList[0].fenceLine.Points.Count; i++)
                {
                    headLineTemplate.Add(mf.bnd.bndList[0].fenceLine.Points[i]);
                }
            }

            start = end = -1;
            isSet = false;
            isA = true;
        }

        private void FixTurnLine(double totalHeadWidth, List<vec3> curBnd, double spacing)
        {
            int lineCount = headLineTemplate.Count;
            double distance;

            //int headCount = mf.bndArr[inTurnNum].bndLine.Count;
            int bndCount = curBnd.Count;
            //remove the points too close to boundary
            for (int j = 0; j < lineCount; j++)
            {
                for (int i = 0; i < bndCount; i++)
                {
                    //make sure distance between headland and boundary is not less then width
                    distance = glm.Distance(curBnd[i], headLineTemplate[j]);
                    if (distance < (totalHeadWidth - 0.5))
                    {
                        headLineTemplate.RemoveAt(j);
                        lineCount--;
                        j--;
                        break;
                    }
                }
            }

            for (int i = 0; i < lineCount - 1; i++)
            {
                distance = glm.Distance(headLineTemplate[i], headLineTemplate[i + 1]);
                if (distance < spacing)
                {
                    headLineTemplate.RemoveAt(i + 1);
                    lineCount--;
                    i--;
                }
            }
        }

        private void btnSetDistance_Click(object sender, EventArgs e)
        {
            reset = false;
            double width = (double)nudSetDistance.Value * mf.ftOrMtoM;

            int endl = end;
            int startl = start;
            if (((headLineTemplate.Count - endl + startl) % headLineTemplate.Count) < ((headLineTemplate.Count - startl + endl) % headLineTemplate.Count)) { int idx = startl; startl = endl; endl = idx; }
            if (((headLineTemplate.Count - startl + endl) % headLineTemplate.Count) < 1) return;

            Build(width, startl, endl);
        }

        private void btnMakeFixedHeadland_Click(object sender, EventArgs e)
        {
            reset = false;
            double width = (double)nudDistance.Value * mf.ftOrMtoM;

            totalHeadlandWidth += width;
            lblHeadlandWidth.Text = (totalHeadlandWidth * mf.m2FtOrM).ToString("N2");

            Build(width, -1, -1);
        }

        private void cboxToolWidths_SelectedIndexChanged(object sender, EventArgs e)
        {
            reset = false;
            BuildHeadLineTemplate(false);
            double width = (Math.Round(mf.tool.toolWidth * cboxToolWidths.SelectedIndex, 1));

            lblHeadlandWidth.Text = (width * mf.m2FtOrM).ToString("N2");
            totalHeadlandWidth = width;

            Build(width, -1, -1);
        }

        private void Build(double width, int startl, int endl)
        {
            if (startl < 0) startl = 0;
            if (endl < 0) endl = headLineTemplate.Count;

            bool loop = endl < startl;
            List<vec3> NewList = new List<vec3>();

            for (int i = 0; i < headLineTemplate.Count; i++)
            {
                if ((loop && i > endl && i < startl) || (!loop && i < startl || i > endl))
                    NewList.Add(headLineTemplate[i]);
                else

                //calculate the point inside the boundary
                NewList.Add(new vec3(headLineTemplate[i].easting + (-Math.Sin(glm.PIBy2 + headLineTemplate[i].heading) * width),
                            headLineTemplate[i].northing + (-Math.Cos(glm.PIBy2 + headLineTemplate[i].heading) * width),
                            headLineTemplate[i].heading));
            }
            headLineTemplate = NewList;

            FixTurnLine(totalHeadlandWidth, mf.bnd.bndList[0].fenceLine.Points, 2);

            isSet = false;
            isA = true;
            start = end = -1;
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

            if (headLineTemplate.Count > 1)
            {
                GL.LineWidth(1);
                GL.Color3(0.20f, 0.96232f, 0.30f);
                GL.PointSize(2);
                GL.Begin(PrimitiveType.LineStrip);
                for (int h = 0; h < headLineTemplate.Count; h++) GL.Vertex3(headLineTemplate[h].easting, headLineTemplate[h].northing, 0);

                GL.Color3(0.60f, 0.9232f, 0.0f);
                GL.Vertex3(headLineTemplate[0].easting, headLineTemplate[0].northing, 0);
                GL.End();
            }

            GL.PointSize(8.0f);
            GL.Begin(PrimitiveType.Points);
            GL.Color3(0.95f, 0.90f, 0.0f);
            GL.Vertex3(mf.pivotAxlePos.easting, mf.pivotAxlePos.northing, 0.0);
            GL.End();

            DrawABTouchLine();

            GL.Flush();
            oglSelf.SwapBuffers();
        }

        private void oglSelf_MouseDown(object sender, MouseEventArgs e)
        {
            if (isSet)
            {
                isSet = false;
                start = end = -1;
                return;
            }

            Point pt = oglSelf.PointToClient(Cursor.Position);

            //convert screen coordinates to field coordinates
            vec2 plotPt = new vec2(mf.fieldCenterX + (pt.X - 350) / 350.0 * mf.minmax, mf.fieldCenterY + (350 - pt.Y) / 350.0 * mf.minmax);

            int A = -1;
            double minDistA = double.MaxValue;

            if (headLineTemplate.Count > 0)
            {
                //find the closest 2 points to current fix
                for (int t = 0; t < headLineTemplate.Count; t++)
                {
                    double dist = ((plotPt.easting - headLineTemplate[t].easting) * (plotPt.easting - headLineTemplate[t].easting))
                                    + ((plotPt.northing - headLineTemplate[t].northing) * (plotPt.northing - headLineTemplate[t].northing));
                    if (dist < minDistA)
                    {
                        minDistA = dist;
                        A = t;
                    }
                }
            }

            if (A >= 0 && isA)
            {
                start = A;
                end = -1;
                isA = false;
            }
            else if (A >= 0)
            {
                end = A;
                isA = true;
                isSet = true;
            }
            else
                start = end = -1;
        }

        private void DrawABTouchLine()
        {
            GL.PointSize(6);
            GL.Begin(PrimitiveType.Points);

            GL.Color3(0.990, 0.00, 0.250);
            if (start > -1) GL.Vertex3(headLineTemplate[start].easting, headLineTemplate[start].northing, 0);

            GL.Color3(0.990, 0.960, 0.250);
            if (end > -1) GL.Vertex3(headLineTemplate[end].easting, headLineTemplate[end].northing, 0);
            GL.End();

            if (start > -1 && end > -1 && headLineTemplate.Count > 1)
            {
                GL.Color3(0.965, 0.250, 0.950);
                //draw the turn line oject
                GL.LineWidth(2.0f);
                int start2 = start;
                int end2 = end;
                if (((headLineTemplate.Count - end2 + start2) % headLineTemplate.Count) < ((headLineTemplate.Count - start2 + end2) % headLineTemplate.Count)) { int index = start2; start2 = end2; end2 = index; }
                bool Loop = end2 < start2;

                GL.Begin(PrimitiveType.LineStrip);
                for (int i = start2; i <= end2 || Loop; i++)
                {
                    if (i >= headLineTemplate.Count)
                    {
                        i = -1;
                        Loop = false;
                        continue;
                    }
                    GL.Vertex3(headLineTemplate[i].easting, headLineTemplate[i].northing, 0);
                }
                GL.End();
            }
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            reset = true;
            BuildHeadLineTemplate(false);
        }

        private void nudDistance_Click(object sender, EventArgs e)
        {
            mf.KeypadToNUD((NumericUpDown)sender, this);
            btnExit.Focus();
        }

        private void nudSetDistance_Click(object sender, EventArgs e)
        {
            mf.KeypadToNUD((NumericUpDown)sender, this);
            btnExit.Focus();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            oglSelf.Refresh();
            if (isSet)
            {
                btnExit.Enabled = false;
                btnMakeFixedHeadland.Enabled = false;
                nudDistance.Enabled = false;

                nudSetDistance.Enabled = true;
                btnSetDistance.Enabled = true;
                //btnMoveLeft.Enabled = true;
                //btnMoveRight.Enabled = true;
                //btnMoveUp.Enabled = true;
                //btnMoveDown.Enabled = true;
                //btnDoneManualMove.Enabled = true;
                btnDeletePoints.Enabled = true;
                //btnStartUp.Enabled = true;
                //btnStartDown.Enabled = true;
                //btnEndDown.Enabled = true;
                //btnEndUp.Enabled = true;
            }
            else
            {
                nudSetDistance.Enabled = false;
                btnSetDistance.Enabled = false;
                //btnMoveLeft.Enabled = false;
                //btnMoveRight.Enabled = false;
                //btnMoveUp.Enabled = false;
                //btnMoveDown.Enabled = false;
                //btnDoneManualMove.Enabled = false;
                btnDeletePoints.Enabled = false;
                //btnStartUp.Enabled = false;
                //btnStartDown.Enabled = false;
                //btnEndDown.Enabled = false;
                //btnEndUp.Enabled = false;

                btnExit.Enabled = true;
                btnMakeFixedHeadland.Enabled = true;
                nudDistance.Enabled = true;
            }
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            mf.bnd.bndList[0].hdLine.Points.Clear();

            //does headland control sections
            mf.bnd.isSectionControlledByHeadland = cboxIsSectionControlled.Checked;
            Properties.Settings.Default.setHeadland_isSectionControlled = cboxIsSectionControlled.Checked;
            Properties.Settings.Default.Save();

            if (!reset)
                mf.bnd.bndList[0].hdLine.Points = headLineTemplate;

            mf.bnd.bndList[0].hdLine.ResetPoints = true;
            mf.FileSaveHeadland();
            isClosing = true;
            Close();
        }

        private void btnTurnOffHeadland_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnDeletePoints_Click(object sender, EventArgs e)
        {
            int start2 = start;
            int end2 = end;
            if (((headLineTemplate.Count - end2 + start2) % headLineTemplate.Count) < ((headLineTemplate.Count - start2 + end2) % headLineTemplate.Count)) { int index = start2; start2 = end2; end2 = index; }
            if (end2 < start2)
            {
                headLineTemplate.RemoveRange(start2, headLineTemplate.Count - start2);
                headLineTemplate.RemoveRange(0, end2);
            }
            else
                headLineTemplate.RemoveRange(start2, end2 - start2);

            start = end = -1;
            isA = true;
            isSet = false;
        }

        private void oglSelf_Load(object sender, EventArgs e)
        {
            oglSelf.MakeCurrent();
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.ClearColor(0.23122f, 0.2318f, 0.2315f, 1.0f);
        }

        private void oglSelf_Resize(object sender, EventArgs e)
        {
            oglSelf.MakeCurrent();

            Matrix4 projection = Matrix4.CreateOrthographicOffCenter(-mf.minmax, mf.minmax, -mf.minmax, mf.minmax, -1.0f, 1.0f);

            GL.MatrixMode(MatrixMode.Projection);//set state to load the projection matrix
            GL.LoadMatrix(ref projection);
            GL.MatrixMode(MatrixMode.Modelview);//set state to draw global coordinates into clip space;
        }

        #region Help
        private void cboxToolWidths_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.hh_cboxToolWidths, gStr.gsHelp);
        }

        private void nudDistance_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.hh_nudDistance, gStr.gsHelp);
        }

        private void btnMakeFixedHeadland_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.hh_btnMakeFixedHeadland, gStr.gsHelp);
        }

        private void nudSetDistance_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.hh_nudSetDistance, gStr.gsHelp);
        }

        private void btnSetDistance_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.hh_btnSetDistance, gStr.gsHelp);
        }

        private void btnDeletePoints_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.hh_btnDeletePoints, gStr.gsHelp);
        }

        private void cboxIsSectionControlled_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.hh_cboxIsSectionControlled, gStr.gsHelp);
        }

        private void btnReset_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.hh_btnReset, gStr.gsHelp);
        }

        private void btnTurnOffHeadland_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.hh_btnTurnOffHeadland, gStr.gsHelp);
        }

        private void btnExit_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            MessageBox.Show(gStr.hh_btnExit, gStr.gsHelp);
        }
        #endregion
    }
}

/*
            
            MessageBox.Show(gStr, gStr.gsHelp);

            DialogResult result2 = MessageBox.Show(gStr, gStr.gsHelp,
                MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (result2 == DialogResult.Yes)
            {
                System.Diagnostics.Process.Start("https://www.youtube.com/watch?v=rsJMRZrcuX4");
            }

*/
