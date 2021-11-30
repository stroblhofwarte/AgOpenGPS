//Please, if you use this, share the improvements

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AgOpenGPS.Properties;
using Microsoft.Win32;

namespace AgOpenGPS
{
    public partial class FormGPS
    {
        public bool isTT;

        #region Right Menu
        private void btnContour_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnContour, gStr.gsHelp);
                return;
            }

            enableABLineButton(false);
            enableCurveButton(false);
            setYouTurnButtonStatus(false);

            //change image to reflect on off
            gyd.isContourBtnOn = !gyd.isContourBtnOn;
            btnContour.Image = gyd.isContourBtnOn ? Properties.Resources.ContourOn : Properties.Resources.ContourOff;

            if (gyd.isContourBtnOn)
            {
                btnCycleLines.Image = Properties.Resources.ColorLocked;
                guidanceLookAheadTime = 0.5;
            }
            else
            {
                btnCycleLines.Image = Properties.Resources.ABLineCycle;
                guidanceLookAheadTime = Properties.Settings.Default.setAS_guidanceLookAheadTime;
            }
        }

        private void btnCurve_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnCurve, gStr.gsHelp);
                return;
            }
            
            //check if window already exists
            Form cf = Application.OpenForms["FormABCurve"];
            if (cf != null)
            {
                cf.Close();
                return;
            }

            //if contour is on, turn it off
            if (gyd.isContourBtnOn) btnContour.PerformClick();

            //change image to reflect on off
            enableABLineButton(false);

            Form form = new FormABCurve(this, Mode.Curve);
            form.Show(this);

            enableCurveButton(true);
        }

        public void enableCurveButton(bool status)
        {
            if (gyd.isBtnCurveOn != status)
            {
                gyd.isBtnCurveOn = status;
                btnCurve.Image = status ? Properties.Resources.CurveOn : Properties.Resources.CurveOff;

                if (btnEditAB.Visible != status && isJobStarted)
                {
                    btnEditAB.Visible = status && isJobStarted;
                    btnSnapToPivot.Visible = status && isJobStarted;
                    cboxpRowWidth.Visible = status && isJobStarted;
                    btnYouSkipEnable.Visible = status && isJobStarted;
                }
            }
        }

        private void btnABLine_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnABLine, gStr.gsHelp);
                return;
            }

            //check if window already exists
            Form f = Application.OpenForms["FormABCurve"];
            if (f != null)
            {
                f.Close();
                return;
            }

            //if contour is on, turn it off
            if (gyd.isContourBtnOn) btnContour.PerformClick();

            enableCurveButton(false);

            //Bring up the form
            var form = new FormABCurve(this, Mode.AB);
            form.Show(this);
            enableABLineButton(true);
        }

        public void enableABLineButton(bool status)
        {
            if (gyd.isBtnABLineOn != status)
            {
                gyd.isBtnABLineOn = status;
                btnABLine.Image = status ? Properties.Resources.ABLineOn : Properties.Resources.ABLineOff;

                if (btnEditAB.Visible != status && isJobStarted)
                {
                    btnEditAB.Visible = status && isJobStarted;
                    btnSnapToPivot.Visible = status && isJobStarted;
                    cboxpRowWidth.Visible = status && isJobStarted;
                    btnYouSkipEnable.Visible = status && isJobStarted;
                }
            }
        }

        private void btnCycleLines_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnCycleLines, gStr.gsHelp);
                return;
            }
            
            if (gyd.isContourBtnOn)
            {
                if (gyd.stripNum > -1 && gyd.curList.Count > 5) gyd.isLocked = !gyd.isLocked;
                return;
            }

            if (gyd.selectedLine == null) return;

            //reset to generate new reference
            gyd.moveDistance = 0;
            gyd.isValid = false;
            yt.ResetYouTurn();

            Mode mode = gyd.isBtnABLineOn ? Mode.AB : Mode.Curve;

            bool found = !(gyd.selectedLine?.Mode.HasFlag(mode) == true);
            bool loop = true;
            for (int i = 0; i < gyd.refList.Count || loop; i++)
            {
                if (i >= gyd.refList.Count)
                {
                    loop = false;
                    i = -1;
                    if (!found) break;
                    else continue;
                }
                if (gyd.selectedLine?.Name == gyd.refList[i].Name)
                    found = true;
                else if (found && gyd.refList[i].Mode.HasFlag(mode))
                {
                    gyd.selectedLine = new CGuidanceLine(gyd.refList[i]);
                    lblCurveLineName.Text = gyd.selectedLine.Name.Trim();
                    break;
                }
            }
            if (!found)
                gyd.selectedLine = null;
        }

        //Section Manual and Auto
        private void btnManualOffOn_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnManualOffOn, gStr.gsHelp);
                return;
            }

            setSectionButtonState(autoBtnState == btnStates.On ? btnStates.Off : btnStates.On);
        }

        private void btnSectionOffAutoOn_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnSectionOffAutoOn, gStr.gsHelp);
                return;
            }

            setSectionButtonState(autoBtnState == btnStates.Auto ? btnStates.Off : btnStates.Auto);
        }

        public void setSectionButtonState(btnStates status, bool? enable = null)
        {
            if (status == btnStates.On || (autoBtnState == btnStates.On && status == btnStates.Off))
                System.Media.SystemSounds.Asterisk.Play();
            else if (status == btnStates.Auto || (autoBtnState == btnStates.Auto && status == btnStates.Off))
                System.Media.SystemSounds.Exclamation.Play();

            autoBtnState = status;
            btnManualOffOn.Image = status == btnStates.On ? Properties.Resources.ManualOn : Properties.Resources.ManualOff;
            btnSectionOffAutoOn.Image = status == btnStates.Auto ? Properties.Resources.SectionMasterOn : Properties.Resources.SectionMasterOff;

            //set sections buttons to status
            for (int j = 0; j < tool.numOfSections; j++)
                section[j].UpdateButton(status, enable);
        }

        private void btnAutoSteer_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnAutoSteer, gStr.gsHelp);
                return;
            }

            enableAutoSteerButton(!isAutoSteerBtnOn);
        }

        public void enableAutoSteerButton(bool status)
        {
            if (isAutoSteerBtnOn != status)
            {
                //new direction so reset where to put turn diagnostic
                yt.ResetCreatedYouTurn();

                if (!status)
                {
                    isAutoSteerBtnOn = false;
                    btnAutoSteer.Image = Properties.Resources.AutoSteerOff;
                    enableYouTurnButton(false);
                    if (sounds.isSteerSoundOn) CSound.sndAutoSteerOff.Play();
                }
                else if (gyd.isBtnABLineOn || gyd.isContourBtnOn || gyd.isBtnCurveOn)
                {
                    isAutoSteerBtnOn = true;
                    btnAutoSteer.Image = Properties.Resources.AutoSteerOn;

                    if (sounds.isSteerSoundOn) CSound.sndAutoSteerOn.Play();
                }
                else
                {
                    var form = new FormTimedMessage(2000, (gStr.gsNoGuidanceLines), (gStr.gsTurnOnContourOrMakeABLine));
                    form.Show(this);
                }
            }
        }

        private void btnAutoYouTurn_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnAutoYouTurn, gStr.gsHelp);
                return;
            }

            if (bnd.bndList.Count == 0)
                TimedMessageBox(2000, gStr.gsNoBoundary, gStr.gsCreateABoundaryFirst);
            else
                enableYouTurnButton(!yt.isYouTurnBtnOn);
        }

        public void enableYouTurnButton(bool status)
        {
            if (yt.isYouTurnBtnOn != status)
            {
                yt.isYouTurnBtnOn = status;
                btnAutoYouTurn.Image = status ? Properties.Resources.Youturn80 : Properties.Resources.YouTurnNo;

                if (status && (gyd.isBtnABLineOn || gyd.isBtnCurveOn))
                    enableAutoSteerButton(true);

                yt.ResetYouTurn();

                yt.rowSkipsWidth = Properties.Vehicle.Default.set_youSkipWidth;
                yt.Set_Alternate_skips();
            }
        }

        public void setYouTurnButtonStatus(bool isOn)
        {
            enableYouTurnButton(false);
            yt.isYouTurnBtnOn = false;
            btnAutoYouTurn.Enabled = isOn;
            btnAutoYouTurn.Image = Properties.Resources.YouTurnNo;
            yt.ResetYouTurn();
        }

        #endregion

        #region Left Panel Menu
        private void toolStripDropDownButtonDistance_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnDistanceArea, gStr.gsHelp);
                return;
            }
            fd.distanceUser = 0;
            fd.workedAreaTotalUser = 0;
        }        
        private void navPanelToolStrip_Click(object sender, EventArgs e)
        {
            //buttonPanelCounter = 0;

            if (panelNavigation.Visible)
            {
                panelNavigation.Visible = false;
            }
            else
            {
                panelNavigation.Visible = true;
                navPanelCounter = 2;
            }
        }
        private void toolStripMenuItemFlagRed_Click(object sender, EventArgs e)
        {
            flagColor = 0;
            btnFlag.Image = Properties.Resources.FlagRed;
        }
        private void toolStripMenuGrn_Click(object sender, EventArgs e)
        {
            flagColor = 1;
            btnFlag.Image = Properties.Resources.FlagGrn;
        }
        private void toolStripMenuYel_Click(object sender, EventArgs e)
        {
            flagColor = 2;
            btnFlag.Image = Properties.Resources.FlagYel;
        }
        private void toolStripMenuFlagForm_Click(object sender, EventArgs e)
        {
            Form fc = Application.OpenForms["FormFlags"];

            if (fc != null)
            {
                fc.Focus();
                return;
            }

            if (flagPts.Count > 0)
            {
                flagNumberPicked = 1;
                Form form = new FormFlags(this);
                form.Show(this);
            }            
        }

        private void stripBtnConfig_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnConfig, gStr.gsHelp);
                return;
            }
            using (FormConfig form = new FormConfig(this))
            {
                form.ShowDialog(this);
            }
        }

        private void btnStanleyPure_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnStanleyPure, gStr.gsHelp);
                return;
            }
            isStanleyUsed = !isStanleyUsed;

            btnStanleyPure.Image = isStanleyUsed ? Resources.ModeStanley : Resources.ModePurePursuit;

            Properties.Vehicle.Default.setVehicle_isStanleyUsed = isStanleyUsed;
            Properties.Vehicle.Default.Save();
        }

        private void btnFlag_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnFlag, gStr.gsHelp);
                return;
            }

            int nextflag = flagPts.Count + 1;
            CFlag flagPt = new CFlag(pn.latitude, pn.longitude, pn.fix.easting, pn.fix.northing, fixHeading, flagColor, nextflag, (nextflag).ToString());
            flagPts.Add(flagPt);
            FileSaveFlags();

            Form fc = Application.OpenForms["FormFlags"];

            if (fc != null)
            {
                fc.Focus();
                return;
            }

            if (flagPts.Count > 0)
            {
                flagNumberPicked = nextflag;
                Form form = new FormFlags(this);
                form.Show(this);
            }
        }

        private void btnStartAgIO_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnStartAgIO, gStr.gsHelp);
                return;
            }
            Process[] processName = Process.GetProcessesByName("AgIO");
            if (processName.Length == 0)
            {
                //Start application here
                DirectoryInfo di = new DirectoryInfo(Application.StartupPath);
                string strPath = di.ToString();
                strPath += "\\AgIO.exe";
                try
                {
                    //TimedMessageBox(2000, "Please Wait", "Starting AgIO");
                    ProcessStartInfo processInfo = new ProcessStartInfo();
                    processInfo.FileName = strPath;
                    //processInfo.ErrorDialog = true;
                    //processInfo.UseShellExecute = false;
                    processInfo.WorkingDirectory = Path.GetDirectoryName(strPath);
                    Process proc = Process.Start(processInfo);
                }
                catch
                {
                    TimedMessageBox(2000, "No File Found", "Can't Find AgIO");
                }
            }
            else
            {
                //Set foreground window
                ShowWindow(processName[0].MainWindowHandle, 9);
                SetForegroundWindow(processName[0].MainWindowHandle);
            }
        }
        private void btnAutoSteerConfig_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnAutoSteerConfig, gStr.gsHelp);
                return;
            }
            //check if window already exists
            Form fc = Application.OpenForms["FormSteer"];

            if (fc != null)
            {
                fc.Focus();
                fc.Close();
                return;
            }

            //
            Form form = new FormSteer(this);
            form.Show(this);

        }
        #endregion

        #region Top Panel
        private void lblSpeed_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_lblSpeed, gStr.gsHelp);
                return;
            }
            
            Form f = Application.OpenForms["FormGPSData"];

            if (f != null)
            {
                f.Focus();
                f.Close();
                return;
            }

            isGPSSentencesOn = true;

            Form form = new FormGPSData(this);
            form.Show(this);

        }
        private void btnShutdown_Click(object sender, EventArgs e)
        {
            Close();
        }
        private void btnMinimizeMainForm_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }
        private void btnMaximizeMainForm_Click(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Maximized)
                this.WindowState = FormWindowState.Normal;
            else this.WindowState = FormWindowState.Maximized;
        }

        #endregion

        #region File Menu

        //File drop down items
        private void setWorkingDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (isJobStarted)
            {
                var form = new FormTimedMessage(2000, gStr.gsFieldIsOpen, gStr.gsCloseFieldFirst);
                form.Show(this);
                return;
            }

            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.ShowNewFolderButton = true;
            fbd.Description = "Currently: " + Settings.Default.setF_workingDirectory;

            if (Settings.Default.setF_workingDirectory == "Default") fbd.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            else fbd.SelectedPath = Settings.Default.setF_workingDirectory;

            if (fbd.ShowDialog(this) == DialogResult.OK)
            {
                if (fbd.SelectedPath != Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))
                {
                    Settings.Default.setF_workingDirectory = fbd.SelectedPath;
                }
                else
                {
                    Settings.Default.setF_workingDirectory = "Default";
                }
                Settings.Default.Save();

                //restart program
                MessageBox.Show(gStr.gsProgramWillExitPleaseRestart);
                Close();
            }
        }

        private void enterSimCoordsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var form = new FormSimCoords(this))
            {
                form.ShowDialog(this);
            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var form = new Form_About())
            {
                form.ShowDialog(this);
            }
        }

        private void resetALLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (isJobStarted)
            {
                MessageBox.Show(gStr.gsCloseFieldFirst);
            }
            else
            {
                DialogResult result2 = MessageBox.Show(gStr.gsReallyResetEverything, gStr.gsResetAll,
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                if (result2 == DialogResult.Yes)
                {
                    ////opening the subkey
                    RegistryKey regKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\AgOpenGPS");

                    if (regKey == null)
                    {
                        RegistryKey Key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\AgOpenGPS");

                        //storing the values
                        Key.SetValue("Language", "en");
                        Key.Close();
                    }
                    else
                    {
                        //adding or editing "Language" subkey to the "SOFTWARE" subkey  
                        RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\AgOpenGPS");

                        //storing the values  
                        key.SetValue("Language", "en");
                        key.Close();
                    }

                    Settings.Default.Reset();
                    Settings.Default.Save();

                    Vehicle.Default.Reset();
                    Vehicle.Default.Save();

                    Settings.Default.setF_culture = "en";
                    Settings.Default.setF_workingDirectory = "Default";
                    Settings.Default.Save();

                    MessageBox.Show(gStr.gsProgramWillExitPleaseRestart);
                    System.Environment.Exit(1);
                }
            }
        }
        private void topFieldViewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Settings.Default.setMenu_isOGLZoomOn == 1)
            {
                Settings.Default.setMenu_isOGLZoomOn = 0;
                Settings.Default.Save();
                topFieldViewToolStripMenuItem.Checked = false;
                oglZoom.Width = 400;
                oglZoom.Height = 400;
                oglZoom.SendToBack();
            }
            else
            {
                Settings.Default.setMenu_isOGLZoomOn = 1;
                Settings.Default.Save();
                topFieldViewToolStripMenuItem.Checked = true;
                oglZoom.Visible = true;
                oglZoom.Width = 300;
                oglZoom.Height = 300;
                oglZoom.Left = 80;
                oglZoom.Top = 80;
                if (isJobStarted) oglZoom.BringToFront();
            }
        }

        private void helpMenuItem_Click(object sender, EventArgs e)
        {
            bool notFound = false;
            try
            {
                switch (Settings.Default.setF_culture)
                {
                    case "en":
                        System.Diagnostics.Process.Start("Manual.pdf");
                        break;

                    case "ru":
                        System.Diagnostics.Process.Start("Manual.ru.pdf");
                        break;

                    case "da":
                        System.Diagnostics.Process.Start("Manual.da.pdf");
                        break;

                    case "de":
                        System.Diagnostics.Process.Start("Manual.de.pdf");
                        break;

                    case "nl":
                        System.Diagnostics.Process.Start("Manual.nl.pdf");
                        break;

                    case "it":
                        System.Diagnostics.Process.Start("Manual.it.pdf");
                        break;

                    case "es":
                        System.Diagnostics.Process.Start("Manual.es.pdf");
                        break;

                    case "fr":
                        System.Diagnostics.Process.Start("Manual.fr.pdf");
                        break;

                    case "uk":
                        System.Diagnostics.Process.Start("Manual.uk.pdf");
                        break;

                    case "sk":
                        System.Diagnostics.Process.Start("Manual.sk.pdf");
                        break;

                    case "pl":
                        System.Diagnostics.Process.Start("Manual.pl.pdf");
                        break;

                    case "af":
                        System.Diagnostics.Process.Start("Manual.af.pdf");
                        break;

                    default:
                        System.Diagnostics.Process.Start("Manual.pdf");
                        break;
                }

            }
            catch
            {
                notFound = true;
            }

            if (notFound)
            {
                try
                {
                    System.Diagnostics.Process.Start("Manual.pdf");
                }
                catch
                {
                    TimedMessageBox(2000, "No File Found", "Can't Find Manual.pdf");
                }
            }
        }

        private void simulatorOnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (isJobStarted)
            {
                TimedMessageBox(2000, gStr.gsFieldIsOpen, gStr.gsCloseFieldFirst);
                return;
            }
            if (simulatorOnToolStripMenuItem.Checked)
            {
                if (sentenceCounter < 299)
                {
                    TimedMessageBox(2000, "Conected", "GPS");
                    simulatorOnToolStripMenuItem.Checked = false;
                    return;
                }
                else
                    SetSimStatus(true);
            }
            else
                SetSimStatus(false);
        }

        private void colorsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var form = new FormColor(this))
            {
                form.ShowDialog(this);
            }
            SettingsIO.ExportAll(vehiclesDirectory + vehicleFileName + ".XML");
        }

        private void colorsSectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var form = new FormSectionColor(this))
            {
                form.ShowDialog(this);
            }
            SettingsIO.ExportAll(vehiclesDirectory + vehicleFileName + ".XML");
        }

        //Languages
        private void menuLanguageEnglish_Click(object sender, EventArgs e)
        {
            SetLanguage("en", true);
        }
        private void menuLanguageDanish_Click(object sender, EventArgs e)
        {
            SetLanguage("da", true);
        }
        private void menuLanguageDeutsch_Click(object sender, EventArgs e)
        {
            SetLanguage("de", true);
        }
        private void menuLanguageRussian_Click(object sender, EventArgs e)
        {
            SetLanguage("ru", true);
        }
        private void menuLanguageDutch_Click(object sender, EventArgs e)
        {
            SetLanguage("nl", true);
        }
        private void menuLanguageSpanish_Click(object sender, EventArgs e)
        {
            SetLanguage("es", true);
        }
        private void menuLanguageFrench_Click(object sender, EventArgs e)
        {
            SetLanguage("fr", true);
        }
        private void menuLanguageItalian_Click(object sender, EventArgs e)
        {
            SetLanguage("it", true);
        }
        private void menuLanguageUkranian_Click(object sender, EventArgs e)
        {
            SetLanguage("uk", true);
        }
        private void menuLanguageSlovak_Click(object sender, EventArgs e)
        {
            SetLanguage("sk", true);
        }
        private void menuLanguagesPolski_Click(object sender, EventArgs e)
        {
            SetLanguage("pl", true);
        }
        private void menuLanguageTest_Click(object sender, EventArgs e)
        {
            SetLanguage("af", true);
        }

        private void SetLanguage(string lang, bool Restart)
        {
            if (Restart && isJobStarted)
            {
                var form = new FormTimedMessage(2000, gStr.gsFieldIsOpen, gStr.gsCloseFieldFirst);
                form.Show(this);
                return;
            }

            //reset them all to false
            menuLanguageEnglish.Checked = false;
            menuLanguageDeutsch.Checked = false;
            menuLanguageRussian.Checked = false;
            menuLanguageDutch.Checked = false;
            menuLanguageSpanish.Checked = false;
            menuLanguageFrench.Checked = false;
            menuLanguageItalian.Checked = false;
            menuLanguageUkranian.Checked = false;
            menuLanguageSlovak.Checked = false;
            menuLanguagePolish.Checked = false;
            menuLanguageDanish.Checked = false;

            menuLanguageTest.Checked = false;

            switch (lang)
            {
                case "en":
                    menuLanguageEnglish.Checked = true;
                    break;

                case "ru":
                    menuLanguageRussian.Checked = true;
                    break;

                case "da":
                    menuLanguageDanish.Checked = true;
                    break;

                case "de":
                    menuLanguageDeutsch.Checked = true;
                    break;

                case "nl":
                    menuLanguageDutch.Checked = true;
                    break;

                case "it":
                    menuLanguageItalian.Checked = true;
                    break;

                case "es":
                    menuLanguageSpanish.Checked = true;
                    break;

                case "fr":
                    menuLanguageFrench.Checked = true;
                    break;

                case "uk":
                    menuLanguageUkranian.Checked = true;
                    break;

                case "sk":
                    menuLanguageSlovak.Checked = true;
                    break;

                case "pl":
                    menuLanguagePolish.Checked = true;
                    break;

                case "af":
                    menuLanguageTest.Checked = true;
                    break;

                default:
                    menuLanguageEnglish.Checked = true;
                    lang = "en";
                    break;
            }

            Settings.Default.setF_culture = lang;
            Settings.Default.Save();

            //adding or editing "Language" subkey to the "SOFTWARE" subkey  
            RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\AgOpenGPS");

            //storing the values  
            key.SetValue("Language", lang);
            key.Close();

            if (Restart)
            {
                MessageBox.Show(gStr.gsProgramWillExitPleaseRestart);
                System.Environment.Exit(1);
            }
        }

        #endregion

        #region Bottom Menu

        private void btnEditAB_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnEditAB, gStr.gsHelp);
                return;
            }

            Form fc = Application.OpenForms["FormEditAB"];

            if (fc != null)
            {
                fc.Focus();
                return;
            }

            if (gyd.selectedLine != null)
            {
                Form form = new FormEditAB(this, gyd.selectedLine.Mode);
                form.Show(this);
            }
        }

        public void CloseTopMosts()
        {
            Form fc = Application.OpenForms["FormSteer"];

            if (fc != null)
            {
                fc.Focus();
                fc.Close();
            }

            fc = Application.OpenForms["FormSteerGraph"];

            if (fc != null)
            {
                fc.Focus();
                fc.Close();
            }

            fc = Application.OpenForms["FormGPSData"];

            if (fc != null)
            {
                fc.Focus();
                fc.Close();
            }

        }

        private void btnOpenConfig_Click(object sender, EventArgs e)
        {
            using (var form = new FormConfig(this))
            {
                form.ShowDialog(this);
            }
        }

        private void btnTramDisplayMode_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnTramDisplayMode, gStr.gsHelp);
                return;
            }

            tram.displayMode++;
            if (tram.displayMode > 3) tram.displayMode = 0;
            FixTramModeButton();
        }

        private void btnChangeMappingColor_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnChangeMappingColor, gStr.gsHelp);
                return;
            }

            using (var form = new FormColorPicker(this, sectionColorDay))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    sectionColorDay = form.useThisColor;
                }
            }

            Settings.Default.setDisplay_colorSectionsDay = sectionColorDay;
            Settings.Default.Save();

            btnChangeMappingColor.Image = ReplaceColor(Resources.SectionMapping, sectionColorDay);
        }

        public Bitmap ReplaceColor(Bitmap _image, Color _colorNew)
        {
            Bitmap bmap = (Bitmap)_image.Clone();

            Color cc = Color.FromArgb(255,0,0,0);

            for (int x = 11; x < bmap.Width - 10; x++)
            {
                for (int y = 11; y < bmap.Height - 10; y++)
                {
                    Color c = bmap.GetPixel(x, y);

                    if (c == cc)
                        bmap.SetPixel(x, y, Color.FromArgb(c.A, _colorNew.R, _colorNew.G, _colorNew.B));
                }
            }
            return bmap;
        }

        //Snaps
        private void btnSnapToPivot_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnSnapToPivot, gStr.gsHelp);
                return;
            }

            if (gyd.selectedLine != null)
                gyd.MoveABLine(gyd.distanceFromCurrentLinePivot);
            else
            {
                var form = new FormTimedMessage(2000, (gStr.gsNoGuidanceLines), (gStr.gsTurnOnContourOrMakeABLine));
                form.Show(this);
            }
        }

        private void btnABDraw_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnABDraw, gStr.gsHelp);
                return;
            }
            if (gyd.isContourBtnOn)
            {
                var form = new FormTimedMessage(2000, (gStr.gsContourOn), ("Turn Off Contour"));
                form.Show(this);
                return;
            }

            if (bnd.bndList.Count == 0)
            {
                TimedMessageBox(2000, gStr.gsNoBoundary, gStr.gsCreateABoundaryFirst);
                return;
            }

            if (gyd.isContourBtnOn) btnContour.PerformClick();

            using (var form = new FormABDraw(this))
            {
                form.ShowDialog(this);
                gyd.moveDistance = 0;
            }
        }

        private void btnYouSkipEnable_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnYouSkipEnable, gStr.gsHelp);
                return;
            }
            
            yt.alternateSkips = !yt.alternateSkips;
            if (yt.alternateSkips)
            {
                btnYouSkipEnable.Image = Resources.YouSkipOn;
                //make sure at least 1
                if (yt.rowSkipsWidth < 2)
                {
                    yt.rowSkipsWidth = 2;
                    cboxpRowWidth.Text = "1";
                }
                yt.Set_Alternate_skips();
                yt.ResetCreatedYouTurn();

                enableYouTurnButton(true);
            }
            else
            {
                btnYouSkipEnable.Image = Resources.YouSkipOff;
            }
        }

        private void cboxpRowWidth_SelectedIndexChanged(object sender, EventArgs e)
        {
            yt.rowSkipsWidth = cboxpRowWidth.SelectedIndex + 1;
            yt.Set_Alternate_skips();
            yt.ResetCreatedYouTurn();
            Properties.Vehicle.Default.set_youSkipWidth = yt.rowSkipsWidth;
            Properties.Vehicle.Default.Save();
        }

        private void btnHeadlandOnOff_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnHeadlandOnOff, gStr.gsHelp);
                return;
            }
            enableHeadlandButton(!bnd.isHeadlandOn);
        }

        public void enableHeadlandButton(bool status)
        {
            if (bnd.bndList.Count == 0 || bnd.bndList[0].hdLine.Points.Count == 0)
            {
                btnHeadlandOnOff.Visible = false;
                vehicle.isHydLiftOn = false;
                status = false;
            }
            else
                btnHeadlandOnOff.Visible = true;

            if (bnd.isHeadlandOn != status)
            {
                btnHeadlandOnOff.Image = status ? Properties.Resources.HeadlandOn : Properties.Resources.HeadlandOff;
                bnd.isHeadlandOn = status;

                enableHydLiftButton(vehicle.isHydLiftOn);
            }
        }

        private void btnHydLift_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnHydLift, gStr.gsHelp);
                return;
            }
            enableHydLiftButton(!vehicle.isHydLiftOn);
        }

        public void enableHydLiftButton(bool status)
        {
            vehicle.isHydLiftOn = bnd.isHeadlandOn && status;
            if (!vehicle.isHydLiftOn)
                p_239.pgn[p_239.hydLift] = 0;

            btnHydLift.Image = vehicle.isHydLiftOn ? Properties.Resources.HydraulicLiftOn : Properties.Resources.HydraulicLiftOff;
            btnHydLift.Visible = bnd.isHeadlandOn;
        }
        #endregion

        #region Tools Menu

        private void SmoothABtoolStripMenu_Click(object sender, EventArgs e)
        {
            if (isJobStarted && gyd.isBtnCurveOn)
            {
                Form f = Application.OpenForms["FormABCurve"];

                if (f != null)
                {
                    f.Focus();
                    return;
                }

                gyd.isSmoothWindowOpen = true;
                using (var form = new FormSmoothAB(this))
                {
                    form.ShowDialog(this);
                }
                gyd.isSmoothWindowOpen = false;
            }
            else
            {
                if (!isJobStarted) TimedMessageBox(2000, gStr.gsFieldNotOpen, gStr.gsStartNewField);
                else TimedMessageBox(2000, gStr.gsCurveNotOn, gStr.gsTurnABCurveOn);
            }
        }

        private void deleteContourPathsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //FileCreateContour();
            gyd.ResetContour();
            contourSaveList.Clear();
        }

        private void toolStripAreYouSure_Click(object sender, EventArgs e)
        {
            if (isJobStarted)
            {
                if (autoBtnState != btnStates.Off)
                {
                    TimedMessageBox(1500, "Sections are on", "Turn Auto or Manual Off First");
                }
                else
                {
                    DialogResult result3 = MessageBox.Show(gStr.gsDeleteAllContoursAndSections,
                        gStr.gsDeleteForSure,
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button2);
                    if (result3 == DialogResult.Yes)
                    {
                        //turn section buttons all OFF and zero square meters
                        for (int j = 0; j < MAXSECTIONS; j++)
                        {
                            section[j].UpdateButton(btnStates.Off);
                            section[j].triangleList.Clear();
                        }

                        //clear out the contour Lists
                        gyd.StopContourLine();
                        gyd.ResetContour();
                        fd.workedAreaTotal = 0;

                        tool.patchList.Clear();
                        patchSaveList.Clear();

                        FileCreateContour();
                        FileCreateSections();
                    }
                    else
                    {
                        TimedMessageBox(1500, gStr.gsNothingDeleted, gStr.gsActionHasBeenCancelled);
                    }
                }
            }
        }
        private void headingChartToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //check if window already exists
            Form fcg = Application.OpenForms["FormHeadingGraph"];

            if (fcg != null)
            {
                fcg.Focus();
                return;
            }

            //
            Form formG = new FormHeadingGraph(this);
            formG.Show(this);
        }
        private void toolStripAutoSteerChart_Click(object sender, EventArgs e)
        {
            //check if window already exists
            Form fcg = Application.OpenForms["FormSteerGraph"];

            if (fcg != null)
            {
                fcg.Focus();
                return;
            }

            //
            Form formG = new FormSteerGraph(this);
            formG.Show(this);
        }
        private void webcamToolStrip_Click(object sender, EventArgs e)
        {
            Form form = new FormWebCam();
            form.Show(this);
        }
        private void offsetFixToolStrip_Click(object sender, EventArgs e)
        {
            using (var form = new FormShiftPos(this))
            {
                form.ShowDialog(this);
            }
        }

        #endregion

        #region Nav Panel
        private void btn2D_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btn2D, gStr.gsHelp);
                return;
            }

            camera.camFollowing = true;
            camera.camPitch = 0;
            navPanelCounter = 2;
        }

        private void btn3D_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btn3D, gStr.gsHelp);
                return;
            }
            camera.camFollowing = true;
            camera.camPitch = -73;
            navPanelCounter = 2;
        }

        private void btnN2D_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnN2D, gStr.gsHelp);
                return;
            }
            camera.camFollowing = false;
            camera.camPitch = 0;
            navPanelCounter = 2;
        }

        private void btnN3D_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnN3D, gStr.gsHelp);
                return;
            }
            camera.camPitch = -73;
            camera.camFollowing = false;
            navPanelCounter = 2;
        }

        private void btnDayNightMode_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnDayNightMode, gStr.gsHelp);
                return;
            }
            SwapDayNightMode();
            navPanelCounter = 2;
        }

        //The zoom tilt buttons
        private void btnZoomIn_MouseDown(object sender, MouseEventArgs e)
        {
            //if (isTT)
            //{
            //    MessageBox.Show(gStr.btnZoomIn, gStr.gsHelp);
            //    isTT = false;
            //    return;
            //}
            if (camera.zoomValue <= 20)
                camera.zoomValue -= camera.zoomValue * 0.1;
            else
                camera.zoomValue -= camera.zoomValue * 0.05;

            SetZoom();
            navPanelCounter = 2;
        }
        private void btnZoomOut_MouseDown(object sender, MouseEventArgs e)
        {
            //if (isTT)
            //{
            //    MessageBox.Show(gStr.btnZoomOut, gStr.gsHelp);
            //    isTT = false;
            //    return;
            //}
            if (camera.zoomValue <= 20)
                camera.zoomValue += camera.zoomValue * 0.1;
            else
                camera.zoomValue += camera.zoomValue * 0.05;
            SetZoom();
            navPanelCounter = 2;
        }
        private void btnpTiltUp_MouseDown(object sender, MouseEventArgs e)
        {
            //if (isTT)
            //{
            //    MessageBox.Show(gStr.btnTiltUp, gStr.gsHelp);
            //    isTT = false;
            //    return;
            //}
            camera.camPitch -= ((camera.camPitch * 0.012) - 1);
            if (camera.camPitch > -58) camera.camPitch = 0;
            navPanelCounter = 2;
        }
        private void btnpTiltDown_MouseDown(object sender, MouseEventArgs e)
        {
            //if (isTT)
            //{
            //    MessageBox.Show(gStr.btnTiltDown, gStr.gsHelp);
            //    isTT = false;
            //    return;
            //}
            if (camera.camPitch > -59) camera.camPitch = -60;
            camera.camPitch += ((camera.camPitch * 0.012) - 1);
            if (camera.camPitch < -76) camera.camPitch = -76;
            navPanelCounter = 2;
        }

        #endregion

        #region Field Menu
        private void toolStripBtnFieldOpen_Click(object sender, EventArgs e)
        {
            //bring up dialog if no job active, close job if one is
            if (!isJobStarted)
            {
                using (var form = new FormJob(this))
                {
                    var result = form.ShowDialog(this);
                    if (result == DialogResult.Yes)
                    {
                        //ask for a directory name
                        using (var form2 = new FormFieldDir(this))
                        { form2.ShowDialog(this); }
                    }

                    //load from  KML
                    else if (result == DialogResult.No)
                    {
                        //ask for a directory name
                        using (var form2 = new FormFieldKML(this))
                        { form2.ShowDialog(this); }
                    }
                }

                //boundaryToolStripBtn.Enabled = true;
                FieldMenuButtonEnableDisable(isJobStarted);
            }
        }

        private void toolStripBtnField_Click(object sender, EventArgs e)
        {
            CloseCurrentJob();
        }

        private void CloseCurrentJob()
        {
            //bring up dialog if no job active, close job if one is

            if (autoBtnState != btnStates.Off)
            {
                TimedMessageBox(2000, "Safe Shutdown", "Turn off Auto Section Control");
                return;
            }

            //close the current job and ask how to or if to save
            if (isJobStarted)
            {
                int choice = SaveOrNot(false);
                if (choice != 1)
                {
                    Settings.Default.setF_CurrentDir = currentFieldDirectory;
                    Settings.Default.Save();
                    FileSaveEverythingBeforeClosingField();

                    if (choice == 0)
                        displayFieldName = gStr.gsNone;
                    else
                    {
                        //ask for a directory name
                        using (var form2 = new FormSaveAs(this))
                        {
                            form2.ShowDialog(this);
                        }
                    }
                }
            }
            //update GUI areas
        }
        private void toolStripBtnMakeBndContour_Click(object sender, EventArgs e)
        {
            //build all the contour guidance lines from boundaries, all of them.
            using (var form = new FormMakeBndCon(this))
            {
                form.ShowDialog(this);
            }
        }

        private void tramLinesMenuField_Click(object sender, EventArgs e)
        {
            if (gyd.isContourBtnOn) btnContour.PerformClick(); 

            if (gyd.selectedLine != null)
            {
                Form form99 = new FormTram(this, gyd.selectedLine.Mode);
                form99.Show(this);
                form99.Left = Width - 275;
                form99.Top = 100;
            }
            else
            {
                var form = new FormTimedMessage(1500, gStr.gsNoABLineActive, gStr.gsPleaseEnterABLine);
                form.Show(this);
                return;
            }
        }
        private void headlandToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (bnd.bndList.Count == 0)
            {
                TimedMessageBox(2000, gStr.gsNoBoundary, gStr.gsCreateABoundaryFirst);
                return;
            }

            GetHeadland();
        }
        public void GetHeadland()
        {
            using (var form = new FormHeadland (this))
            {
                form.ShowDialog(this);
            }

            enableHeadlandButton(true);
        }
        private void boundariesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (isJobStarted)
            {
                using (var form = new FormBoundary(this))
                {
                    if (form.ShowDialog(this) == DialogResult.OK)
                    {
                        Form form2 = new FormBoundaryPlayer(this);
                        form2.Show(this);
                    }
                }
            }
            else { TimedMessageBox(3000, gStr.gsFieldNotOpen, gStr.gsStartNewField); }
        }

        //Recorded Path
        private void btnPathGoStop_Click(object sender, EventArgs e)
        {
            #region Turn off Guidance
            //if contour is on, turn it off
            if (gyd.isContourBtnOn) btnContour.PerformClick();
            enableABLineButton(false);
            enableAutoSteerButton(false);
            enableCurveButton(false);
            setYouTurnButtonStatus(false);

            #endregion

            //already running?
            if (recPath.isDrivingRecordedPath)
            {
                recPath.StopDrivingRecordedPath();
            }
            //start the recorded path driving process
            else if (!recPath.StartDrivingRecordedPath())
            {
                //Cancel the recPath - something went seriously wrong
                recPath.StopDrivingRecordedPath();
                TimedMessageBox(1500, gStr.gsProblemMakingPath, gStr.gsCouldntGenerateValidPath);
            }
            else
                enableRecordButton(false);
        }

        public void enableRecordButton(bool isOn)
        {
            btnPathGoStop.Image = isOn ? Properties.Resources.boundaryPlay : Properties.Resources.boundaryStop;
            btnPathRecordStop.Enabled = isOn;
            btnPathDelete.Enabled = isOn;
        }

        private void btnPathRecordStop_Click(object sender, EventArgs e)
        {
            if (recPath.isRecordOn)
            {
                FileSaveRecPath();
                recPath.isRecordOn = false;
                btnPathRecordStop.Image = Properties.Resources.BoundaryRecord;
                btnPathGoStop.Enabled = true;
                btnPathDelete.Enabled = true;
            }
            else if (isJobStarted)
            {
                recPath.recList.Clear();
                recPath.isRecordOn = true;
                btnPathRecordStop.Image = Properties.Resources.boundaryStop;
                btnPathGoStop.Enabled = false;
                btnPathDelete.Enabled = false;
            }
        }

        private void btnPathDelete_Click(object sender, EventArgs e)
        {
            recPath.recList.Clear();
            recPath.StopDrivingRecordedPath();
            FileSaveRecPath();
        }

        private void recordedPathStripMenu_Click(object sender, EventArgs e)
        {
            if (isJobStarted)
                enableRecordPanel(panelDrag.Visible = !panelDrag.Visible);
            else
                TimedMessageBox(3000, gStr.gsFieldNotOpen, gStr.gsStartNewField); 
        }
        
        public void enableRecordPanel(bool isOn)
        {
            btnPathGoStop.Image = isOn ? Properties.Resources.boundaryPlay : Properties.Resources.boundaryStop;
            btnPathRecordStop.Enabled = isOn;
            btnPathDelete.Enabled = isOn;
        }

        #endregion

        #region OpenGL Window context Menu and functions
        private void contextMenuStripOpenGL_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //dont bring up menu if no flag selected
            if (flagNumberPicked == 0) e.Cancel = true;
        }
        private void googleEarthOpenGLContextMenu_Click(object sender, EventArgs e)
        {
            if (isJobStarted)
            {
                //save new copy of kml with selected flag and view in GoogleEarth
                FileSaveSingleFlagKML(flagNumberPicked);

                //Process.Start(@"C:\Program Files (x86)\Google\Google Earth\client\googleearth", workingDirectory + currentFieldDirectory + "\\Flags.KML");
                Process.Start(fieldsDirectory + currentFieldDirectory + "\\Flag.KML");
            }
        }

        #endregion

        #region Sim controls
        private void timerSim_Tick(object sender, EventArgs e)
        {
            if (recPath.isDrivingRecordedPath || isAutoSteerBtnOn && (guidanceLineDistanceOff != 32000))
                sim.DoSimTick(guidanceLineSteerAngle * 0.01);
            else sim.DoSimTick(sim.steerAngleScrollBar);
        }

        private void hsbarSteerAngle_Scroll(object sender, ScrollEventArgs e)
        {
            sim.steerAngleScrollBar = (hsbarSteerAngle.Value - 400) * 0.1;
            btnResetSteerAngle.Text = sim.steerAngleScrollBar.ToString("N1");
        }
        private void hsbarStepDistance_Scroll(object sender, ScrollEventArgs e)
        {
            sim.stepDistance = ((double)(hsbarStepDistance.Value)) / 5.0 / (double)fixUpdateHz;
        }
        private void btnResetSteerAngle_Click(object sender, EventArgs e)
        {
            sim.steerAngleScrollBar = 0;
            hsbarSteerAngle.Value = 400;
            btnResetSteerAngle.Text = sim.steerAngleScrollBar.ToString("N1");
        }
        private void btnResetSim_Click(object sender, EventArgs e)
        {
            sim.latitude = Properties.Settings.Default.setGPS_SimLatitude;
            sim.longitude = Properties.Settings.Default.setGPS_SimLongitude;
        }
        private void btnSimSetSpeedToZero_Click(object sender, EventArgs e)
        {
            sim.stepDistance = 0;
            hsbarStepDistance.Value = 0;
        }
        #endregion


        private void lbludpWatchCounts_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_lbludpWatchCounts, gStr.gsHelp);
                return;
            }
        }

        private void lblInty_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_lblIntegral, gStr.gsHelp);
                return;
            }
        }

        private void cboxpRowWidth_Click(object sender, EventArgs e)
        {
            if (isTT)
            {
                MessageBox.Show(gStr.h_btnRowWidthSkips, gStr.gsHelp);
                return;
            }
        }


        public void FixTramModeButton()
        {
            if (tram.tramList.Count > 0 || tram.tramBndOuterArr.Count > 0)
                btnTramDisplayMode.Visible = true;
            else btnTramDisplayMode.Visible = false;

            switch (tram.displayMode)
            {
                case 0:
                    btnTramDisplayMode.Image = Properties.Resources.TramOff;
                    break;
                case 1:
                    btnTramDisplayMode.Image = Properties.Resources.TramAll;
                    break;
                case 2:
                    btnTramDisplayMode.Image = Properties.Resources.TramLines;
                    break;
                case 3:
                    btnTramDisplayMode.Image = Properties.Resources.TramOuter;
                    break;

                default:
                    break;
            }
        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            isTT = !isTT;
            if (isTT)
                btnHelp.Image = Resources.HelpCancel;
            else
            {
                btnHelp.Image = Resources.Help;
                isTT = false;
            }
        }
    }//end class
}//end namespace