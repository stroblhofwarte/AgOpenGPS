//Please, if you use this, share the improvements

using AgOpenGPS.Properties;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public enum TBrand { AGOpenGPS, Case, Claas, Deutz, Fendt, JDeere, Kubota, Massey, NewHolland, Same, Steyr, Ursus, Valtra }
    public enum HBrand { AGOpenGPS, Case, Claas, JDeere, NewHolland }
    public enum WDBrand { AGOpenGPS, Case, Challenger, JDeere, NewHolland }

    //the main form object
    public partial class FormGPS : Form
    {
        //To bring forward AgIO if running
        [System.Runtime.InteropServices.DllImport("User32.dll")]
        private static extern bool SetForegroundWindow(IntPtr handle);

        [System.Runtime.InteropServices.DllImport("User32.dll")]
        private static extern bool ShowWindow(IntPtr hWind, int nCmdShow);

        #region // Class Props and instances

        //maximum sections available
        public const int MAXSECTIONS = 17;

        //How many boundaries allowed
        public const int MAXBOUNDARIES = 6;

        //How many headlands allowed
        public const int MAXHEADS = 6;

        //The base directory where AgOpenGPS will be stored and fields and vehicles branch from
        public string baseDirectory;

        //current directory of vehicle
        public string vehiclesDirectory, vehicleFileName = "";

        //current directory of tools
        public string toolsDirectory, toolFileName = "";

        //current directory of Environments
        public string envDirectory, envFileName = "";

        //current fields and field directory
        public string fieldsDirectory, currentFieldDirectory, displayFieldName;

        private bool leftMouseDownOnOpenGL; //mousedown event in opengl window
        public int flagNumberPicked = 0;

        //bool for whether or not a job is active
        public bool isJobStarted = false, isAutoSteerBtnOn, isLidarBtnOn = true;

        //if we are saving a file
        public bool isSavingFile = false, isLogNMEA = false, isLogElevation = false;

        //texture holders
        public uint[] texture;

        //the currentversion of software
        public string currentVersionStr, inoVersionStr;
        public int inoVersionInt;

        //create instance of a stopwatch for timing of frames and NMEA hz determination
        private readonly Stopwatch swFrame = new Stopwatch();

        public double secondsSinceStart;

        //private readonly Stopwatch swDraw = new Stopwatch();
        //swDraw.Reset();
        //swDraw.Start();
        //swDraw.Stop();
        //label3.Text = ((double) swDraw.ElapsedTicks / (double) System.Diagnostics.Stopwatch.Frequency * 1000).ToString();

        //Time to do fix position update and draw routine
        public double frameTime = 0;

        //create instance of a stopwatch for timing of frames and NMEA hz determination
        private readonly Stopwatch swHz = new Stopwatch();

        //Time to do fix position update and draw routine
        private double HzTime = 5;

        //For field saving in background
        private int minuteCounter = 1;
        private int tenMinuteCounter = 1;

        //whether or not to use Stanley control
        public bool isStanleyUsed = true;

        //used to update the screen status bar etc
        private int displayUpdateHalfSecondCounter = 0, displayUpdateOneSecondCounter = 0, displayUpdateOneFifthCounter = 0, displayUpdateThreeSecondCounter = 0;

        private int threeSecondCounter = 0, threeSeconds = 0;
        private int oneSecondCounter = 0, oneSecond = 0;
        private int oneHalfSecondCounter = 0, oneHalfSecond = 0;
        private int oneFifthSecondCounter = 0, oneFifthSecond = 0;

        public int pbarSteer, pbarMachine, pbarUDP;

        public double nudNumber = 0;

        public double m2InchOrCm, inchOrCm2m, m2FtOrM, ftOrMtoM, cm2CmOrIn, inOrCm2Cm;
        public string unitsFtM, unitsInCm;

        //used by filePicker Form to return picked file and directory
        public string filePickerFileAndDirectory;

        //private int fiveSecondCounter = 0, fiveSeconds = 0;

        //the autoManual drive button. Assume in Auto
        public bool isInAutoDrive = true;

        //isGPSData form up
        public bool isGPSSentencesOn = false;

        /// <summary>
        /// create the scene camera
        /// </summary>
        public CCamera camera = new CCamera();

        /// <summary>
        /// create world grid
        /// </summary>
        public CWorldGrid worldGrid;

        /// <summary>
        /// The NMEA class that decodes it
        /// </summary>
        public CNMEA pn;

        /// <summary>
        /// an array of sections, so far 16 section + 1 fullWidth Section
        /// </summary>
        public CSection[] section;

        /// <summary>
        /// TramLine class for boundary and settings
        /// </summary>
        public CTram tram;

        /// <summary>
        /// Auto Headland YouTurn
        /// </summary>
        public CYouTurn yt;

        /// <summary>
        /// Our vehicle only
        /// </summary>
        public CVehicle vehicle;

        /// <summary>
        /// Just the tool attachment that includes the sections
        /// </summary>
        public CTool tool;

        /// <summary>
        /// All the structs for recv and send of information out ports
        /// </summary>
        public CModuleComm mc;

        /// <summary>
        /// The boundary object
        /// </summary>
        public CBoundary bnd;

        /// <summary>
        /// The internal simulator
        /// </summary>
        public CSim sim;

        /// <summary>
        /// Resource manager for gloabal strings
        /// </summary>
        //public ResourceManager _rm;

        /// <summary>
        /// Heading, Roll, Pitch, GPS, Properties
        /// </summary>
        public CAHRS ahrs;

        /// <summary>
        /// Recorded Path
        /// </summary>
        public CRecordedPath recPath;

        /// <summary>
        /// Most of the displayed field data for GUI
        /// </summary>
        public CFieldData fd;

        ///// <summary>
        ///// Sound
        ///// </summary>
        public CSound sounds;

        /// <summary>
        /// The font class
        /// </summary>
        public CFont font;

        /// <summary>
        /// The new steer algorithms
        /// </summary>
        public CGuidance gyd;

        #endregion // Class Props and instances

        // Constructor, Initializes a new instance of the "FormGPS" class.
        public FormGPS()
        {
            //winform initialization
            InitializeComponent();

            CheckSettingsNotNull();


            //ControlExtension.Draggable(panelSnap, true);
            ControlExtension.Draggable(oglZoom, true);
            ControlExtension.Draggable(oglBack, true);

            setWorkingDirectoryToolStripMenuItem.Text = gStr.gsDirectories;
            enterSimCoordsToolStripMenuItem.Text = gStr.gsEnterSimCoords;
            aboutToolStripMenuItem.Text = gStr.gsAbout;
            menustripLanguage.Text = gStr.gsLanguage;


            simulatorOnToolStripMenuItem.Text = gStr.gsSimulatorOn;

            resetALLToolStripMenuItem.Text = gStr.gsResetAll;
            colorsToolStripMenuItem1.Text = gStr.gsColors;
            topFieldViewToolStripMenuItem.Text = gStr.gsTopFieldView;

            resetEverythingToolStripMenuItem.Text = gStr.gsResetAllForSure;

            steerChartStripMenu.Text = gStr.gsSteerChart;

            //Tools Menu
            SmoothABtoolStripMenu.Text = gStr.gsSmoothABCurve;
            //toolStripBtnMakeBndContour.Text = gStr.gsMakeBoundaryContours;
            boundariesToolStripMenuItem.Text = gStr.gsBoundary;
            headlandToolStripMenuItem.Text = gStr.gsHeadland;
            deleteContourPathsToolStripMenuItem.Text = gStr.gsDeleteContourPaths;
            deleteAppliedAreaToolStripMenuItem.Text = gStr.gsDeleteAppliedArea;
            deleteForSureToolStripMenuItem.Text = gStr.gsAreYouSure;
            webcamToolStrip.Text = gStr.gsWebCam;
            //googleEarthFlagsToolStrip.Text = gStr.gsGoogleEarth;
            offsetFixToolStrip.Text = gStr.gsOffsetFix;

            btnChangeMappingColor.Text = Application.ProductVersion.ToString(CultureInfo.InvariantCulture);

            //time keeper
            secondsSinceStart = (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds;

            //build the gesture structures
            //SetupStructSizes();

            //create the world grid
            worldGrid = new CWorldGrid(this);

            //our vehicle made with gl object and pointer of mainform
            vehicle = new CVehicle(this);

            tool = new CTool(this);

            //create a new section and set left and right positions
            //created whether used or not, saves restarting program

            section = new CSection[MAXSECTIONS];
            for (int j = 0; j < MAXSECTIONS; j++)
            {
                section[j] = new CSection(this, j);
                oglMain.Controls.Add(section[j].button);
            }

            //enable disable manual buttons
            LineUpManualBtns();

            //our NMEA parser
            pn = new CNMEA(this);

            ////new instance of auto headland turn
            yt = new CYouTurn(this);

            //module communication
            mc = new CModuleComm(this);

            //boundary object
            bnd = new CBoundary(this);

            //nmea simulator built in.
            sim = new CSim(this);

            ////all the attitude, heading, roll, pitch reference system
            ahrs = new CAHRS();

            //A recorded path
            recPath = new CRecordedPath(this);

            //fieldData all in one place
            fd = new CFieldData(this);

            //start the stopwatch
            //swFrame.Start();

            //instance of tram
            tram = new CTram(this);

            //resource for gloabal language strings
            //_rm = new ResourceManager("AgOpenGPS.gStr", Assembly.GetExecutingAssembly());

            //access to font class
            font = new CFont(this);

            //the new steer algorithms
            gyd = new CGuidance(this);

            //sounds class
            sounds = new CSound();
        }

        //Initialize items before the form Loads or is visible
        private void FormGPS_Load(object sender, EventArgs e)
        {
            this.MouseWheel += ZoomByMouseWheel;

            //start udp server is required
            StartLoopbackServer();

            timer2.Enabled = true;
            //panel1.BringToFront();
            pictureboxStart.BringToFront();
            pictureboxStart.Dock = System.Windows.Forms.DockStyle.Fill;

            //set the language to last used
            SetLanguage(Settings.Default.setF_culture, false);

            currentVersionStr = Application.ProductVersion.ToString(CultureInfo.InvariantCulture);

            string[] fullVers = currentVersionStr.Split('.');
            int inoV = int.Parse(fullVers[0], CultureInfo.InvariantCulture);
            inoV += int.Parse(fullVers[1], CultureInfo.InvariantCulture);
            inoV += int.Parse(fullVers[2], CultureInfo.InvariantCulture);
            inoVersionInt = inoV;
            inoVersionStr = inoV.ToString();


            if (Settings.Default.setF_workingDirectory == "Default")
                baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\AgOpenGPS\\";
            else baseDirectory = Settings.Default.setF_workingDirectory + "\\AgOpenGPS\\";

            //get the fields directory, if not exist, create
            fieldsDirectory = baseDirectory + "Fields\\";
            string dir = Path.GetDirectoryName(fieldsDirectory);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) { Directory.CreateDirectory(dir); }

            //get the fields directory, if not exist, create
            vehiclesDirectory = baseDirectory + "Vehicles\\";
            dir = Path.GetDirectoryName(vehiclesDirectory);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) { Directory.CreateDirectory(dir); }

            //get the abLines directory, if not exist, create
            ablinesDirectory = baseDirectory + "ABLines\\";
            dir = Path.GetDirectoryName(fieldsDirectory);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) { Directory.CreateDirectory(dir); }

            //make sure current field directory exists, null if not
            currentFieldDirectory = Settings.Default.setF_CurrentDir;

            string curDir;
            if (currentFieldDirectory != "")
            {
                curDir = fieldsDirectory + currentFieldDirectory + "//";
                dir = Path.GetDirectoryName(curDir);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    currentFieldDirectory = "";
                    Settings.Default.setF_CurrentDir = "";
                    Settings.Default.Save();
                }
            }
            // load all the gui elements in gui.designer.cs
            LoadSettings();

            if (Settings.Default.setMenu_isOGLZoomOn == 1)
                topFieldViewToolStripMenuItem.Checked = true;
            else topFieldViewToolStripMenuItem.Checked = false;

            oglZoom.Width = 400;
            oglZoom.Height = 400;
            oglZoom.Visible = true;
            oglZoom.Left = 300;
            oglZoom.Top = 80;

            if (!topFieldViewToolStripMenuItem.Checked)
            {
                oglZoom.SendToBack();
            }

            //Start AgIO process
            Process[] processName = Process.GetProcessesByName("AgIO");
            if (processName.Length == 0)
            {
                //Start application here
                DirectoryInfo di = new DirectoryInfo(Application.StartupPath);
                string strPath = di.ToString();
                strPath += "\\AgIO.exe";
                try
                {
                    ProcessStartInfo processInfo = new ProcessStartInfo
                    {
                        FileName = strPath,
                        WorkingDirectory = Path.GetDirectoryName(strPath)
                    };
                    Process proc = Process.Start(processInfo);
                }
                catch
                {
                    TimedMessageBox(2000, "No File Found", "Can't Find AgIO");
                }
            }

            //nmea limiter
            udpWatch.Start();
        }

        private void btnVideoHelpRecPath_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(gStr.v_RecordedPathForm))
                System.Diagnostics.Process.Start(gStr.v_RecordedPathForm);
        }

        private void lblCurveLineName_Click(object sender, EventArgs e)
        {
            mode += 1;
            if (mode > 2) mode = 0;
        }

        //form is closing so tidy up and save settings
        private void FormGPS_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (isJobStarted)
            {
                if (autoBtnState != btnStates.Off)
                {
                    TimedMessageBox(2000, "Safe Shutdown", "Turn off Auto Section Control");
                    e.Cancel = true;
                    return;
                }

                bool closing = true;
                int choice = SaveOrNot(closing);

                if (choice == 1)
                {
                    e.Cancel = true;
                    return;
                }

                //Save, return, cancel save
                if (isJobStarted)
                {
                    if (choice == 3)
                    {
                        e.Cancel = true;
                        return;
                    }
                    else if (choice == 0)
                    {
                        Settings.Default.setF_CurrentDir = currentFieldDirectory;
                        Settings.Default.Save();

                        FileSaveEverythingBeforeClosingField();

                        displayFieldName = gStr.gsNone;
                    }
                }
            }

            SaveFormGPSWindowSettings();

            if (sendToAppSocket != null)
            {
                try
                {
                    sendToAppSocket.Shutdown(SocketShutdown.Both);
                }
                catch { }
                finally { sendToAppSocket.Close(); }
            }

            if (recvFromAppSocket != null)
            {
                try
                {
                    recvFromAppSocket.Shutdown(SocketShutdown.Both);
                }
                catch { }
                finally { recvFromAppSocket.Close(); }
            }

            //save current vehicle
            SettingsIO.ExportAll(vehiclesDirectory + vehicleFileName + ".XML");
        }

        // Load Bitmaps And Convert To Textures

        public enum textures : uint
        {
            SkyDay, Floor, Font,
            Turn, TurnCancel, TurnManual,
            Compass, Speedo, SpeedoNeedle,
            Lift, SkyNight, SteerPointer,
            SteerDot, Tractor, QuestionMark,
            FrontWheels, FourWDFront, FourWDRear,
            Harvester, Lateral
        }

        public void CheckSettingsNotNull()
        {
            if (Settings.Default.setFeatures == null)
            {
                Settings.Default.setFeatures = new CFeatureSettings();
            }
        }

        public void LoadGLTextures()
        {
            GL.Enable(EnableCap.Texture2D);

            Bitmap[] oglTextures = new Bitmap[]
            {
                Properties.Resources.z_SkyDay,Properties.Resources.z_Floor,Properties.Resources.z_Font,
                Properties.Resources.z_Turn,Properties.Resources.z_TurnCancel,Properties.Resources.z_TurnManual,
                Properties.Resources.z_Compass,Properties.Resources.z_Speedo,Properties.Resources.z_SpeedoNeedle,
                Properties.Resources.z_Lift,Properties.Resources.z_SkyNight,Properties.Resources.z_SteerPointer,
                Properties.Resources.z_SteerDot,GetTractorBrand(Settings.Default.setBrand_TBrand),Properties.Resources.z_QuestionMark,
                Properties.Resources.z_FrontWheels,Get4WDBrandFront(Settings.Default.setBrand_WDBrand), Get4WDBrandRear(Settings.Default.setBrand_WDBrand),
                GetHarvesterBrand(Settings.Default.setBrand_HBrand), Properties.Resources.z_LateralManual
            };

            texture = new uint[oglTextures.Length];

            for (int h = 0; h < oglTextures.Length; h++)
            {
                using (Bitmap bitmap = oglTextures[h])
                {
                    GL.GenTextures(1, out texture[h]);
                    GL.BindTexture(TextureTarget.Texture2D, texture[h]);
                    BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bitmapData.Width, bitmapData.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, bitmapData.Scan0);
                    bitmap.UnlockBits(bitmapData);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, 9729);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, 9729);
                }
            }
        }


        //Load Bitmaps brand
        public Bitmap GetTractorBrand(TBrand brand)
        {
            Bitmap bitmap;
            if (brand == TBrand.Case)
                bitmap = Resources.z_TractorCase;
            else if (brand == TBrand.Claas)
                bitmap = Resources.z_TractorClaas;
            else if (brand == TBrand.Deutz)
                bitmap = Resources.z_TractorDeutz;
            else if (brand == TBrand.Fendt)
                bitmap = Resources.z_TractorFendt;
            else if (brand == TBrand.JDeere)
                bitmap = Resources.z_TractorJDeere;
            else if (brand == TBrand.Kubota)
                bitmap = Resources.z_TractorKubota;
            else if (brand == TBrand.Massey)
                bitmap = Resources.z_TractorMassey;
            else if (brand == TBrand.NewHolland)
                bitmap = Resources.z_TractorNH;
            else if (brand == TBrand.Same)
                bitmap = Resources.z_TractorSame;
            else if (brand == TBrand.Steyr)
                bitmap = Resources.z_TractorSteyr;
            else if (brand == TBrand.Ursus)
                bitmap = Resources.z_TractorUrsus;
            else if (brand == TBrand.Valtra)
                bitmap = Resources.z_TractorValtra;
            else
                bitmap = Resources.z_TractorAoG;

            return bitmap;
        }

        public Bitmap GetHarvesterBrand(HBrand brandH)
        {
            Bitmap harvesterbitmap;
            if (brandH == HBrand.Case)
                harvesterbitmap = Resources.z_HarvesterCase;
            else if (brandH == HBrand.Claas)
                harvesterbitmap = Resources.z_HarvesterClaas;
            else if (brandH == HBrand.JDeere)
                harvesterbitmap = Resources.z_HarvesterJD;
            else if (brandH == HBrand.NewHolland)
                harvesterbitmap = Resources.z_HarvesterNH;
            else
                harvesterbitmap = Resources.z_HarvesterAoG;

            return harvesterbitmap;
        }

        public Bitmap Get4WDBrandFront(WDBrand brandWDF)
        {
            Bitmap bitmap4WDFront;
            if (brandWDF == WDBrand.Case)
                bitmap4WDFront = Resources.z_4WDFrontCase;
            else if (brandWDF == WDBrand.Challenger)
                bitmap4WDFront = Resources.z_4WDFrontChallenger;
            else if (brandWDF == WDBrand.JDeere)
                bitmap4WDFront = Resources.z_4WDFrontJDeere;
            else if (brandWDF == WDBrand.NewHolland)
                bitmap4WDFront = Resources.z_4WDFrontNH;
            else
                bitmap4WDFront = Resources.z_4WDFrontAoG;

            return bitmap4WDFront;
        }

        public Bitmap Get4WDBrandRear(WDBrand brandWDR)
        {
            Bitmap bitmap4WDRear;
            if (brandWDR == WDBrand.Case)
                bitmap4WDRear = Resources.z_4WDRearCase;
            else if (brandWDR == WDBrand.Challenger)
                bitmap4WDRear = Resources.z_4WDRearChallenger;
            else if (brandWDR == WDBrand.JDeere)
                bitmap4WDRear = Resources.z_4WDRearJDeere;
            else if (brandWDR == WDBrand.NewHolland)
                bitmap4WDRear = Resources.z_4WDRearNH;
            else
                bitmap4WDRear = Resources.z_4WDRearAoG;

            return bitmap4WDRear;
        }

        public void SwapDirection()
        {
            if (!yt.isYouTurnTriggered)
            {
                yt.isYouTurnRight = !yt.isYouTurnRight;
                yt.ResetCreatedYouTurn();
            }
            else
                enableYouTurnButton(false);
        }

        private void BuildMachineByte()
        {
            int set = 1;
            int reset = 2046;
            p_254.pgn[p_254.sc1to8] = 0;
            p_254.pgn[p_254.sc9to16] = 0;

            int machine = 0;

            //check if super section is on
            if (section[tool.numOfSections].isSectionOn)
            {
                for (int j = 0; j < tool.numOfSections; j++)
                {
                    //all the sections are on, so set them
                    machine |= set;
                    set <<= 1;
                }
            }

            else
            {
                for (int j = 0; j < MAXSECTIONS; j++)
                {
                    //set if on, reset bit if off
                    if (section[j].isSectionOn) machine |= set;
                    else machine &= reset;

                    //move set and reset over 1 bit left
                    set <<= 1;
                    reset <<= 1;
                    reset += 1;
                }
            }

            //sections in autosteer
            p_254.pgn[p_254.sc9to16] = unchecked((byte)(machine >> 8));
            p_254.pgn[p_254.sc1to8] = unchecked((byte)machine);

            //machine pgn
            p_239.pgn[p_239.sc9to16] = p_254.pgn[p_254.sc9to16];
            p_239.pgn[p_239.sc1to8] = p_254.pgn[p_254.sc1to8];
            p_239.pgn[p_239.tram] = unchecked((byte)tram.controlByte);

            //out serial to autosteer module  //indivdual classes load the distance and heading deltas 
        }

        //dialog for requesting user to save or cancel
        public int SaveOrNot(bool closing)
        {
            CloseTopMosts();

            using (FormSaveOrNot form = new FormSaveOrNot(closing))
            {
                DialogResult result = form.ShowDialog(this);

                if (result == DialogResult.OK) return 0;      //Save and Exit
                if (result == DialogResult.Ignore) return 1;   //Ignore
                if (result == DialogResult.Yes) return 2;      //Save As
                return 3;  // oops something is really busted
            }
        }

        //make the start picture disappear
        private void timer2_Tick(object sender, EventArgs e)
        {
            this.Controls.Remove(pictureboxStart);
            pictureboxStart.Dispose();
            //panel1.SendToBack();
            timer2.Enabled = false;
            timer2.Dispose();
        }

        public bool KeypadToNUD(NumericUpDown sender, Form owner)
        {
            sender.BackColor = Color.Red;

            using (FormNumeric form = new FormNumeric((double)sender.Minimum, (double)sender.Maximum, (double)Math.Round(sender.Value, sender.DecimalPlaces)))
            {
                DialogResult result = form.ShowDialog(owner);
                sender.BackColor = Color.AliceBlue;
                if (result == DialogResult.OK)
                {
                    sender.Value = (decimal)form.ReturnValue;
                    return true;
                }
                return false;
            }
        }

        public void KeyboardToText(TextBox sender, Form owner)
        {
            sender.BackColor = Color.Red;
            using (FormKeyboard form = new FormKeyboard(sender.Text))
            {
                if (form.ShowDialog(owner) == DialogResult.OK)
                {
                    sender.Text = form.ReturnString;
                }
            }
            sender.BackColor = Color.AliceBlue;
        }

        //function to set section positions
        public void SectionSetPosition()
        {
            section[0].positionLeft = (double)Vehicle.Default.setSection_position1 + Vehicle.Default.setVehicle_toolOffset;
            section[1].positionLeft = section[0].positionRight = (double)Vehicle.Default.setSection_position2 + Vehicle.Default.setVehicle_toolOffset;
            section[2].positionLeft = section[1].positionRight = (double)Vehicle.Default.setSection_position3 + Vehicle.Default.setVehicle_toolOffset;
            section[3].positionLeft = section[2].positionRight = (double)Vehicle.Default.setSection_position4 + Vehicle.Default.setVehicle_toolOffset;
            section[4].positionLeft = section[3].positionRight = (double)Vehicle.Default.setSection_position5 + Vehicle.Default.setVehicle_toolOffset;
            section[5].positionLeft = section[4].positionRight = (double)Vehicle.Default.setSection_position6 + Vehicle.Default.setVehicle_toolOffset;
            section[6].positionLeft = section[5].positionRight = (double)Vehicle.Default.setSection_position7 + Vehicle.Default.setVehicle_toolOffset;
            section[7].positionLeft = section[6].positionRight = (double)Vehicle.Default.setSection_position8 + Vehicle.Default.setVehicle_toolOffset;
            section[8].positionLeft = section[7].positionRight = (double)Vehicle.Default.setSection_position9 + Vehicle.Default.setVehicle_toolOffset;
            section[9].positionLeft = section[8].positionRight = (double)Vehicle.Default.setSection_position10 + Vehicle.Default.setVehicle_toolOffset;
            section[10].positionLeft = section[9].positionRight = (double)Vehicle.Default.setSection_position11 + Vehicle.Default.setVehicle_toolOffset;
            section[11].positionLeft = section[10].positionRight = (double)Vehicle.Default.setSection_position12 + Vehicle.Default.setVehicle_toolOffset;
            section[12].positionLeft = section[11].positionRight = (double)Vehicle.Default.setSection_position13 + Vehicle.Default.setVehicle_toolOffset;
            section[13].positionLeft = section[12].positionRight = (double)Vehicle.Default.setSection_position14 + Vehicle.Default.setVehicle_toolOffset;
            section[14].positionLeft = section[13].positionRight = (double)Vehicle.Default.setSection_position15 + Vehicle.Default.setVehicle_toolOffset;
            section[15].positionLeft = section[14].positionRight = (double)Vehicle.Default.setSection_position16 + Vehicle.Default.setVehicle_toolOffset;
            section[15].positionRight = (double)Vehicle.Default.setSection_position17 + Vehicle.Default.setVehicle_toolOffset;
        }

        //function to calculate the width of each section and update
        public void SectionCalcWidths()
        {
            for (int j = 0; j < MAXSECTIONS; j++)
            {
                section[j].sectionWidth = (section[j].positionRight - section[j].positionLeft);
                section[j].rpSectionPosition = 250 + (int)(Math.Round(section[j].positionLeft * 10, 0, MidpointRounding.AwayFromZero));
                section[j].rpSectionWidth = (int)(Math.Round(section[j].sectionWidth * 10, 0, MidpointRounding.AwayFromZero));
            }

            //calculate tool width based on extreme right and left values
            tool.toolWidth = (section[tool.numOfSections - 1].positionRight) - (section[0].positionLeft);

            //left and right tool position
            tool.toolFarLeftPosition = section[0].positionLeft;
            tool.toolFarRightPosition = section[tool.numOfSections - 1].positionRight;

            //now do the full width section
            section[tool.numOfSections].sectionWidth = tool.toolWidth;
            section[tool.numOfSections].positionLeft = tool.toolFarLeftPosition;
            section[tool.numOfSections].positionRight = tool.toolFarRightPosition;

            //find the right side pixel position
            tool.rpXPosition = 375 + (int)(Math.Round(tool.toolFarLeftPosition * 10, 0, MidpointRounding.AwayFromZero));
            tool.rpWidth = (int)(Math.Round(tool.toolWidth * 10, 0, MidpointRounding.AwayFromZero));
        }

        //request a new job
        public void JobNew()
        {
            if (Settings.Default.setMenu_isOGLZoomOn == 1)
            {
                oglZoom.BringToFront();
                oglZoom.Width = 300;
                oglZoom.Height = 300;
            }

            //SendSteerSettingsOutAutoSteerPort();
            isJobStarted = true;
            startCounter = 0;

            //turn section buttons all On
            for (int j = 0; j < MAXSECTIONS; j++)
            {
                section[j].UpdateButton(btnStates.Off, true);
            }

            //update the menu
            this.menustripLanguage.Enabled = false;

            FieldMenuButtonEnableDisable(isJobStarted);
        }

        public void FieldMenuButtonEnableDisable(bool isOn)
        {
            //right and bottom bar
            panelRight.Visible = isOn;
            panelBottom.Visible = isOn;

            //left bar items
            SmoothABtoolStripMenu.Enabled = isOn;
            deleteContourPathsToolStripMenuItem.Enabled = isOn;
            deleteAppliedAreaToolStripMenuItem.Enabled = isOn;
            btnABDraw.Enabled = isOn;

            toolStripMenuItem9.Visible = isOn;
            boundariesToolStripMenuItem.Visible = isOn && Settings.Default.setFeatures.isBoundaryOn;
            headlandToolStripMenuItem.Visible = isOn && Settings.Default.setFeatures.isHeadlandOn;
            tramLinesMenuField.Visible = isOn && Settings.Default.setFeatures.isTramOn;
            toolStripBtnMakeBndContour.Visible = isOn && Settings.Default.setFeatures.isBndContourOn;
            recordedPathStripMenu.Visible = isOn && Settings.Default.setFeatures.isRecPathOn;

            lblFieldStatus.Visible = isOn;
            //lblFieldDataTopField.Visible = isOn;
            //lblFieldDataTopDone.Visible = isOn;
            //lblFieldDataTopRemain.Visible = isOn;
        }

        //close the current job
        public void JobClose()
        {
            isJobStarted = false;

            //reset field offsets
            pn.fixOffset.easting = 0;
            pn.fixOffset.northing = 0;
            
            //zoom gone
            oglZoom.SendToBack();

            //clean all the lines
            bnd.bndList.Clear();


            FieldMenuButtonEnableDisable(isJobStarted);

            menustripLanguage.Enabled = true;

            //fix ManualOffOnAuto buttons
            setSectionButtonState(btnStates.Off, false);

            //turn section buttons all OFF
            for (int j = 0; j < MAXSECTIONS; j++)
            {
                section[j].triangleList.Clear();
            }

            tool.patchList.Clear();
            //clear the section lists

            //clear the flags
            flagPts.Clear();



            gyd.ResetContour();
            gyd.isContourBtnOn = false;
            btnContour.Image = Properties.Resources.ContourOff;
            gyd.ContourIndex = null;

            gyd.selectedLine = null;
            gyd.moveDistance = 0;
            gyd.isValid = false;
            gyd.howManyPathsAway = 0;
            gyd.oldHowManyPathsAway = double.NaN;

            gyd.refList.Clear();
            gyd.curList.Clear();

            //Buttons
            enableABLineButton(false);
            enableAutoSteerButton(false);
            enableCurveButton(false);
            setYouTurnButtonStatus(false);

            //turn off headland buttons
            enableHeadlandButton(false);
            enableHydLiftButton(false);

            btnCycleLines.Image = Properties.Resources.ABLineCycle;


            //clean up tram
            tram.displayMode = 0;
            tram.tramList.Clear();
            tram.tramBndInnerArr.Clear();
            tram.tramBndOuterArr.Clear();
            FixTramModeButton();

            //reset acre and distance counters
            fd.workedAreaTotal = 0;

            //reset GUI areas
            fd.UpdateFieldBoundaryGUIAreas();

            displayFieldName = gStr.gsNone;

            panelDrag.Visible = false;
            recPath.isRecordOn = false;
            enableRecordPanel(false);
            recPath.recList.Clear();
            recPath.shortestDubinsList.Clear();
            recPath.shuttleDubinsList.Clear();
        }

        //Does the logic to process section on off requests
        private void ProcessSectionOnOffRequests()
        {
            double mapFactor = 1 + ((100 - tool.minCoverage) * 0.01);
            for (int j = 0; j < tool.numOfSections; j++)
            {
                //SECTIONS - 
                if (section[j].sectionOnRequest)
                {
                    section[j].isSectionOn = true;

                    if (mode == 1 || mode == 2)
                        section[j].sectionOverlapTimer = (int)Math.Max(HzTime * tool.turnOffDelay, 1);
                    else
                    {
                        double sped = 1 / ((pn.speed + 3) * 0.5);
                        if (sped < 0.3) sped = 0.3;

                        //keep setting the timer so full when ready to turn off
                        section[j].sectionOverlapTimer = (int)(fixUpdateHz * mapFactor * sped + (fixUpdateHz * tool.turnOffDelay) + 1);
                    }

                    if (section[j].mappingOnTimer == 0) section[j].mappingOnTimer = (int)Math.Max(HzTime * tool.lookAheadOnSetting - 0.5, 1);//tool.mappingOnDelay
                    section[j].mappingOffTimer = (int)(HzTime * tool.lookAheadOffSetting + 2);//tool.mappingOffDelay
                }
                else if (section[j].sectionOverlapTimer > 0)
                {
                    section[j].mappingOffTimer = (int)(HzTime * tool.lookAheadOffSetting + 2);//tool.mappingOffDelay
                    section[j].sectionOverlapTimer--;
                    if (section[j].isSectionOn && section[j].sectionOverlapTimer == 0)
                        section[j].isSectionOn = false;
                }

                //MAPPING -
                if (tool.isSuperSectionAllowedOn)
                {
                    if (section[j].isMappingOn)
                    {
                        section[j].TurnMappingOff();
                        section[j].mappingOnTimer = 1;
                    }
                }
                else
                {
                    if (section[j].mappingOnTimer > 0)
                    {
                        section[j].mappingOnTimer--;
                        if (!section[j].isMappingOn && section[j].mappingOnTimer == 0)
                            section[j].TurnMappingOn();
                    }

                    if (section[j].mappingOffTimer > 0)
                    {
                        section[j].mappingOffTimer--;
                        if (section[j].mappingOffTimer == 0)
                        {
                            section[j].mappingOnTimer = 0;
                            if (section[j].isMappingOn)
                                section[j].TurnMappingOff();
                        }
                    }
                }
            }

            if (tool.isSuperSectionAllowedOn && !section[tool.numOfSections].isMappingOn)
                section[tool.numOfSections].TurnMappingOn();
            else if (!tool.isSuperSectionAllowedOn && section[tool.numOfSections].isMappingOn)
                section[tool.numOfSections].TurnMappingOff();

            #region notes
            //Turn ON
            //if requested to be on, set the timer to Max 10 (1 seconds) = 10 frames per second
            //if (section[j].sectionOnRequest && !section[j].sectionOnOffCycle)
            //{
            //    section[j].sectionOnTimer = (int)(pn.speed * section[j].lookAheadOn) + 1;
            //    if (section[j].sectionOnTimer > fixUpdateHz + 3) section[j].sectionOnTimer = fixUpdateHz + 3;
            //    section[j].sectionOnOffCycle = true;
            //}

            ////reset the ON request
            //section[j].sectionOnRequest = false;

            ////decrement the timer if not zero
            //if (section[j].sectionOnTimer > 0)
            //{
            //    //turn the section ON if not and decrement timer
            //    section[j].sectionOnTimer--;
            //    if (!section[j].isSectionOn) section[j].isSectionOn = true;

            //    //keep resetting the section OFF timer while the ON is active
            //    //section[j].sectionOffTimer = (int)(fixUpdateHz * tool.toolTurnOffDelay);
            //}
            //if (!section[j].sectionOffRequest) 
            //    section[j].sectionOffTimer = (int)(fixUpdateHz * tool.turnOffDelay);

            ////decrement the off timer
            //if (section[j].sectionOffTimer > 0 && section[j].sectionOnTimer == 0) section[j].sectionOffTimer--;

            ////Turn OFF
            ////if Off section timer is zero, turn off the section
            //if (section[j].sectionOffTimer == 0 && section[j].sectionOnTimer == 0 && section[j].sectionOffRequest)
            //{
            //    if (section[j].isSectionOn) section[j].isSectionOn = false;
            //    //section[j].sectionOnOffCycle = false;
            //    section[j].sectionOffRequest = false;
            //    //}
            //}
            //Turn ON
            //if requested to be on, set the timer to Max 10 (1 seconds) = 10 frames per second
            //if (section[j].mappingOnRequest && !section[j].mappingOnOffCycle)
            //{
            //    section[j].mappingOnTimer = (int)(fixUpdateHz * 1) + 1;
            //    section[j].mappingOnOffCycle = true;
            //}

            ////reset the ON request
            //section[j].mappingOnRequest = false;

            //decrement the timer if not zero
            #endregion notes
        }

        //take the distance from object and convert to camera data
        public void SetZoom()
        {
            if (camera.zoomValue > 180) camera.zoomValue = 180;
            if (camera.zoomValue < 6.0) camera.zoomValue = 6.0;

            camera.camSetDistance = camera.zoomValue * camera.zoomValue * -1;
            //match grid to cam distance and redo perspective
            if (camera.camSetDistance < -10000) camera.gridZoom = 2000;
            else if (camera.camSetDistance < -5000) camera.gridZoom = 1000;
            else if (camera.camSetDistance < -2000) camera.gridZoom = 500;
            else if (camera.camSetDistance < -1000) camera.gridZoom = 200.0;
            else if (camera.camSetDistance < -500) camera.gridZoom = 100.0;
            else if (camera.camSetDistance < -250) camera.gridZoom = 50.0;
            else if (camera.camSetDistance < -150) camera.gridZoom = 20.0;
            else if (camera.camSetDistance < -50) camera.gridZoom = 10.0;
        }

        //All the files that need to be saved when closing field or app
        private void FileSaveEverythingBeforeClosingField()
        {
            //turn off contour line if on
            if (gyd.ContourIndex != null)
                gyd.StopContourLine();

            //turn off all the sections
            for (int j = 0; j < tool.numOfSections + 1; j++)
            {
                if (section[j].isMappingOn) section[j].TurnMappingOff();
            }

            //FileSaveHeadland();
            FileSaveBoundary();
            FileSaveSections();
            FileSaveContour();
            FileSaveFieldKML();

            JobClose();
            Text = "AgOpenGPS";
        }

        //an error log called by all try catches
        public void WriteErrorLog(string strErrorText)
        {
            try
            {
                //set up file and folder if it doesn't exist
                const string strFileName = "Error Log.txt";
                //string strPath = Application.StartupPath;

                //Write out the error appending to existing
                File.AppendAllText(baseDirectory + "\\" + strFileName, strErrorText + " - " +
                    DateTime.Now.ToString() + "\r\n\r\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error in WriteErrorLog: " + ex.Message, "Error Logging", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        //message box pops up with info then goes away
        public void TimedMessageBox(int timeout, string s1, string s2)
        {
            FormTimedMessage form = new FormTimedMessage(timeout, s1, s2);
            form.Show(this);
        }
    }//class FormGPS
}//namespace AgOpenGPS

/*The order is:
 *
 * The watchdog timer times out and runs this function tmrWatchdog_tick().
 * 50 times per second so statusUpdateCounter counts to 25 and updates strip menu etc at 2 hz
 * it also makes sure there is new sentences showing up otherwise it shows **** No GGA....
 * saveCounter ticks 2 x per second, used at end of draw routine every minute to save a backup of field
 * then ScanForNMEA function checks for a complete sentence if contained in pn.rawbuffer
 * if not it comes right back and waits for next watchdog trigger and starts all over
 * if a new sentence is there, UpdateFix() is called
 * Right away CalculateLookAhead(), no skips, is called to determine lookaheads and trigger distances to save triangles plotted
 * Then UpdateFix() continues.
 * Hitch, pivot, antenna locations etc and directions are figured out if trigDistance is triggered
 * When that is done, DoRender() is called on the visible OpenGL screen and its draw routine _draw is run
 * before triangles are drawn, frustum cull figures out how many of the triangles should be drawn
 * When its all the way thru, it triggers the sectioncontrol Draw, its frustum cull, and determines if sections should be on
 * ProcessSectionOnOffRequests() runs and that does the section on off magic
 * SectionControlToArduino() runs and spits out the port machine control based on sections on or off
 * If field needs saving (1.5 minute since last time) field is saved
 * Now the program is "Done" and waits for the next watchdog trigger, determines if a new sentence is available etc
 * and starts all over from the top.
 */