namespace AgOpenGPS
{
    public class CModuleComm
    {
        //copy of the mainform address
        private readonly FormGPS mf;

        //Critical Safety Properties
        public bool isOutOfBounds = true;

        public int pwmDisplay = 0;
        public double actualSteerAngleDegrees = 0;
        public int actualSteerAngleChart = 0;


        //for the workswitch
        public bool isWorkSwitchActiveLow, isWorkSwitchEnabled, isWorkSwitchManual, isSteerControlsManual;

        public int workSwitchValue, oldWorkSwitchValue, steerSwitchValue = 0, oldsteerSwitchValue;

        //flag for free drive window to control autosteer
        public bool isInFreeDriveMode;

        //the trackbar angle for free drive
        public double driveFreeSteerAngle = 0;

        //constructor
        public CModuleComm(FormGPS _f)
        {
            mf = _f;

            //WorkSwitch logic
            isWorkSwitchEnabled = false;

            //does a low, grounded out, mean on
            isWorkSwitchActiveLow = true;
            isInFreeDriveMode = false;
        }

        //Called from "OpenGL.Designer.cs" when requied
        public void CheckWorkSwitch()
        {
            if (isSteerControlsManual) workSwitchValue = steerSwitchValue;

            //AutoSteerAuto button enable - Ray Bear inspired code - Thx Ray!
            if (mf.ahrs.isAutoSteerAuto && steerSwitchValue != oldsteerSwitchValue)
            {
                oldsteerSwitchValue = steerSwitchValue;
                mf.enableAutoSteerButton(steerSwitchValue == 0);
            }

            if (workSwitchValue != oldWorkSwitchValue)
            {
                oldWorkSwitchValue = workSwitchValue;

                if (workSwitchValue == 0 == isWorkSwitchActiveLow)
                {
                    if (isWorkSwitchManual)
                    {
                        if (mf.autoBtnState != btnStates.On)
                            mf.setSectionButtonState(btnStates.On);
                    }
                    else if (mf.autoBtnState != btnStates.Auto)
                        mf.setSectionButtonState(btnStates.Auto);
                }
                //Checks both on-screen buttons, performs click if button is not off
                else if (mf.autoBtnState != btnStates.Off)
                    mf.setSectionButtonState(btnStates.Off);
            }
        }
    }
}