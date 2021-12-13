﻿//Please, if you use this, share the improvements

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AgOpenGPS
{
    public partial class FormGPS
    {
        //very first fix to setup grid etc
        public bool isFirstFixPositionSet = false, isGPSPositionInitialized = false, isFirstHeadingSet = false, 
            isReverse = false, isSuperSlow = false;

        //string to record fixes for elevation maps
        public StringBuilder sbFix = new StringBuilder();

        // autosteer variables for sending serial
        public short guidanceLineDistanceOff, guidanceLineSteerAngle;

        public short errorAngVel;
        public double setAngVel;
        public bool isAngVelGuidance;

        //guidance line look ahead
        public double guidanceLookAheadTime = 2;
        public vec2 guidanceLookPos = new vec2(0, 0);

        //how many fix updates per sec
        public int fixUpdateHz = 5;
        public double fixUpdateTime = 0.2;

        //for heading or Atan2 as camera
        public string headingFromSource;

        public vec3 pivotAxlePos = new vec3(0, 0, 0);
        public vec3 steerAxlePos = new vec3(0, 0, 0);
        public vec3 toolPos = new vec3(0, 0, 0);
        public vec3 tankPos = new vec3(0, 0, 0);
        public vec2 hitchPos = new vec2(0, 0);

        //history
        public vec2 prevFix = new vec2(0, 0);
        public vec2 lastReverseFix = new vec2(0, 0);

        //headings
        public double fixHeading = 0.0, camHeading = 0.0, gpsHeading = 10.0, prevGPSHeading = 0.0;

        //storage for the cos and sin of heading
        public double cosSectionHeading = 1.0, sinSectionHeading = 0.0;

        //how far travelled since last section was added, section points
        double sectionTriggerDistance = 0, sectionTriggerStepDistance = 0;

        public vec2 prevSectionPos = new vec2(0, 0);
        public vec2 prevBoundaryPos = new vec2(0, 0);

        //Everything is so wonky at the start
        int startCounter = 0;

        //individual points for the flags in a list
        public List<CFlag> flagPts = new List<CFlag>();

        //tally counters for display
        //public double totalSquareMetersWorked = 0, totalUserSquareMeters = 0, userSquareMetersAlarm = 0;

        public double avgSpeed;//for average speed
        public int crossTrackError;

        //youturn
        public double distancePivotToTurnLine = -2222;
        public double distanceToolToTurnLine = -2222;

        //the value to fill in you turn progress bar
        public int youTurnProgressBar = 0;

        //IMU 
        public double rollCorrectionDistance = 0;
        public double imuGPS_Offset, imuCorrected;

        //step position - slow speed spinner killer
        private int currentStepFix = 0;
        private const int totalFixSteps = 20;
        public vecFix2Fix[] stepFixPts = new vecFix2Fix[totalFixSteps];
        public double distanceCurrentStepFix = 0, minFixStepDist = 1, startSpeed = 0.5;

        private double nowHz = 0;

        public bool isRTK, isRTK_KillAutosteer;

        public vec2 lastGPS = new vec2(0, 0);

        public int framesToDo = 0;

        public void UpdateFixPosition()
        {
            //Measure the frequency of the GPS updates
            swHz.Stop();
            nowHz = ((double)System.Diagnostics.Stopwatch.Frequency) / (double)swHz.ElapsedTicks;

            //simple comp filter
            if (nowHz < 35) HzTime = 0.97 * HzTime + 0.03 * nowHz;

            //auto set gps freq
            fixUpdateHz = (int)(HzTime + 0.5);
            fixUpdateTime = 1 / HzTime;

            swHz.Reset();
            swHz.Start();

            //start the watch and time till it finishes
            swFrame.Reset();
            swFrame.Start();

            startCounter++;

            if (!isGPSPositionInitialized)
            {
                InitializeFirstFewGPSPositions();
                return;
            }

            switch (headingFromSource)
            {
                case "Fix":
                    {
                        //calculate current heading only when moving, otherwise use last

                        if (Math.Abs(avgSpeed) < 1.5 && !isFirstHeadingSet)
                            goto byPass;

                        if (!isFirstHeadingSet) //set in steer settings, Stanley
                        {
                            prevFix.easting = stepFixPts[0].easting; prevFix.northing = stepFixPts[0].northing;

                            if (stepFixPts[2].isSet == 0)
                            {
                                //this is the first position no roll or offset correction
                                if (stepFixPts[0].isSet == 0)
                                {
                                    stepFixPts[0].easting = pn.fix.easting;
                                    stepFixPts[0].northing = pn.fix.northing;
                                    stepFixPts[0].isSet = 1;
                                    return;
                                }

                                //and the second
                                if (stepFixPts[1].isSet == 0)
                                {
                                    for (int i = totalFixSteps - 1; i > 0; i--) stepFixPts[i] = stepFixPts[i - 1];
                                    stepFixPts[0].easting = pn.fix.easting;
                                    stepFixPts[0].northing = pn.fix.northing;
                                    stepFixPts[0].isSet = 1;
                                    return;
                                }

                                //the critcal moment for checking initial direction/heading.
                                for (int i = totalFixSteps - 1; i > 0; i--) stepFixPts[i] = stepFixPts[i - 1];
                                stepFixPts[0].easting = pn.fix.easting;
                                stepFixPts[0].northing = pn.fix.northing;
                                stepFixPts[0].isSet = 1;

                                gpsHeading = Math.Atan2(pn.fix.easting - stepFixPts[2].easting,
                                    pn.fix.northing - stepFixPts[2].northing);

                                if (gpsHeading < 0) gpsHeading += glm.twoPI;
                                else if (gpsHeading > glm.twoPI) gpsHeading -= glm.twoPI;

                                fixHeading = gpsHeading;
                                camHeading = glm.toDegrees(gpsHeading);

                                //now we have a heading, fix the first 3
                                if (vehicle.antennaOffset != 0)
                                {
                                    for (int i = 0; i < 3; i++)
                                    {
                                        stepFixPts[i].easting = (Math.Cos(-gpsHeading) * vehicle.antennaOffset) + stepFixPts[i].easting;
                                        stepFixPts[i].northing = (Math.Sin(-gpsHeading) * vehicle.antennaOffset) + stepFixPts[i].northing;
                                    }
                                }

                                if (ahrs.imuRoll != 88888)
                                {

                                    //change for roll to the right is positive times -1
                                    rollCorrectionDistance = Math.Tan(glm.toRadians((ahrs.imuRoll))) * -vehicle.antennaHeight;

                                    // roll to left is positive  **** important!!
                                    // not any more - April 30, 2019 - roll to right is positive Now! Still Important
                                    for (int i = 0; i < 3; i++)
                                    {
                                        stepFixPts[i].easting = (Math.Cos(-gpsHeading) * rollCorrectionDistance) + stepFixPts[i].easting;
                                        stepFixPts[i].northing = (Math.Sin(-gpsHeading) * rollCorrectionDistance) + stepFixPts[i].northing;
                                    }
                                }

                                //get the distance from first to 2nd point, update fix with new offset/roll
                                stepFixPts[0].distance = glm.Distance(stepFixPts[1], stepFixPts[0]);
                                pn.fix.easting = stepFixPts[0].easting;
                                pn.fix.northing = stepFixPts[0].northing;

                                isFirstHeadingSet = true;
                                TimedMessageBox(2000, "Direction Reset", "Forward is Set");

                                return;
                            }
                        }

                        if (vehicle.antennaOffset != 0)
                        {
                            pn.fix.easting = (Math.Cos(-gpsHeading) * vehicle.antennaOffset) + pn.fix.easting;
                            pn.fix.northing = (Math.Sin(-gpsHeading) * vehicle.antennaOffset) + pn.fix.northing;
                        }

                        //originalEasting = pn.fix.easting;
                        if (ahrs.imuRoll != 88888)
                        {
                            //change for roll to the right is positive times -1
                            rollCorrectionDistance = Math.Tan(glm.toRadians((ahrs.imuRoll))) * -vehicle.antennaHeight;

                            // roll to left is positive  **** important!!
                            // not any more - April 30, 2019 - roll to right is positive Now! Still Important
                            pn.fix.easting = (Math.Cos(-gpsHeading) * rollCorrectionDistance) + pn.fix.easting;
                            pn.fix.northing = (Math.Sin(-gpsHeading) * rollCorrectionDistance) + pn.fix.northing;
                        }

                        //initializing all done
                        if (Math.Abs(avgSpeed) > startSpeed)
                        {
                            isSuperSlow = false;
                            //how far since last fix
                            distanceCurrentStepFix = glm.Distance(stepFixPts[0], pn.fix);

                            //save current fix and distance and set as valid
                            for (int i = totalFixSteps - 1; i > 0; i--) stepFixPts[i] = stepFixPts[i - 1];
                            stepFixPts[0].easting = pn.fix.easting;
                            stepFixPts[0].northing = pn.fix.northing;
                            stepFixPts[0].isSet = 1;
                            stepFixPts[0].distance = distanceCurrentStepFix;

                            if (stepFixPts[1].isSet == 0)
                                return;

                            //find back the fix to fix distance, then heading
                            double dist = 0;
                            for (int i = 1; i < totalFixSteps; i++)
                            {
                                if (stepFixPts[i].isSet == 0)
                                {
                                    currentStepFix = i - 1;
                                    break;
                                }
                                dist += stepFixPts[i - 1].distance;
                                currentStepFix = i;
                                if (dist > minFixStepDist)
                                    break;
                            }

                            if (dist < 0.25) goto byPass;

                            //most recent heading
                            double newHeading = Math.Atan2(pn.fix.easting - stepFixPts[currentStepFix].easting,
                                                        pn.fix.northing - stepFixPts[currentStepFix].northing);
                            if (newHeading < 0) newHeading += glm.twoPI;

                            //update the last gps for slow speed.
                            lastGPS = pn.fix;

                            if (ahrs.isReverseOn)
                            {
                                //what is angle between the last valid heading before stopping and one just now
                                double delta = Math.Abs(Math.PI - Math.Abs(Math.Abs(newHeading - gpsHeading) - Math.PI));

                                //ie change in direction
                                if (delta > 1.57) //
                                {
                                    isReverse = true;
                                    newHeading += Math.PI;
                                    if (newHeading < 0) newHeading += glm.twoPI;
                                    else if (newHeading >= glm.twoPI) newHeading -= glm.twoPI;
                                }
                                else
                                    isReverse = false;
                            }

                            if (isReverse)
                                newHeading -= glm.toRadians(vehicle.antennaPivot / 1
                                    * mc.actualSteerAngleDegrees * ahrs.reverseComp);
                            else
                                newHeading -= glm.toRadians(vehicle.antennaPivot / 1
                                    * mc.actualSteerAngleDegrees * ahrs.forwardComp);

                            if (newHeading < 0) newHeading += glm.twoPI;
                            else if (newHeading >= glm.twoPI) newHeading -= glm.twoPI;

                            //set the headings
                            fixHeading = gpsHeading = newHeading;
                            camHeading = glm.toDegrees(gpsHeading);
                        }

                        //slow speed and reverse
                        else
                        {
                            isSuperSlow = true;

                            if (stepFixPts[0].isSet == 1)
                            {
                                //going back and forth clear out the array so only 1 value
                                for (int i = 0; i < stepFixPts.Length; i++)
                                {
                                    stepFixPts[i].easting = 0;
                                    stepFixPts[i].northing = 0;
                                    stepFixPts[i].isSet = 0;
                                    stepFixPts[i].distance = 0;
                                }
                                return;
                            }

                            if (!ahrs.isReverseOn) goto byPass;

                            //how far since last fix
                            distanceCurrentStepFix = glm.Distance(lastGPS, pn.fix);

                            //new heading if exceeded fix heading step distance
                            if (distanceCurrentStepFix > minFixStepDist)
                            {
                                //most recent heading
                                double newHeading = Math.Atan2(pn.fix.easting - lastGPS.easting,
                                                            pn.fix.northing - lastGPS.northing);

                                //Pointing the opposite way the fixes are moving
                                //if (vehicle.isReverse) gpsHeading += Math.PI;
                                if (newHeading < 0) newHeading += glm.twoPI;

                                if (ahrs.isReverseOn)
                                {
                                    //what is angle between the last valid heading before stopping and one just now
                                    double delta = Math.Abs(Math.PI - Math.Abs(Math.Abs(newHeading - gpsHeading) - Math.PI));

                                    //ie change in direction
                                    {
                                        if (delta > 1.57) //
                                        {
                                            isReverse = true;
                                            newHeading += Math.PI;
                                            if (newHeading < 0) newHeading += glm.twoPI;
                                        }
                                        else
                                            isReverse = false;
                                    }
                                }

                                //set the headings
                                fixHeading = gpsHeading = newHeading;
                                camHeading = glm.toDegrees(gpsHeading);

                                lastGPS.easting = pn.fix.easting;
                                lastGPS.northing = pn.fix.northing;
                            }
                        }

                        // IMU Fusion with heading correction, add the correction
                        if (ahrs.imuHeading != 99999)
                        {
                            //current gyro angle in radians
                            double imuHeading = (glm.toRadians(ahrs.imuHeading));

                            //Difference between the IMU heading and the GPS heading
                            double gyroDelta = (imuHeading + imuGPS_Offset) - gpsHeading;
                            //double gyroDelta = Math.Abs(Math.PI - Math.Abs(Math.Abs(imuHeading + gyroCorrection) - gpsHeading) - Math.PI);

                            if (gyroDelta < 0) gyroDelta += glm.twoPI;
                            else if (gyroDelta > glm.twoPI) gyroDelta -= glm.twoPI;

                            //calculate delta based on circular data problem 0 to 360 to 0, clamp to +- 2 Pi
                            if (gyroDelta >= -glm.PIBy2 && gyroDelta <= glm.PIBy2) gyroDelta *= -1.0;
                            else
                            {
                                if (gyroDelta > glm.PIBy2) { gyroDelta = glm.twoPI - gyroDelta; }
                                else { gyroDelta = (glm.twoPI + gyroDelta) * -1.0; }
                            }
                            if (gyroDelta > glm.twoPI) gyroDelta -= glm.twoPI;
                            else if (gyroDelta < -glm.twoPI) gyroDelta += glm.twoPI;

                            if (!isReverse)
                                imuGPS_Offset += (gyroDelta * (ahrs.fusionWeight));
                            else
                                imuGPS_Offset += (gyroDelta * (0.005));

                            if (imuGPS_Offset > glm.twoPI) imuGPS_Offset -= glm.twoPI;
                            else if (imuGPS_Offset < 0) imuGPS_Offset += glm.twoPI;

                            //determine the Corrected heading based on gyro and GPS
                            imuCorrected = imuHeading + imuGPS_Offset;
                            if (imuCorrected > glm.twoPI) imuCorrected -= glm.twoPI;
                            else if (imuCorrected < 0) imuCorrected += glm.twoPI;

                            //use imu as heading when going slow
                            fixHeading = imuCorrected;

                            camHeading = fixHeading;
                            if (camHeading > glm.twoPI) camHeading -= glm.twoPI;
                            camHeading = glm.toDegrees(camHeading);
                        }

                        //Calculate a million other things
                        byPass:
                        break;
                    }

                case "VTG":
                    {
                        isFirstHeadingSet = true;
                        if (pn.speed > startSpeed)
                        {
                            //use NMEA headings for camera and tractor graphic
                            fixHeading = glm.toRadians(pn.headingTrue);
                            camHeading = pn.headingTrue;
                            gpsHeading = fixHeading;
                        }

                        //grab the most current fix to last fix distance
                        distanceCurrentStepFix = glm.Distance(pn.fix, prevFix);

                        //an IMU with heading correction, add the correction
                        if (ahrs.imuHeading != 99999)
                        {
                            //current gyro angle in radians
                            double correctionHeading = (glm.toRadians(ahrs.imuHeading));

                            //Difference between the IMU heading and the GPS heading
                            double gyroDelta = (correctionHeading + imuGPS_Offset) - gpsHeading;
                            if (gyroDelta < 0) gyroDelta += glm.twoPI;

                            //calculate delta based on circular data problem 0 to 360 to 0, clamp to +- 2 Pi
                            if (gyroDelta >= -glm.PIBy2 && gyroDelta <= glm.PIBy2) gyroDelta *= -1.0;
                            else
                            {
                                if (gyroDelta > glm.PIBy2) { gyroDelta = glm.twoPI - gyroDelta; }
                                else { gyroDelta = (glm.twoPI + gyroDelta) * -1.0; }
                            }
                            if (gyroDelta > glm.twoPI) gyroDelta -= glm.twoPI;
                            if (gyroDelta < -glm.twoPI) gyroDelta += glm.twoPI;

                            //if the gyro and last corrected fix is < 10 degrees, super low pass for gps
                            if (Math.Abs(gyroDelta) < 0.18)
                            {
                                //a bit of delta and add to correction to current gyro
                                imuGPS_Offset += (gyroDelta * (0.1));
                                if (imuGPS_Offset > glm.twoPI) imuGPS_Offset -= glm.twoPI;
                                if (imuGPS_Offset < -glm.twoPI) imuGPS_Offset += glm.twoPI;
                            }
                            else
                            {
                                //a bit of delta and add to correction to current gyro
                                imuGPS_Offset += (gyroDelta * (0.2));
                                if (imuGPS_Offset > glm.twoPI) imuGPS_Offset -= glm.twoPI;
                                if (imuGPS_Offset < -glm.twoPI) imuGPS_Offset += glm.twoPI;
                            }

                            //determine the Corrected heading based on gyro and GPS
                            imuCorrected = correctionHeading + imuGPS_Offset;
                            if (imuCorrected > glm.twoPI) imuCorrected -= glm.twoPI;
                            if (imuCorrected < 0) imuCorrected += glm.twoPI;

                            fixHeading = imuCorrected;

                            camHeading = fixHeading;
                            if (camHeading > glm.twoPI) camHeading -= glm.twoPI;
                            camHeading = glm.toDegrees(camHeading);
                        }

                        #region Antenna Offset

                        if (vehicle.antennaOffset != 0)
                        {
                            pn.fix.easting = (Math.Cos(-fixHeading) * vehicle.antennaOffset) + pn.fix.easting;
                            pn.fix.northing = (Math.Sin(-fixHeading) * vehicle.antennaOffset) + pn.fix.northing;
                        }
                        #endregion

                        #region Roll

                        if (ahrs.imuRoll != 88888)
                        {
                            //change for roll to the right is positive times -1
                            rollCorrectionDistance = Math.Tan(glm.toRadians((ahrs.imuRoll))) * -vehicle.antennaHeight;

                            // roll to left is positive  **** important!!
                            // not any more - April 30, 2019 - roll to right is positive Now! Still Important
                            pn.fix.easting = (Math.Cos(-fixHeading) * rollCorrectionDistance) + pn.fix.easting;
                            pn.fix.northing = (Math.Sin(-fixHeading) * rollCorrectionDistance) + pn.fix.northing;
                        }

                        #endregion Roll

                        //most recent fixes are now the prev ones
                        prevFix.easting = pn.fix.easting; prevFix.northing = pn.fix.northing;

                        break;
                    }

                case "Dual":
                    {
                        isFirstHeadingSet = true;
                        //use Dual Antenna heading for camera and tractor graphic
                        fixHeading = glm.toRadians(pn.headingTrueDual);
                        camHeading = pn.headingTrueDual;
                        gpsHeading = fixHeading;


                        if (glm.DistanceSquared(lastReverseFix, pn.fix) > 0.6)
                        {
                            //most recent heading
                            double newHeading = Math.Atan2(pn.fix.easting - lastReverseFix.easting,
                                                        pn.fix.northing - lastReverseFix.northing);

                            //what is angle between the last reverse heading and current dual heading
                            double delta = Math.Abs(Math.PI - Math.Abs(Math.Abs(newHeading - fixHeading) - Math.PI));

                            //are we going backwards
                            isReverse = delta > 2 ? true : false;

                            //save for next meter check
                            lastReverseFix = pn.fix;
                        }

                        if (vehicle.antennaOffset != 0)
                        {
                            pn.fix.easting = (Math.Cos(-fixHeading) * vehicle.antennaOffset) + pn.fix.easting;
                            pn.fix.northing = (Math.Sin(-fixHeading) * vehicle.antennaOffset) + pn.fix.northing;
                        }

                        if (ahrs.imuRoll != 88888 && vehicle.antennaHeight != 0)
                        {

                            //change for roll to the right is positive times -1
                            rollCorrectionDistance = Math.Tan(glm.toRadians((ahrs.imuRoll))) * -vehicle.antennaHeight;
                            pn.fix.easting = (Math.Cos(-gpsHeading) * rollCorrectionDistance) + pn.fix.easting;
                            pn.fix.northing = (Math.Sin(-gpsHeading) * rollCorrectionDistance) + pn.fix.northing;
                        }

                        //grab the most current fix and save the distance from the last fix
                        distanceCurrentStepFix = glm.Distance(pn.fix, prevFix);

                        break;
                    }

                default:
                    break;
            }

            //most recent fixes are now the prev ones
            prevFix.easting = pn.fix.easting; prevFix.northing = pn.fix.northing;

            //positions and headings 
            CalculatePositionHeading();

            //calculate lookahead at full speed, no sentence misses
            CalculateSectionLookAhead(toolPos.northing, toolPos.easting, cosSectionHeading, sinSectionHeading);

            //To prevent drawing high numbers of triangles, determine and test before drawing vertex
            sectionTriggerDistance = glm.Distance(pn.fix, prevSectionPos);

            //section on off and points, contour points
            if (sectionTriggerDistance > sectionTriggerStepDistance && isJobStarted)
            {
                AddSectionOrContourPathPoints();

                //grab fix and elevation
                if (isLogElevation) sbFix.Append(pn.fix.easting.ToString("N2") + "," + pn.fix.northing.ToString("N2") + ","
                                                    + pn.altitude.ToString("N2") + ","
                                                    + pn.latitude + "," + pn.longitude + "\r\n");
            }

            //test if travelled far enough for new boundary point
            if (bnd.isOkToAddPoints)
            {
                double boundaryDistance = glm.Distance(pn.fix, prevBoundaryPos);
                if (boundaryDistance > 1) AddBoundaryPoint();
            }

            //calc distance travelled since last GPS fix
            //distance = glm.Distance(pn.fix, prevFix);
            if (pn.speed > 1)
            {
                if ((fd.distanceUser += distanceCurrentStepFix) > 3000) fd.distanceUser = 0; ;//userDistance can be reset
            }


            #region AutoSteer

            //preset the values
            guidanceLineDistanceOff = 32000;

            if (gyd.isContourBtnOn || gyd.isBtnABLineOn || gyd.isBtnCurveOn)
                gyd.GetCurrentLine(pivotAxlePos, steerAxlePos);

            // autosteer at full speed of updates

            //if the whole path driving driving process is green
            if (recPath.isDrivingRecordedPath) recPath.UpdatePosition();

            // If Drive button off - normal autosteer 
            if (!mc.isInFreeDriveMode)
            {
                //fill up0 the appropriate arrays with new values
                p_254.pgn[p_254.speedHi] = unchecked((byte)((int)(Math.Abs(pn.speed) * 10.0) >> 8));
                p_254.pgn[p_254.speedLo] = unchecked((byte)((int)(Math.Abs(pn.speed) * 10.0)));
                //mc.machineControlData[mc.cnSpeed] = mc.autoSteerData[mc.sdSpeed];

                //save distance for display
                lightbarDistance = guidanceLineDistanceOff;

                if (!isAutoSteerBtnOn) //32020 means auto steer is off
                {
                    guidanceLineDistanceOff = 32020;
                    p_254.pgn[p_254.status] = 0;
                }

                else p_254.pgn[p_254.status] = 1;

                if (recPath.isDrivingRecordedPath || recPath.isFollowingDubinsToPath) p_254.pgn[p_254.status] = 1;

                //mc.autoSteerData[7] = unchecked((byte)(guidanceLineDistanceOff >> 8));
                //mc.autoSteerData[8] = unchecked((byte)(guidanceLineDistanceOff));

                //convert to cm from mm and divide by 2 - lightbar
                int distanceX2;
                if (guidanceLineDistanceOff == 32020 || guidanceLineDistanceOff == 32000)
                    distanceX2 = 255;

                else
                {
                    distanceX2 = (int)(guidanceLineDistanceOff * 0.05);

                    if (distanceX2 < -127) distanceX2 = -127;
                    else if (distanceX2 > 127) distanceX2 = 127;
                    distanceX2 += 127;
                }

                p_254.pgn[p_254.lineDistance] = unchecked((byte)distanceX2);

                if (isAngVelGuidance)
                {
                    errorAngVel = (short)(((int)(setAngVel) - ahrs.angVel));

                    p_254.pgn[p_254.steerAngleHi] = unchecked((byte)(errorAngVel >> 8));
                    p_254.pgn[p_254.steerAngleLo] = unchecked((byte)(errorAngVel));
                }
                else
                {
                    p_254.pgn[p_254.steerAngleHi] = unchecked((byte)(guidanceLineSteerAngle >> 8));
                    p_254.pgn[p_254.steerAngleLo] = unchecked((byte)(guidanceLineSteerAngle));
                }
            }
            else //Drive button is on
            {
                //fill up the auto steer array with free drive values
                p_254.pgn[p_254.speedHi] = unchecked((byte)((int)(80) >> 8));
                p_254.pgn[p_254.speedLo] = unchecked((byte)((int)(80)));

                //turn on status to operate
                p_254.pgn[p_254.status] = 1;

                //send the steer angle
                guidanceLineSteerAngle = (Int16)(mc.driveFreeSteerAngle * 100);

                if (isAngVelGuidance)
                {
                    setAngVel = 0.277777 * avgSpeed * (Math.Tan(glm.toRadians(mc.driveFreeSteerAngle))) / vehicle.wheelbase;
                    setAngVel = glm.toDegrees(setAngVel) * 100;

                    errorAngVel = (short)(((int)(setAngVel) - ahrs.angVel));

                    p_254.pgn[p_254.steerAngleHi] = unchecked((byte)(errorAngVel >> 8));
                    p_254.pgn[p_254.steerAngleLo] = unchecked((byte)(errorAngVel));
                }
                else
                {
                    p_254.pgn[p_254.steerAngleHi] = unchecked((byte)(guidanceLineSteerAngle >> 8));
                    p_254.pgn[p_254.steerAngleLo] = unchecked((byte)(guidanceLineSteerAngle));
                }
            }

            //out serial to autosteer module  //indivdual classes load the distance and heading deltas 
            SendPgnToLoop(p_254.pgn);

            //for average cross track error
            if (guidanceLineDistanceOff < 29000)
            {
                crossTrackError = (int)((double)crossTrackError * 0.90 + Math.Abs((double)guidanceLineDistanceOff) * 0.1);
            }
            else
            {
                crossTrackError = 0;
            }

            #endregion

            #region Youturn

            //reset the fault distance to an appropriate weird number
            //-2222 means it fell out of the loop completely
            //-3333 means unable to find a nearest point at all even though inside the work area of field
            // -4444 means cross trac error too high
            distancePivotToTurnLine = -4444;

            //always force out of bounds and change only if in bounds after proven so
            mc.isOutOfBounds = true;

            //if an outer boundary is set, then apply critical stop logic
            if (bnd.bndList.Count > 0)
            {
                //Are we inside outer and outside inner all turn boundaries, no turn creation problems
                if (bnd.IsPointInsideFenceArea(pivotAxlePos) && !yt.isTurnCreationTooClose && !yt.isTurnCreationNotCrossingError)
                {
                    //reset critical stop for bounds violation
                    mc.isOutOfBounds = false;

                    //do the auto youturn logic if everything is on.
                    if (yt.isYouTurnBtnOn && isAutoSteerBtnOn)
                    {
                        //if we are too much off track > 1.3m, kill the diagnostic creation, start again
                        if (crossTrackError > 1300 && !yt.isYouTurnTriggered)
                        {
                            yt.ResetCreatedYouTurn();
                        }
                        else
                        {
                            //now check to make sure we are not in an inner turn boundary - drive thru is ok
                            if (yt.youTurnPhase != 3)
                            {
                                if (crossTrackError > 500)
                                    yt.ResetCreatedYouTurn();
                                else if (gyd.isBtnABLineOn)
                                    yt.BuildABLineDubinsYouTurn(yt.isYouTurnRight);
                                else
                                    yt.BuildCurveDubinsYouTurn(yt.isYouTurnRight, pivotAxlePos);
                                
                                if (yt.youTurnPhase == 3) yt.SmoothYouTurn(yt.uTurnSmoothing);
                            }
                            else //wait to trigger the actual turn since its made and waiting
                            {
                                //distance from current pivot to first point of youturn pattern
                                distancePivotToTurnLine = glm.Distance(yt.ytList[5], pivotAxlePos);

                                if ((distancePivotToTurnLine <= 20.0) && (distancePivotToTurnLine >= 18.0) && !yt.isYouTurnTriggered)

                                    if (!sounds.isBoundAlarming)
                                    {
                                        if (sounds.isTurnSoundOn) CSound.sndBoundaryAlarm.Play();
                                        sounds.isBoundAlarming = true;
                                    }

                                //if we are close enough to pattern, trigger.
                                if ((distancePivotToTurnLine <= 1.0) && (distancePivotToTurnLine >= 0) && !yt.isYouTurnTriggered)
                                {
                                    yt.YouTurnTrigger();
                                    sounds.isBoundAlarming = false;
                                }
                            }
                        }
                    } // end of isInWorkingArea
                }
                // here is stop logic for out of bounds - in an inner or out the outer turn border.
                else
                {
                    mc.isOutOfBounds = true;
                    if (yt.isYouTurnBtnOn)
                    {
                        yt.ResetCreatedYouTurn();
                        sim.stepDistance = 0;
                    }
                }
            }
            else
            {
                mc.isOutOfBounds = false;
            }

            #endregion

            if (isGPSPositionInitialized && isJobStarted)
            {
                //draw the section control window off screen buffer
                if (isFastSections || bbCounter++ > 0)
                {
                    bbCounter = 0;
                    oglBack.Refresh();
                    SendPgnToLoop(p_239.pgn);
                }
            }

            if (framesToDo < 2)
            {
                //update main window
                oglMain.MakeCurrent();
                oglMain.Refresh();
            }
            else
            {
                udpWatchCounts++;
                if (isLogNMEA) pn.logNMEASentence.Append("*** "
                    + DateTime.UtcNow.ToString("ss.ff -> ", CultureInfo.InvariantCulture) + "\r\n");
            }

            //end of UppdateFixPosition
            swFrame.Stop();

            //stop the timer and calc how long it took to do calcs and draw
            double frameTimeRough = (double)swFrame.ElapsedTicks / (double)System.Diagnostics.Stopwatch.Frequency * 1000;

            if (frameTimeRough > 30) frameTimeRough = 30;
            frameTime = frameTime * 0.99 + frameTimeRough * 0.01;
        }

        //all the hitch, pivot, section, trailing hitch, headings and fixes
        private void CalculatePositionHeading()
        {
            #region pivot hitch trail

            //translate from pivot position to steer axle and pivot axle position
            //translate world to the pivot axle
            pivotAxlePos.easting = pn.fix.easting - (Math.Sin(fixHeading) * vehicle.antennaPivot);
            pivotAxlePos.northing = pn.fix.northing - (Math.Cos(fixHeading) * vehicle.antennaPivot);
            pivotAxlePos.heading = fixHeading;

            steerAxlePos.easting = pivotAxlePos.easting + (Math.Sin(fixHeading) * vehicle.wheelbase);
            steerAxlePos.northing = pivotAxlePos.northing + (Math.Cos(fixHeading) * vehicle.wheelbase);
            steerAxlePos.heading = fixHeading;

            //guidance look ahead distance based on time or tool width at least 
            
            if (!gyd.isLateralTriggered)
            {
                double guidanceLookDist = (Math.Max(tool.toolWidth * 0.5, avgSpeed * 0.277777 * guidanceLookAheadTime));
                guidanceLookPos.easting = pivotAxlePos.easting + (Math.Sin(fixHeading) * guidanceLookDist);
                guidanceLookPos.northing = pivotAxlePos.northing + (Math.Cos(fixHeading) * guidanceLookDist);
            }

            //determine where the rigid vehicle hitch ends
            hitchPos.easting = pn.fix.easting + (Math.Sin(fixHeading) * (tool.hitchLength - vehicle.antennaPivot));
            hitchPos.northing = pn.fix.northing + (Math.Cos(fixHeading) * (tool.hitchLength - vehicle.antennaPivot));

            //tool attached via a trailing hitch
            if (tool.isToolTrailing)
            {
                double over;
                if (tool.isToolTBT)
                {
                    //Torriem rules!!!!! Oh yes, this is all his. Thank-you
                    if (distanceCurrentStepFix != 0)
                    {
                        double t = (tool.toolTankTrailingHitchLength) / distanceCurrentStepFix;
                        tankPos.easting = hitchPos.easting + t * (hitchPos.easting - tankPos.easting);
                        tankPos.northing = hitchPos.northing + t * (hitchPos.northing - tankPos.northing);
                        tankPos.heading = Math.Atan2(hitchPos.easting - tankPos.easting, hitchPos.northing - tankPos.northing);
                    }

                    ////the tool is seriously jacknifed or just starting out so just spring it back.
                    over = Math.Abs(Math.PI - Math.Abs(Math.Abs(tankPos.heading - fixHeading) - Math.PI));

                    if (over < 2.0 && startCounter > 50)
                    {
                        tankPos.easting = hitchPos.easting + (Math.Sin(tankPos.heading) * (tool.toolTankTrailingHitchLength));
                        tankPos.northing = hitchPos.northing + (Math.Cos(tankPos.heading) * (tool.toolTankTrailingHitchLength));
                    }

                    //criteria for a forced reset to put tool directly behind vehicle
                    if (over > 2.0 | startCounter < 51)
                    {
                        tankPos.heading = fixHeading;
                        tankPos.easting = hitchPos.easting + (Math.Sin(tankPos.heading) * (tool.toolTankTrailingHitchLength));
                        tankPos.northing = hitchPos.northing + (Math.Cos(tankPos.heading) * (tool.toolTankTrailingHitchLength));
                    }
                }
                else
                {
                    tankPos.heading = fixHeading;
                    tankPos.easting = hitchPos.easting;
                    tankPos.northing = hitchPos.northing;
                }

                //Torriem rules!!!!! Oh yes, this is all his. Thank-you
                if (distanceCurrentStepFix != 0)
                {
                    double t = (tool.toolTrailingHitchLength) / distanceCurrentStepFix;
                    toolPos.easting = tankPos.easting + t * (tankPos.easting - toolPos.easting);
                    toolPos.northing = tankPos.northing + t * (tankPos.northing - toolPos.northing);
                    toolPos.heading = Math.Atan2(tankPos.easting - toolPos.easting, tankPos.northing - toolPos.northing);
                }

                ////the tool is seriously jacknifed or just starting out so just spring it back.
                over = Math.Abs(Math.PI - Math.Abs(Math.Abs(toolPos.heading - tankPos.heading) - Math.PI));

                if (over < 1.9 && startCounter > 50)
                {
                    toolPos.easting = tankPos.easting + (Math.Sin(toolPos.heading) * (tool.toolTrailingHitchLength));
                    toolPos.northing = tankPos.northing + (Math.Cos(toolPos.heading) * (tool.toolTrailingHitchLength));
                }

                //criteria for a forced reset to put tool directly behind vehicle
                if (over > 1.9 | startCounter < 51)
                {
                    toolPos.heading = tankPos.heading;
                    toolPos.easting = tankPos.easting + (Math.Sin(toolPos.heading) * (tool.toolTrailingHitchLength));
                    toolPos.northing = tankPos.northing + (Math.Cos(toolPos.heading) * (tool.toolTrailingHitchLength));
                }
            }

            //rigidly connected to vehicle
            else
            {
                toolPos.heading = fixHeading;
                toolPos.easting = hitchPos.easting;
                toolPos.northing = hitchPos.northing;
            }

            #endregion

            //used to increase triangle count when going around corners, less on straight
            //pick the slow moving side edge of tool
            double distance = tool.toolWidth * 0.5;
            if (distance > 3) distance = 3;

            //whichever is less
            if (Math.Abs(tool.toolFarLeftSpeed) < Math.Abs(tool.toolFarRightSpeed))
            {
                double twist = Math.Abs(tool.toolFarLeftSpeed / tool.toolFarRightSpeed);
                //twist *= twist;
                if (twist < 0.2) twist = 0.2;
                sectionTriggerStepDistance = distance * twist * twist;
            }
            else
            {
                double twist = Math.Abs(tool.toolFarRightSpeed / tool.toolFarLeftSpeed);
                //twist *= twist;
                if (twist < 0.2) twist = 0.2;

                sectionTriggerStepDistance = distance * twist * twist;
            }

            //finally fixed distance for making a curve line
            if (!gyd.isOkToAddDesPoints) sectionTriggerStepDistance = sectionTriggerStepDistance + 0.2;
            if (gyd.isContourBtnOn) sectionTriggerStepDistance *= 0.5;

            //precalc the sin and cos of heading * -1
            sinSectionHeading = Math.Sin(-toolPos.heading);
            cosSectionHeading = Math.Cos(-toolPos.heading);
        }

        //perimeter and boundary point generation
        public void AddBoundaryPoint()
        {
            //save the north & east as previous
            prevBoundaryPos.easting = pn.fix.easting;
            prevBoundaryPos.northing = pn.fix.northing;

            //build the boundary line

            if (bnd.isOkToAddPoints)
            {
                if (bnd.isDrawRightSide)
                {
                    //Right side
                    vec3 point = new vec3(
                        pivotAxlePos.easting + (Math.Sin(pivotAxlePos.heading - glm.PIBy2) * -bnd.createBndOffset),
                        pivotAxlePos.northing + (Math.Cos(pivotAxlePos.heading - glm.PIBy2) * -bnd.createBndOffset), 
                        pivotAxlePos.heading);
                    bnd.bndBeingMadePts.Add(point);
                }

                //draw on left side
                else
                {
                    //Right side
                    vec3 point = new vec3(
                        pivotAxlePos.easting + (Math.Sin(pivotAxlePos.heading - glm.PIBy2) * bnd.createBndOffset),
                        pivotAxlePos.northing + (Math.Cos(pivotAxlePos.heading - glm.PIBy2) * bnd.createBndOffset), 
                        pivotAxlePos.heading);
                    bnd.bndBeingMadePts.Add(point);
                }
            }
        }

        //add the points for section, contour line points, Area Calc feature
        private void AddSectionOrContourPathPoints()
        {
            if (recPath.isRecordOn)
            {
                //keep minimum speed of 1.0
                double speed = pn.speed;
                if (pn.speed < 1.0) speed = 1.0;
                bool autoBtn = (autoBtnState == btnStates.Auto);

                recPath.recList.Add(new CRecPathPt(pivotAxlePos.easting, pivotAxlePos.northing, pivotAxlePos.heading, speed, autoBtn));
            }

            if (gyd.isOkToAddDesPoints)
                gyd.desList.Add(new vec3(pivotAxlePos.easting, pivotAxlePos.northing, pivotAxlePos.heading));

            //save the north & east as previous
            prevSectionPos.northing = pn.fix.northing;
            prevSectionPos.easting = pn.fix.easting;

            // if non zero, at least one section is on.
            int sectionCounter = 0;

            //send the current and previous GPS fore/aft corrected fix to each section
            for (int j = 0; j < tool.numOfSections + 1; j++)
            {
                if (section[j].isMappingOn)
                {
                    section[j].AddMappingPoint();
                    sectionCounter++;
                }
            }

            if (sectionCounter == 0 || (isAutoSteerBtnOn && !gyd.isContourBtnOn && (gyd.isBtnABLineOn || gyd.isBtnCurveOn)))
            {
                //no contour recorded
                if (gyd.ContourIndex != null)
                    gyd.StopContourLine();
            }
            else//keep the line going, everything is on for recording path
                gyd.AddPoint(pivotAxlePos);
        }

        //calculate the extreme tool left, right velocities, each section lookahead, and whether or not its going backwards
        public void CalculateSectionLookAhead(double northing, double easting, double cosHeading, double sinHeading)
        {
            //calculate left side of section 1
            vec2 left = new vec2();
            vec2 right = left;
            double leftSpeed = 0, rightSpeed = 0;

            //speed max for section kmh*0.277 to m/s * 10 cm per pixel * 1.7 max speed
            double meterPerSecPerPixel = Math.Abs(pn.speed) * 4.5;

            //now loop all the section rights and the one extreme left
            for (int j = 0; j < tool.numOfSections; j++)
            {
                if (j == 0)
                {
                    vec2 lastLeftPoint = section[j].leftPoint;
                    //only one first left point, the rest are all rights moved over to left
                    section[j].leftPoint = new vec2(cosHeading * (section[j].positionLeft) + easting,
                                       sinHeading * (section[j].positionLeft) + northing);

                    left = section[j].leftPoint - lastLeftPoint;

                    //get the speed for left side only once
                    leftSpeed = left.GetLength() / fixUpdateTime;
                    if (leftSpeed > 27.7778) leftSpeed = 27.7778;

                    //Is section outer going forward or backward
                    if (Math.PI - Math.Abs(Math.Abs(left.HeadingXZ() - toolPos.heading) - Math.PI) > glm.PIBy2)
                    {
                        if (leftSpeed > 0) leftSpeed *= -1;
                    }
                    tool.toolFarLeftSpeed = tool.toolFarLeftSpeed * 0.9 + leftSpeed * 0.1;
                }
                else
                {
                    section[j].leftPoint = section[j - 1].rightPoint;
                    leftSpeed = rightSpeed;
                }

                vec2 lastRightPoint = section[j].rightPoint;

                section[j].rightPoint = new vec2(cosHeading * (section[j].positionRight) + easting,
                                    sinHeading * (section[j].positionRight) + northing);

                //now we have left and right for this section
                right = section[j].rightPoint - lastRightPoint;

                //grab vector length and convert to meters/sec/10 pixels per meter                
                rightSpeed = right.GetLength() / fixUpdateTime;
                if (rightSpeed > 27.7778) rightSpeed = 27.7778;

                if (Math.PI - Math.Abs(Math.Abs(right.HeadingXZ() - toolPos.heading) - Math.PI) > glm.PIBy2)
                    if (rightSpeed > 0) rightSpeed *= -1;

                //save the far left and right speed in m/sec averaged over 20%
                if (j == tool.numOfSections - 1)
                    tool.toolFarRightSpeed = tool.toolFarRightSpeed * 0.9 + rightSpeed * 0.1;

                double sped = (leftSpeed > rightSpeed) ? leftSpeed : rightSpeed; //choose fastest speed
                section[j].speedPixels = section[j].speedPixels * 0.9 + sped;
            }

            //fill in tool positions
            section[tool.numOfSections].leftPoint = section[0].leftPoint;
            section[tool.numOfSections].rightPoint = section[tool.numOfSections-1].rightPoint;

            double oneFrameLeft = tool.toolFarLeftSpeed * fixUpdateTime * 10;
            double oneFrameRight = tool.toolFarRightSpeed * fixUpdateTime * 10;

            if (!isFastSections)
            {
                oneFrameLeft *= 2;
                oneFrameRight *= 2;
            }

            //set the look ahead for hyd Lift in pixels per second
            vehicle.hydLiftLookAheadDistanceLeft = oneFrameLeft + tool.toolFarLeftSpeed * vehicle.hydLiftLookAheadTime * 10;
            vehicle.hydLiftLookAheadDistanceRight = oneFrameRight + tool.toolFarRightSpeed * vehicle.hydLiftLookAheadTime * 10;

            if (vehicle.hydLiftLookAheadDistanceLeft > 250) vehicle.hydLiftLookAheadDistanceLeft = 250;
            else if (vehicle.hydLiftLookAheadDistanceLeft < -250) vehicle.hydLiftLookAheadDistanceLeft = -250;
            if (vehicle.hydLiftLookAheadDistanceRight > 250) vehicle.hydLiftLookAheadDistanceRight = 250;
            else if (vehicle.hydLiftLookAheadDistanceRight < -250) vehicle.hydLiftLookAheadDistanceRight = -250;

            tool.lookAheadDistanceOnPixelsLeft = oneFrameLeft + tool.toolFarLeftSpeed * tool.lookAheadOnSetting * 10;
            tool.lookAheadDistanceOnPixelsRight = oneFrameRight + tool.toolFarRightSpeed * tool.lookAheadOnSetting * 10;

            tool.lookAheadDistanceOffPixelsLeft = -oneFrameLeft + tool.toolFarLeftSpeed * tool.lookAheadOffSetting * 10;
            tool.lookAheadDistanceOffPixelsRight = -oneFrameLeft + tool.toolFarRightSpeed * tool.lookAheadOffSetting * 10;

            if (tool.lookAheadDistanceOnPixelsLeft > 250)
            {
                tool.lookAheadDistanceOnPixelsLeft = 250;
                double ss = (1.0 / tool.lookAheadOnSetting) * tool.lookAheadOffSetting;
                if (tool.lookAheadDistanceOffPixelsLeft > 250 * ss)
                    tool.lookAheadDistanceOffPixelsLeft = 250 * ss;
            }
            else if (tool.lookAheadDistanceOnPixelsLeft < -250)
            {
                tool.lookAheadDistanceOnPixelsLeft = -250;
                double ss = (1.0 / tool.lookAheadOnSetting) * tool.lookAheadOffSetting;
                if (tool.lookAheadDistanceOffPixelsLeft < -250 * ss)
                    tool.lookAheadDistanceOffPixelsLeft = -250 * ss;
            }
            if (tool.lookAheadDistanceOnPixelsRight > 250)
            {
                tool.lookAheadDistanceOnPixelsRight = 250;
                double ss = (1.0 / tool.lookAheadOnSetting) * tool.lookAheadOffSetting;
                if (tool.lookAheadDistanceOffPixelsRight > 250 * ss)
                    tool.lookAheadDistanceOffPixelsRight = 250 * ss;
            }
            else if (tool.lookAheadDistanceOnPixelsRight < -250)
            {
                tool.lookAheadDistanceOnPixelsRight = -250;
                double ss = (1.0 / tool.lookAheadOnSetting) * tool.lookAheadOffSetting;
                if (tool.lookAheadDistanceOffPixelsRight < -250 * ss)
                    tool.lookAheadDistanceOffPixelsRight = -250 * ss;
            }
        }

        //the start of first few frames to initialize entire program
        private void InitializeFirstFewGPSPositions()
        {
            if (!isFirstFixPositionSet)
            {
                if (!isJobStarted)
                {
                    pn.latStart = pn.latitude;
                    pn.lonStart = pn.longitude;
                    pn.SetLocalMetersPerDegree();
                }

                pn.ConvertWGS84ToLocal(pn.latitude, pn.longitude, out pn.fix.northing, out pn.fix.easting);

                //Draw a grid once we know where in the world we are.
                isFirstFixPositionSet = true;

                //most recent fixes
                prevFix.easting = pn.fix.easting;
                prevFix.northing = pn.fix.northing;

                return;
            }

            else
            {
                prevFix.easting = pn.fix.easting; prevFix.northing = pn.fix.northing;

                //keep here till valid data
                if (startCounter > (20))
                {
                    isGPSPositionInitialized = true;
                    lastReverseFix = pn.fix;
                }

                //in radians
                fixHeading = 0;
                toolPos.heading = fixHeading;

                //send out initial zero settings
                if (isGPSPositionInitialized)
                {
                    IsBetweenSunriseSunset(pn.latitude, pn.longitude);

                    //set display accordingly
                    isDayTime = (DateTime.Now.Ticks < sunset.Ticks && DateTime.Now.Ticks > sunrise.Ticks);

                    //if (isAutoDayNight)
                    //{
                    //    isDay = isDayTime;
                    //    isDay = !isDay;
                    //    SwapDayNightMode();
                    //}
                    //if ( headingFromSource == "Fix" )TimedMessageBox(5000, "Set Inital Direction", "Drive Straight Forward Please");

                }
                return;
            }
        }
    }//end class
}//end namespace

////its a drive thru inner boundary
//else
//{

//    if (distPivot < yt.triggerDistance && distPivot > (yt.triggerDistance - 2.0) && !yt.isEnteringDriveThru && !yt.isInboundary && isBndInWay)
//    {
//        //our direction heading into turn
//        //yt.youTurnTriggerPoint = pivotAxlePos;
//        yt.isEnteringDriveThru = true;
//        headlandAngleOffPerpendicular = Math.PI - Math.Abs(Math.Abs(hl.closestHeadlandPt.heading - pivotAxlePos.heading) - Math.PI);
//        if (headlandAngleOffPerpendicular < 0) headlandAngleOffPerpendicular += glm.twoPI;
//        //while (headlandAngleOffPerpendicular > 1.57) headlandAngleOffPerpendicular -= 1.57;
//        headlandAngleOffPerpendicular -= glm.PIBy2;
//        headlandDistanceDelta = Math.Tan(Math.Abs(headlandAngleOffPerpendicular));
//        headlandDistanceDelta *= tool.toolWidth;
//    }

//    if (yt.isEnteringDriveThru)
//    {
//        int c = 0;
//        for (int i = 0; i < FormGPS.MAXFUNCTIONS; i++)
//        {
//            //checked for any not triggered yet (false) - if there is, not done yet
//            if (!seq.seqEnter[i].isTrig) c++;
//        }

//        if (c == 0)
//        {
//            //sequences all done so reset everything
//            //yt.isSequenceTriggered = false;
//            yt.whereAmI = 0;
//            yt.ResetSequenceEventTriggers();
//            distTool = -2222;
//            yt.isEnteringDriveThru = false;
//            yt.isExitingDriveThru = true;
//            //yt.youTurnTriggerPoint = pivotAxlePos;
//        }
//    }

//    if (yt.isExitingDriveThru)
//    {
//        int c = 0;
//        for (int i = 0; i < FormGPS.MAXFUNCTIONS; i++)
//        {
//            //checked for any not triggered yet (false) - if there is, not done yet
//            if (!seq.seqExit[i].isTrig) c++;
//        }

//        if (c == 0)
//        {
//            //sequences all done so reset everything
//            //yt.isSequenceTriggered = false;
//            yt.whereAmI = 0;
//            yt.ResetSequenceEventTriggers();
//            distTool = -2222;
//            yt.isEnteringDriveThru = false;
//            yt.isExitingDriveThru = false;
//            yt.youTurnTriggerPoint = pivotAxlePos;
//        }
//    }
//}

//Do the sequencing of functions around the turn.
//if (yt.isSequenceTriggered) yt.DoSequenceEvent();

//do sequencing for drive thru boundaries
//if (yt.isEnteringDriveThru || yt.isExitingDriveThru) yt.DoDriveThruSequenceEvent();

//else //make sure youturn and sequence is off - we are not in normal turn here
//{
//    if (yt.isYouTurnTriggered | yt.isSequenceTriggered)
//    {
//        yt.ResetYouTurn();
//    }
//}
