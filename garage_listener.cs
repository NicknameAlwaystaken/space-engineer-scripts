const string TimerBlockName = "Timer Block (To park)";
const double ParkingDistance = 400.0;
const string ListenerStateKey = "ListenerState";

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

public void Main(string argument)
{
    string listenerState = Me.CustomData;

    IMyBroadcastListener listener = IGC.RegisterBroadcastListener("GaragePosition");
    while (listener.HasPendingMessage)
    {
        MyIGCMessage message = listener.AcceptMessage();
        if (message.Tag == "GaragePosition")
        {
            string[] data = message.Data.ToString().Split('|');
            if (data.Length == 4)
            {
                Vector3D garagePosition = new Vector3D(Convert.ToDouble(data[1]), Convert.ToDouble(data[2]), Convert.ToDouble(data[3]));
                double distance = Vector3D.Distance(Me.GetPosition(), garagePosition);

                if (distance <= ParkingDistance && listenerState != "Parking")
                {
                    // Enter Parking state
                    ActivateTimerBlock(TimerBlockName);
                    Me.CustomData = "Parking";
                    Echo("Entering parking state and activating timer block.");
                }
                else if (distance > ParkingDistance && listenerState == "Parking")
                {
                    // Exit Parking state
                    Me.CustomData = "Not Parking";
                    Echo("Exiting parking state.");
                }
            }
        }
    }
}

private void ActivateTimerBlock(string timerBlockName)
{
    var timerBlock = GridTerminalSystem.GetBlockWithName(timerBlockName) as IMyTimerBlock;
    if (timerBlock != null)
    {
        // timerBlock.Trigger();
        timerBlock.StartCountdown();
    }
    else
    {
        Echo("Timer block not found.");
    }
}
