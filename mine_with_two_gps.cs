enum DroneState
{
    Idle,
    Liftoff,
    Leave,
    Return
}

DroneState currentState = DroneState.Idle;
DroneState lastTaskDone = DroneState.Idle;
DroneState switcherState = DroneState.Idle;
DroneState nextTask = DroneState.Idle; // Store the next task to perform after liftoff

Vector3D initialPosition;
float liftoffHeight = 20.0f; // Desired liftoff altitude in meters

private double kp = 0.1; // Proportional gain
private double ki = 0.01; // Integral gain
private double kd = 0.1; // Derivative gain

private double integral;
private double lastError;

public Program()
{
    // Set the script to run the Main method every 100 game ticks (approximately every 1.6 seconds)
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

public void Main(string argument, UpdateType updateSource)
{
    StringBuilder echoStringBuilder = new StringBuilder();

    // Fetch the cockpit
    IMyCockpit cockpit = GridTerminalSystem.GetBlockWithName("Cargo Drone Control Seat") as IMyCockpit;
    if (cockpit == null)
    {
        Echo("Control Seat not found");
        return;
    }

    IMyRemoteControl remoteControl = GetBlockWithName<IMyRemoteControl>("Cargo Drone Remote Control");
    if (remoteControl == null)
    {
        echoStringBuilder.AppendLine("Remote Control not found");
        Echo(echoStringBuilder.ToString());
        return;
    }
    else {
        PrintWaypoints(remoteControl, echoStringBuilder);
    }

    
    // Retrieve the current state from the Switcher
    switcherState = GetStateFromSwitcher();

    if (switcherState != currentState && switcherState != lastTaskDone)
    {
        // Check if we are transitioning from Idle to Leave or Return
        if (currentState == DroneState.Idle && (switcherState == DroneState.Leave || switcherState == DroneState.Return))
        {
            // Set currentState to Liftoff and store the next task
            currentState = DroneState.Liftoff;
            nextTask = switcherState;
        }
        // Add a check for transitioning from Leave or Return to the other state
        else if ((currentState == DroneState.Leave || currentState == DroneState.Return) && 
                 (switcherState == DroneState.Leave || switcherState == DroneState.Return))
        {
            // Transition to Liftoff before switching to the other state
            currentState = DroneState.Liftoff;
            nextTask = switcherState;
        }
        else
        {
            currentState = switcherState;
        }
        echoStringBuilder.AppendLine("New State: " + currentState.ToString());
    }
    else if (IsLastWaypointReached(remoteControl))
    {
        currentState = DroneState.Idle;
        echoStringBuilder.AppendLine("Transitioning to Idle state.");
    }

    // When the script is triggered by a player or terminal (not by the timer)
    if ((updateSource & UpdateType.Trigger) != 0 || (updateSource & UpdateType.Terminal) != 0)
    {
        if (!string.IsNullOrEmpty(argument) && argument.ToLower().StartsWith("gps"))
        {
            remoteControl.ClearWaypoints();
            ProcessArgument(argument, remoteControl);
            remoteControl.ApplyAction("CollisionAvoidance_On");
            remoteControl.SetAutoPilotEnabled(true);
            echoStringBuilder.AppendLine("Waypoints set for route.");
        }
        else
        {
            echoStringBuilder.AppendLine("Argument invalid, give 2 gps values");
        }
    }

    // Check for last waypoint, but only if it's an update call (timer or automatic)
    if ((updateSource & UpdateType.Update100) != 0)
    {
        echoStringBuilder.AppendLine("Current state: " + currentState.ToString());

        if (currentState == DroneState.Leave || currentState == DroneState.Return)
        {
            // Toggle connector lock when leaving
            ToggleConnectorLock();
            HandleLandingGear();
        }

        // Process the route based on the current state
        switch (currentState)
        {
            case DroneState.Idle:
                echoStringBuilder.AppendLine("Idle state active");
                remoteControl.SetAutoPilotEnabled(false);
                break;

            case DroneState.Leave:
                echoStringBuilder.AppendLine("Leave state active");
                if (lastTaskDone != DroneState.Leave)
                {
                    TurnOffThrusters(remoteControl);
                    HandleLeaveState(remoteControl);
                    lastTaskDone = DroneState.Leave;
                }
                break;

            case DroneState.Liftoff:
                HandleLiftoffState(remoteControl);
                break;

            case DroneState.Return:
                echoStringBuilder.AppendLine("Return state active");
                if (lastTaskDone != DroneState.Return)
                {
                    TurnOffThrusters(remoteControl);
                    HandleReturnState(remoteControl);
                    lastTaskDone = DroneState.Return;
                }
                break;
        }
    }

    // Write the contents of echoStringBuilder to the text panel
    // lcd.WriteText(echoStringBuilder.ToString());

    // Write the contents of echoStringBuilder to the cockpit's LCD screen
    cockpit.GetSurface(0).WriteText(echoStringBuilder.ToString());
}

private bool IsLastWaypointReached(IMyRemoteControl remoteControl)
{
    List<MyWaypointInfo> waypoints = new List<MyWaypointInfo>();
    remoteControl.GetWaypointInfo(waypoints);

    if (waypoints.Count == 0)
    {
        // No waypoints means we're done
        return true;
    }

    MyWaypointInfo lastWaypoint = waypoints[waypoints.Count - 1];
    Vector3D lastWaypointPosition = lastWaypoint.Coords;

    // Get the current position of the remote control
    Vector3D currentPosition = remoteControl.GetPosition();

    // Calculate the distance to the last waypoint
    double distanceToLastWaypoint = Vector3D.Distance(currentPosition, lastWaypointPosition);

    // Define a small threshold for how close the drone needs to be to the waypoint
    // to consider it as reached. This can be adjusted based on your needs.
    double thresholdDistance = 5.0; // meters

    return distanceToLastWaypoint < thresholdDistance;
}


private void ToggleConnectorLock()
{
    var connectors = new List<IMyShipConnector>();
    GridTerminalSystem.GetBlockGroupWithName("Cargo Drone Connectors")?.GetBlocksOfType(connectors);

    foreach (var connector in connectors)
    {
        if (connector.Status == MyShipConnectorStatus.Connected)
        {
            // If connected, unlock
            connector.Disconnect();
        }
        else
        {
            // If not connected, try to lock
            connector.Connect();
        }
    }
}

private void PrintWaypoints(IMyRemoteControl remoteControl, StringBuilder echoStringBuilder)
{
    List<MyWaypointInfo> waypoints = new List<MyWaypointInfo>();
    remoteControl.GetWaypointInfo(waypoints);

    if (waypoints.Count == 0)
    {
        echoStringBuilder.AppendLine("No waypoints set.");
        return;
    }

    echoStringBuilder.AppendLine("Waypoints in order:");
    foreach (var waypoint in waypoints)
    {
        echoStringBuilder.AppendLine($"{waypoint.Name}");
    }
}



private DroneState GetStateFromSwitcher()
{
    var switcher = GridTerminalSystem.GetBlockWithName("Cargo Drone Switcher") as IMyProgrammableBlock;
    if (switcher == null)
    {
        Echo("Switcher not found");
        return DroneState.Idle; // Default to Idle if not found
    }

    DroneState switcherState;
    if (Enum.TryParse(switcher.CustomData, out switcherState))
    {
        return switcherState;
    }
    else
    {
        return DroneState.Idle; // Default to Idle if parsing fails
    }
}

private void HandleLeaveState(IMyRemoteControl remoteControl)
{
    Vector3D home, destination;
    if (RetrieveGpsFromCustomData(out home, out destination))
    {
        remoteControl.ClearWaypoints();
        remoteControl.AddWaypoint(destination, "Destination");
        remoteControl.SetAutoPilotEnabled(true);
    }
    else
    {
        Echo("Failed to retrieve GPS data for leave journey.");
    }

    lastTaskDone = DroneState.Leave;
}


private void HandleLiftoffState(IMyRemoteControl remoteControl)
{
    double desiredHeight = 20.0; // Desired liftoff height in meters
    if (lastTaskDone != DroneState.Liftoff)
    {
        // Store the initial position when first entering the liftoff state
        initialPosition = remoteControl.GetPosition();
        lastTaskDone = DroneState.Liftoff;
        integral = 0.0;
        lastError = 0.0;
    }

    Vector3D currentPosition = remoteControl.GetPosition();
    double currentHeight = currentPosition.Y - initialPosition.Y;
    double error = desiredHeight - currentHeight;
    integral += error;
    double derivative = error - lastError;
    lastError = error;

    double thrust = kp * error + ki * integral + kd * derivative;

    // Clamp thrust between 0 and 1
    thrust = Math.Max(0.0, Math.Min(thrust, 1.0));

    // Apply thrust to thrusters
    SetThrusterOverride("Cargo Drone Up Thrusters", thrust);

    if (Math.Abs(error) < 1.0) // Check if the drone is close enough to the desired height
    {
        // Turn off thrusters
        TurnOffThrusters(remoteControl);

        // Transition to the next task (Leave or Return)
        currentState = nextTask;
    }
}

private void SetThrusterOverride(string group, double overrideValue)
{
    var thrusters = new List<IMyThrust>();
    GridTerminalSystem.GetBlockGroupWithName(group)?.GetBlocksOfType(thrusters);

    foreach (var thruster in thrusters)
    {
        thruster.ThrustOverridePercentage = (float)overrideValue; // Set override
    }
}

private void TurnOffThrusters(IMyRemoteControl remoteControl)
{
    SetThrusterOverride("Cargo Drone Up Thrusters", 0.0);
}


private bool LiftoffCompleted(IMyRemoteControl remoteControl)
{
    Vector3D currentPosition = remoteControl.GetPosition();
    Vector3D gravityDirection = remoteControl.GetNaturalGravity();
    Vector3D upDirection = -Vector3D.Normalize(gravityDirection); // Upward direction

    // Calculate the current altitude relative to the initial position
    double altitudeChange = Vector3D.Dot((currentPosition - initialPosition), upDirection);
    
    // Check if the drone has reached the desired altitude
    return altitudeChange >= liftoffHeight;
}

private void HandleReturnState(IMyRemoteControl remoteControl)
{
    Vector3D home, destination;
    if (RetrieveGpsFromCustomData(out home, out destination))
    {
        remoteControl.ClearWaypoints();
        remoteControl.AddWaypoint(home, "Home");
        remoteControl.SetAutoPilotEnabled(true);
    }
    else
    {
        Echo("Failed to retrieve GPS data for return journey.");
    }

    lastTaskDone = DroneState.Return;
}


private bool RetrieveGpsFromCustomData(out Vector3D home, out Vector3D destination)
{
    string customData = Me.CustomData;
    string[] entries = customData.Split(new[] { "GPS:" }, StringSplitOptions.RemoveEmptyEntries);

    home = destination = new Vector3D();

    if (entries.Length < 2) // Expecting at least 2 entries: Home and Destination
    {
        Echo("Not enough GPS data in Custom Data.");
        return false;
    }

    // Try parsing the first two GPS entries (assuming they are Home and Destination)
    return TryParseGpsEntry(entries[0], out home) &&
           TryParseGpsEntry(entries[1], out destination);
}

private bool TryParseGpsEntry(string entry, out Vector3D coordinates)
{
    // Extract the part inside parentheses
    int startIndex = entry.IndexOf('(');
    int endIndex = entry.IndexOf(')');
    if (startIndex == -1 || endIndex == -1 || endIndex <= startIndex)
    {
        Echo("Invalid GPS entry format.");
        coordinates = new Vector3D();
        return false;
    }

    string coordsPart = entry.Substring(startIndex + 1, endIndex - startIndex - 1);
    string[] parts = coordsPart.Split(':');

    return TryParseCoordinates(parts[0], parts[1], parts[2], out coordinates);
}



// Method to handle landing gear
private void HandleLandingGear()
{
    var landing_gears = new List<IMyLandingGear>();
    GridTerminalSystem.GetBlockGroupWithName("Cargo Drone Landing Gears")?.GetBlocksOfType(landing_gears);
    
    foreach (var landing_gear in landing_gears)
    {
        // Turn on the landing gear to ensure it's active
        landing_gear.ApplyAction("OnOff_On");

        // Unlock landing gear
        landing_gear.ApplyAction("Unlock");
    }
}

private T GetBlockWithName<T>(string name) where T : class
{
    T block = GridTerminalSystem.GetBlockWithName(name) as T;
    if (block == null)
    {
        Echo($"{typeof(T).Name} named '{name}' not found.");
    }
    return block;
}

private void ProcessArgument(string argument, IMyRemoteControl remoteControl)
{
    string[] args = argument.Split(new char[] { ':' });

    if (args.Length < 4) // Minimum length to include at least one set of coordinates
    {
        Echo("Invalid argument format. Not enough data for GPS location.");
        return;
    }

    // The GPS token is expected to be the first in the array
    if (args[0].ToLower() != "gps")
    {
        Echo("GPS token not found at the expected position.");
        return;
    }

    // The GPS name is expected to be the second in the array, coordinates start from the third
    int coordinateIndex = 2;

    // Ensure there are enough parts for the coordinates
    if (coordinateIndex + 2 >= args.Length)
    {
        Echo("Incomplete GPS coordinates.");
        return;
    }

    Vector3D destination;
    if (TryParseCoordinates(args[coordinateIndex], args[coordinateIndex + 1], args[coordinateIndex + 2], out destination))
    {
        Vector3D currentPos = remoteControl.GetPosition(); // Current drone position (Home)

        remoteControl.ClearWaypoints();

        // Append GPS data to Custom Data
        AppendGpsToCustomData(currentPos, destination);
    }
    else
    {
        Echo("Invalid GPS coordinates format.");
    }
}

private void AppendGpsToCustomData(Vector3D home, Vector3D destination)
{
    string customData = "";
    customData += $"GPS:Home({home.X}:{home.Y}:{home.Z}), ";
    customData += $"GPS:Destination({destination.X}:{destination.Y}:{destination.Z}), ";
    Me.CustomData = customData;
}

private bool TryParseCoordinates(string x, string y, string z, out Vector3D result)
{
    double xCoord, yCoord, zCoord;
    result = new Vector3D();
    bool xValid = double.TryParse(x, out xCoord);
    bool yValid = double.TryParse(y, out yCoord);
    bool zValid = double.TryParse(z, out zCoord);

    if (xValid && yValid && zValid)
    {
        result = new Vector3D(xCoord, yCoord, zCoord);
        return true;
    }
    else
    {
        Echo("Invalid GPS coordinates.");
        return false;
    }
}
