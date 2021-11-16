using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;

namespace AgOpenGPS
{
    public class CABLine
    {
        public double abLength;

        public int lineWidth;

        public List<vec3> curList = new List<vec3>(); 

        public int numABLines, selectedABIndex = -1;

        public bool isABLineBeingSet;
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
            if (selectedABIndex > -1 && mf.gyd.refList[selectedABIndex].curvePts.Count > 1)
            {
                //x2-x1
                dy = mf.gyd.refList[selectedABIndex].curvePts[1].easting - mf.gyd.refList[selectedABIndex].curvePts[0].easting;
                //z2-z1
                dx = mf.gyd.refList[selectedABIndex].curvePts[1].northing - mf.gyd.refList[selectedABIndex].curvePts[0].northing;

                double heading = Math.Atan2(dy,dx);
                double distanceFromRefLine = ((dx * mf.guidanceLookPos.easting) - (dy * mf.guidanceLookPos.northing) + (mf.gyd.refList[selectedABIndex].curvePts[1].easting
                                        * mf.gyd.refList[selectedABIndex].curvePts[0].northing) - (mf.gyd.refList[selectedABIndex].curvePts[1].northing * mf.gyd.refList[selectedABIndex].curvePts[0].easting))
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
                vec2 point1 = new vec2(mf.gyd.refList[selectedABIndex].curvePts[0].easting + Math.Cos(-heading) * distAway, mf.gyd.refList[selectedABIndex].curvePts[0].northing + Math.Sin(-heading) * distAway);

                //create the new line extent points for current ABLine based on original heading of AB line
                curList.Add(new vec3(point1.easting - (Math.Sin(heading) * abLength), point1.northing - (Math.Cos(heading) * abLength), heading));
                curList.Add(new vec3(point1.easting + (Math.Sin(heading) * abLength), point1.northing + (Math.Cos(heading) * abLength), heading));

                mf.gyd.isValid = true;
            }
        }

        public void DrawABLines()
        {
            GL.LineWidth(lineWidth);
            //Draw AB Points
            GL.PointSize(8.0f);
            if (selectedABIndex > -1 && mf.gyd.refList[selectedABIndex].curvePts.Count > 1)
            {
                GL.Color3(0.95f, 0.0f, 0.0f);

                GL.Begin(PrimitiveType.Points);
                GL.Vertex3(mf.gyd.refList[selectedABIndex].curvePts[0].easting, mf.gyd.refList[selectedABIndex].curvePts[0].northing, 0.0);
                GL.End();

                if (mf.font.isFontOn && !isABLineBeingSet)
                    mf.font.DrawText3D(mf.gyd.refList[selectedABIndex].curvePts[0].easting, mf.gyd.refList[selectedABIndex].curvePts[0].northing, "&A");
                

                //Draw reference AB line
                GL.Enable(EnableCap.LineStipple);
                GL.LineStipple(1, 0x0F00);
                GL.Color3(0.930f, 0.2f, 0.2f);

                double heading = Math.Atan2(mf.gyd.refList[selectedABIndex].curvePts[1].easting - mf.gyd.refList[selectedABIndex].curvePts[0].easting, mf.gyd.refList[selectedABIndex].curvePts[1].northing - mf.gyd.refList[selectedABIndex].curvePts[0].northing);

                GL.Begin(PrimitiveType.Lines);
                  GL.Vertex3(mf.gyd.refList[selectedABIndex].curvePts[0].easting - (Math.Sin(heading) * abLength), mf.gyd.refList[selectedABIndex].curvePts[0].northing - (Math.Cos(heading) * abLength), 0);
                  GL.Vertex3(mf.gyd.refList[selectedABIndex].curvePts[1].easting + (Math.Sin(heading) * abLength), mf.gyd.refList[selectedABIndex].curvePts[1].northing + (Math.Cos(heading) * abLength), 0);
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
            if (selectedABIndex > -1 && mf.gyd.refList[selectedABIndex].curvePts.Count > 1)
            {
                List<vec2> tramRef = new List<vec2>();

                bool isBndExist = mf.bnd.bndList.Count != 0;

                double pass = 0.5;

                double heading = Math.Atan2(mf.gyd.refList[selectedABIndex].curvePts[1].easting - mf.gyd.refList[selectedABIndex].curvePts[0].easting, mf.gyd.refList[selectedABIndex].curvePts[1].northing - mf.gyd.refList[selectedABIndex].curvePts[0].northing);
                double hsin = Math.Sin(heading);
                double hcos = Math.Cos(heading);

                //divide up the AB line into segments
                vec2 P1 = new vec2();
                for (int i = (int)-abLength; i < abLength; i += 4)
                {
                    P1.easting = mf.gyd.refList[selectedABIndex].curvePts[0].easting + (hsin * i);
                    P1.northing = mf.gyd.refList[selectedABIndex].curvePts[0].northing + (hcos * i);
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
            selectedABIndex = -1;
            curList.Clear();

            mf.gyd.moveDistance = 0;
            mf.gyd.howManyPathsAway = 0.0;
        }

        public void SetABLineByHeading(double heading)
        {
            if (selectedABIndex > -1 && mf.gyd.refList[selectedABIndex].curvePts.Count > 1)
            {
                mf.gyd.refList[selectedABIndex].curvePts[0] = new vec3(mf.gyd.refList[selectedABIndex].curvePts[0].easting, mf.gyd.refList[selectedABIndex].curvePts[0].northing, heading);

                mf.gyd.refList[selectedABIndex].curvePts[1] = new vec3(mf.gyd.refList[selectedABIndex].curvePts[0].easting + Math.Sin(heading), mf.gyd.refList[selectedABIndex].curvePts[0].northing + Math.Cos(heading), heading);
            }
        }

        public void MoveABLine(double dist)
        {
            if (selectedABIndex > -1 && mf.gyd.refList[selectedABIndex].curvePts.Count > 1)
            {
                mf.gyd.moveDistance += mf.gyd.isHeadingSameWay ? dist : -dist;

                double heading = Math.Atan2(mf.gyd.refList[selectedABIndex].curvePts[1].easting - mf.gyd.refList[selectedABIndex].curvePts[0].easting, mf.gyd.refList[selectedABIndex].curvePts[1].northing - mf.gyd.refList[selectedABIndex].curvePts[0].northing);
                
                mf.gyd.refList[selectedABIndex].curvePts[0] = new vec3(mf.gyd.refList[selectedABIndex].curvePts[0].easting + Math.Cos(heading) * (mf.gyd.isHeadingSameWay ? dist : -dist),
                    mf.gyd.refList[selectedABIndex].curvePts[0].northing - Math.Sin(heading) * (mf.gyd.isHeadingSameWay ? dist : -dist), heading);
                mf.gyd.refList[selectedABIndex].curvePts[1] = new vec3(mf.gyd.refList[selectedABIndex].curvePts[0].easting + Math.Sin(heading), mf.gyd.refList[selectedABIndex].curvePts[0].northing + Math.Cos(heading), heading);
            }
            mf.gyd.isValid = false;
        }
    }
}