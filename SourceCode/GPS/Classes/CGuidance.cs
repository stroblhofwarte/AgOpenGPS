using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;

namespace AgOpenGPS
{
    public partial class CGuidance
    {
        private readonly FormGPS mf;

        public double abLength;
        public int lineWidth;

        //list of the list of individual Lines for entire field
        public List<CGuidanceLine> refList = new List<CGuidanceLine>();
        public List<vec3> curList = new List<vec3>();

        public bool isBtnABLineOn, isBtnCurveOn, isContourBtnOn;
        public int numABLines = 0, numCurveLines;
        public CGuidanceLine selectedLine, ContourIndex;

        public bool isSmoothWindowOpen;
        public bool isOkToAddDesPoints;
        public List<vec3> desList = new List<vec3>();

        public bool isHeadingSameWay = true;
        public double howManyPathsAway, oldHowManyPathsAway;
        public double lastSecond = 0;

        //steer, pivot, and ref indexes
        public int sA, sB, pA, pB, onA;

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
            if (!isContourBtnOn && (!isValid || ((mf.secondsSinceStart - lastSecond) > 0.66 && (!mf.isAutoSteerBtnOn || mf.mc.steerSwitchValue != 0))))
                    BuildCurrentList(pivot, selectedLine);
            else if (isContourBtnOn && (((!isValid || curList.Count < 9) && (mf.secondsSinceStart - lastSecond) > 0.66) || (mf.secondsSinceStart - lastSecond) > 2.0))
                    BuildCurrentContourList(pivot);
            
            List<vec3> curList2 = mf.yt.isYouTurnTriggered ? mf.yt.ytList : curList;
            if (curList2.Count > (isContourBtnOn ? 8 : 1))
            {
                if (mf.isStanleyUsed)
                    StanleyGuidance(pivot, steer, curList2);
                else
                    PurePursuitGuidance(pivot, steer, curList2);
            }
            else
            {
                if (mf.yt.isYouTurnTriggered)
                    mf.yt.CompleteYouTurn();

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
            bool CompleteYouTurn = false;
            double dx, dz, U;
            //find the closest point roughly
            int cc = 0, dd;
            int ptCount = curList.Count;
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

            if (mf.yt.isYouTurnTriggered)
            {
                //feed backward to turn slower to keep pivot on
                sA -= 7;
                if (sA < 0)
                    sA = 0;
                sB = sA + 1;

                //return and reset if too far away or end of the line
                if (minDistA > 16 || sB >= ptCount - 8)
                    CompleteYouTurn = true;
            }

            if (sA > ptCount - 1 || sB > ptCount - 1) return;

            if (!mf.yt.isYouTurnTriggered && !isContourBtnOn)
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

            if (!mf.yt.isYouTurnTriggered)
            {
                if (!isHeadingSameWay)
                {
                    steerA = curList[sB];
                    steerA.heading += Math.PI;
                    if (steerA.heading > glm.twoPI) steerA.heading -= glm.twoPI;

                    steerB = curList[sA];
                    steerB.heading += Math.PI;
                    if (steerB.heading > glm.twoPI) steerB.heading -= glm.twoPI;
                }
                if (!isContourBtnOn)
                {
                    //create the AB segment to offset
                    steerA.easting += (Math.Sin(steerA.heading + glm.PIBy2) * (inty));
                    steerA.northing += (Math.Cos(steerA.heading + glm.PIBy2) * (inty));

                    steerB.easting += (Math.Sin(steerB.heading + glm.PIBy2) * (inty));
                    steerB.northing += (Math.Cos(steerB.heading + glm.PIBy2) * (inty));
                }
            }

            dx = steerB.easting - steerA.easting;
            dz = steerB.northing - steerA.northing;

            if (Math.Abs(dx) < double.Epsilon && Math.Abs(dz) < double.Epsilon) return;

            //how far from current AB Line is fix
            distanceFromCurrentLineSteer = ((dz * steer.easting) - (dx * steer.northing) + (steerB.easting
                        * steerA.northing) - (steerB.northing * steerA.easting))
                            / Math.Sqrt((dz * dz) + (dx * dx));

            if (isContourBtnOn || mf.yt.isYouTurnTriggered)
                distanceFromCurrentLinePivot = distanceFromCurrentLineSteer;

            double abHeading = Math.Atan2(dx, dz);
            if (abHeading < 0) abHeading += glm.twoPI;

            // calc point on ABLine closest to current position - for display only
            U = (((steer.easting - steerA.easting) * dx)
                            + ((steer.northing - steerA.northing) * dz))
                            / ((dx * dx) + (dz * dz));

            rEastSteer = steerA.easting + (U * dx);
            rNorthSteer = steerA.northing + (U * dz);
            if (isContourBtnOn)
                steerHeadingError = (steer.heading - abHeading);
            if (mf.yt.isYouTurnTriggered)
                steerHeadingError = (steer.heading - steerA.heading);
            else if (isBtnABLineOn)
            {
                double steerErr = Math.Atan2(rEastSteer - rEastPivot, rNorthSteer - rNorthPivot);
                steerHeadingError = (steer.heading - steerErr);
            }
            else if (isBtnCurveOn)
                steerHeadingError = steer.heading - steerB.heading;
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

            if (isContourBtnOn || mf.yt.isYouTurnTriggered)
            {
                if (steerHeadingError > 0.74) steerHeadingError = 0.74;
                if (steerHeadingError < -0.74) steerHeadingError = -0.74;

                if (isContourBtnOn)
                    steerAngle = Math.Atan((distanceFromCurrentLineSteer * mf.vehicle.stanleyDistanceErrorGain)
                        / ((Math.Abs(mf.pn.speed) * 0.277777) + 1));
                else
                    steerAngle = Math.Atan((distanceFromCurrentLineSteer * mf.vehicle.stanleyDistanceErrorGain)
                        / ((mf.pn.speed * 0.277777) + 1));

                //clamp it to max 42 degrees
                if (steerAngle > 0.74) steerAngle = 0.74;
                if (steerAngle < -0.74) steerAngle = -0.74;

                //add them up and clamp to max in vehicle settings
                steerAngle = glm.toDegrees((steerAngle + steerHeadingError) * -1.0);
            }
            else
            {

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
            if (CompleteYouTurn)
                mf.yt.CompleteYouTurn();
        }

        public void PurePursuitGuidance(vec3 pivot, vec3 steer, List<vec3> curList)
        {
            bool CompleteYouTurn = false;
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

            if (mf.yt.isYouTurnTriggered)
            {
                onA = curList.Count / 2;
                if (pA < onA)
                    onA = -pA;
                else
                    onA = curList.Count - pA;

                //just need to make sure the points continue ascending or heading switches all over the place
                if (pA > pB) { int C = pA; pA = pB; pB = C; }
                //return and reset if too far away or end of the line
                if (pB >= curList.Count - 1)
                    CompleteYouTurn = true;
            }
            //just need to make sure the points continue ascending or heading switches all over the place
            else if (pA > pB) { int C = pA; pA = pB; pB = C; }

            if (isContourBtnOn && isLocked && (pA < 2 || pB > curList.Count - 3))
            {
                //ctList.Clear();
                isLocked = false;
                lastLockPt = int.MaxValue;
                return;
            }

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
            if (!mf.yt.isYouTurnTriggered)
            {
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
            }

            if (!mf.yt.isYouTurnTriggered && !isHeadingSameWay)
                distanceFromCurrentLinePivot *= -1.0;

            // ** Pure pursuit ** - calc point on ABLine closest to current position
            double U = (((pivot.easting - curList[pA].easting) * dx)
                        + ((pivot.northing - curList[pA].northing) * dz))
                        / ((dx * dx) + (dz * dz));

            rEastPivot = curList[pA].easting + (U * dx);
            rNorthPivot = curList[pA].northing + (U * dz);
            manualUturnHeading = curList[pA].heading;
            //double minx, maxx, miny, maxy;

            //update base on autosteer settings and distance from line
            double goalPointDistance = mf.vehicle.UpdateGoalPointDistance() * (mf.yt.isYouTurnTriggered ? 0.8 : 1.0);

            bool ReverseHeading = mf.yt.isYouTurnTriggered ? mf.isReverse : (mf.isReverse ? !isHeadingSameWay : isHeadingSameWay);

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

                if (mf.yt.isYouTurnTriggered && i == curList.Count - 1)//goalPointDistance is longer than remaining u-turn
                    CompleteYouTurn = true;
            }

            //calc "D" the distance from pivot axle to lookahead point
            double goalPointDistanceSquared = glm.DistanceSquared(goalPoint.northing, goalPoint.easting, pivot.northing, pivot.easting);

            //calculate the the delta x in local coordinates and steering angle degrees based on wheelbase
            double localHeading = glm.twoPI - mf.fixHeading + (mf.yt.isYouTurnTriggered ? 0 : (ReverseHeading ? inty : -inty));

            ppRadius = goalPointDistanceSquared / (2 * (((goalPoint.easting - pivot.easting) * Math.Cos(localHeading)) + ((goalPoint.northing - pivot.northing) * Math.Sin(localHeading))));

            steerAngle = glm.toDegrees(Math.Atan(2 * (((goalPoint.easting - pivot.easting) * Math.Cos(localHeading))
                + ((goalPoint.northing - pivot.northing) * Math.Sin(localHeading))) * mf.vehicle.wheelbase / goalPointDistanceSquared));

            if (!mf.yt.isYouTurnTriggered && mf.ahrs.imuRoll != 88888)
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
            else if (!mf.yt.isYouTurnTriggered)
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

            //Convert to centimeters
            mf.guidanceLineDistanceOff = (short)Math.Round(distanceFromCurrentLinePivot * 1000.0, MidpointRounding.AwayFromZero);
            mf.guidanceLineSteerAngle = (short)(steerAngle * 100);
            if (CompleteYouTurn)
                mf.yt.CompleteYouTurn();
        }

        public void BuildCurrentList(vec3 pivot, CGuidanceLine CurrentLine)
        {
            if (CurrentLine?.curvePts.Count > 1)
            {
                double minDistA = double.MaxValue, minDistB;

                //move the Line over based on the overlap amount set in
                double widthMinusOverlap = mf.tool.toolWidth - mf.tool.toolOverlap;

                int refCount = CurrentLine.curvePts.Count;

                //close call hit
                int cc = 0, dd;

                for (int j = 0; j < refCount; j += 10)
                {
                    double dist = ((mf.guidanceLookPos.easting - CurrentLine.curvePts[j].easting) * (mf.guidanceLookPos.easting - CurrentLine.curvePts[j].easting))
                                    + ((mf.guidanceLookPos.northing - CurrentLine.curvePts[j].northing) * (mf.guidanceLookPos.northing - CurrentLine.curvePts[j].northing));
                    if (dist < minDistA)
                    {
                        minDistA = dist;
                        cc = j;
                    }
                }

                minDistA = minDistB = double.MaxValue;

                dd = cc + 7; if (dd > refCount) dd = refCount;
                cc -= 7; if (cc < 0) cc = 0;
                int rB = refCount; int rA = refCount;

                //find the closest 2 points to current close call
                for (int j = cc; j < dd; j++)
                {
                    double dist = ((mf.guidanceLookPos.easting - CurrentLine.curvePts[j].easting) * (mf.guidanceLookPos.easting - CurrentLine.curvePts[j].easting))
                                    + ((mf.guidanceLookPos.northing - CurrentLine.curvePts[j].northing) * (mf.guidanceLookPos.northing - CurrentLine.curvePts[j].northing));
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

                if (rA > rB) { int C = rA; rA = rB; rB = C; }
                //which side of the closest point are we on is next
                //calculate endpoints of reference line based on closest point

                //x2-x1
                double dx = CurrentLine.curvePts[rB].easting - CurrentLine.curvePts[rA].easting;
                //z2-z1
                double dz = CurrentLine.curvePts[rB].northing - CurrentLine.curvePts[rA].northing;

                double heading = CurrentLine.curvePts[rA].heading;// Math.Atan2(dx, dz);/

                //same way as line creation or not
                isHeadingSameWay = Math.PI - Math.Abs(Math.Abs(pivot.heading - heading) - Math.PI) < glm.PIBy2;

                if (mf.yt.isYouTurnTriggered) isHeadingSameWay = !isHeadingSameWay;

                //how far are we away from the reference line at 90 degrees - 2D cross product and distance
                double distanceFromRefLine = ((dz * mf.guidanceLookPos.easting) - (dx * mf.guidanceLookPos.northing) + (CurrentLine.curvePts[rB].easting
                                    * CurrentLine.curvePts[rA].northing) - (CurrentLine.curvePts[rB].northing * CurrentLine.curvePts[rA].easting))
                                    / Math.Sqrt((dz * dz) + (dx * dx));

                //Which ABLine is the vehicle on, negative is left and positive is right side
                double RefDist = (distanceFromRefLine + (isHeadingSameWay ? mf.tool.toolOffset : -mf.tool.toolOffset)) / widthMinusOverlap;
                if (RefDist < 0) howManyPathsAway = (int)(RefDist - 0.5);
                else howManyPathsAway = (int)(RefDist + 0.5);

                if (!isValid || howManyPathsAway != oldHowManyPathsAway)
                {
                    oldHowManyPathsAway = howManyPathsAway;
                    isValid = true;

                    //build the current line
                    curList.Clear();

                    double distAway = widthMinusOverlap * howManyPathsAway + (isHeadingSameWay ? -mf.tool.toolOffset : mf.tool.toolOffset);

                    if (CurrentLine.Mode.HasFlag(Mode.AB))
                    {
                        //depending which way you are going, the offset can be either side
                        vec2 point1 = new vec2(CurrentLine.curvePts[0].easting + Math.Cos(-heading) * distAway, CurrentLine.curvePts[0].northing + Math.Sin(-heading) * distAway);

                        //create the new line extent points for current ABLine based on original heading of AB line
                        curList.Add(new vec3(point1.easting - (Math.Sin(heading) * abLength), point1.northing - (Math.Cos(heading) * abLength), heading));
                        curList.Add(new vec3(point1.easting + (Math.Sin(heading) * abLength), point1.northing + (Math.Cos(heading) * abLength), heading));
                    }
                    else
                    {
                        double distSqAway = (distAway * distAway) - 0.01;

                        for (int i = 0; i < refCount - 1; i++)
                        {
                            vec3 point = new vec3(
                            CurrentLine.curvePts[i].easting + (Math.Sin(glm.PIBy2 + CurrentLine.curvePts[i].heading) * distAway),
                            CurrentLine.curvePts[i].northing + (Math.Cos(glm.PIBy2 + CurrentLine.curvePts[i].heading) * distAway),
                            CurrentLine.curvePts[i].heading);
                            bool Add = true;
                            for (int t = 0; t < refCount; t++)
                            {
                                double dist = ((point.easting - CurrentLine.curvePts[t].easting) * (point.easting - CurrentLine.curvePts[t].easting))
                                    + ((point.northing - CurrentLine.curvePts[t].northing) * (point.northing - CurrentLine.curvePts[t].northing));
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
                    }
                }
            }
            else
                curList.Clear();

            lastSecond = mf.secondsSinceStart;
        }

        public void DrawLines()
        {
            GL.LineWidth(lineWidth);

            if (desList.Count > 1)
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

                if (isBtnABLineOn)
                {
                    double heading = Math.Atan2(desList[1].easting - desList[0].easting, desList[1].northing - desList[0].northing);

                    GL.Color3(0.95f, 0.20f, 0.950f);

                    GL.Begin(PrimitiveType.Lines);
                    GL.Vertex3(desList[0].easting - (Math.Sin(heading) * abLength), desList[0].northing - (Math.Cos(heading) * abLength), 0.0);
                    GL.Vertex3(desList[0].easting + (Math.Sin(heading) * abLength), desList[0].northing + (Math.Cos(heading) * abLength), 0.0);
                    GL.End();

                    GL.Color3(0.2f, 0.950f, 0.20f);
                    mf.font.DrawText3D(desList[0].easting, desList[0].northing, "&A");
                    mf.font.DrawText3D(desList[1].easting, desList[1].northing, "&B");
                }
                else
                {
                    for (int h = 0; h < desList.Count; h++)
                        GL.Vertex3(desList[h].easting, desList[h].northing, 0);

                    if (isOkToAddDesPoints)
                        GL.Vertex3(mf.pivotAxlePos.easting, mf.pivotAxlePos.northing, 0);
                }
                GL.End();
            }
            else
            {
                if (isContourBtnOn)
                {
                    if (stripNum > -1)
                    {
                        //Draw the captured ref strip, red if locked
                        if (isLocked)
                        {
                            GL.Color3(0.983f, 0.2f, 0.20f);
                            GL.LineWidth(4);
                        }
                        else
                        {
                            GL.Color3(0.3f, 0.982f, 0.0f);
                            GL.LineWidth(lineWidth);
                        }
                        GL.Begin(PrimitiveType.Points);
                        for (int h = 0; h < refList[stripNum].curvePts.Count; h++) GL.Vertex3(refList[stripNum].curvePts[h].easting, refList[stripNum].curvePts[h].northing, 0);
                        GL.End();
                    }
                }
                else if (selectedLine?.curvePts.Count > 1)
                {
                    if (selectedLine.Mode.HasFlag(Mode.AB))
                    {
                        GL.Color3(0.95f, 0.0f, 0.0f);
                        GL.PointSize(8.0f);
                        GL.Begin(PrimitiveType.Points);
                        GL.Vertex3(selectedLine.curvePts[0].easting, selectedLine.curvePts[0].northing, 0.0);
                        GL.End();

                        if (mf.font.isFontOn)
                            mf.font.DrawText3D(selectedLine.curvePts[0].easting, selectedLine.curvePts[0].northing, "&A");

                        //Draw reference AB line
                        GL.Enable(EnableCap.LineStipple);
                        GL.LineStipple(1, 0x0F00);
                        GL.Color3(0.930f, 0.2f, 0.2f);

                        double heading = Math.Atan2(selectedLine.curvePts[1].easting - selectedLine.curvePts[0].easting, selectedLine.curvePts[1].northing - selectedLine.curvePts[0].northing);

                        GL.Begin(PrimitiveType.Lines);
                        GL.Vertex3(selectedLine.curvePts[0].easting - (Math.Sin(heading) * abLength), selectedLine.curvePts[0].northing - (Math.Cos(heading) * abLength), 0);
                        GL.Vertex3(selectedLine.curvePts[1].easting + (Math.Sin(heading) * abLength), selectedLine.curvePts[1].northing + (Math.Cos(heading) * abLength), 0);
                        GL.End();
                        GL.Disable(EnableCap.LineStipple);
                        GL.PointSize(1.0f);
                    }
                    else
                    {
                        GL.Color3(0.96, 0.2f, 0.2f);
                        GL.Enable(EnableCap.LineStipple);
                        GL.LineStipple(1, 0x0F00);

                        if (selectedLine.Mode.HasFlag(Mode.Boundary))
                            GL.Begin(PrimitiveType.LineLoop);
                        else
                            GL.Begin(PrimitiveType.LineStrip);
                        for (int h = 0; h < selectedLine.curvePts.Count; h++) GL.Vertex3(selectedLine.curvePts[h].easting, selectedLine.curvePts[h].northing, 0);
                        GL.End();

                        GL.Disable(EnableCap.LineStipple);

                        if (mf.font.isFontOn && selectedLine.curvePts.Count > 410)
                        {
                            GL.Color3(0.40f, 0.90f, 0.95f);
                            mf.font.DrawText3D(selectedLine.curvePts[201].easting, selectedLine.curvePts[201].northing, "&A");
                            mf.font.DrawText3D(selectedLine.curvePts[selectedLine.curvePts.Count - 200].easting, selectedLine.curvePts[selectedLine.curvePts.Count - 200].northing, "&B");
                        }
                    }
                }

                if (curList.Count > 1)
                {
                    GL.Color3(0.95f, 0.2f, 0.95f);
                    GL.Begin(PrimitiveType.LineStrip);
                    for (int h = 0; h < curList.Count; h++) GL.Vertex3(curList[h].easting, curList[h].northing, 0);
                    GL.End();

                    if (isContourBtnOn)
                    {
                        GL.Begin(PrimitiveType.Points);
                        GL.Color3(0.87f, 08.7f, 0.25f);
                        for (int h = 0; h < curList.Count; h++) GL.Vertex3(curList[h].easting, curList[h].northing, 0);
                        GL.End();
                    }

                    if (mf.isPureDisplayOn && !mf.isStanleyUsed)
                    {
                        if (!isContourBtnOn && ppRadius < 200 && ppRadius > -200)
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
                        GL.Color3(1.0f, 1.0f, 0.0f);
                        GL.Vertex3(goalPoint.easting, goalPoint.northing, 0.0);
                        GL.End();
                        GL.PointSize(1.0f);
                    }

                    if (isBtnABLineOn && mf.isSideGuideLines && mf.camera.camSetDistance > mf.tool.toolWidth * -120 && curList.Count > 1)
                    {
                        //get the tool offset and width
                        double toolOffset = mf.tool.toolOffset * 2;
                        double toolWidth = mf.tool.toolWidth - mf.tool.toolOverlap;

                        double heading = Math.Atan2(curList[1].easting - curList[0].easting, curList[1].northing - curList[0].northing);

                        double cosHeading = Math.Cos(-heading);
                        double sinHeading = Math.Sin(-heading);

                        GL.Color3(0.756f, 0.7650f, 0.7650f);
                        GL.Enable(EnableCap.LineStipple);
                        GL.LineStipple(1, 0x0303);

                        GL.Begin(PrimitiveType.Lines);

                        /*
                        for (double i = -2.5; i < 3; i++)
                        {
                            GL.Vertex3((cosHeading * ((mf.tool.toolWidth - mf.tool.toolOverlap) * (howManyPathsAway + i))) + refPoint1.easting, (sinHeading * ((mf.tool.toolWidth - mf.tool.toolOverlap) * (howManyPathsAway + i))) + refPoint1.northing, 0);
                            GL.Vertex3((cosHeading * ((mf.tool.toolWidth - mf.tool.toolOverlap) * (howManyPathsAway + i))) + refPoint2.easting, (sinHeading * ((mf.tool.toolWidth - mf.tool.toolOverlap) * (howManyPathsAway + i))) + refPoint2.northing, 0);
                        }
                        */

                        if (isHeadingSameWay)
                        {
                            GL.Vertex3((cosHeading * (toolWidth + toolOffset)) + curList[0].easting, (sinHeading * (toolWidth + toolOffset)) + curList[0].northing, 0);
                            GL.Vertex3((cosHeading * (toolWidth + toolOffset)) + curList[1].easting, (sinHeading * (toolWidth + toolOffset)) + curList[1].northing, 0);
                            GL.Vertex3((cosHeading * (-toolWidth + toolOffset)) + curList[0].easting, (sinHeading * (-toolWidth + toolOffset)) + curList[0].northing, 0);
                            GL.Vertex3((cosHeading * (-toolWidth + toolOffset)) + curList[1].easting, (sinHeading * (-toolWidth + toolOffset)) + curList[1].northing, 0);

                            toolWidth *= 2;
                            GL.Vertex3((cosHeading * toolWidth) + curList[0].easting, (sinHeading * toolWidth) + curList[0].northing, 0);
                            GL.Vertex3((cosHeading * toolWidth) + curList[1].easting, (sinHeading * toolWidth) + curList[1].northing, 0);
                            GL.Vertex3((cosHeading * (-toolWidth)) + curList[0].easting, (sinHeading * (-toolWidth)) + curList[0].northing, 0);
                            GL.Vertex3((cosHeading * (-toolWidth)) + curList[1].easting, (sinHeading * (-toolWidth)) + curList[1].northing, 0);
                        }
                        else
                        {
                            GL.Vertex3((cosHeading * (toolWidth - toolOffset)) + curList[0].easting, (sinHeading * (toolWidth - toolOffset)) + curList[0].northing, 0);
                            GL.Vertex3((cosHeading * (toolWidth - toolOffset)) + curList[1].easting, (sinHeading * (toolWidth - toolOffset)) + curList[1].northing, 0);
                            GL.Vertex3((cosHeading * (-toolWidth - toolOffset)) + curList[0].easting, (sinHeading * (-toolWidth - toolOffset)) + curList[0].northing, 0);
                            GL.Vertex3((cosHeading * (-toolWidth - toolOffset)) + curList[1].easting, (sinHeading * (-toolWidth - toolOffset)) + curList[1].northing, 0);

                            toolWidth *= 2;
                            GL.Vertex3((cosHeading * toolWidth) + curList[0].easting, (sinHeading * toolWidth) + curList[0].northing, 0);
                            GL.Vertex3((cosHeading * toolWidth) + curList[1].easting, (sinHeading * toolWidth) + curList[1].northing, 0);
                            GL.Vertex3((cosHeading * (-toolWidth)) + curList[0].easting, (sinHeading * (-toolWidth)) + curList[0].northing, 0);
                            GL.Vertex3((cosHeading * (-toolWidth)) + curList[1].easting, (sinHeading * (-toolWidth)) + curList[1].northing, 0);
                        }

                        GL.End();
                        GL.Disable(EnableCap.LineStipple);
                    }
                    mf.yt.DrawYouTurn();
                }
            }
            GL.PointSize(1);
            GL.LineWidth(1);
        }

        public void BuildTram()
        {
            mf.tram.BuildTramBnd();

            mf.tram.tramList.Clear();

            if (selectedLine?.curvePts.Count > 1)
            {
                if (selectedLine.Mode.HasFlag(Mode.AB))
                {
                    bool isBndExist = mf.bnd.bndList.Count != 0;

                    double pass = 0.5;

                    double heading = Math.Atan2(selectedLine.curvePts[1].easting - selectedLine.curvePts[0].easting, selectedLine.curvePts[1].northing - selectedLine.curvePts[0].northing);
                    double hsin = Math.Sin(heading);
                    double hcos = Math.Cos(heading);

                    List<vec2> tramRef = new List<vec2>();
                    //divide up the AB line into segments
                    vec2 P1 = new vec2();
                    for (int i = (int)-abLength; i < abLength; i += 4)
                    {
                        P1.easting = selectedLine.curvePts[0].easting + (hsin * i);
                        P1.northing = selectedLine.curvePts[0].northing + (hcos * i);
                        tramRef.Add(P1);
                    }

                    //create list of list of points of triangle strip of AB Highlight
                    double headingCalc = heading + glm.PIBy2;
                    hsin = Math.Sin(headingCalc);
                    hcos = Math.Cos(headingCalc);


                    //no boundary starts on first pass
                    int cntr = 0;
                    if (isBndExist) cntr = 1;

                    for (int i = cntr; i < mf.tram.passes; i++)
                    {
                        List<vec2> tramArr = new List<vec2>(128);

                        for (int j = 0; j < tramRef.Count; j++)
                        {
                            P1.easting = (hsin * ((mf.tram.tramWidth * (pass + i)) - mf.tram.halfWheelTrack + mf.tool.halfToolWidth)) + tramRef[j].easting;
                            P1.northing = (hcos * ((mf.tram.tramWidth * (pass + i)) - mf.tram.halfWheelTrack + mf.tool.halfToolWidth)) + tramRef[j].northing;

                            if (!isBndExist || mf.bnd.bndList[0].fenceLineEar.IsPointInPolygon(P1))
                            {
                                tramArr.Add(P1);
                            }
                        }

                        mf.tram.tramList.Add(tramArr);
                    }

                    for (int i = cntr; i < mf.tram.passes; i++)
                    {
                        List<vec2> tramArr = new List<vec2>(128);

                        for (int j = 0; j < tramRef.Count; j++)
                        {
                            P1.easting = (hsin * ((mf.tram.tramWidth * (pass + i)) + mf.tram.halfWheelTrack + mf.tool.halfToolWidth)) + tramRef[j].easting;
                            P1.northing = (hcos * ((mf.tram.tramWidth * (pass + i)) + mf.tram.halfWheelTrack + mf.tool.halfToolWidth)) + tramRef[j].northing;

                            if (!isBndExist || mf.bnd.bndList[0].fenceLineEar.IsPointInPolygon(P1))
                            {
                                tramArr.Add(P1);
                            }
                        }
                        mf.tram.tramList.Add(tramArr);
                    }
                }
                else
                {
                    bool isBndExist = mf.bnd.bndList.Count != 0;

                    double pass = 0.5;

                    int refCount = selectedLine.curvePts.Count;

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
                            (Math.Sin(glm.PIBy2 + selectedLine.curvePts[j].heading) *
                                ((mf.tram.tramWidth * (pass + i)) - mf.tram.halfWheelTrack + mf.tool.halfToolWidth)) + selectedLine.curvePts[j].easting,
                            (Math.Cos(glm.PIBy2 + selectedLine.curvePts[j].heading) *
                                ((mf.tram.tramWidth * (pass + i)) - mf.tram.halfWheelTrack + mf.tool.halfToolWidth)) + selectedLine.curvePts[j].northing);

                            bool Add = true;
                            for (int t = 0; t < refCount; t++)
                            {
                                //distance check to be not too close to ref line
                                double dist = ((point.easting - selectedLine.curvePts[t].easting) * (point.easting - selectedLine.curvePts[t].easting))
                                    + ((point.northing - selectedLine.curvePts[t].northing) * (point.northing - selectedLine.curvePts[t].northing));
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
                            (Math.Sin(glm.PIBy2 + selectedLine.curvePts[j].heading) *
                                ((mf.tram.tramWidth * (pass + i)) + mf.tram.halfWheelTrack + mf.tool.halfToolWidth)) + selectedLine.curvePts[j].easting,
                            (Math.Cos(glm.PIBy2 + selectedLine.curvePts[j].heading) *
                                ((mf.tram.tramWidth * (pass + i)) + mf.tram.halfWheelTrack + mf.tool.halfToolWidth)) + selectedLine.curvePts[j].northing);

                            bool Add = true;
                            for (int t = 0; t < refCount; t++)
                            {
                                //distance check to be not too close to ref line
                                double dist = ((point.easting - selectedLine.curvePts[t].easting) * (point.easting - selectedLine.curvePts[t].easting))
                                    + ((point.northing - selectedLine.curvePts[t].northing) * (point.northing - selectedLine.curvePts[t].northing));
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
        }

        public void SetABLineByHeading(double heading)
        {
            if (selectedLine?.curvePts.Count > 1)
            {
                selectedLine.curvePts[0] = new vec3(selectedLine.curvePts[0].easting, selectedLine.curvePts[0].northing, heading);

                selectedLine.curvePts[1] = new vec3(selectedLine.curvePts[0].easting + Math.Sin(heading), selectedLine.curvePts[0].northing + Math.Cos(heading), heading);
            }
        }

        public void MoveABLine(double dist)
        {
            isValid = false;

            if (selectedLine?.curvePts.Count > 1)
            {
                if (selectedLine.Mode.HasFlag(Mode.AB))
                {
                    moveDistance += isHeadingSameWay ? dist : -dist;

                    double heading = Math.Atan2(selectedLine.curvePts[1].easting - selectedLine.curvePts[0].easting, selectedLine.curvePts[1].northing - selectedLine.curvePts[0].northing);

                    selectedLine.curvePts[0] = new vec3(selectedLine.curvePts[0].easting + Math.Cos(heading) * (isHeadingSameWay ? dist : -dist),
                        selectedLine.curvePts[0].northing - Math.Sin(heading) * (isHeadingSameWay ? dist : -dist), heading);
                    selectedLine.curvePts[1] = new vec3(selectedLine.curvePts[0].easting + Math.Sin(heading), selectedLine.curvePts[0].northing + Math.Cos(heading), heading);
                }
                else
                {
                    int cnt = selectedLine.curvePts.Count;
                    vec3[] arr = new vec3[cnt];
                    selectedLine.curvePts.CopyTo(arr);
                    selectedLine.curvePts.Clear();

                    moveDistance += isHeadingSameWay ? dist : -dist;

                    for (int i = 0; i < cnt; i++)
                    {
                        arr[i].easting += Math.Cos(arr[i].heading) * (isHeadingSameWay ? dist : -dist);
                        arr[i].northing -= Math.Sin(arr[i].heading) * (isHeadingSameWay ? dist : -dist);
                        selectedLine.curvePts.Add(arr[i]);
                    }
                }
            }
        }
    }

    public enum Mode { Boundary = 1, AB = 2, Curve = 4, Contour = 8};//, Heading, Circle, Spiral

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
