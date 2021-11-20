using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;

namespace AgOpenGPS
{
    public partial class CGuidance
    {
        public bool isSmoothWindowOpen;
        public bool isOkToAddDesPoints;
        public List<vec3> desList = new List<vec3>();

        public void BuildCurrentCurveList(vec3 pivot)
        {
            if (selectedCurveLine?.curvePts.Count > 1)
            {
                double minDistA = double.MaxValue, minDistB;
                //move the ABLine over based on the overlap amount set in vehicle
                double widthMinusOverlap = mf.tool.toolWidth - mf.tool.toolOverlap;

                int refCount = selectedCurveLine.curvePts.Count;

                //close call hit
                int cc = 0, dd;

                for (int j = 0; j < refCount; j += 10)
                {
                    double dist = ((mf.guidanceLookPos.easting - selectedCurveLine.curvePts[j].easting) * (mf.guidanceLookPos.easting - selectedCurveLine.curvePts[j].easting))
                                    + ((mf.guidanceLookPos.northing - selectedCurveLine.curvePts[j].northing) * (mf.guidanceLookPos.northing - selectedCurveLine.curvePts[j].northing));
                    if (dist < minDistA)
                    {
                        minDistA = dist;
                        cc = j;
                    }
                }

                minDistA = minDistB = double.MaxValue;

                dd = cc + 7; if (dd > refCount - 1) dd = refCount;
                cc -= 7; if (cc < 0) cc = 0;
                int rB = refCount; int rA = refCount;

                //find the closest 2 points to current close call
                for (int j = cc; j < dd; j++)
                {
                    double dist = ((mf.guidanceLookPos.easting - selectedCurveLine.curvePts[j].easting) * (mf.guidanceLookPos.easting - selectedCurveLine.curvePts[j].easting))
                                    + ((mf.guidanceLookPos.northing - selectedCurveLine.curvePts[j].northing) * (mf.guidanceLookPos.northing - selectedCurveLine.curvePts[j].northing));
                    if (dist < minDistA)
                    {
                        minDistB = minDistA;
                        rB = rA;
                        minDistA = dist;
                        rA = j;
                    }
                    else if (dist < minDistB)
                    {
                        minDistB = dist;
                        rB = j;
                    }
                }

                //reset the line over jump
                isLateralTriggered = false;

                if (rA >= refCount - 1 || rB >= refCount) return;

                if (rA > rB) { int C = rA; rA = rB; rB = C; }

                //same way as line creation or not
                isHeadingSameWay = Math.PI - Math.Abs(Math.Abs(pivot.heading - selectedCurveLine.curvePts[rA].heading) - Math.PI) < glm.PIBy2;

                if (mf.yt.isYouTurnTriggered) isHeadingSameWay = !isHeadingSameWay;

                //which side of the closest point are we on is next
                //calculate endpoints of reference line based on closest point

                //x2-x1
                double dx = selectedCurveLine.curvePts[rB].easting - selectedCurveLine.curvePts[rA].easting;
                //z2-z1
                double dz = selectedCurveLine.curvePts[rB].northing - selectedCurveLine.curvePts[rA].northing;

                //how far are we away from the reference line at 90 degrees - 2D cross product and distance
                double distanceFromRefLine = ((dz * mf.guidanceLookPos.easting) - (dx * mf.guidanceLookPos.northing) + (selectedCurveLine.curvePts[rB].easting
                                    * selectedCurveLine.curvePts[rA].northing) - (selectedCurveLine.curvePts[rB].northing * selectedCurveLine.curvePts[rA].easting))
                                    / Math.Sqrt((dz * dz) + (dx * dx));

                double RefDist = (distanceFromRefLine + (isHeadingSameWay ? mf.tool.toolOffset : -mf.tool.toolOffset)) / widthMinusOverlap;
                if (RefDist < 0) howManyPathsAway = (int)(RefDist - 0.5);
                else howManyPathsAway = (int)(RefDist + 0.5);

                if (!isValid || howManyPathsAway != oldHowManyPathsAway)
                {
                    oldHowManyPathsAway = howManyPathsAway;
                    //build current list
                    isValid = true;

                    //build the current line
                    curList.Clear();

                    double distAway = widthMinusOverlap * howManyPathsAway + (isHeadingSameWay ? -mf.tool.toolOffset : mf.tool.toolOffset);

                    double distSqAway = (distAway * distAway) - 0.01;

                    for (int i = 0; i < refCount - 1; i++)
                    {
                        vec3 point = new vec3(
                        selectedCurveLine.curvePts[i].easting + (Math.Sin(glm.PIBy2 + selectedCurveLine.curvePts[i].heading) * distAway),
                        selectedCurveLine.curvePts[i].northing + (Math.Cos(glm.PIBy2 + selectedCurveLine.curvePts[i].heading) * distAway),
                        selectedCurveLine.curvePts[i].heading);
                        bool Add = true;
                        for (int t = 0; t < refCount; t++)
                        {
                            double dist = ((point.easting - selectedCurveLine.curvePts[t].easting) * (point.easting - selectedCurveLine.curvePts[t].easting))
                                + ((point.northing - selectedCurveLine.curvePts[t].northing) * (point.northing - selectedCurveLine.curvePts[t].northing));
                            if (dist < distSqAway)
                            {
                                Add = false;
                                break;
                            }
                        }
                        if (Add)
                        {
                            if (curList.Count > 0)
                            {
                                double dist = ((point.easting - curList[curList.Count - 1].easting) * (point.easting - curList[curList.Count - 1].easting))
                                    + ((point.northing - curList[curList.Count - 1].northing) * (point.northing - curList[curList.Count - 1].northing));
                                if (dist > 1)
                                    curList.Add(point);
                            }
                            else curList.Add(point);
                        }
                    }

                    int cnt = curList.Count;
                    if (cnt > 6)
                    {
                        vec3[] arr = new vec3[cnt];
                        curList.CopyTo(arr);

                        for (int i = 1; i < (curList.Count - 1); i++)
                        {
                            arr[i].easting = (curList[i - 1].easting + curList[i].easting + curList[i + 1].easting) / 3;
                            arr[i].northing = (curList[i - 1].northing + curList[i].northing + curList[i + 1].northing) / 3;
                        }
                        curList.Clear();

                        for (int i = 0; i < (arr.Length - 1); i++)
                        {
                            arr[i].heading = Math.Atan2(arr[i + 1].easting - arr[i].easting, arr[i + 1].northing - arr[i].northing);
                            if (arr[i].heading < 0) arr[i].heading += glm.twoPI;
                            if (arr[i].heading >= glm.twoPI) arr[i].heading -= glm.twoPI;
                        }

                        arr[arr.Length - 1].heading = arr[arr.Length - 2].heading;


                        if (mf.tool.isToolTrailing)
                        {
                            //depending on hitch is different profile of draft
                            double hitch;
                            if (mf.tool.isToolTBT && mf.tool.toolTankTrailingHitchLength < 0)
                            {
                                hitch = mf.tool.toolTankTrailingHitchLength * 0.85;
                                hitch += mf.tool.toolTrailingHitchLength * 0.65;
                            }
                            else hitch = mf.tool.toolTrailingHitchLength * 1.0;// - mf.vehicle.wheelbase;

                            //move the line forward based on hitch length ratio
                            for (int i = 0; i < arr.Length; i++)
                            {
                                arr[i].easting -= Math.Sin(arr[i].heading) * (hitch);
                                arr[i].northing -= Math.Cos(arr[i].heading) * (hitch);
                            }

                            ////average the points over 3, center weighted
                            //for (int i = 1; i < arr.Length - 2; i++)
                            //{
                            //    arr2[i].easting = (arr[i - 1].easting + arr[i].easting + arr[i + 1].easting) / 3;
                            //    arr2[i].northing = (arr[i - 1].northing + arr[i].northing + arr[i + 1].northing) / 3;
                            //}

                            //recalculate the heading
                            for (int i = 0; i < (arr.Length - 1); i++)
                            {
                                arr[i].heading = Math.Atan2(arr[i + 1].easting - arr[i].easting, arr[i + 1].northing - arr[i].northing);
                                if (arr[i].heading < 0) arr[i].heading += glm.twoPI;
                                if (arr[i].heading >= glm.twoPI) arr[i].heading -= glm.twoPI;
                            }

                            arr[arr.Length - 1].heading = arr[arr.Length - 2].heading;
                        }

                        //replace the array 
                        //curList.AddRange(arr);
                        cnt = arr.Length;
                        double distance;
                        double spacing = 0.5;

                        //add the first point of loop - it will be p1
                        curList.Add(arr[0]);
                        curList.Add(arr[1]);

                        for (int i = 0; i < cnt - 3; i++)
                        {
                            // add p1
                            curList.Add(arr[i + 1]);

                            distance = glm.Distance(arr[i + 1], arr[i + 2]);

                            if (distance > spacing)
                            {
                                int loopTimes = (int)(distance / spacing + 1);
                                for (int j = 1; j < loopTimes; j++)
                                {
                                    vec3 pos = new vec3(glm.Catmull(j / (double)(loopTimes), arr[i], arr[i + 1], arr[i + 2], arr[i + 3]));
                                    curList.Add(pos);
                                }
                            }
                        }

                        curList.Add(arr[cnt - 2]);
                        curList.Add(arr[cnt - 1]);

                        //to calc heading based on next and previous points to give an average heading.
                        cnt = curList.Count;
                        arr = new vec3[cnt];
                        cnt--;
                        curList.CopyTo(arr);
                        curList.Clear();

                        //middle points
                        for (int i = 1; i < cnt; i++)
                        {
                            vec3 pt3 = arr[i];
                            pt3.heading = Math.Atan2(arr[i + 1].easting - arr[i - 1].easting, arr[i + 1].northing - arr[i - 1].northing);
                            if (pt3.heading < 0) pt3.heading += glm.twoPI;
                            curList.Add(pt3);
                        }
                    }
                    lastSecond = mf.secondsSinceStart;
                }
            }
            else
                curList.Clear();
        }

        public void DrawCurve()
        {
            GL.LineWidth(lineWidth);

            if (desList.Count > 0)
            {
                if (isSmoothWindowOpen)
                {
                    GL.Color3(0.930f, 0.92f, 0.260f);
                    GL.Begin(PrimitiveType.Lines);
                }
                else
                {
                    GL.Color3(0.95f, 0.42f, 0.750f);
                    GL.Begin(PrimitiveType.LineStrip);
                }
                for (int h = 0; h < desList.Count; h++) GL.Vertex3(desList[h].easting, desList[h].northing, 0);
                if (isOkToAddDesPoints)
                    GL.Vertex3(mf.pivotAxlePos.easting, mf.pivotAxlePos.northing, 0);
                GL.End();
            }
            else
            {
                if (selectedCurveLine?.curvePts.Count > 1)
                {
                    GL.Color3(0.96, 0.2f, 0.2f);
                    GL.Begin(PrimitiveType.Lines);
                    for (int h = 0; h < selectedCurveLine.curvePts.Count; h++) GL.Vertex3(selectedCurveLine.curvePts[h].easting, selectedCurveLine.curvePts[h].northing, 0);
                    GL.End();

                    //GL.PointSize(8.0f);
                    //GL.Begin(PrimitiveType.Points);
                    //GL.Color3(1.0f, 1.0f, 0.0f);
                    ////GL.Vertex3(goalPointAB.easting, goalPointAB.northing, 0.0);
                    //GL.Vertex3(rEastSteer, rNorthSteer, 0.0);
                    //GL.Color3(1.0f, 0.0f, 1.0f);
                    //GL.Vertex3(rEastPivot, rNorthPivot, 0.0);
                    //GL.End();
                    //GL.PointSize(1.0f);

                    if (mf.font.isFontOn && selectedCurveLine.curvePts.Count > 410)
                    {
                        GL.Color3(0.40f, 0.90f, 0.95f);
                        mf.font.DrawText3D(selectedCurveLine.curvePts[201].easting, selectedCurveLine.curvePts[201].northing, "&A");
                        mf.font.DrawText3D(selectedCurveLine.curvePts[selectedCurveLine.curvePts.Count - 200].easting, selectedCurveLine.curvePts[selectedCurveLine.curvePts.Count - 200].northing, "&B");
                    }
                }

                if (curList.Count > 1)
                {
                    GL.PointSize(2);

                    GL.Color3(0.95f, 0.2f, 0.95f);
                    GL.Begin(PrimitiveType.LineStrip);
                    for (int h = 0; h < curList.Count; h++) GL.Vertex3(curList[h].easting, curList[h].northing, 0);
                    GL.End();

                    if (mf.isPureDisplayOn && !mf.isStanleyUsed)
                    {
                        if (ppRadius < 200 && ppRadius > -200)
                        {
                            const int numSegments = 100;
                            double theta = glm.twoPI / numSegments;
                            double c = Math.Cos(theta);//precalculate the sine and cosine
                            double s = Math.Sin(theta);
                            double x = ppRadius;//we start at angle = 0
                            double y = 0;

                            GL.LineWidth(1);
                            GL.Color3(0.53f, 0.530f, 0.950f);
                            GL.Begin(PrimitiveType.LineLoop);
                            for (int ii = 0; ii < numSegments; ii++)
                            {
                                //glVertex2f(x + cx, y + cy);//output vertex
                                GL.Vertex3(x + radiusPoint.easting, y + radiusPoint.northing, 0);//output vertex
                                double t = x;//apply the rotation matrix
                                x = (c * x) - (s * y);
                                y = (s * t) + (c * y);
                            }
                            GL.End();
                        }

                        //Draw lookahead Point
                        GL.PointSize(8.0f);
                        GL.Begin(PrimitiveType.Points);
                        GL.Color3(1.0f, 0.95f, 0.195f);
                        GL.Vertex3(goalPoint.easting, goalPoint.northing, 0.0);
                        GL.End();
                    }
                }

                mf.yt.DrawYouTurn();
            }
            
            GL.PointSize(1.0f);


            //if (isEditing)
            //{
            //    int ptCount = refList.Count;
            //    if (refList.Count == 0) return;

            //    GL.LineWidth(lineWidth);
            //    GL.Color3(0.930f, 0.2f, 0.260f);
            //    GL.Begin(PrimitiveType.Lines);
            //    for (int h = 0; h < ptCount; h++) GL.Vertex3(refList[h].easting, refList[h].northing, 0);
            //    GL.End();

            //    //current line
            //    if (curList.Count > 0 && isCurveSet)
            //    {
            //        ptCount = curList.Count;
            //        GL.Color3(0.95f, 0.2f, 0.950f);
            //        GL.Begin(PrimitiveType.LineStrip);
            //        for (int h = 0; h < ptCount; h++) GL.Vertex3(curList[h].easting, curList[h].northing, 0);
            //        GL.End();
            //    }

        }

        public void BuildTram2()
        {
            mf.tram.BuildTramBnd();
            mf.tram.tramList.Clear();

            if (selectedCurveLine?.curvePts.Count > 1)
            {
                bool isBndExist = mf.bnd.bndList.Count != 0;

                double pass = 0.5;

                int refCount = selectedCurveLine.curvePts.Count;

                int cntr = 0;
                if (isBndExist) cntr = 1;

                for (int i = cntr; i <= mf.tram.passes; i++)
                {
                    double distSqAway = (mf.tram.tramWidth * (i + 0.5) - mf.tram.halfWheelTrack + mf.tool.halfToolWidth)
                            * (mf.tram.tramWidth * (i + 0.5) - mf.tram.halfWheelTrack + mf.tool.halfToolWidth) * 0.999999;

                    List<vec2> tramArr = new List<vec2>(128);

                    for (int j = 0; j < refCount; j += 1)
                    {
                        vec2 point = new vec2(
                        (Math.Sin(glm.PIBy2 + selectedCurveLine.curvePts[j].heading) *
                            ((mf.tram.tramWidth * (pass + i)) - mf.tram.halfWheelTrack + mf.tool.halfToolWidth)) + selectedCurveLine.curvePts[j].easting,
                        (Math.Cos(glm.PIBy2 + selectedCurveLine.curvePts[j].heading) *
                            ((mf.tram.tramWidth * (pass + i)) - mf.tram.halfWheelTrack + mf.tool.halfToolWidth)) + selectedCurveLine.curvePts[j].northing);

                        bool Add = true;
                        for (int t = 0; t < refCount; t++)
                        {
                            //distance check to be not too close to ref line
                            double dist = ((point.easting - selectedCurveLine.curvePts[t].easting) * (point.easting - selectedCurveLine.curvePts[t].easting))
                                + ((point.northing - selectedCurveLine.curvePts[t].northing) * (point.northing - selectedCurveLine.curvePts[t].northing));
                            if (dist < distSqAway)
                            {
                                Add = false;
                                break;
                            }
                        }
                        if (Add)
                        {
                            //a new point only every 2 meters
                            double dist = tramArr.Count > 0 ? ((point.easting - tramArr[tramArr.Count - 1].easting) * (point.easting - tramArr[tramArr.Count - 1].easting))
                                + ((point.northing - tramArr[tramArr.Count - 1].northing) * (point.northing - tramArr[tramArr.Count - 1].northing)) : 3.0;
                            if (dist > 2)
                            {
                                //if inside the boundary, add
                                if (!isBndExist || mf.bnd.bndList[0].fenceLineEar.IsPointInPolygon(point))
                                {
                                    tramArr.Add(point);
                                }
                            }
                        }
                    }
                    mf.tram.tramList.Add(tramArr);
                }

                for (int i = cntr; i <= mf.tram.passes; i++)
                {
                    double distSqAway = (mf.tram.tramWidth * (i + 0.5) + mf.tram.halfWheelTrack + mf.tool.halfToolWidth)
                            * (mf.tram.tramWidth * (i + 0.5) + mf.tram.halfWheelTrack + mf.tool.halfToolWidth) * 0.999999;

                    List<vec2> tramArr = new List<vec2>(128);

                    for (int j = 0; j < refCount; j += 1)
                    {
                        vec2 point = new vec2(
                        (Math.Sin(glm.PIBy2 + selectedCurveLine.curvePts[j].heading) *
                            ((mf.tram.tramWidth * (pass + i)) + mf.tram.halfWheelTrack + mf.tool.halfToolWidth)) + selectedCurveLine.curvePts[j].easting,
                        (Math.Cos(glm.PIBy2 + selectedCurveLine.curvePts[j].heading) *
                            ((mf.tram.tramWidth * (pass + i)) + mf.tram.halfWheelTrack + mf.tool.halfToolWidth)) + selectedCurveLine.curvePts[j].northing);

                        bool Add = true;
                        for (int t = 0; t < refCount; t++)
                        {
                            //distance check to be not too close to ref line
                            double dist = ((point.easting - selectedCurveLine.curvePts[t].easting) * (point.easting - selectedCurveLine.curvePts[t].easting))
                                + ((point.northing - selectedCurveLine.curvePts[t].northing) * (point.northing - selectedCurveLine.curvePts[t].northing));
                            if (dist < distSqAway)
                            {
                                Add = false;
                                break;
                            }
                        }
                        if (Add)
                        {
                            //a new point only every 2 meters
                            double dist = tramArr.Count > 0 ? ((point.easting - tramArr[tramArr.Count - 1].easting) * (point.easting - tramArr[tramArr.Count - 1].easting))
                                + ((point.northing - tramArr[tramArr.Count - 1].northing) * (point.northing - tramArr[tramArr.Count - 1].northing)) : 3.0;
                            if (dist > 2)
                            {
                                //if inside the boundary, add
                                if (!isBndExist || mf.bnd.bndList[0].fenceLineEar.IsPointInPolygon(point))
                                {
                                    tramArr.Add(point);
                                }
                            }
                        }
                    }
                    mf.tram.tramList.Add(tramArr);
                }
            }
        }

        //for calculating for display the averaged new line
        public void SmoothAB(int smPts)
        {
            if (selectedCurveLine?.curvePts.Count > 399)
            {
                //count the reference list of original curve
                int cnt = selectedCurveLine.curvePts.Count;

                //just go back if not very long
                if (cnt < 400) return;

                //the temp array
                vec3[] arr = new vec3[cnt];

                //read the points before and after the setpoint
                for (int s = 0; s < smPts / 2; s++)
                {
                    arr[s].easting = selectedCurveLine.curvePts[s].easting;
                    arr[s].northing = selectedCurveLine.curvePts[s].northing;
                    arr[s].heading = selectedCurveLine.curvePts[s].heading;
                }

                for (int s = cnt - (smPts / 2); s < cnt; s++)
                {
                    arr[s].easting = selectedCurveLine.curvePts[s].easting;
                    arr[s].northing = selectedCurveLine.curvePts[s].northing;
                    arr[s].heading = selectedCurveLine.curvePts[s].heading;
                }

                //average them - center weighted average
                for (int i = smPts / 2; i < cnt - (smPts / 2); i++)
                {
                    for (int j = -smPts / 2; j < smPts / 2; j++)
                    {
                        arr[i].easting += selectedCurveLine.curvePts[j + i].easting;
                        arr[i].northing += selectedCurveLine.curvePts[j + i].northing;
                    }
                    arr[i].easting /= smPts;
                    arr[i].northing /= smPts;
                    arr[i].heading = selectedCurveLine.curvePts[i].heading;
                }

                //make a list to draw
                desList.Clear();
                for (int i = 0; i < cnt; i++)
                {
                    desList.Add(arr[i]);
                }
            }
        }

        //turning the visual line into the real reference line to use
        public void SaveSmoothAsRefList()
        {
            if (selectedCurveLine != null)
            {
                //oops no smooth list generated
                int cnt = desList.Count;
                if (cnt == 0) return;

                //eek
                selectedCurveLine.curvePts.Clear();

                //copy to an array to calculate all the new headings
                vec3[] arr = new vec3[cnt];
                desList.CopyTo(arr);

                //calculate new headings on smoothed line
                for (int i = 1; i < cnt - 1; i++)
                {
                    arr[i].heading = Math.Atan2(arr[i + 1].easting - arr[i].easting, arr[i + 1].northing - arr[i].northing);
                    if (arr[i].heading < 0) arr[i].heading += glm.twoPI;
                    selectedCurveLine.curvePts.Add(arr[i]);
                }
            }
        }

        public void MoveABCurve(double dist)
        {
            isValid = false;
            lastSecond = 0;

            if (selectedCurveLine != null)
            {
                int cnt = selectedCurveLine.curvePts.Count;
                vec3[] arr = new vec3[cnt];
                selectedCurveLine.curvePts.CopyTo(arr);
                selectedCurveLine.curvePts.Clear();

                moveDistance += isHeadingSameWay ? dist : -dist;

                for (int i = 0; i < cnt; i++)
                {
                    arr[i].easting += Math.Cos(arr[i].heading) * (isHeadingSameWay ? dist : -dist);
                    arr[i].northing -= Math.Sin(arr[i].heading) * (isHeadingSameWay ? dist : -dist);
                    selectedCurveLine.curvePts.Add(arr[i]);
                }
            }
        }

        public bool PointOnLine(vec3 pt1, vec3 pt2, vec3 pt)
        {
            vec2 r = new vec2(0, 0);
            if (pt1.northing == pt2.northing && pt1.easting == pt2.easting) { pt1.northing -= 0.00001; }

            double U = ((pt.northing - pt1.northing) * (pt2.northing - pt1.northing)) + ((pt.easting - pt1.easting) * (pt2.easting - pt1.easting));

            double Udenom = Math.Pow(pt2.northing - pt1.northing, 2) + Math.Pow(pt2.easting - pt1.easting, 2);

            U /= Udenom;

            r.northing = pt1.northing + (U * (pt2.northing - pt1.northing));
            r.easting = pt1.easting + (U * (pt2.easting - pt1.easting));

            double minx, maxx, miny, maxy;

            minx = Math.Min(pt1.northing, pt2.northing);
            maxx = Math.Max(pt1.northing, pt2.northing);

            miny = Math.Min(pt1.easting, pt2.easting);
            maxy = Math.Max(pt1.easting, pt2.easting);
            return _ = r.northing >= minx && r.northing <= maxx && (r.easting >= miny && r.easting <= maxy);
        }
    }
}