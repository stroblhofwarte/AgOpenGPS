using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;

namespace AgOpenGPS
{
    public class CContour
    {
        //copy of the mainform address
        private readonly FormGPS mf;

        public bool isContourOn, isContourBtnOn, isLocked = false;

        private int stripNum, lastLockPt = int.MaxValue, backSpacing = 30, Index = -1;

        //list of the list of individual Lines for entire field
        public List<List<vec3>> stripList = new List<List<vec3>>();

        //list of points for the new contour line
        public List<vec3> ctList = new List<vec3>(128);

        //constructor
        public CContour(FormGPS _f)
        {
            mf = _f;
        }

        public void BuildCurrentContourList(vec3 pivot)
        {
            mf.gyd.lastSecond = mf.secondsSinceStart;
            int ptCount;
            double minDistA = double.MaxValue;
            int start, stop;

            int pt = 0;

            if (stripNum < 0 || !isLocked)
            {
                stripNum = -1;
                for (int s = 0; s < stripList.Count; s++)
                {
                    int p;

                    //if making a new strip ignore the last part or it will win always
                    if (s == Index)
                        ptCount = stripList[s].Count - (int)Math.Max(backSpacing, mf.tool.toolWidth);
                    else
                        ptCount = stripList[s].Count;

                    if (ptCount < 2) continue;
                    double dist;
                    bool last = true;

                    for (p = 0; p < ptCount || last; p += 6)
                    {
                        if (p >= ptCount)
                        {
                            last = false;
                            p = ptCount - 1;
                        }
                        dist = ((pivot.easting - stripList[s][p].easting) * (pivot.easting - stripList[s][p].easting))
                            + ((pivot.northing - stripList[s][p].northing) * (pivot.northing - stripList[s][p].northing));
                        if (dist < minDistA)
                        {
                            minDistA = dist;
                            stripNum = s;
                            lastLockPt = p;
                        }
                    }
                }
            }

            int currentStripBox = stripNum == Index ? (int)Math.Max(backSpacing, mf.tool.toolWidth) : 1;

            if (stripNum < 0 || (ptCount = stripList[stripNum].Count - currentStripBox) < 2)
            {
                ctList.Clear();
                isLocked = false;
                return;
            }

            start = lastLockPt - 10; if (start < 0) start = 0;
            stop = lastLockPt + 10; if (stop > ptCount) stop = ptCount;

            //determine closest point
            double minDistance = double.MaxValue;

            for (int i = start; i < stop; i++)
            {
                double dist = ((pivot.easting - stripList[stripNum][i].easting) * (pivot.easting - stripList[stripNum][i].easting))
                    + ((pivot.northing - stripList[stripNum][i].northing) * (pivot.northing - stripList[stripNum][i].northing));

                if (dist < minDistance)
                {
                    minDistance = dist;
                    pt = lastLockPt = i;
                }
            }

            minDistance = Math.Sqrt(minDistance);

            if (minDistance > (isLocked ? 2.0 : 2.6) * mf.tool.toolWidth)
            {
                ctList.Clear();
                isLocked = false;
                return;
            }

            //now we have closest point, the distance squared from it, and which patch and point its from
            
            double dy = stripList[stripNum][pt + 1].easting - stripList[stripNum][pt].easting;
            double dx = stripList[stripNum][pt + 1].northing - stripList[stripNum][pt].northing;

            //how far are we away from the reference line at 90 degrees - 2D cross product and distance
            double distanceFromRefLine = ((dx * pivot.easting) - (dy * pivot.northing) + (stripList[stripNum][pt + 1].easting
                                    * stripList[stripNum][pt].northing) - (stripList[stripNum][pt + 1].northing * stripList[stripNum][pt].easting))
                                    / Math.Sqrt((dx * dx) + (dy * dy));

            double heading = Math.Atan2(dy, dx);
            //are we going same direction as stripList was created?
            bool isSameWay = Math.PI - Math.Abs(Math.Abs(mf.fixHeading - stripList[stripNum][pt].heading) - Math.PI) < 1.57;

            double RefDist = (distanceFromRefLine + (isSameWay ? mf.tool.toolOffset : -mf.tool.toolOffset)) / (mf.tool.toolWidth - mf.tool.toolOverlap);

            double howManyPathsAway;

            if (RefDist < 0) howManyPathsAway = (int)(RefDist - 0.5);
            else howManyPathsAway = (int)(RefDist + 0.5);

            if (howManyPathsAway >= -2 && howManyPathsAway <= 2)
            {

                ctList.Clear();

                //don't guide behind yourself
                if (stripNum == Index && howManyPathsAway == 0) return;

                //make the new guidance line list called guideList
                ptCount = stripList[stripNum].Count;

                //shorter behind you
                if (isSameWay)
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
                    double distAway = (mf.tool.toolWidth - mf.tool.toolOverlap) * howManyPathsAway + (isSameWay ? -mf.tool.toolOffset : mf.tool.toolOffset);
                    double distSqAway = (distAway * distAway) * 0.97;


                    for (int i = start; i < stop; i++)
                    {
                        vec3 point = new vec3(
                            stripList[stripNum][i].easting + (Math.Cos(stripList[stripNum][i].heading) * distAway),
                            stripList[stripNum][i].northing - (Math.Sin(stripList[stripNum][i].heading) * distAway),
                            stripList[stripNum][i].heading);

                        bool Add = true;
                        //make sure its not closer then 1 eq width
                        for (int j = start; j < stop; j++)
                        {
                            double check = glm.DistanceSquared(point.northing, point.easting, stripList[stripNum][j].northing, stripList[stripNum][j].easting);
                            if (check < distSqAway)
                            {
                                //Add = false;
                                break;
                            }
                        }
                        if (Add)
                        {
                            double dist = ctList.Count > 0 ? ((point.easting - ctList[ctList.Count - 1].easting) * (point.easting - ctList[ctList.Count - 1].easting))
                                + ((point.northing - ctList[ctList.Count - 1].northing) * (point.northing - ctList[ctList.Count - 1].northing)) : 2.0;
                            if (dist > 0.3)
                                ctList.Add(point);
                        }
                    }
                }

                int ptc = ctList.Count;
                if (ptc < 5)
                {
                    ctList.Clear();
                    isLocked = false;
                    return;
                }
            }
            else
            {
                ctList.Clear();
                isLocked = false;
                return;
            }
        }

        //determine distance from contour guidance line
        public void GetCurrentContourLine(vec3 pivot, vec3 steer)
        {
            //build new current ref line if required
            if ((ctList.Count < 9 && (mf.secondsSinceStart - mf.gyd.lastSecond) > 0.66) || (mf.secondsSinceStart - mf.gyd.lastSecond) > 2.0)
                BuildCurrentContourList(pivot);

            double minDistA = 1000000, minDistB = 1000000;
            int ptCount = ctList.Count;
            //distanceFromCurrentLine = 9999;
            if (ptCount > 8)
            {
                if (mf.isStanleyUsed)
                {
                    //find the closest 2 points to current fix
                    for (int t = 0; t < ptCount; t++)
                    {
                        double dist = ((steer.easting - ctList[t].easting) * (steer.easting - ctList[t].easting))
                                        + ((steer.northing - ctList[t].northing) * (steer.northing - ctList[t].northing));
                        if (dist < minDistA)
                        {
                            minDistB = minDistA;
                            mf.gyd.sB = mf.gyd.sA;
                            minDistA = dist;
                            mf.gyd.sA = t;
                        }
                        else if (dist < minDistB)
                        {
                            minDistB = dist;
                            mf.gyd.sB = t;
                        }
                    }

                    //just need to make sure the points continue ascending in list order or heading switches all over the place
                    if (mf.gyd.sA > mf.gyd.sB) { int C = mf.gyd.sA; mf.gyd.sA = mf.gyd.sB; mf.gyd.sB = C; }

                    //get the distance from currently active AB line
                    //x2-x1
                    double dx = ctList[mf.gyd.sB].easting - ctList[mf.gyd.sA].easting;
                    //z2-z1
                    double dy = ctList[mf.gyd.sB].northing - ctList[mf.gyd.sA].northing;

                    if (Math.Abs(dx) < double.Epsilon && Math.Abs(dy) < double.Epsilon) return;

                    //how far from current AB Line is fix
                    mf.gyd.distanceFromCurrentLinePivot = ((dy * steer.easting) - (dx * steer.northing) + (ctList[mf.gyd.sB].easting
                                * ctList[mf.gyd.sA].northing) - (ctList[mf.gyd.sB].northing * ctList[mf.gyd.sA].easting))
                                    / Math.Sqrt((dy * dy) + (dx * dx));

                    double abHeading = Math.Atan2(dx, dy);
                    if (abHeading < 0) abHeading += glm.twoPI;

                    mf.gyd.isHeadingSameWay = Math.PI - Math.Abs(Math.Abs(pivot.heading - abHeading) - Math.PI) < glm.PIBy2;

                    // calc point on ABLine closest to current position
                    double U = (((steer.easting - ctList[mf.gyd.sA].easting) * dx) + ((steer.northing - ctList[mf.gyd.sA].northing) * dy))
                                / ((dx * dx) + (dy * dy));

                    mf.gyd.rEast = ctList[mf.gyd.sA].easting + (U * dx);
                    mf.gyd.rNorth = ctList[mf.gyd.sA].northing + (U * dy);

                    //distance is negative if on left, positive if on right
                    double abFixHeadingDelta = steer.heading - abHeading;
                    if (!mf.gyd.isHeadingSameWay)
                    {
                        mf.gyd.distanceFromCurrentLinePivot *= -1.0;
                        abFixHeadingDelta += Math.PI;
                    }

                    //Fix the circular error
                    if (abFixHeadingDelta > Math.PI) abFixHeadingDelta -= Math.PI;
                    else if (abFixHeadingDelta < Math.PI) abFixHeadingDelta += Math.PI;

                    if (abFixHeadingDelta > glm.PIBy2) abFixHeadingDelta -= Math.PI;
                    else if (abFixHeadingDelta < -glm.PIBy2) abFixHeadingDelta += Math.PI;

                    if (mf.isReverse) abFixHeadingDelta *= -1;

                    abFixHeadingDelta *= mf.vehicle.stanleyHeadingErrorGain;
                    if (abFixHeadingDelta > 0.74) abFixHeadingDelta = 0.74;
                    if (abFixHeadingDelta < -0.74) abFixHeadingDelta = -0.74;

                    mf.gyd.steerAngle = Math.Atan((mf.gyd.distanceFromCurrentLinePivot * mf.vehicle.stanleyDistanceErrorGain)
                        / ((Math.Abs(mf.pn.speed) * 0.277777) + 1));

                    if (mf.gyd.steerAngle > 0.74) mf.gyd.steerAngle = 0.74;
                    if (mf.gyd.steerAngle < -0.74) mf.gyd.steerAngle = -0.74;

                    mf.gyd.steerAngle = glm.toDegrees((mf.gyd.steerAngle + abFixHeadingDelta) * -1.0);

                    if (mf.gyd.steerAngle < -mf.vehicle.maxSteerAngle) mf.gyd.steerAngle = -mf.vehicle.maxSteerAngle;
                    if (mf.gyd.steerAngle > mf.vehicle.maxSteerAngle) mf.gyd.steerAngle = mf.vehicle.maxSteerAngle;
                }
                else
                {
                    //find the closest 2 points to current fix
                    for (int t = 0; t < ptCount; t++)
                    {
                        double dist = ((pivot.easting - ctList[t].easting) * (pivot.easting - ctList[t].easting))
                                        + ((pivot.northing - ctList[t].northing) * (pivot.northing - ctList[t].northing));
                        if (dist < minDistA)
                        {
                            minDistB = minDistA;
                            mf.gyd.pB = mf.gyd.pA;
                            minDistA = dist;
                            mf.gyd.pA = t;
                        }
                        else if (dist < minDistB)
                        {
                            minDistB = dist;
                            mf.gyd.pB = t;
                        }
                    }


                    //just need to make sure the points continue ascending in list order or heading switches all over the place
                    if (mf.gyd.pA > mf.gyd.pB) { int C = mf.gyd.pA; mf.gyd.pA = mf.gyd.pB; mf.gyd.pB = C; }

                    if (isLocked &&  (mf.gyd.pA < 2 || mf.gyd.pB > ptCount - 3))
                    {
                        //ctList.Clear();
                        isLocked = false;
                        lastLockPt = int.MaxValue;
                        return;
                    }

                    //get the distance from currently active AB line
                    //x2-x1
                    double dx = ctList[mf.gyd.pB].easting - ctList[mf.gyd.pA].easting;
                    //z2-z1
                    double dy = ctList[mf.gyd.pB].northing - ctList[mf.gyd.pA].northing;

                    if (Math.Abs(dx) < double.Epsilon && Math.Abs(dy) < double.Epsilon) return;

                    //how far from current AB Line is fix
                    mf.gyd.distanceFromCurrentLinePivot = ((dy * mf.pn.fix.easting) - (dx * mf.pn.fix.northing) + (ctList[mf.gyd.pB].easting
                                * ctList[mf.gyd.pA].northing) - (ctList[mf.gyd.pB].northing * ctList[mf.gyd.pA].easting))
                                    / Math.Sqrt((dy * dy) + (dx * dx));

                    //integral slider is set to 0
                    if (mf.vehicle.purePursuitIntegralGain != 0)
                    {
                        mf.gyd.pivotDistanceError = mf.gyd.distanceFromCurrentLinePivot * 0.2 + mf.gyd.pivotDistanceError * 0.8;

                        if (mf.gyd.counter2++ > 4)
                        {
                            mf.gyd.pivotDerivative = mf.gyd.pivotDistanceError - mf.gyd.pivotDistanceErrorLast;
                            mf.gyd.pivotDistanceErrorLast = mf.gyd.pivotDistanceError;
                            mf.gyd.counter2 = 0;
                            mf.gyd.pivotDerivative *= 2;

                            //limit the derivative
                            //if (pivotDerivative > 0.03) pivotDerivative = 0.03;
                            //if (pivotDerivative < -0.03) pivotDerivative = -0.03;
                            //if (Math.Abs(pivotDerivative) < 0.01) pivotDerivative = 0;
                        }

                        //pivotErrorTotal = pivotDistanceError + pivotDerivative;

                        if (mf.isAutoSteerBtnOn
                            && Math.Abs(mf.gyd.pivotDerivative) < (0.1)
                            && mf.avgSpeed > 2.5
                            && !mf.yt.isYouTurnTriggered)
                        {
                            //if over the line heading wrong way, rapidly decrease integral
                            if ((mf.gyd.inty < 0 && mf.gyd.distanceFromCurrentLinePivot < 0) || (mf.gyd.inty > 0 && mf.gyd.distanceFromCurrentLinePivot > 0))
                            {
                                mf.gyd.inty += mf.gyd.pivotDistanceError * mf.vehicle.purePursuitIntegralGain * -0.06;
                            }
                            else
                            {
                                if (Math.Abs(mf.gyd.distanceFromCurrentLinePivot) > 0.02)
                                {
                                    mf.gyd.inty += mf.gyd.pivotDistanceError * mf.vehicle.purePursuitIntegralGain * -0.02;
                                    if (mf.gyd.inty > 0.2) mf.gyd.inty = 0.2;
                                    else if (mf.gyd.inty < -0.2) mf.gyd.inty = -0.2;
                                }
                            }
                        }
                        else mf.gyd.inty *= 0.95;
                    }
                    else mf.gyd.inty = 0;

                    if (mf.isReverse) mf.gyd.inty = 0;


                    mf.gyd.isHeadingSameWay = Math.PI - Math.Abs(Math.Abs(pivot.heading - ctList[mf.gyd.pA].heading) - Math.PI) < glm.PIBy2;

                    if (!mf.gyd.isHeadingSameWay)
                        mf.gyd.distanceFromCurrentLinePivot *= -1.0;

                    // ** Pure pursuit ** - calc point on ABLine closest to current position
                    double U = (((pivot.easting - ctList[mf.gyd.pA].easting) * dx) + ((pivot.northing - ctList[mf.gyd.pA].northing) * dy))
                            / ((dx * dx) + (dy * dy));

                    mf.gyd.rEast = ctList[mf.gyd.pA].easting + (U * dx);
                    mf.gyd.rNorth = ctList[mf.gyd.pA].northing + (U * dy);


                    //update base on autosteer settings and distance from line
                    double goalPointDistance = mf.vehicle.UpdateGoalPointDistance();

                    bool ReverseHeading = mf.isReverse ? !mf.gyd.isHeadingSameWay : mf.gyd.isHeadingSameWay;

                    int count = ReverseHeading ? 1 : -1;
                    vec3 start = new vec3(mf.gyd.rEast, mf.gyd.rNorth, 0);
                    double distSoFar = 0;

                    for (int i = ReverseHeading ? mf.gyd.pB : mf.gyd.pA; i < ptCount && i >= 0; i += count)
                    {
                        // used for calculating the length squared of next segment.
                        double tempDist = glm.Distance(start, ctList[i]);

                        //will we go too far?
                        if ((tempDist + distSoFar) > goalPointDistance)
                        {
                            double j = (goalPointDistance - distSoFar) / tempDist; // the remainder to yet travel

                            mf.gyd.goalPoint.easting = (((1 - j) * start.easting) + (j * ctList[i].easting));
                            mf.gyd.goalPoint.northing = (((1 - j) * start.northing) + (j * ctList[i].northing));
                            break;
                        }
                        else distSoFar += tempDist;
                        start = ctList[i];
                    }

                    //calc "D" the distance from pivot axle to lookahead point
                    double goalPointDistanceSquared = glm.DistanceSquared(mf.gyd.goalPoint.northing, mf.gyd.goalPoint.easting, pivot.northing, pivot.easting);

                    //calculate the the delta x in local coordinates and steering angle degrees based on wheelbase
                    double localHeading;// = glm.twoPI - mf.fixHeading;

                    if (mf.gyd.isHeadingSameWay) localHeading = glm.twoPI - mf.fixHeading + mf.gyd.inty;
                    else localHeading = glm.twoPI - mf.fixHeading - mf.gyd.inty;

                    mf.gyd.steerAngle = glm.toDegrees(Math.Atan(2 * (((mf.gyd.goalPoint.easting - pivot.easting) * Math.Cos(localHeading))
                        + ((mf.gyd.goalPoint.northing - pivot.northing) * Math.Sin(localHeading))) * mf.vehicle.wheelbase / goalPointDistanceSquared));

                    if (mf.ahrs.imuRoll != 88888)
                        mf.gyd.steerAngle += mf.ahrs.imuRoll * -mf.gyd.sideHillCompFactor;

                    if (mf.gyd.steerAngle < -mf.vehicle.maxSteerAngle) mf.gyd.steerAngle = -mf.vehicle.maxSteerAngle;
                    if (mf.gyd.steerAngle > mf.vehicle.maxSteerAngle) mf.gyd.steerAngle = mf.vehicle.maxSteerAngle;

                    //angular velocity in rads/sec  = 2PI * m/sec * radians/meters
                    double angVel = glm.twoPI * 0.277777 * mf.pn.speed * (Math.Tan(glm.toRadians(mf.gyd.steerAngle))) / mf.vehicle.wheelbase;

                    //clamp the steering angle to not exceed safe angular velocity
                    if (Math.Abs(angVel) > mf.vehicle.maxAngularVelocity)
                    {
                        mf.gyd.steerAngle = glm.toDegrees(mf.gyd.steerAngle > 0 ?
                                (Math.Atan((mf.vehicle.wheelbase * mf.vehicle.maxAngularVelocity) / (glm.twoPI * mf.pn.speed * 0.277777)))
                            : (Math.Atan((mf.vehicle.wheelbase * -mf.vehicle.maxAngularVelocity) / (glm.twoPI * mf.pn.speed * 0.277777))));
                    }
                }

                //fill in the autosteer variables
                mf.guidanceLineDistanceOff = (short)Math.Round(mf.gyd.distanceFromCurrentLinePivot * 1000.0, MidpointRounding.AwayFromZero);
                mf.guidanceLineSteerAngle = (short)(mf.gyd.steerAngle * 100);
            }
            else
            {
                //invalid distance so tell AS module
                mf.gyd.distanceFromCurrentLinePivot = 32000;
                mf.guidanceLineDistanceOff = 32000;
            }
        }

        //Add current position to stripList
        public void AddPoint(vec3 pivot)
        {
            if (!isContourOn)
            {
                isContourOn = true;

                stripList.Add(new List<vec3>());
                Index = stripList.Count - 1;
            }
            else if (Index > -1)
                stripList[Index].Add(new vec3(pivot.easting + Math.Cos(pivot.heading) * mf.tool.toolOffset, pivot.northing - Math.Sin(pivot.heading) * mf.tool.toolOffset, pivot.heading));
        }

        //End the strip
        public void StopContourLine()
        {
            //make sure its long enough to bother
            if (Index > -1 && stripList[Index].Count > 5)
            {
                //build tale
                double head = stripList[Index][0].heading;
                int length = (int)mf.tool.toolWidth+3;
                vec3 pnt;
                for (int a = 0; a < length; a ++)
                {
                    pnt.easting = stripList[Index][0].easting - (Math.Sin(head));
                    pnt.northing = stripList[Index][0].northing - (Math.Cos(head));
                    pnt.heading = stripList[Index][0].heading;
                    stripList[Index].Insert(0, pnt);
                }

                int ptc = stripList[Index].Count - 1;
                head = stripList[Index][ptc].heading;

                for (double i = 1; i < length; i += 1)
                {
                    pnt.easting = stripList[Index][ptc].easting + (Math.Sin(head) * i);
                    pnt.northing = stripList[Index][ptc].northing + (Math.Cos(head) * i);
                    pnt.heading = head;
                    stripList[Index].Add(pnt);
                }

                //add the point list to the save list for appending to contour file
                mf.contourSaveList.Add(stripList[Index]);

                stripList[Index] = new List<vec3>(32);
                stripList.Add(stripList[Index]);

            }

            //delete ptList
            else if (Index > -1)
                stripList.RemoveAt(Index);

            Index = -1;
            //turn it off
            isContourOn = false;
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

                List<vec3> ptList = new List<vec3>(ptCount);

                for (int i = 0; i < ptCount; i++)
                {
                    //calculate the point inside the boundary
                    point.easting = mf.bnd.bndList[j].fenceLine.Points[i].easting - (signPass * Math.Sin(glm.PIBy2 + mf.bnd.bndList[j].fenceLine.Points[i].heading) * -totalHeadWidth);
                    point.northing = mf.bnd.bndList[j].fenceLine.Points[i].northing - (signPass * Math.Cos(glm.PIBy2 + mf.bnd.bndList[j].fenceLine.Points[i].heading) * -totalHeadWidth);

                    point.heading = mf.bnd.bndList[j].fenceLine.Points[i].heading - (j == 0 ? Math.PI : 0);
                    if (point.heading < -glm.twoPI) point.heading += glm.twoPI;

                    //only add if inside actual field boundary
                    ptList.Add(point);
                }
                stripList.Add(ptList);
            }

            mf.TimedMessageBox(1500, "Boundary Contour", "Contour Path Created");
        }

        //draw the red follow me line
        public void DrawContourLine()
        {
            ////draw the guidance line
            int ptCount = ctList.Count;
            if (ptCount < 2) return;
            GL.LineWidth(mf.ABLine.lineWidth);
            GL.Color3(0.98f, 0.2f, 0.980f);
            GL.Begin(PrimitiveType.LineStrip);
            for (int h = 0; h < ptCount; h++) GL.Vertex3(ctList[h].easting, ctList[h].northing, 0);
            GL.End();

            GL.PointSize(mf.ABLine.lineWidth);
            GL.Begin(PrimitiveType.Points);

            GL.Color3(0.87f, 08.7f, 0.25f);
            for (int h = 0; h < ptCount; h++) GL.Vertex3(ctList[h].easting, ctList[h].northing, 0);

            GL.End();

            //Draw the captured ref strip, red if locked
            if (isLocked)
            {
                GL.Color3(0.983f, 0.2f, 0.20f);
                GL.LineWidth(4);
            }
            else
            {
                GL.Color3(0.3f, 0.982f, 0.0f);
                GL.LineWidth(mf.ABLine.lineWidth);
            }

            //GL.PointSize(6.0f);
            GL.Begin(PrimitiveType.Points);
            for (int h = 0; h < stripList[stripNum].Count; h++) GL.Vertex3(stripList[stripNum][h].easting, stripList[stripNum][h].northing, 0);
            GL.End();

            //GL.Begin(PrimitiveType.Points);
            //GL.Color3(1.0f, 0.95f, 0.095f);
            //GL.Vertex3(rEastCT, rNorthCT, 0.0);
            //GL.End();
            //GL.PointSize(1.0f);

            //GL.Color3(0.98f, 0.98f, 0.50f);
            //GL.Begin(PrimitiveType.LineStrip);
            //GL.Vertex3(boxE.easting, boxE.northing, 0);
            //GL.Vertex3(boxA.easting, boxA.northing, 0);
            //GL.Vertex3(boxD.easting, boxD.northing, 0);
            //GL.Vertex3(boxG.easting, boxG.northing, 0);
            //GL.Vertex3(boxE.easting, boxE.northing, 0);
            //GL.End();

            //GL.Begin(PrimitiveType.LineStrip);
            //GL.Vertex3(boxF.easting, boxF.northing, 0);
            //GL.Vertex3(boxH.easting, boxH.northing, 0);
            //GL.Vertex3(boxC.easting, boxC.northing, 0);
            //GL.Vertex3(boxB.easting, boxB.northing, 0);
            //GL.Vertex3(boxF.easting, boxF.northing, 0);
            //GL.End();

            ////draw the reference line
            //GL.PointSize(3.0f);
            ////if (isContourBtnOn)
            //{
            //    ptCount = stripList.Count;
            //    if (ptCount > 0)
            //    {
            //        ptCount = stripList[closestRefPatch].Count;
            //        GL.Begin(PrimitiveType.Points);
            //        for (int i = 0; i < ptCount; i++)
            //        {
            //            GL.Vertex2(stripList[closestRefPatch][i].easting, stripList[closestRefPatch][i].northing);
            //        }
            //        GL.End();
            //    }
            //}

            //ptCount = conList.Count;
            //if (ptCount > 0)
            //{
            //    //draw closest point and side of line points
            //    GL.Color3(0.5f, 0.900f, 0.90f);
            //    GL.PointSize(4.0f);
            //    GL.Begin(PrimitiveType.Points);
            //    for (int i = 0; i < ptCount; i++) GL.Vertex3(conList[i].x, conList[i].z, 0);
            //    GL.End();

            //    GL.Color3(0.35f, 0.30f, 0.90f);
            //    GL.PointSize(6.0f);
            //    GL.Begin(PrimitiveType.Points);
            //    GL.Vertex3(conList[closestRefPoint].x, conList[closestRefPoint].z, 0);
            //    GL.End();
            //}

            if (mf.isPureDisplayOn && mf.gyd.distanceFromCurrentLinePivot != 32000 && !mf.isStanleyUsed)
            {
                //if (ppRadiusCT < 50 && ppRadiusCT > -50)
                //{
                //    const int numSegments = 100;
                //    double theta = glm.twoPI / numSegments;
                //    double c = Math.Cos(theta);//precalculate the sine and cosine
                //    double s = Math.Sin(theta);
                //    double x = ppRadiusCT;//we start at angle = 0
                //    double y = 0;

                //    GL.LineWidth(1);
                //    GL.Color3(0.795f, 0.230f, 0.7950f);
                //    GL.Begin(PrimitiveType.LineLoop);
                //    for (int ii = 0; ii < numSegments; ii++)
                //    {
                //        //glVertex2f(x + cx, y + cy);//output vertex
                //        GL.Vertex3(x + radiusPointCT.easting, y + radiusPointCT.northing, 0);//output vertex

                //        //apply the rotation matrix
                //        double t = x;
                //        x = (c * x) - (s * y);
                //        y = (s * t) + (c * y);
                //    }
                //    GL.End();
                //}

                //Draw lookahead Point
                GL.PointSize(6.0f);
                GL.Begin(PrimitiveType.Points);

                GL.Color3(1.0f, 0.95f, 0.095f);
                GL.Vertex3(mf.gyd.goalPoint.easting, mf.gyd.goalPoint.northing, 0.0);
                GL.End();
                GL.PointSize(1.0f);
            }
        }

        //Reset the contour to zip
        public void ResetContour()
        {
            Index = -1;
            isContourOn = false;
            stripList.Clear();
            ctList.Clear();
        }
    }//class
}//namespace