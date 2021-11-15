using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;

namespace AgOpenGPS
{
    public class CABLine
    {
        public double abLength;

        public int lineWidth;

        public bool isABValid;

        public List<vec3> refList = new List<vec3>();

        public List<vec3> curList = new List<vec3>(); 

        //List of all available ABLines
        public List<CABLines> lineArr = new List<CABLines>();

        public int numABLines, numABLineSelected;

        public bool isABLineBeingSet;
        public bool isABLineSet, isABLineLoaded;
        public bool isBtnABLineOn;

        //design
        public vec2 desPoint1 = new vec2(0.2, 0.15);
        public vec2 desPoint2 = new vec2(0.3, 0.3);

        //pointers to mainform controls
        private readonly FormGPS mf;

        public CABLine(FormGPS _f)
        {
            //constructor
            mf = _f;
            //isOnTramLine = true;
            lineWidth = Properties.Settings.Default.setDisplay_lineWidth;
            abLength = Properties.Settings.Default.setAB_lineLength;
        }

        public void BuildCurrentABLineList(vec3 pivot)
        {
            double dy, dx;

            mf.gyd.lastSecond = mf.secondsSinceStart;

            //move the ABLine over based on the overlap amount set in
            double widthMinusOverlap = mf.tool.toolWidth - mf.tool.toolOverlap;

            curList.Clear();
            if (refList.Count > 1)
            {
                //x2-x1
                dy = refList[1].easting - refList[0].easting;
                //z2-z1
                dx = refList[1].northing - refList[0].northing;

                double heading = Math.Atan2(dy,dx);
                double distanceFromRefLine = ((dx * mf.guidanceLookPos.easting) - (dy * mf.guidanceLookPos.northing) + (refList[1].easting
                                        * refList[0].northing) - (refList[1].northing * refList[0].easting))
                                            / Math.Sqrt((dx * dx) + (dy * dy));

                mf.gyd.isLateralTriggered = false;

                mf.gyd.isHeadingSameWay = Math.PI - Math.Abs(Math.Abs(pivot.heading - heading) - Math.PI) < glm.PIBy2;

                if (mf.yt.isYouTurnTriggered) mf.gyd.isHeadingSameWay = !mf.gyd.isHeadingSameWay;

                //Which ABLine is the vehicle on, negative is left and positive is right side
                double RefDist = (distanceFromRefLine + (mf.gyd.isHeadingSameWay ? mf.tool.toolOffset : -mf.tool.toolOffset)) / widthMinusOverlap;
                if (RefDist < 0) mf.gyd.howManyPathsAway = (int)(RefDist - 0.5);
                else mf.gyd.howManyPathsAway = (int)(RefDist + 0.5);

                double distAway = widthMinusOverlap * mf.gyd.howManyPathsAway + (mf.gyd.isHeadingSameWay ? -mf.tool.toolOffset : mf.tool.toolOffset);

                //depending which way you are going, the offset can be either side
                vec2 point1 = new vec2(refList[0].easting + Math.Cos(-heading) * distAway, refList[0].northing + Math.Sin(-heading) * distAway);

                //create the new line extent points for current ABLine based on original heading of AB line
                curList.Add(new vec3(point1.easting - (Math.Sin(heading) * abLength), point1.northing - (Math.Cos(heading) * abLength), heading));
                curList.Add(new vec3(point1.easting + (Math.Sin(heading) * abLength), point1.northing + (Math.Cos(heading) * abLength), heading));

                isABValid = true;
            }
        }

        public void GetCurrentABLine(vec3 pivot, vec3 steer)
        {
            double dx, dy;

            //build new current ref line if required
            if (!isABValid || ((mf.secondsSinceStart - mf.gyd.lastSecond) > 0.66 && (!mf.isAutoSteerBtnOn || mf.mc.steerSwitchValue != 0)))
                BuildCurrentABLineList(pivot);

            //Check uturn first
            if (mf.yt.isYouTurnTriggered && mf.yt.DistanceFromYouTurnLine())//do the pure pursuit from youTurn
            {
            }
            //Stanley
            else if (mf.isStanleyUsed)
                mf.gyd.StanleyGuidance(pivot, steer, ref curList, true);
            //Pure Pursuit
            else if (curList.Count > 1)
            {
                //get the distance from currently active AB line
                //x2-x1
                dx = curList[1].easting - curList[0].easting;
                //z2-z1
                dy = curList[1].northing - curList[0].northing;

                double heading = Math.Atan2(dx, dy);
                //how far from current AB Line is fix
                mf.gyd.distanceFromCurrentLinePivot = ((dy * pivot.easting) - (dx * pivot.northing) + (curList[1].easting
                            * curList[0].northing) - (curList[1].northing * curList[0].easting))
                            / Math.Sqrt((dy * dy) + (dx * dx));

                //integral slider is set to 0
                if (mf.vehicle.purePursuitIntegralGain != 0 && !mf.isReverse)
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
                    //&& Math.Abs(pivotDistanceError) < 0.2)

                    {
                        //if over the line heading wrong way, rapidly decrease integral
                        if ((mf.gyd.inty < 0 && mf.gyd.distanceFromCurrentLinePivot < 0) || (mf.gyd.inty > 0 && mf.gyd.distanceFromCurrentLinePivot > 0))
                        {
                            mf.gyd.inty += mf.gyd.pivotDistanceError * mf.vehicle.purePursuitIntegralGain * -0.04;
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


                // ** Pure pursuit ** - calc point on ABLine closest to current position
                double U = (((pivot.easting - curList[0].easting) * dx)
                            + ((pivot.northing - curList[0].northing) * dy))
                            / ((dx * dx) + (dy * dy));

                //point on AB line closest to pivot axle point
                mf.gyd.rEast = curList[0].easting + (U * dx);
                mf.gyd.rNorth = curList[0].northing + (U * dy);

                //update base on autosteer settings and distance from line
                double goalPointDistance = mf.vehicle.UpdateGoalPointDistance();

                if (mf.isReverse ? mf.gyd.isHeadingSameWay : !mf.gyd.isHeadingSameWay)
                {
                    mf.gyd.goalPoint.easting = mf.gyd.rEast - (Math.Sin(heading) * goalPointDistance);
                    mf.gyd.goalPoint.northing = mf.gyd.rNorth - (Math.Cos(heading) * goalPointDistance);
                }
                else
                {
                    mf.gyd.goalPoint.easting = mf.gyd.rEast + (Math.Sin(heading) * goalPointDistance);
                    mf.gyd.goalPoint.northing = mf.gyd.rNorth + (Math.Cos(heading) * goalPointDistance);
                }

                //calc "D" the distance from pivot axle to lookahead point
                double goalPointDistanceDSquared
                    = glm.DistanceSquared(mf.gyd.goalPoint.northing, mf.gyd.goalPoint.easting, pivot.northing, pivot.easting);

                //calculate the the new x in local coordinates and steering angle degrees based on wheelbase
                double localHeading;

                if (mf.gyd.isHeadingSameWay) localHeading = glm.twoPI - mf.fixHeading + mf.gyd.inty;
                else localHeading = glm.twoPI - mf.fixHeading - mf.gyd.inty;

                mf.gyd.ppRadius = goalPointDistanceDSquared / (2 * (((mf.gyd.goalPoint.easting - pivot.easting) * Math.Cos(localHeading))
                    + ((mf.gyd.goalPoint.northing - pivot.northing) * Math.Sin(localHeading))));

                mf.gyd.steerAngle = glm.toDegrees(Math.Atan(2 * (((mf.gyd.goalPoint.easting - pivot.easting) * Math.Cos(localHeading))
                    + ((mf.gyd.goalPoint.northing - pivot.northing) * Math.Sin(localHeading))) * mf.vehicle.wheelbase
                    / goalPointDistanceDSquared));

                if (mf.ahrs.imuRoll != 88888)
                    mf.gyd.steerAngle += mf.ahrs.imuRoll * -mf.gyd.sideHillCompFactor;

                if (mf.gyd.steerAngle < -mf.vehicle.maxSteerAngle) mf.gyd.steerAngle = -mf.vehicle.maxSteerAngle;
                if (mf.gyd.steerAngle > mf.vehicle.maxSteerAngle) mf.gyd.steerAngle = mf.vehicle.maxSteerAngle;

                //limit circle size for display purpose
                if (mf.gyd.ppRadius < -500) mf.gyd.ppRadius = -500;
                if (mf.gyd.ppRadius > 500) mf.gyd.ppRadius = 500;

                mf.gyd.radiusPoint.easting = pivot.easting + (mf.gyd.ppRadius * Math.Cos(localHeading));
                mf.gyd.radiusPoint.northing = pivot.northing + (mf.gyd.ppRadius * Math.Sin(localHeading));

                if (mf.isAngVelGuidance)
                {
                    //angular velocity in rads/sec  = 2PI * m/sec * radians/meters
                    mf.setAngVel = 0.277777 * mf.pn.speed * (Math.Tan(glm.toRadians(mf.gyd.steerAngle))) / mf.vehicle.wheelbase;
                    mf.setAngVel = glm.toDegrees(mf.setAngVel) * 100;

                    //clamp the steering angle to not exceed safe angular velocity
                    if (Math.Abs(mf.setAngVel) > 1000)
                    {
                        //mf.setAngVel = mf.setAngVel < 0 ? -mf.vehicle.maxAngularVelocity : mf.vehicle.maxAngularVelocity;
                        mf.setAngVel = mf.setAngVel < 0 ? -1000 : 1000;
                    }
                }

                //distance is negative if on left, positive if on right
                if (!mf.gyd.isHeadingSameWay)
                    mf.gyd.distanceFromCurrentLinePivot *= -1.0;

                //Convert to millimeters
                mf.guidanceLineDistanceOff = (short)Math.Round(mf.gyd.distanceFromCurrentLinePivot * 1000.0, MidpointRounding.AwayFromZero);
                mf.guidanceLineSteerAngle = (short)(mf.gyd.steerAngle * 100);
            }
        }

        public void DrawABLines()
        {
            GL.LineWidth(lineWidth);
            //Draw AB Points
            GL.PointSize(8.0f);
            if (refList.Count > 1)
            {
                GL.Color3(0.95f, 0.0f, 0.0f);

                GL.Begin(PrimitiveType.Points);
                GL.Vertex3(refList[0].easting, refList[0].northing, 0.0);
                GL.End();

                if (mf.font.isFontOn && !isABLineBeingSet)
                    mf.font.DrawText3D(refList[0].easting, refList[0].northing, "&A");
                

                //Draw reference AB line
                GL.Enable(EnableCap.LineStipple);
                GL.LineStipple(1, 0x0F00);
                GL.Color3(0.930f, 0.2f, 0.2f);

                double heading = Math.Atan2(refList[1].easting - refList[0].easting, refList[1].northing - refList[0].northing);

                GL.Begin(PrimitiveType.Lines);
                  GL.Vertex3(refList[0].easting - (Math.Sin(heading) * abLength), refList[0].northing - (Math.Cos(heading) * abLength), 0);
                  GL.Vertex3(refList[1].easting + (Math.Sin(heading) * abLength), refList[1].northing + (Math.Cos(heading) * abLength), 0);
                GL.End();
                GL.Disable(EnableCap.LineStipple);
            }
            GL.PointSize(1.0f);

            //draw current AB Line
            if (curList.Count > 1)
            {
                GL.Begin(PrimitiveType.Lines);
                GL.Color3(0.95f, 0.20f, 0.950f);
                GL.Vertex3(curList[0].easting, curList[0].northing, 0.0);
                GL.Vertex3(curList[1].easting, curList[1].northing, 0.0);
                GL.End();
            }
            //ABLine currently being designed
            if (isABLineBeingSet)
            {
                double heading = Math.Atan2(desPoint2.easting - desPoint1.easting, desPoint2.northing - desPoint1.northing);

                GL.Color3(0.95f, 0.20f, 0.950f);

                GL.Begin(PrimitiveType.Lines);
                  GL.Vertex3(desPoint1.easting - (Math.Sin(heading) * abLength), desPoint1.northing - (Math.Cos(heading) * abLength), 0.0);
                  GL.Vertex3(desPoint1.easting + (Math.Sin(heading) * abLength), desPoint1.northing + (Math.Cos(heading) * abLength), 0.0);
                GL.End();

                GL.Color3(0.2f, 0.950f, 0.20f);
                mf.font.DrawText3D(desPoint1.easting, desPoint1.northing, "&A");
                mf.font.DrawText3D(desPoint2.easting, desPoint2.northing, "&B");
            }

            if (mf.isSideGuideLines && mf.camera.camSetDistance > mf.tool.toolWidth * -120 && curList.Count > 1)
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

                if (mf.gyd.isHeadingSameWay)
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

            if (!mf.isStanleyUsed && mf.camera.camSetDistance > -200)
            {
                //Draw lookahead Point
                GL.PointSize(8.0f);
                GL.Begin(PrimitiveType.Points);
                GL.Color3(1.0f, 1.0f, 0.0f);
                GL.Vertex3(mf.gyd.goalPoint.easting, mf.gyd.goalPoint.northing, 0.0);
                //GL.Vertex3(mf.gyd.rEastSteer, mf.gyd.rNorthSteer, 0.0);
                //GL.Vertex3(mf.gyd.rEastPivot, mf.gyd.rNorthPivot, 0.0);
                GL.End();
                GL.PointSize(1.0f);

                if (mf.gyd.ppRadius < 50 && mf.gyd.ppRadius > -50)
                {
                    const int numSegments = 100;
                    double theta = glm.twoPI / numSegments;
                    double c = Math.Cos(theta);//precalculate the sine and cosine
                    double s = Math.Sin(theta);
                    double x = mf.gyd.ppRadius;//we start at angle = 0
                    double y = 0;

                    GL.LineWidth(1);
                    GL.Color3(0.53f, 0.530f, 0.950f);
                    GL.Begin(PrimitiveType.LineLoop);
                    for (int ii = 0; ii < numSegments; ii++)
                    {
                        //glVertex2f(x + cx, y + cy);//output vertex
                        GL.Vertex3(x + mf.gyd.radiusPoint.easting, y + mf.gyd.radiusPoint.northing, 0);//output vertex
                        double t = x;//apply the rotation matrix
                        x = (c * x) - (s * y);
                        y = (s * t) + (c * y);
                    }
                    GL.End();
                }
            }

            mf.yt.DrawYouTurn();

            GL.PointSize(1.0f);
            GL.LineWidth(1);
        }

        public void BuildTram()
        {
            mf.tram.BuildTramBnd();

            mf.tram.tramList.Clear();
            if (refList.Count > 1)
            {
                List<vec2> tramRef = new List<vec2>();

                bool isBndExist = mf.bnd.bndList.Count != 0;

                double pass = 0.5;

                double heading = Math.Atan2(refList[1].easting - refList[0].easting, refList[1].northing - refList[0].northing);
                double hsin = Math.Sin(heading);
                double hcos = Math.Cos(heading);

                //divide up the AB line into segments
                vec2 P1 = new vec2();
                for (int i = (int)-abLength; i < abLength; i += 4)
                {
                    P1.easting = refList[0].easting + (hsin * i);
                    P1.northing = refList[0].northing + (hcos * i);
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
        }

        public void DeleteAB()
        {
            refList.Clear();
            curList.Clear();

            mf.gyd.howManyPathsAway = 0.0;
            isABLineSet = false;
            isABLineLoaded = false;
        }

        public void SetABLineByHeading(double heading)
        {
            if (refList.Count > 1)
            {
                refList[0] = new vec3(refList[0].easting, refList[0].northing, heading);

                refList[1] = new vec3(refList[0].easting + Math.Sin(heading), refList[0].northing + Math.Cos(heading), heading);
            }

            isABLineSet = true;
            isABLineLoaded = true;
        }

        public void MoveABLine(double dist)
        {
            mf.gyd.moveDistance += mf.gyd.isHeadingSameWay ? dist : -dist;

            if (refList.Count > 1)
            {
                double heading = Math.Atan2(refList[1].easting - refList[0].easting, refList[1].northing - refList[0].northing);

                refList[0] = new vec3(refList[0].easting + Math.Cos(heading) * (mf.gyd.isHeadingSameWay ? dist : -dist),
                    refList[0].northing - Math.Sin(heading) * (mf.gyd.isHeadingSameWay ? dist : -dist), heading);
                refList[1] = new vec3(refList[0].easting + Math.Sin(heading), refList[0].northing + Math.Cos(heading), heading);
            }
            isABValid = false;
        }
    }

    public class CABLines
    {
        public List<vec3> curvePts = new List<vec3>();
        public string Name = "aa";
    }
}