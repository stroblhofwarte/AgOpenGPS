using System;
using System.Collections.Generic;

namespace AgOpenGPS
{
    public partial class CGuidance
    {
        private readonly FormGPS mf;

        //list of the list of individual Lines for entire field
        public List<CGuidanceLine> refList = new List<CGuidanceLine>();
        public List<vec3> curList = new List<vec3>();

        public bool isBtnABLineOn, isBtnCurveOn, isContourBtnOn;
        public int numABLines = 0, numCurveLines;
        public CGuidanceLine selectedABLine, selectedCurveLine, ContourIndex;

        public bool isHeadingSameWay = true;
        public double howManyPathsAway, oldHowManyPathsAway;
        public double lastSecond = 0;

        //steer, pivot, and ref indexes
        public int sA, sB, pA, pB;

        public bool isValid;
        public int currentLocationIndex;

        public double distanceFromCurrentLineSteer, distanceFromCurrentLinePivot;
        public double steerAngle, rEastSteer, rNorthSteer, rEastPivot, rNorthPivot;

        public double inty, xTrackSteerCorrection = 0;
        public double steerHeadingError, steerHeadingErrorDegrees;

        public double distSteerError, lastDistSteerError, derivativeDistError;

        public double pivotDistanceError, pivotDistanceErrorLast, pivotDerivative, pivotDerivativeSmoothed;

        //for adding steering angle based on side slope hill
        public double sideHillCompFactor, moveDistance;

        //derivative counter
        public int counter2;

        public bool isLateralTriggered;
        //pure pursuit values
        public vec2 goalPoint = new vec2(0, 0);

        public vec2 radiusPoint = new vec2(0, 0);
        public double ppRadius, manualUturnHeading;

        public CGuidance(FormGPS _f)
        {
            //constructor
            mf = _f;
            sideHillCompFactor = Properties.Settings.Default.setAS_sideHillComp;
            lineWidth = Properties.Settings.Default.setDisplay_lineWidth;
            abLength = Properties.Settings.Default.setAB_lineLength;
        }

        public void GetCurrentLine(vec3 pivot, vec3 steer)
        {
            //build new current ref line if required
            if (isContourBtnOn)
            {
                if (((!isValid || curList.Count < 9) && (mf.secondsSinceStart - lastSecond) > 0.66) || (mf.secondsSinceStart - lastSecond) > 2.0)
                    BuildCurrentContourList(pivot);
            }
            else if (!isValid || ((mf.secondsSinceStart - lastSecond) > 0.66 && (!mf.isAutoSteerBtnOn || mf.mc.steerSwitchValue != 0)))
            {
                if (isBtnABLineOn)
                    BuildCurrentABLineList(pivot);
                else
                    BuildCurrentCurveList(pivot);
            }

            if (curList.Count > (isContourBtnOn ? 8 : 1))
            {
                if (!isContourBtnOn && mf.yt.isYouTurnTriggered && mf.yt.DistanceFromYouTurnLine())//do the pure pursuit from youTurn
                {
                
                }
                else if (mf.isStanleyUsed)
                    StanleyGuidance(pivot, steer, curList);
                else
                    PurePursuitGuidance(pivot, steer, curList);
            }
            else
            {
                isLocked = false;
                isValid = false;
                oldHowManyPathsAway = double.NaN;
                //invalid distance so tell AS module
                distanceFromCurrentLinePivot = 32000;
                mf.guidanceLineDistanceOff = 32000;
            }
        }

        public void StanleyGuidance(vec3 pivot, vec3 steer, List<vec3> curList)
        {
            double dx, dz, U;
            //find the closest point roughly
            int cc = 0, dd;
            int ptCount = curList.Count;
            if (ptCount > 1)
            {
                double minDistA = double.MaxValue, minDistB;

                for (int j = 0; j < ptCount; j += 10)
                {
                    double dist = ((steer.easting - curList[j].easting) * (steer.easting - curList[j].easting))
                                    + ((steer.northing - curList[j].northing) * (steer.northing - curList[j].northing));
                    if (dist < minDistA)
                    {
                        minDistA = dist;
                        cc = j;
                    }
                }

                minDistA = minDistB = double.MaxValue;
                dd = cc + 7; if (dd > ptCount) dd = ptCount;
                cc -= 7; if (cc < 0) cc = 0;

                //find the closest 2 points to current close call
                for (int j = cc; j < dd; j++)
                {
                    double dist = ((steer.easting - curList[j].easting) * (steer.easting - curList[j].easting))
                                    + ((steer.northing - curList[j].northing) * (steer.northing - curList[j].northing));
                    if (dist < minDistA)
                    {
                        minDistB = minDistA;
                        sB = sA;
                        minDistA = dist;
                        sA = j;
                    }
                    else if (dist < minDistB)
                    {
                        minDistB = dist;
                        sB = j;
                    }
                }

                //just need to make sure the points continue ascending or heading switches all over the place
                if (sA > sB) { int C = sA; sA = sB; sB = C; }

                //currentLocationIndex = sA;
                if (sA > ptCount - 1 || sB > ptCount - 1) return;

                if (!isContourBtnOn)
                {
                    minDistA = minDistB = double.MaxValue;

                    if (isHeadingSameWay)
                        dd = sB + 1; cc = dd - 12; if (cc < 0) cc = 0;
                    else
                        cc = sA; dd = sA + 12; if (dd > ptCount) dd = ptCount;

                    //find the closest 2 points of pivot back from steer
                    for (int j = cc; j < dd; j++)
                    {
                        double dist = ((pivot.easting - curList[j].easting) * (pivot.easting - curList[j].easting))
                                        + ((pivot.northing - curList[j].northing) * (pivot.northing - curList[j].northing));
                        if (dist < minDistA)
                        {
                            minDistB = minDistA;
                            pB = pA;
                            minDistA = dist;
                            pA = j;
                        }
                        else if (dist < minDistB)
                        {
                            minDistB = dist;
                            pB = j;
                        }
                    }

                    //just need to make sure the points continue ascending or heading switches all over the place
                    if (pA > pB) { int C = pA; pA = pB; pB = C; }

                    if (pA > ptCount - 1 || pB > ptCount - 1)
                    {
                        pA = ptCount - 2;
                        pB = ptCount - 1;
                    }

                    vec3 pivA = new vec3(curList[pA]);
                    vec3 pivB = new vec3(curList[pB]);

                    if (!isHeadingSameWay)
                    {
                        pivA = curList[pB];
                        pivB = curList[pA];

                        pivA.heading += Math.PI;
                        if (pivA.heading > glm.twoPI) pivA.heading -= glm.twoPI;
                    }

                    manualUturnHeading = pivA.heading;

                    //get the pivot distance from currently active AB segment   ///////////  Pivot  ////////////
                    dx = pivB.easting - pivA.easting;
                    dz = pivB.northing - pivA.northing;



                    if (Math.Abs(dx) < double.Epsilon && Math.Abs(dz) < double.Epsilon) return;

                    //how far from current AB Line is fix
                    distanceFromCurrentLinePivot = ((dz * pivot.easting) - (dx * pivot.northing) + (pivB.easting
                                * pivA.northing) - (pivB.northing * pivA.easting))
                                    / Math.Sqrt((dz * dz) + (dx * dx));

                    U = (((pivot.easting - pivA.easting) * dx)
                                    + ((pivot.northing - pivA.northing) * dz))
                                    / ((dx * dx) + (dz * dz));

                    rEastPivot = pivA.easting + (U * dx);
                    rNorthPivot = pivA.northing + (U * dz);
                    currentLocationIndex = pA;
                }
                else
                    currentLocationIndex = sA;//not used!

                //get the distance from currently active AB segment of steer axle //////// steer /////////////
                vec3 steerA = new vec3(curList[sA]);
                vec3 steerB = new vec3(curList[sB]);

                if (!isHeadingSameWay)
                {
                    steerA = curList[sB];
                    steerA.heading += Math.PI;
                    if (steerA.heading > glm.twoPI) steerA.heading -= glm.twoPI;

                    steerB = curList[sA];
                    steerB.heading += Math.PI;
                    if (steerB.heading > glm.twoPI) steerB.heading -= glm.twoPI;
                }

                //double curvature = pivA.heading - steerA.heading;
                //if (curvature > Math.PI) curvature -= Math.PI; else if (curvature < Math.PI) curvature += Math.PI;
                //if (curvature > glm.PIBy2) curvature -= Math.PI; else if (curvature < -glm.PIBy2) curvature += Math.PI;

                ////because of draft 
                //curvature = Math.Sin(curvature) * mf.vehicle.wheelbase * 0.8;
                //pivotCurvatureOffset = (pivotCurvatureOffset * 0.7) + (curvature * 0.3);
                //pivotCurvatureOffset = 0;

                //create the AB segment to offset
                steerA.easting += (Math.Sin(steerA.heading + glm.PIBy2) * (inty));
                steerA.northing += (Math.Cos(steerA.heading + glm.PIBy2) * (inty));

                steerB.easting += (Math.Sin(steerB.heading + glm.PIBy2) * (inty));
                steerB.northing += (Math.Cos(steerB.heading + glm.PIBy2) * (inty));

                dx = steerB.easting - steerA.easting;
                dz = steerB.northing - steerA.northing;

                if (Math.Abs(dx) < double.Epsilon && Math.Abs(dz) < double.Epsilon) return;

                //how far from current AB Line is fix
                distanceFromCurrentLineSteer = ((dz * steer.easting) - (dx * steer.northing) + (steerB.easting
                            * steerA.northing) - (steerB.northing * steerA.easting))
                                / Math.Sqrt((dz * dz) + (dx * dx));

                if (isContourBtnOn)
                    distanceFromCurrentLinePivot = distanceFromCurrentLineSteer;

                // calc point on ABLine closest to current position - for display only
                U = (((steer.easting - steerA.easting) * dx)
                                + ((steer.northing - steerA.northing) * dz))
                                / ((dx * dx) + (dz * dz));

                rEastSteer = steerA.easting + (U * dx);
                rNorthSteer = steerA.northing + (U * dz);

                if (isBtnABLineOn)
                {
                    double steerErr = Math.Atan2(rEastSteer - rEastPivot, rNorthSteer - rNorthPivot);
                    steerHeadingError = (steer.heading - steerErr);
                }
                else
                    steerHeadingError = steer.heading - (steerA.heading + steerB.heading) / 2;

                //Fix the circular error
                if (steerHeadingError > Math.PI) steerHeadingError -= Math.PI;
                else if (steerHeadingError < Math.PI) steerHeadingError += Math.PI;

                if (steerHeadingError > glm.PIBy2) steerHeadingError -= Math.PI;
                else if (steerHeadingError < -glm.PIBy2) steerHeadingError += Math.PI;

                if (mf.isReverse) steerHeadingError *= -1;
                //Overshoot setting on Stanley tab
                steerHeadingError *= mf.vehicle.stanleyHeadingErrorGain;

                if (isContourBtnOn)
                {
                    if (steerHeadingError > 0.74) steerHeadingError = 0.74;
                    if (steerHeadingError < -0.74) steerHeadingError = -0.74;
                }

                double sped = Math.Abs(mf.avgSpeed);
                if (sped > 1) sped = 1 + 0.277 * (sped - 1);
                else sped = 1;
                double XTEc = Math.Atan((distanceFromCurrentLineSteer * mf.vehicle.stanleyDistanceErrorGain)
                    / (sped));

                xTrackSteerCorrection = (xTrackSteerCorrection * 0.5) + XTEc * (0.5);

                //derivative of steer distance error
                distSteerError = (distSteerError * 0.95) + ((xTrackSteerCorrection * 60) * 0.05);
                if (counter2++ > 5)
                {
                    derivativeDistError = distSteerError - lastDistSteerError;
                    lastDistSteerError = distSteerError;
                    counter2 = 0;
                }

                steerAngle = glm.toDegrees((xTrackSteerCorrection + steerHeadingError) * -1.0);

                if (!isContourBtnOn)
                {
                    if (Math.Abs(distanceFromCurrentLineSteer) > 0.5) steerAngle *= 0.5;
                    else steerAngle *= (1 - Math.Abs(distanceFromCurrentLineSteer));

                    //pivot PID
                    pivotDistanceError = (pivotDistanceError * 0.6) + (distanceFromCurrentLinePivot * 0.4);
                    //pivotDistanceError = Math.Atan((distanceFromCurrentLinePivot) / (sped)) * 0.2;
                    //pivotErrorTotal = pivotDistanceError + pivotDerivative;

                    if (mf.pn.speed > mf.startSpeed
                        && mf.isAutoSteerBtnOn
                        && Math.Abs(derivativeDistError) < 1
                        && Math.Abs(pivotDistanceError) < 0.25)
                    {
                        //if over the line heading wrong way, rapidly decrease integral
                        if ((inty < 0 && distanceFromCurrentLinePivot < 0) || (inty > 0 && distanceFromCurrentLinePivot > 0))
                        {
                            inty += pivotDistanceError * mf.vehicle.stanleyIntegralGainAB * -0.1;
                        }
                        else
                        {
                            inty += pivotDistanceError * mf.vehicle.stanleyIntegralGainAB * -0.01;
                        }

                        //integral slider is set to 0
                        if (mf.vehicle.stanleyIntegralGainAB == 0) inty = 0;
                    }
                    else inty *= 0.7;

                    if (mf.isReverse) inty = 0;

                    if (mf.ahrs.imuRoll != 88888)
                        steerAngle += mf.ahrs.imuRoll * -sideHillCompFactor;
                }


                if (steerAngle < -mf.vehicle.maxSteerAngle) steerAngle = -mf.vehicle.maxSteerAngle;
                else if (steerAngle > mf.vehicle.maxSteerAngle) steerAngle = mf.vehicle.maxSteerAngle;

                //Convert to millimeters from meters
                mf.guidanceLineDistanceOff = (short)Math.Round(distanceFromCurrentLinePivot * 1000.0, MidpointRounding.AwayFromZero);
                mf.guidanceLineSteerAngle = (short)(steerAngle * 100);
            }
            else
            {
                //invalid distance so tell AS module
                distanceFromCurrentLineSteer = 32000;
                mf.guidanceLineDistanceOff = 32000;
            }
        }

        public void PurePursuitGuidance(vec3 pivot, vec3 steer, List<vec3> curList)
        {
            double dist, dx, dz;
            double minDistA = double.MaxValue, minDistB = double.MaxValue;

            //find the closest 2 points to current fix
            for (int t = 0; t < curList.Count; t++)
            {
                dist = glm.DistanceSquared(pivot, curList[t]);

                if (dist < minDistA)
                {
                    minDistB = minDistA;
                    pB = pA;
                    minDistA = dist;
                    pA = t;
                }
                else if (dist < minDistB)
                {
                    minDistB = dist;
                    pB = t;
                }
            }

            //just need to make sure the points continue ascending or heading switches all over the place
            if (pA > pB) { int C = pA; pA = pB; pB = C; }

            currentLocationIndex = pA;

            //get the distance from currently active AB line
            dx = curList[pB].easting - curList[pA].easting;
            dz = curList[pB].northing - curList[pA].northing;

            if (Math.Abs(dx) < double.Epsilon && Math.Abs(dz) < double.Epsilon) return;

            //abHeading = Math.Atan2(dz, dx);

            //how far from current AB Line is fix
            distanceFromCurrentLinePivot = ((dz * pivot.easting) - (dx * pivot.northing) + (curList[pB].easting
                        * curList[pA].northing) - (curList[pB].northing * curList[pA].easting))
                            / Math.Sqrt((dz * dz) + (dx * dx));

            //integral slider is set to 0
            if (mf.vehicle.purePursuitIntegralGain != 0 && !mf.isReverse)
            {
                pivotDistanceError = distanceFromCurrentLinePivot * 0.2 + pivotDistanceError * 0.8;

                if (counter2++ > 4)
                {
                    pivotDerivative = pivotDistanceError - pivotDistanceErrorLast;
                    pivotDistanceErrorLast = pivotDistanceError;
                    counter2 = 0;
                    pivotDerivative *= 2;

                    //limit the derivative
                    //if (pivotDerivative > 0.03) pivotDerivative = 0.03;
                    //if (pivotDerivative < -0.03) pivotDerivative = -0.03;
                    //if (Math.Abs(pivotDerivative) < 0.01) pivotDerivative = 0;
                }

                if (mf.isAutoSteerBtnOn && mf.avgSpeed > 2.5 && Math.Abs(pivotDerivative) < 0.1 && !mf.yt.isYouTurnTriggered)
                {
                    //if over the line heading wrong way, rapidly decrease integral
                    if ((inty < 0 && distanceFromCurrentLinePivot < 0) || (inty > 0 && distanceFromCurrentLinePivot > 0))
                    {
                        if (isContourBtnOn)
                            inty += pivotDistanceError * mf.vehicle.purePursuitIntegralGain * -0.06;
                        else
                            inty += pivotDistanceError * mf.vehicle.purePursuitIntegralGain * -0.04;
                    }
                    else
                    {
                        if (Math.Abs(distanceFromCurrentLinePivot) > 0.02)
                        {
                            inty += pivotDistanceError * mf.vehicle.purePursuitIntegralGain * -0.02;
                            if (inty > 0.2) inty = 0.2;
                            else if (inty < -0.2) inty = -0.2;
                        }
                    }
                }
                else inty *= 0.95;
            }
            else inty = 0;

            // ** Pure pursuit ** - calc point on ABLine closest to current position
            double U = (((pivot.easting - curList[pA].easting) * dx)
                        + ((pivot.northing - curList[pA].northing) * dz))
                        / ((dx * dx) + (dz * dz));

            rEastPivot = curList[pA].easting + (U * dx);
            rNorthPivot = curList[pA].northing + (U * dz);
            manualUturnHeading = curList[pA].heading;
            //double minx, maxx, miny, maxy;

            //update base on autosteer settings and distance from line
            double goalPointDistance = mf.vehicle.UpdateGoalPointDistance();

            bool ReverseHeading = mf.isReverse ? !isHeadingSameWay : isHeadingSameWay;

            int count = ReverseHeading ? 1 : -1;
            vec3 start = new vec3(rEastPivot, rNorthPivot, 0);
            double distSoFar = 0;

            for (int i = ReverseHeading ? pB : pA; i < curList.Count && i >= 0; i += count)
            {
                // used for calculating the length squared of next segment.
                double tempDist = glm.Distance(start, curList[i]);

                //will we go too far?
                if ((tempDist + distSoFar) > goalPointDistance)
                {
                    double j = (goalPointDistance - distSoFar) / tempDist; // the remainder to yet travel

                    goalPoint.easting = (((1 - j) * start.easting) + (j * curList[i].easting));
                    goalPoint.northing = (((1 - j) * start.northing) + (j * curList[i].northing));
                    break;
                }
                else distSoFar += tempDist;
                start = curList[i];
            }

            //calc "D" the distance from pivot axle to lookahead point
            double goalPointDistanceSquared = glm.DistanceSquared(goalPoint.northing, goalPoint.easting, pivot.northing, pivot.easting);

            //calculate the the delta x in local coordinates and steering angle degrees based on wheelbase
            double localHeading = glm.twoPI - mf.fixHeading + (ReverseHeading ? inty : -inty);

            ppRadius = goalPointDistanceSquared / (2 * (((goalPoint.easting - pivot.easting) * Math.Cos(localHeading)) + ((goalPoint.northing - pivot.northing) * Math.Sin(localHeading))));

            steerAngle = glm.toDegrees(Math.Atan(2 * (((goalPoint.easting - pivot.easting) * Math.Cos(localHeading))
                + ((goalPoint.northing - pivot.northing) * Math.Sin(localHeading))) * mf.vehicle.wheelbase / goalPointDistanceSquared));

            if (mf.ahrs.imuRoll != 88888)
                steerAngle += mf.ahrs.imuRoll * -sideHillCompFactor;

            if (steerAngle < -mf.vehicle.maxSteerAngle) steerAngle = -mf.vehicle.maxSteerAngle;
            if (steerAngle > mf.vehicle.maxSteerAngle) steerAngle = mf.vehicle.maxSteerAngle;

            if (ppRadius < -500) ppRadius = -500;
            if (ppRadius > 500) ppRadius = 500;

            radiusPoint.easting = pivot.easting + (ppRadius * Math.Cos(localHeading));
            radiusPoint.northing = pivot.northing + (ppRadius * Math.Sin(localHeading));

            if (isBtnABLineOn)
            {
                if (mf.isAngVelGuidance)
                {
                    //angular velocity in rads/sec  = 2PI * m/sec * radians/meters
                    mf.setAngVel = 0.277777 * mf.pn.speed * (Math.Tan(glm.toRadians(steerAngle))) / mf.vehicle.wheelbase;
                    mf.setAngVel = glm.toDegrees(mf.setAngVel) * 100;

                    //clamp the steering angle to not exceed safe angular velocity
                    if (Math.Abs(mf.setAngVel) > 1000)
                    {
                        //mf.setAngVel = mf.setAngVel < 0 ? -mf.vehicle.maxAngularVelocity : mf.vehicle.maxAngularVelocity;
                        mf.setAngVel = mf.setAngVel < 0 ? -1000 : 1000;
                    }
                }
            }
            else
            {
                //angular velocity in rads/sec  = 2PI * m/sec * radians/meters
                double angVel = glm.twoPI * 0.277777 * mf.pn.speed * (Math.Tan(glm.toRadians(steerAngle))) / mf.vehicle.wheelbase;

                //clamp the steering angle to not exceed safe angular velocity
                if (Math.Abs(angVel) > mf.vehicle.maxAngularVelocity)
                {
                    steerAngle = glm.toDegrees(steerAngle > 0 ?
                            (Math.Atan((mf.vehicle.wheelbase * mf.vehicle.maxAngularVelocity) / (glm.twoPI * mf.avgSpeed * 0.277777)))
                        : (Math.Atan((mf.vehicle.wheelbase * -mf.vehicle.maxAngularVelocity) / (glm.twoPI * mf.avgSpeed * 0.277777))));
                }
            }

            if (!isHeadingSameWay)
                distanceFromCurrentLinePivot *= -1.0;

            //Convert to centimeters
            mf.guidanceLineDistanceOff = (short)Math.Round(distanceFromCurrentLinePivot * 1000.0, MidpointRounding.AwayFromZero);
            mf.guidanceLineSteerAngle = (short)(steerAngle * 100);
        }
    }
    
    public enum Mode { AB, Boundary, BoundaryContour, Contour, Curve };//, Heading, Circle, Spiral

    public class CGuidanceLine
    {
        public List<vec3> curvePts = new List<vec3>();
        public string Name = "aa";
        public Mode Mode;

        public CGuidanceLine(Mode mode)
        {
            Mode = mode;
        }
    }
}
