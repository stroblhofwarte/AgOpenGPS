namespace AgOpenGPS
{
    public class CModuleComm
    {
        //copy of the mainform address
        private readonly FormGPS mf;

        //Critical Safety Properties
        public bool isOutOfBounds = true;

        //receive strings
        public string serialRecvAutoSteerStr;
        public string serialRecvMachineStr;

        // ---- Section control switches to AOG  ---------------------------------------------------------
        //PGN - 32736 - 127.249 0x7FF9
        public byte[] ss = new byte[9];
        public byte[] ssP = new byte[9];
        public int
            swHeader = 0,
            swMain = 1,
            swReserve = 2,
            swReserve2 = 3,
            swNumSections = 4,
            swOnGr0 = 5,
            swOffGr0 = 6,
            swOnGr1 = 7,
            swOffGr1 = 8;


        //LIDAR
        //UDP sentence just rec'd
        public string recvUDPSentence = "Inital UDP";

        public int lidarDistance;

        public int pwmDisplay = 0;
        public double actualSteerAngleDegrees = 0;
        public int actualSteerAngleChart = 0;


        //for the workswitch
        public bool isWorkSwitchActiveLow, isWorkSwitchEnabled, isWorkSwitchManual, isSteerControlsManual;

        public int workSwitchValue, oldWorkSwitchValue, steerSwitchValue = 0;

        //constructor
        public CModuleComm(FormGPS _f)
        {
            mf = _f;

            //WorkSwitch logic
            isWorkSwitchEnabled = false;

            //does a low, grounded out, mean on
            isWorkSwitchActiveLow = true;
        }

        //Called from "OpenGL.Designer.cs" when requied
        public void CheckWorkSwitch()
        {
            if (isSteerControlsManual) workSwitchValue = steerSwitchValue;

            if (workSwitchValue != oldWorkSwitchValue)
            {
                oldWorkSwitchValue = workSwitchValue;

                if (workSwitchValue == 0 == isWorkSwitchActiveLow)
                {
                    if (isWorkSwitchManual)
                    {
                        if (mf.manualBtnState != btnStates.On)
                        {
                            mf.btnManualOffOn.PerformClick();
                        }
                    }
                    else if (mf.autoBtnState != btnStates.Auto)
                    {
                        mf.btnSectionOffAutoOn.PerformClick();
                    }
                }
                //Checks both on-screen buttons, performs click if button is not off
                else if (mf.autoBtnState != btnStates.Off)
                    mf.btnSectionOffAutoOn.PerformClick();
                else if (mf.manualBtnState != btnStates.Off)
                    mf.btnManualOffOn.PerformClick();
            }
        }
    }
}