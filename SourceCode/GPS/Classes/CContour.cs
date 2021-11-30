using System;

namespace AgOpenGPS
{
    public partial class CGuidance
    {
        public bool isLocked = false;

        public int stripNum, lastLockPt = int.MaxValue;

        public void BuildCurrentContourList(vec3 pivot)
        {
            lastSecond = mf.secondsSinceStart;
            int ptCount;
            double minDistA = double.MaxValue;
            int start, stop;

            int pt = 0;

            if (stripNum < 0 || !isLocked)
            {
                stripNum = -1;
                for (int s = 0; s < refList.Count; s++)
                {
                    if (refList[s].Mode.HasFlag(Mode.Contour))
                    {
                        //if making a new strip ignore the last part or it will win always
                        if (refList[s] == ContourIndex)
                            ptCount = refList[s].curvePts.Count - (int)Math.Max(30, mf.tool.toolWidth);
                        else
                            ptCount = refList[s].curvePts.Count;

                        if (ptCount < 2) continue;
                        double dist;
                        bool last = true;

                        for (int p = 0; p < ptCount || last; p += 6)
                        {
                            if (p >= ptCount)
                            {
                                last = false;
                                p = ptCount - 1;
                            }
                            dist = ((pivot.easting - refList[s].curvePts[p].easting) * (pivot.easting - refList[s].curvePts[p].easting))
                                + ((pivot.northing - refList[s].curvePts[p].northing) * (pivot.northing - refList[s].curvePts[p].northing));
                            if (dist < minDistA)
                            {
                                minDistA = dist;
                                stripNum = s;
                                lastLockPt = p;
                            }
                        }
                    }
                }
            }

            int currentStripBox = refList[stripNum] == ContourIndex ? (int)Math.Max(30, mf.tool.toolWidth) : 1;

            if (stripNum < 0 || (ptCount = refList[stripNum].curvePts.Count - currentStripBox) < 2)
            {
                curList.Clear();
                isLocked = false;
                return;
            }

            start = lastLockPt - 10; if (start < 0) start = 0;
            stop = lastLockPt + 10; if (stop > ptCount) stop = ptCount;

            //determine closest point
            double minDistance = double.MaxValue;

            for (int i = start; i < stop; i++)
            {
                double dist = ((pivot.easting - refList[stripNum].curvePts[i].easting) * (pivot.easting - refList[stripNum].curvePts[i].easting))
                    + ((pivot.northing - refList[stripNum].curvePts[i].northing) * (pivot.northing - refList[stripNum].curvePts[i].northing));

                if (dist < minDistance)
                {
                    minDistance = dist;
                    pt = lastLockPt = i;
                }
            }

            minDistance = Math.Sqrt(minDistance);

            if (minDistance > (isLocked ? 2.0 : 2.6) * mf.tool.toolWidth)
            {
                curList.Clear();
                isLocked = false;
                return;
            }

            //now we have closest point, the distance squared from it, and which patch and point its from
            
            double dy = refList[stripNum].curvePts[pt + 1].easting - refList[stripNum].curvePts[pt].easting;
            double dx = refList[stripNum].curvePts[pt + 1].northing - refList[stripNum].curvePts[pt].northing;

            //how far are we away from the reference line at 90 degrees - 2D cross product and distance
            double distanceFromRefLine = ((dx * pivot.easting) - (dy * pivot.northing) + (refList[stripNum].curvePts[pt + 1].easting
                                    * refList[stripNum].curvePts[pt].northing) - (refList[stripNum].curvePts[pt + 1].northing * refList[stripNum].curvePts[pt].easting))
                                    / Math.Sqrt((dx * dx) + (dy * dy));

            //double heading = Math.Atan2(dy, dx);
            //are we going same direction as stripList was created?
            if(!isLocked)
                isHeadingSameWay = Math.PI - Math.Abs(Math.Abs(mf.fixHeading - refList[stripNum].curvePts[pt].heading) - Math.PI) < 1.57;

            double RefDist = (distanceFromRefLine + (isHeadingSameWay ? mf.tool.toolOffset : -mf.tool.toolOffset)) / (mf.tool.toolWidth - mf.tool.toolOverlap);

            double howManyPathsAway;

            if (RefDist < 0) howManyPathsAway = (int)(RefDist - 0.5);
            else howManyPathsAway = (int)(RefDist + 0.5);

            if (howManyPathsAway >= -2 && howManyPathsAway <= 2)
            {

                curList.Clear();

                //don't guide behind yourself
                if (refList[stripNum] == ContourIndex && howManyPathsAway == 0) return;

                //make the new guidance line list called guideList
                ptCount = refList[stripNum].curvePts.Count;

                //shorter behind you
                if (isHeadingSameWay)
                {
                    start = pt - 6; if (start < 0) start = 0;
                    stop = pt + 45; if (stop > ptCount) stop = ptCount;
                }
                else
                {
                    start = pt - 45; if (start < 0) start = 0;
                    stop = pt + 6; if (stop > ptCount) stop = ptCount;
                }

                //if (howManyPathsAway != 0 && (mf.tool.halfToolWidth < (0.5*mf.tool.toolOffset)))
                {
                    double distAway = (mf.tool.toolWidth - mf.tool.toolOverlap) * howManyPathsAway + (isHeadingSameWay ? -mf.tool.toolOffset : mf.tool.toolOffset);
                    double distSqAway = (distAway * distAway) * 0.97;


                    for (int i = start; i < stop; i++)
                    {
                        vec3 point = new vec3(
                            refList[stripNum].curvePts[i].easting + (Math.Cos(refList[stripNum].curvePts[i].heading) * distAway),
                            refList[stripNum].curvePts[i].northing - (Math.Sin(refList[stripNum].curvePts[i].heading) * distAway),
                            refList[stripNum].curvePts[i].heading);

                        bool Add = true;
                        //make sure its not closer then 1 eq width
                        for (int j = start; j < stop; j++)
                        {
                            double check = glm.DistanceSquared(point.northing, point.easting, refList[stripNum].curvePts[j].northing, refList[stripNum].curvePts[j].easting);
                            if (check < distSqAway)
                            {
                                //Add = false;
                                break;
                            }
                        }
                        if (Add)
                        {
                            double dist = curList.Count > 0 ? ((point.easting - curList[curList.Count - 1].easting) * (point.easting - curList[curList.Count - 1].easting))
                                + ((point.northing - curList[curList.Count - 1].northing) * (point.northing - curList[curList.Count - 1].northing)) : 2.0;
                            if (dist > 0.3)
                                curList.Add(point);
                        }
                    }
                }

                int ptc = curList.Count;
                if (ptc < 5)
                {
                    curList.Clear();
                    isLocked = false;
                    return;
                }
            }
            else
            {
                curList.Clear();
                isLocked = false;
                return;
            }
        }

        //Add current position to stripList
        public void AddPoint(vec3 pivot)
        {
            if (ContourIndex == null)
            {
                ContourIndex = new CGuidanceLine(Mode.Contour);
                refList.Add(ContourIndex);
            }
            ContourIndex.curvePts.Add(new vec3(pivot.easting + Math.Cos(pivot.heading) * mf.tool.toolOffset, pivot.northing - Math.Sin(pivot.heading) * mf.tool.toolOffset, pivot.heading));
        }

        //End the strip
        public void StopContourLine()
        {
            //make sure its long enough to bother
            if (ContourIndex?.curvePts.Count > 5)
            {
                //build tale
                double head = ContourIndex.curvePts[0].heading;
                int length = (int)mf.tool.toolWidth + 3;
                vec3 pnt;
                for (int a = 0; a < length; a++)
                {
                    pnt.easting = ContourIndex.curvePts[0].easting - (Math.Sin(head));
                    pnt.northing = ContourIndex.curvePts[0].northing - (Math.Cos(head));
                    pnt.heading = ContourIndex.curvePts[0].heading;
                    ContourIndex.curvePts.Insert(0, pnt);
                }

                int ptc = ContourIndex.curvePts.Count - 1;
                head = ContourIndex.curvePts[ptc].heading;

                for (double i = 1; i < length; i += 1)
                {
                    pnt.easting = ContourIndex.curvePts[ptc].easting + (Math.Sin(head) * i);
                    pnt.northing = ContourIndex.curvePts[ptc].northing + (Math.Cos(head) * i);
                    pnt.heading = head;
                    ContourIndex.curvePts.Add(pnt);
                }

                //add the point list to the save list for appending to contour file
                mf.contourSaveList.Add(ContourIndex.curvePts);
            }
            else if (ContourIndex != null)
                refList.Remove(ContourIndex);

            ContourIndex = null;
        }

        //build contours for boundaries
        public void BuildFenceContours(int pass, int spacingInt)
        {
            if (mf.bnd.bndList.Count == 0)
            {
                mf.TimedMessageBox(1500, "Boundary Contour Error", "No Boundaries Made");
                return;
            }

            vec3 point = new vec3();
            double totalHeadWidth;
            int signPass;

            if (pass == 1)
            {
                signPass = -1;
                //determine how wide a headland space
                totalHeadWidth = ((mf.tool.toolWidth - mf.tool.toolOverlap) * 0.5) - spacingInt;
            }

            else
            {
                signPass = 1;
                totalHeadWidth = ((mf.tool.toolWidth - mf.tool.toolOverlap) * pass) + spacingInt +
                    ((mf.tool.toolWidth - mf.tool.toolOverlap) * 0.5);
            }

            //totalHeadWidth = (mf.tool.toolWidth - mf.tool.toolOverlap) * 0.5 + 0.2 + (mf.tool.toolWidth - mf.tool.toolOverlap);

            for (int j = 0; j < mf.bnd.bndList.Count; j++)
            {
                //count the points from the boundary
                int ptCount = mf.bnd.bndList[j].fenceLine.Points.Count;

                CGuidanceLine ptList = new CGuidanceLine(Mode.Contour | Mode.Boundary);

                for (int i = 0; i < ptCount; i++)
                {
                    //calculate the point inside the boundary
                    point.easting = mf.bnd.bndList[j].fenceLine.Points[i].easting - (signPass * Math.Sin(glm.PIBy2 + mf.bnd.bndList[j].fenceLine.Points[i].heading) * -totalHeadWidth);
                    point.northing = mf.bnd.bndList[j].fenceLine.Points[i].northing - (signPass * Math.Cos(glm.PIBy2 + mf.bnd.bndList[j].fenceLine.Points[i].heading) * -totalHeadWidth);

                    point.heading = mf.bnd.bndList[j].fenceLine.Points[i].heading - (j == 0 ? Math.PI : 0);
                    if (point.heading < -glm.twoPI) point.heading += glm.twoPI;

                    //only add if inside actual field boundary
                    ptList.curvePts.Add(point);
                }
                refList.Add(ptList);
            }

            mf.TimedMessageBox(1500, "Boundary Contour", "Contour Path Created");
        }

        //Reset the contour to zip
        public void ResetContour()
        {
            ContourIndex = null;
            for (int i = refList.Count - 1; i >= 0; i--)
            {
                if (refList[i].Mode.HasFlag(Mode.Contour))
                    refList.RemoveAt(i);
            }
        }
    }//class
}//namespace