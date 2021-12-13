﻿//Please, if you use this, share the improvements

using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using AgOpenGPS.Properties;
using System.Globalization;
using System.IO;

//C:\Program Files(x86)\Arduino\hardware\tools\avr / bin / avrdude - CC:\Program Files(x86)\Arduino\hardware\tools\avr / etc / avrdude.conf 
//- v - patmega328p - carduino - PCOM3 - b57600 - D - Uflash:w: C: \Users\FarmPC\AppData\Local\Temp\arduino_build_448484 / Autosteer_UDP_20.ino.hex:i

namespace AgOpenGPS
{
    //master Manual and Auto, 3 states possible
    public enum btnStates { Off, Auto, On }

    public partial class FormGPS
    {
        //ABLines directory
        public string ablinesDirectory;

        //colors for sections and field background
        public byte flagColor = 0;

        //how many cm off line per big pixel
        public int lightbarCmPerPixel;

        //polygon mode for section drawing
        public bool isDrawPolygons;

        public CFeatureSettings featureSettings = new CFeatureSettings();

        public Color frameDayColor;
        public Color frameNightColor;
        public Color sectionColorDay;
        public Color fieldColorDay;
        public Color fieldColorNight;

        public Color textColorDay;
        public Color textColorNight;

        public Color vehicleColor;
        public double vehicleOpacity;
        public byte vehicleOpacityByte;
        public bool isVehicleImage;

        //Is it in 2D or 3D, metric or imperial, display lightbar, display grid etc
        public bool isMetric = true, isLightbarOn = true, isGridOn;
        public bool isUTurnAlwaysOn, isCompassOn, isSpeedoOn, isAutoDayNight, isSideGuideLines = true;
        public bool isPureDisplayOn = true, isSkyOn = true, isRollMeterOn = false, isTextureOn = true;
        public bool isDay = true, isDayTime = true;
        public bool isKeyboardOn = true;

        public bool isUTurnOn = true, isLateralOn = true;

        public btnStates autoBtnState = btnStates.Off;

        public int[] customColorsList = new int[16];

        //sunrise sunset
        public DateTime dateToday = DateTime.Today;
        public DateTime sunrise = DateTime.Now;
        public DateTime sunset = DateTime.Now;

        public bool isFlashOnOff = false;

        //makes nav panel disappear after 6 seconds
        private int navPanelCounter = 0;

        public uint sentenceCounter = 0;

        //Timer triggers at 125 msec
        private void tmrWatchdog_tick(object sender, EventArgs e)
        {
            //Check for a newline char, if none then just return
            if (++sentenceCounter > 20)
            {
                ShowNoGPSWarning();
                return;
            }

            if (threeSecondCounter++ >= 12)
            {
                threeSecondCounter = 0;
                threeSeconds++;
            }
            if (oneSecondCounter++ >= 4)
            {
                oneSecondCounter = 0;
                oneSecond++;
            }
            if (oneHalfSecondCounter++ >= 2)
            {
                oneHalfSecondCounter = 0;
                oneHalfSecond++;
            }
            if (oneFifthSecondCounter++ >= 0)
            {
                oneFifthSecondCounter = 0;
                oneFifthSecond++;
            }

            /////////////////////////////////////////////////////////   333333333333333  ////////////////////////////////////////
            //every 3 second update status
            if (displayUpdateThreeSecondCounter != threeSeconds)
            {
                //reset the counter
                displayUpdateThreeSecondCounter = threeSeconds;

                //check to make sure the grid is big enough
                worldGrid.checkZoomWorldGrid(pn.fix.northing, pn.fix.easting);

                if (panelNavigation.Visible)
                    lblHz.Text = fixUpdateHz + " ~ " + (frameTime.ToString("N1")) + " " + FixQuality;

                if (isMetric)
                {
                    //fieldStatusStripText.Text = fd.WorkedAreaRemainHectares + "\r\n"+
                    //                               fd.WorkedAreaRemainPercentage +"\r\n" +
                    //                               fd.TimeTillFinished + "\r\n" +
                    //                               fd.WorkRateHectares;
                    if (bnd.bndList.Count > 0)
                        lblFieldStatus.Text = fd.AreaBoundaryLessInnersHectares + "   " +
                                              fd.WorkedAreaRemainHectares  + "    " + fd.TimeTillFinished 
                                              + "  " + fd.WorkedAreaRemainPercentage+"      "
                                              +fd.WorkedHectares ;
                    else
                        lblFieldStatus.Text = fd.WorkedHectares;

                }
                else //imperial
                {
                    if (bnd.bndList.Count > 0)
                        lblFieldStatus.Text = fd.AreaBoundaryLessInnersAcres + "   " + fd.WorkedAreaRemainAcres + "   " + 
                                           fd.TimeTillFinished + "  " + fd.WorkedAreaRemainPercentage + "      " +
                                            fd.WorkedAcres;
                    else
                        lblFieldStatus.Text = fd.WorkedAcres;
                }

                //hide the NAv panel in 6  secs
                if (panelNavigation.Visible)
                {
                    if (navPanelCounter-- < 1) panelNavigation.Visible = false;
                }

                lblTopData.Text = (tool.toolWidth * m2FtOrM).ToString("N2") + unitsFtM + " - " + vehicleFileName;
                lblFix.Text = FixQuality;
                lblAge.Text = pn.age.ToString("N1");

                if (isJobStarted)
                {
                    lblCurrentField.Text = "Field: " + displayFieldName;

                    if (gyd.selectedLine != null && (gyd.isBtnCurveOn || gyd.isBtnABLineOn))
                        lblCurveLineName.Text = (gyd.selectedLine.Mode.HasFlag(Mode.AB) ? "AB-" : "Cur-") + gyd.selectedLine.Name.Trim();
                    else lblCurveLineName.Text = string.Empty;
                }
                else
                    lblCurveLineName.Text = lblCurrentField.Text = string.Empty;

                lbludpWatchCounts.Text = udpWatchCounts.ToString();

                //save nmea log file
                if (isLogNMEA) FileSaveNMEA();

            }//end every 3 seconds

            //every second update all status ///////////////////////////   1 1 1 1 1 1 ////////////////////////////
            if (displayUpdateOneSecondCounter != oneSecond)
            {
                //reset the counter
                displayUpdateOneSecondCounter = oneSecond;

                //counter used for saving field in background
                minuteCounter++;
                tenMinuteCounter++;

                if (gyd.isBtnCurveOn || gyd.isBtnABLineOn || gyd.isContourBtnOn)
                    lblInty.Text = gyd.inty.ToString("N3");

                if ((gyd.isBtnABLineOn || gyd.isBtnCurveOn) && !gyd.isContourBtnOn)
                    btnEditAB.Text = ((int)(gyd.moveDistance * 100)).ToString();

                //the main formgps window
                if (isMetric)  //metric or imperial
                {
                    //status strip values
                    distanceToolBtn.Text = fd.DistanceUserMeters + "\r\n" + fd.WorkedUserHectares;

                }
                else  //Imperial Measurements
                {
                    //acres on the master section soft control and sections
                    //status strip values
                    distanceToolBtn.Text = fd.DistanceUserFeet + "\r\n" + fd.WorkedUserAcres;
                }

                //statusbar flash red undefined headland
                if (mc.isOutOfBounds && panelSim.BackColor == Color.Transparent)
                    panelSim.BackColor = Color.Tomato;
                else if (!mc.isOutOfBounds && panelSim.BackColor == Color.Tomato)
                    panelSim.BackColor = Color.Transparent;
            }

            //every half of a second update all status  ////////////////    0.5  0.5   0.5    0.5    /////////////////
            if (displayUpdateHalfSecondCounter != oneHalfSecond)
            {
                //reset the counter
                displayUpdateHalfSecondCounter = oneHalfSecond;

                isFlashOnOff = !isFlashOnOff;

                if ((!gyd.isBtnABLineOn && !gyd.isContourBtnOn && !gyd.isBtnCurveOn && isAutoSteerBtnOn))
                    enableAutoSteerButton(false);

                //the main formgps window
                if (isMetric)  //metric or imperial
                {
                    lblSpeed.Text = SpeedKPH;
                    //btnContour.Text = XTE; //cross track error

                }
                else  //Imperial Measurements
                {
                    lblSpeed.Text = SpeedMPH;
                    //btnContour.Text = InchXTE; //cross track error
                }


            } //end every 1/2 second

            //every fifth second update  ///////////////////////////   FIFTH Fifth ////////////////////////////
            if (displayUpdateOneFifthCounter != oneFifthSecond)
            {
                //reset the counter
                displayUpdateOneFifthCounter = oneFifthSecond;

                btnAutoSteerConfig.Text = SetSteerAngle + "\r\n" + ActualSteerAngle;

                secondsSinceStart = (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds;

                //integralStatusLeftSide.Text = "I: " + gyd.inty.ToString("N3");

                //lblAV.Text = ABLine.angVel.ToString("N3");
            }
        }//wait till timer fires again.  

        private void IsBetweenSunriseSunset(double lat, double lon)
        {
            CSunTimes.Instance.CalculateSunRiseSetTimes(pn.latitude, pn.longitude, dateToday, ref sunrise, ref sunset);
            //isDay = (DateTime.Now.Ticks < sunset.Ticks && DateTime.Now.Ticks > sunrise.Ticks);
        }

        public void LoadSettings()
        {            //metric settings

            CheckSettingsNotNull();

            isMetric = Settings.Default.setMenu_isMetric;

            SmoothABtoolStripMenu.Visible = Properties.Settings.Default.setFeatures.isABSmoothOn;
            deleteContourPathsToolStripMenuItem.Visible = Properties.Settings.Default.setFeatures.isHideContourOn;
            webcamToolStrip.Visible = Properties.Settings.Default.setFeatures.isWebCamOn;
            offsetFixToolStrip.Visible = Properties.Settings.Default.setFeatures.isOffsetFixOn;
            btnContour.Visible = Properties.Settings.Default.setFeatures.isContourOn;
            btnAutoYouTurn.Visible = Properties.Settings.Default.setFeatures.isYouTurnOn;
            btnStanleyPure.Visible = Properties.Settings.Default.setFeatures.isSteerModeOn;
            btnStartAgIO.Visible = Properties.Settings.Default.setFeatures.isAgIOOn;

            btnAutoSteer.Visible = Properties.Settings.Default.setFeatures.isAutoSteerOn;
            btnCycleLines.Visible = Properties.Settings.Default.setFeatures.isCycleLinesOn;
            btnManualOffOn.Visible = Properties.Settings.Default.setFeatures.isManualSectionOn;
            btnSectionOffAutoOn.Visible = Properties.Settings.Default.setFeatures.isAutoSectionOn;
            btnABLine.Visible = Properties.Settings.Default.setFeatures.isABLineOn;
            btnCurve.Visible = Properties.Settings.Default.setFeatures.isCurveOn;

            isUTurnOn = Properties.Settings.Default.setFeatures.isUTurnOn;
            isLateralOn = Properties.Settings.Default.setFeatures.isLateralOn;

            if (isMetric)
            {
                inchOrCm2m = 0.01;
                m2InchOrCm = 100.0;

                m2FtOrM = 1.0;
                ftOrMtoM = 1.0;

                inOrCm2Cm = 1.0;
                cm2CmOrIn = 1.0;

                unitsFtM = " m";
                unitsInCm = " cm";
            }
            else
            {
                inchOrCm2m = glm.in2m;
                m2InchOrCm = glm.m2in;

                m2FtOrM = glm.m2ft;
                ftOrMtoM = glm.ft2m;

                inOrCm2Cm = 2.54;
                cm2CmOrIn = 0.3937;


                unitsInCm = " in";
                unitsFtM = " ft";
            }

            startSpeed = Vehicle.Default.setVehicle_startSpeed;

            //load up colors

            //load the string of custom colors
            string[] words = Properties.Settings.Default.setDisplay_customColors.Split(',');
            for (int i = 0; i < 16; i++)
            {
                Color test;
                customColorsList[i] = int.Parse(words[i], CultureInfo.InvariantCulture);
                test = Color.FromArgb(customColorsList[i]).CheckColorFor255();
                int iCol = (test.A << 24) | (test.R << 16) | (test.G << 8) | test.B;
                customColorsList[i] = iCol;
            }

            Properties.Settings.Default.setDisplay_customColors = "";
            for (int i = 0; i < 15; i++)
                Properties.Settings.Default.setDisplay_customColors += customColorsList[i].ToString() + ",";
            Properties.Settings.Default.setDisplay_customColors += customColorsList[15].ToString();

            frameDayColor = Properties.Settings.Default.setDisplay_colorDayFrame.CheckColorFor255();
            frameNightColor = Properties.Settings.Default.setDisplay_colorNightFrame.CheckColorFor255();
            sectionColorDay = Properties.Settings.Default.setDisplay_colorSectionsDay.CheckColorFor255();
            fieldColorDay = Properties.Settings.Default.setDisplay_colorFieldDay.CheckColorFor255();
            fieldColorNight = Properties.Settings.Default.setDisplay_colorFieldNight.CheckColorFor255();
            textColorDay = Settings.Default.setDisplay_colorTextDay.CheckColorFor255();
            textColorNight = Settings.Default.setDisplay_colorTextNight.CheckColorFor255();
            vehicleColor = Settings.Default.setDisplay_colorVehicle.CheckColorFor255();

            Properties.Settings.Default.setDisplay_colorDayFrame = frameDayColor;
            Properties.Settings.Default.setDisplay_colorNightFrame = frameNightColor;
            Properties.Settings.Default.setDisplay_colorSectionsDay = sectionColorDay;
            Properties.Settings.Default.setDisplay_colorFieldDay = fieldColorDay;
            Properties.Settings.Default.setDisplay_colorFieldNight = fieldColorNight;
            Properties.Settings.Default.Save();

            isSkyOn = Settings.Default.setMenu_isSkyOn;
            isTextureOn = Settings.Default.setDisplay_isTextureOn;

            isGridOn = Settings.Default.setMenu_isGridOn;
            isCompassOn = Settings.Default.setMenu_isCompassOn;
            isSpeedoOn = Settings.Default.setMenu_isSpeedoOn;
            isAutoDayNight = Settings.Default.setDisplay_isAutoDayNight;
            isSideGuideLines = Settings.Default.setMenu_isSideGuideLines;
            //isLogNMEA = Settings.Default.setMenu_isLogNMEA;
            isPureDisplayOn = Settings.Default.setMenu_isPureOn;

            panelNavigation.Location = new System.Drawing.Point(90, 100);
            panelDrag.Location = new System.Drawing.Point(87, 268);

            vehicleOpacity = ((double)(Properties.Settings.Default.setDisplay_vehicleOpacity) * 0.01);
            vehicleOpacityByte = (byte)(255 * ((double)(Properties.Settings.Default.setDisplay_vehicleOpacity) * 0.01));
            isVehicleImage = Properties.Settings.Default.setDisplay_isVehicleImage;

            string directoryName = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);

            //grab the current vehicle filename - make sure it exists
            vehicleFileName = Vehicle.Default.setVehicle_vehicleName;

            simulatorOnToolStripMenuItem.Checked = Settings.Default.setMenu_isSimulatorOn;
            SetSimStatus(simulatorOnToolStripMenuItem.Checked);

            if (timerSim.Enabled) fixUpdateHz = 10;
            fixUpdateTime = 1 / (double)fixUpdateHz;

            //set the flag mark button to red dot
            btnFlag.Image = Properties.Resources.FlagRed;


            isLightbarOn = Settings.Default.setMenu_isLightbarOn;

            //set up grid and lightbar

            isKeyboardOn = Settings.Default.setDisplay_isKeyboardOn;

            if (Properties.Settings.Default.setAS_isAutoSteerAutoOn) btnAutoSteer.Text = "R";
            else btnAutoSteer.Text = "M";

            btnChangeMappingColor.Image = ReplaceColor(Resources.SectionMapping, sectionColorDay);
            btnChangeMappingColor.Text = Application.ProductVersion.ToString(CultureInfo.InvariantCulture);


            //is rtk on?
            isRTK = Properties.Settings.Default.setGPS_isRTK;
            isRTK_KillAutosteer = Properties.Settings.Default.setGPS_isRTK_KillAutoSteer;

            pn.ageAlarm = Properties.Settings.Default.setGPS_ageAlarm;

            isAngVelGuidance = Properties.Settings.Default.setAS_isAngVelGuidance;

            guidanceLookAheadTime = Properties.Settings.Default.setAS_guidanceLookAheadTime;

            gyd.sideHillCompFactor = Properties.Settings.Default.setAS_sideHillComp;

            //ahrs.isReverseOn = Properties.Settings.Default.setIMU_isReverseOn;
            //ahrs.reverseComp = Properties.Settings.Default.setGPS_reverseComp;
            //ahrs.forwardComp = Properties.Settings.Default.setGPS_forwardComp;

            ahrs = new CAHRS();

            //Set width of section and positions for each section
            SectionSetPosition();

            //Calculate total width and each section width
            SectionCalcWidths();
            LineUpManualBtns();

            //fast or slow section update
            isFastSections = Properties.Vehicle.Default.setSection_isFast;

            yt.rowSkipsWidth = Properties.Vehicle.Default.set_youSkipWidth;
            cboxpRowWidth.SelectedIndex = yt.rowSkipsWidth - 1;
            yt.Set_Alternate_skips();

            enableYouTurnButton(false);

            //which heading source is being used
            headingFromSource = Settings.Default.setGPS_headingFromWhichSource;

            //workswitch stuff
            mc.isWorkSwitchEnabled = Settings.Default.setF_IsWorkSwitchEnabled;
            mc.isWorkSwitchActiveLow = Settings.Default.setF_IsWorkSwitchActiveLow;
            mc.isWorkSwitchManual = Settings.Default.setF_IsWorkSwitchManual;
            mc.isSteerControlsManual = Settings.Default.setF_steerControlsManual;

            minFixStepDist = Settings.Default.setF_minFixStep;

            fd.workedAreaTotalUser = Settings.Default.setF_UserTotalArea;

            yt.uTurnSmoothing = Settings.Default.setAS_uTurnSmoothing;

            tool.halfToolWidth = (tool.toolWidth - tool.toolOverlap) / 2.0;

            //load the lightbar resolution
            lightbarCmPerPixel = Properties.Settings.Default.setDisplay_lightbarCmPerPixel;

            //Stanley guidance
            isStanleyUsed = Properties.Vehicle.Default.setVehicle_isStanleyUsed;
            if (isStanleyUsed)
            {
                btnStanleyPure.Image = Resources.ModeStanley;
            }
            else
            {
                btnStanleyPure.Image = Resources.ModePurePursuit;
            }

            if (Properties.Settings.Default.setDisplay_isStartFullScreen)
                this.WindowState = FormWindowState.Maximized;

            //main window first
            if (Settings.Default.setWindow_Maximized)
            {
                WindowState = FormWindowState.Normal;
                Location = Settings.Default.setWindow_Location;
                Size = Settings.Default.setWindow_Size;
            }
            else if (Settings.Default.setWindow_Minimized)
            {
                //WindowState = FormWindowState.Minimized;
                Location = Settings.Default.setWindow_Location;
                Size = Settings.Default.setWindow_Size;
            }
            else
            {
                Location = Settings.Default.setWindow_Location;
                Size = Settings.Default.setWindow_Size;
            }

            isTramOnBackBuffer = Properties.Settings.Default.setTram_isTramOnBackBuffer;

            //night mode
            isDay = Properties.Settings.Default.setDisplay_isDayMode;
            isDay = !isDay;
            SwapDayNightMode();

            if (!Properties.Settings.Default.setDisplay_isTermsAccepted)
            {
                using (var form = new Form_First())
                {
                    if (form.ShowDialog(this) != DialogResult.OK)
                    {
                        Close();
                    }
                }
            }
            FieldMenuButtonEnableDisable(isJobStarted);

            SetZoom();
        }

        private void ZoomByMouseWheel(object sender, MouseEventArgs e)
        {
            if (camera.zoomValue <= 20)
                camera.zoomValue -= Math.Sign(e.Delta) * camera.zoomValue * 0.06;
            else
                camera.zoomValue -= Math.Sign(e.Delta) * camera.zoomValue * 0.02;

            SetZoom();
        }

        public void SwapDayNightMode()
        {
            isDay = !isDay;
            if (isDay)
            {
                btnDayNightMode.Image = Properties.Resources.WindowNightMode;

                this.BackColor = frameDayColor;
                foreach (Control c in this.Controls)
                {
                    c.ForeColor = textColorDay;
                }
            }
            else //nightmode
            {
                btnDayNightMode.Image = Properties.Resources.WindowDayMode;
                this.BackColor = frameNightColor;

                foreach (Control c in this.Controls)
                {
                    c.ForeColor = textColorNight;
                }
            }
            btnAutoSteerConfig.ForeColor = Color.Black;
            btnEditAB.ForeColor = Color.Black;

            Properties.Settings.Default.setDisplay_isDayMode = isDay;
            Properties.Settings.Default.Save();
        }

        //line up section On Off Auto buttons based on how many there are
        public void LineUpManualBtns()
        {
            int oglCenter = oglMain.Width / 2;

            int buttonMaxWidth = 400;

            int top = oglMain.Height - (panelSim.Visible ? 100 : 70);

            panelSim.Left = oglCenter - panelSim.Width / 2;
            panelSim.Top = oglMain.Height - 60;

            int oglButtonWidth = oglMain.Width * 3 / 4;

            int buttonWidth = oglButtonWidth / tool.numOfSections;
            if (buttonWidth > buttonMaxWidth) buttonWidth = buttonMaxWidth;

            Size size = new System.Drawing.Size(buttonWidth, 25);
            int Left = (oglCenter) - (tool.numOfSections * size.Width) / 2;

            //turn section buttons all On
            for (int j = 0; j < MAXSECTIONS; j++)
            {
                section[j].button.Top = top;
                section[j].button.Left = Left + size.Width * j;
                section[j].button.Size = size;
                section[j].button.Visible = tool.numOfSections > j;
            }
        }

        public void SaveFormGPSWindowSettings()
        {
            //save window settings
            if (WindowState == FormWindowState.Maximized)
            {
                Settings.Default.setWindow_Location = RestoreBounds.Location;
                Settings.Default.setWindow_Size = RestoreBounds.Size;
                Settings.Default.setWindow_Maximized = false;
                Settings.Default.setWindow_Minimized = false;
            }
            else if (WindowState == FormWindowState.Normal)
            {
                Settings.Default.setWindow_Location = Location;
                Settings.Default.setWindow_Size = Size;
                Settings.Default.setWindow_Maximized = false;
                Settings.Default.setWindow_Minimized = false;
            }
            else
            {
                Settings.Default.setWindow_Location = RestoreBounds.Location;
                Settings.Default.setWindow_Size = RestoreBounds.Size;
                Settings.Default.setWindow_Maximized = false;
                Settings.Default.setWindow_Minimized = true;
            }

            Settings.Default.setDisplay_camPitch = camera.camPitch;
            Properties.Settings.Default.setDisplay_camZoom = camera.zoomValue;

            Settings.Default.setF_UserTotalArea = fd.workedAreaTotalUser;

            Settings.Default.Save();
        }

        public string FindDirection(double heading)
        {
            if (heading < 0) heading += glm.twoPI;

            heading = glm.toDegrees(heading);

            if (heading > 337.5 || heading < 22.5)
            {
                return (" " +  gStr.gsNorth + " ");
            }
            if (heading > 22.5 && heading < 67.5)
            {
                return (" " +  gStr.gsN_East + " ");
            }
            if (heading > 67.5 && heading < 111.5)
            {
                return (" " +  gStr.gsEast + " ");
            }
            if (heading > 111.5 && heading < 157.5)
            {
                return (" " +  gStr.gsS_East + " ");
            }
            if (heading > 157.5 && heading < 202.5)
            {
                return (" " +  gStr.gsSouth + " ");
            }
            if (heading > 202.5 && heading < 247.5)
            {
                return (" " +  gStr.gsS_West + " ");
            }
            if (heading > 247.5 && heading < 292.5)
            {
                return (" " +  gStr.gsWest + " ");
            }
            if (heading > 292.5 && heading < 337.5)
            {
                return (" " +  gStr.gsN_West + " ");
            }
            return (" ?? ");
        }

        //Mouse Clicks 
        private void oglMain_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                //0 at bottom for opengl, 0 at top for windows, so invert Y value
                Point point = oglMain.PointToClient(Cursor.Position);

                if (point.Y < 90 && point.Y > 30 && (gyd.isBtnABLineOn || gyd.isBtnCurveOn))
                {
                    int middle = oglMain.Width / 2 + oglMain.Width / 5;
                    if (point.X > middle - 80 && point.X < middle + 80)
                    {
                        if (isTT)
                        {
                            MessageBox.Show(gStr.h_lblSwapDirectionCancel, gStr.gsHelp);
                            ResetHelpBtn();
                            return;
                        }
                        SwapDirection();
                        return;
                    }

                    //manual uturn triggering
                    middle = oglMain.Width / 2 - oglMain.Width / 4;
                    if (point.X > middle - 140 && point.X < middle && isUTurnOn)
                    {
                        if (isTT)
                        {
                            MessageBox.Show(gStr.h_lblManualTurnCancelTouch, gStr.gsHelp);
                            ResetHelpBtn();
                            return;
                        }

                        if (yt.isYouTurnTriggered)
                        {
                            yt.ResetYouTurn();
                        }
                        else
                        {
                            yt.BuildManualYouTurn(false, true);
                            return;
                        }
                    }

                    if (point.X > middle && point.X < middle + 140 && isUTurnOn)
                    {
                        if (isTT)
                        {
                            MessageBox.Show(gStr.h_lblManualTurnCancelTouch, gStr.gsHelp);
                            ResetHelpBtn();
                            return;
                        }

                        if (yt.isYouTurnTriggered)
                        {
                            yt.ResetYouTurn();
                        }
                        else
                        {
                            yt.BuildManualYouTurn(true, true);
                            return;
                        }
                    }
                }

                if (point.Y < 150 && point.Y > 90 && (gyd.isBtnABLineOn || gyd.isBtnCurveOn))
                {
                    int middle = oglMain.Width / 2 - oglMain.Width / 4;
                    if (point.X > middle - 140 && point.X < middle && isLateralOn)
                    {
                        if (isTT)
                        {
                            MessageBox.Show(gStr.h_lblLateralTurnTouch, gStr.gsHelp);
                            ResetHelpBtn();
                            return;
                        }

                        yt.BuildManualYouLateral(false);
                        return;
                    }

                    if (point.X > middle && point.X < middle + 140 && isLateralOn)
                    {
                        if (isTT)
                        {
                            MessageBox.Show(gStr.h_lblLateralTurnTouch, gStr.gsHelp);
                            ResetHelpBtn();
                            return;
                        }

                        yt.BuildManualYouLateral(true);
                        return;
                    }
                }

                //vehicle direcvtion reset
                int centerLeft = oglMain.Width / 2;
                int centerUp = oglMain.Height / 2;

                if (point.X > centerLeft - 40 && point.X < centerLeft + 40 && point.Y > centerUp - 60 && point.Y < centerUp + 60)
                {
                    if (isTT)
                    {
                        MessageBox.Show(gStr.h_lblVehicleDirectionResetTouch, gStr.gsHelp);        
                        ResetHelpBtn();
                        return;
                    }

                    Array.Clear(stepFixPts, 0, stepFixPts.Length);
                    isFirstHeadingSet = false;
                    isReverse = false;
                    TimedMessageBox(2000, "Reset Direction", "Drive Forward > 1.5 kmh");
                    return;
                }

                //prevent flag selection if flag form is up
                Form fc = Application.OpenForms["FormFlags"];
                if (fc != null)
                {
                    fc.Focus();
                    return;
                }

                if (point.X > oglMain.Width - 80 && point.Y < 180)
                {
                    int sign = Math.Sign(point.Y - 90);

                    if (camera.zoomValue <= 20)
                        camera.zoomValue += sign * camera.zoomValue * 0.2;
                    else
                        camera.zoomValue += sign * camera.zoomValue * 0.1;

                    SetZoom();
                    return;
                }

                //check for help touch on steer circle
                if (isTT)
                {
                    int sizer = oglMain.Height / 9;
                    if(point.Y > oglMain.Height-sizer && point.X > oglMain.Width - sizer)
                    {
                        MessageBox.Show(gStr.h_lblSteerCircleTouch, gStr.gsHelp);
                        ResetHelpBtn();
                        return;
                    }
                }

                mouseX = point.X;
                mouseY = oglMain.Height - point.Y;
                leftMouseDownOnOpenGL = true;
            }

            ResetHelpBtn();
        }
        private void oglZoom_MouseClick(object sender, MouseEventArgs e)
        {
            if (initialControlLocation != (sender as Control).Location) return;

            if (oglZoom.Width == 180)
            {
                oglZoom.Width = 300;
                oglZoom.Height = 300;
            }

            else if (oglZoom.Width == 300)
            {
                oglZoom.Width = 180;
                oglZoom.Height = 180;
            }
        } 
        
        //Function to delete flag
        public void DeleteSelectedFlag()
        {
            //delete selected flag and set selected to none
            flagPts.RemoveAt(flagNumberPicked - 1);
            flagNumberPicked = 0;

            // re-sort the id's based on how many flags left
            int flagCnt = flagPts.Count;
            if (flagCnt > 0)
            {
                for (int i = 0; i < flagCnt; i++) flagPts[i].ID = i + 1;
            }
        }

        private void ShowNoGPSWarning()
        {
            //update main window
            sentenceCounter = 300;
            oglMain.MakeCurrent();
            oglMain.Refresh();
        }

        #region Properties // ---------------------------------------------------------------------

        public string Latitude { get { return Convert.ToString(Math.Round(pn.latitude, 7)); } }
        public string Longitude { get { return Convert.ToString(Math.Round(pn.longitude, 7)); } }

        public string SatsTracked { get { return Convert.ToString(pn.satellitesTracked); } }
        public string HDOP { get { return Convert.ToString(pn.hdop); } }
        public string Heading { get { return Convert.ToString(Math.Round(glm.toDegrees(fixHeading), 1)) + "\u00B0"; } }
        public string GPSHeading { get { return (Math.Round(glm.toDegrees(fixHeading), 1)) + "\u00B0"; } }
        public string FixQuality
        {
            get
            {
                if (timerSim.Enabled)
                    return "Sim: ";
                else if (pn.fixQuality == 0) return "Invalid: ";
                else if (pn.fixQuality == 1) return "GPS single: ";
                else if (pn.fixQuality == 2) return "DGPS : ";
                else if (pn.fixQuality == 3) return "PPS : ";
                else if (pn.fixQuality == 4) return "RTK fix: ";
                else if (pn.fixQuality == 5) return "Float: ";
                else if (pn.fixQuality == 6) return "Estimate: ";
                else if (pn.fixQuality == 7) return "Man IP: ";
                else if (pn.fixQuality == 8) return "Sim: ";
                else return "Unknown: ";
            }
        }

        public string GyroInDegrees
        {
            get
            {
                if (ahrs.imuHeading != 99999)
                    return Math.Round(ahrs.imuHeading, 1) + "\u00B0";
                else return "-";
            }
        }
        public string RollInDegrees
        {
            get
            {
                if (ahrs.imuRoll != 88888)
                    return Math.Round((ahrs.imuRoll), 1) + "\u00B0";
                else return "-";
            }
        }
        public string SetSteerAngle { get { return ((double)(guidanceLineSteerAngle) * 0.01).ToString("N1"); } }
        public string ActualSteerAngle { get { return ((mc.actualSteerAngleDegrees) ).ToString("N1") ; } }

        //Metric and Imperial Properties
        public string SpeedMPH
        {
            get
            {
                return Convert.ToString(Math.Round(avgSpeed*0.62137, 1));
            }
        }
        public string SpeedKPH
        {
            get
            {
                return Convert.ToString(Math.Round(avgSpeed, 1));
            }
        }

        public string FixOffset { get { return (pn.fixOffset.easting.ToString("N2") + ", " + pn.fixOffset.northing.ToString("N2")); } }
        public string FixOffsetInch { get { return ((pn.fixOffset.easting*glm.m2in).ToString("N0")+ ", " + (pn.fixOffset.northing*glm.m2in).ToString("N0")); } }

        public string Altitude { get { return Convert.ToString(Math.Round(pn.altitude,1)); } }
        public string AltitudeFeet { get { return Convert.ToString((Math.Round((pn.altitude * 3.28084),1))); } }
        public string DistPivotM
        {
            get
            {
                if (distancePivotToTurnLine > 0 )
                    return ((int)(distancePivotToTurnLine)) + " m";
                else return "--";
            }
        }
        public string DistPivotFt
        {
            get
            {
                if (distancePivotToTurnLine > 0 ) return (((int)(glm.m2ft * (distancePivotToTurnLine))) + " ft");
                else return "--";
            }
        }

        #endregion properties 
    }//end class
}//end namespace