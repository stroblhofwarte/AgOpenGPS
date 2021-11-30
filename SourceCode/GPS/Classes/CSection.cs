//Please, if you use this, share the improvements

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AgOpenGPS
{
    //each section is composed of a patchlist and triangle list
    //the triangle list makes up the individual triangles that make the block or patch of applied (green spot)
    //the patch list is a list of the list of triangles

    public class CSection
    {
        //copy of the mainform address
        private readonly FormGPS mf;

        //list of patch data individual triangles
        public List<vec3> triangleList = new List<vec3>();

        //is this section on or off
        public bool isSectionOn = false;

        public bool sectionOnRequest = false;
        private int index;
        public int sectionOverlapTimer = 0;

        //mapping
        public int mappingOffTimer = 0;
        public int mappingOnTimer = 0;
        public bool isMappingOn = false;

        public double speedPixels = 0;


        //the left side is always negative, right side is positive
        //so a section on the left side only would be -8, -4
        //in the center -4,4  on the right side only 4,8
        //reads from left to right
        //   ------========---------||----------========---------
        //        -8      -4      -1  1         4      8
        // in (meters)

        public double positionLeft = -4;
        public double positionRight = 4;
        public double sectionWidth = 0;

        public double foreDistance = 0;

        //used by readpixel to determine color in pixel array
        public int rpSectionWidth = 0;
        public int rpSectionPosition = 0;

        //points in world space that start and end of section are in
        public vec2 leftPoint;
        public vec2 rightPoint;

        //whether or not this section is in boundary, headland
        public bool isInBoundary = true, isHydLiftInWorkArea = true;
        public bool isInHeadlandArea = true;
        public bool isLookOnInHeadland = true;
        public int numTriangles = 0;

        //used to determine state of Manual section button - Off Auto On
        public btnStates manBtnState = btnStates.Off;
        public Button button;

        //simple constructor, position is set in GPSWinForm_Load in FormGPS when creating new object
        public CSection(FormGPS _f, int idx)
        {
            //constructor
            mf = _f;
            index = idx;

            button = new Button();

            button.BackColor = System.Drawing.Color.Silver;
            button.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            button.Enabled = false;
            button.FlatAppearance.BorderColor = System.Drawing.SystemColors.ActiveCaptionText;
            button.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            button.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            button.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            button.Location = new System.Drawing.Point(646, 178);
            button.Size = new System.Drawing.Size(34, 25);
            button.Text = (idx + 1).ToString();
            button.UseVisualStyleBackColor = false;
            button.Click += new System.EventHandler(this.button_Click);
        }

        private void button_Click(object sender, EventArgs e)
        {
            //if auto is off just have on-off for choices of section buttons
            if (manBtnState == btnStates.On)
                UpdateButton(btnStates.Off);
            else if (manBtnState == btnStates.Off && mf.autoBtnState == btnStates.Auto)
                UpdateButton(btnStates.Auto);
            else if (mf.autoBtnState != btnStates.Off)
                UpdateButton(btnStates.On);
        }

        public void UpdateButton(btnStates status, bool? enable = null)
        {
            if (mf.isDay)
                button.ForeColor = Color.Black;
            else
                button.ForeColor = Color.White;

            manBtnState = status;

            if (enable.HasValue)
            {
                button.Enabled = enable.Value;
                button.BackColor = enable.Value ? Color.Red : Color.Silver;
            }
            else if (manBtnState == btnStates.Auto)
            {
                if (mf.isDay)
                    button.BackColor = Color.Lime;
                else
                    button.BackColor = Color.ForestGreen;
            }
            else if (manBtnState == btnStates.On)
            {
                if (mf.isDay)
                    button.BackColor = Color.Yellow;
                else
                    button.BackColor = Color.DarkGoldenrod;
            }
            else if (manBtnState == btnStates.Off)
            {
                if (mf.isDay)
                    button.BackColor = Color.Red;
                else
                    button.BackColor = Color.Crimson;
            }
        }
        
        public void TurnMappingOn()
        {
            //do not tally square meters on inital point, that would be silly
            if (!isMappingOn)
            {
                //set the section bool to on
                isMappingOn = true;

                //starting a new patch chunk so create a new triangle list
                triangleList = new List<vec3>(32);

                if (!mf.tool.isMultiColoredSections)
                {
                    vec3 colur = new vec3(mf.sectionColorDay.R, mf.sectionColorDay.G, mf.sectionColorDay.B);
                    triangleList.Add(colur);
                }

                else
                {
                    vec3 collor = new vec3(mf.tool.secColors[index].R, mf.tool.secColors[index].G, mf.tool.secColors[index].B);
                    triangleList.Add(collor);
                }

                AddMappingPoint();
            }
        }

        public void TurnMappingOff()
        {
            AddMappingPoint();

            isMappingOn = false;

            if (triangleList.Count > 4)
            {
                //save the triangle list in a patch list to add to saving file
                mf.patchSaveList.Add(triangleList);
                mf.tool.patchList.Add(triangleList);
            }
            else
            {
                triangleList.Clear();
            }
        }

        //every time a new fix, a new patch point from last point to this point
        //only need prev point on the first points of triangle strip that makes a box (2 triangles)

        public void AddMappingPoint()
        {
            //add two triangles for next step.
            //left side
            vec3 point = new vec3(leftPoint.easting, leftPoint.northing, 0);

            //add the point to List
            triangleList.Add(point);

            //Right side
            vec3 point2 = new vec3(rightPoint.easting, rightPoint.northing, 0);

            //add the point to the list
            triangleList.Add(point2);

            //quick count
            int c = triangleList.Count - 1;

            //when closing a job the triangle patches all are emptied but the section delay keeps going.
            //Prevented by quick check. 4 points plus colour
            if (c >= 5)
            {
                //calculate area of these 2 new triangles - AbsoluteValue of (Ax(By-Cy) + Bx(Cy-Ay) + Cx(Ay-By)/2)
                {
                    double temp = (triangleList[c].easting * (triangleList[c - 1].northing - triangleList[c - 2].northing))
                              + (triangleList[c - 1].easting * (triangleList[c - 2].northing - triangleList[c].northing))
                                  + (triangleList[c - 2].easting * (triangleList[c].northing - triangleList[c - 1].northing));

                    temp = Math.Abs(temp / 2.0);
                    mf.fd.workedAreaTotal += temp;
                    mf.fd.workedAreaTotalUser += temp;

                    //temp = 0;
                    temp = (triangleList[c - 1].easting * (triangleList[c - 2].northing - triangleList[c - 3].northing))
                              + (triangleList[c - 2].easting * (triangleList[c - 3].northing - triangleList[c - 1].northing))
                                  + (triangleList[c - 3].easting * (triangleList[c - 1].northing - triangleList[c - 2].northing));

                    temp = Math.Abs(temp / 2.0);
                    mf.fd.workedAreaTotal += temp;
                    mf.fd.workedAreaTotalUser += temp;
                }

                if (c > 126)
                {
                    mf.tool.patchList.Add(triangleList);

                    //save the cutoff patch to be saved later
                    mf.patchSaveList.Add(triangleList);

                    triangleList = new List<vec3>(32);

                    //Add Patch colour
                    if (!mf.tool.isMultiColoredSections)
                        triangleList.Add(new vec3(mf.sectionColorDay.R, mf.sectionColorDay.G, mf.sectionColorDay.B));
                    else
                        triangleList.Add(new vec3(mf.tool.secColors[index].R, mf.tool.secColors[index].G, mf.tool.secColors[index].B));

                    //add the points to List, yes its more points, but breaks up patches for culling
                    triangleList.Add(point);
                    triangleList.Add(point2);
                }
            }
        }
    }
}